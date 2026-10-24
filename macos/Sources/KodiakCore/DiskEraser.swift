import Foundation

/// One diskutil command in an Entire Drive wipe.
public struct DiskStep: Equatable {
    public enum Kind: Equatable {
        /// An overwrite pass (number, of how many, what it writes).
        case pass(Int, Int, String)
        /// Removes the volumes and writes an empty partition map, ahead of clearing it (Leave It Uninitialized).
        case prepare
        /// Zeros the start and end of the disk, where the MBR and both GPT copies live (Leave It Uninitialized).
        case clearMap
        /// Writes the partition map and formats one volume.
        case setup(DiskFormat)
    }

    public var kind: Kind
    public var arguments: [String]

    public var isPass: Bool { if case .pass = kind { return true } else { return false } }

    /// What the progress line says while the step runs.
    public var title: String {
        switch kind {
        case .pass(let number, let count, let name): return "pass \(number) of \(count) (\(name))"
        case .prepare: return "removing its volumes"
        case .clearMap: return "removing the partition map"
        case .setup(let format): return "setting it up as \(format.partitionStyle) with \(format.fileSystem.short)"
        }
    }
}

/// What an Entire Drive wipe did.
public struct DiskEraseResult: Equatable {
    public var passesDone = 0
    public var passes = 0
    public var stopped = false
    /// The partition map was written (formatted) or removed (left uninitialized).
    public var setUp = false
    /// The format it ended with (nil when left uninitialized, or when setting up failed).
    public var format: DiskFormat?
    public var fellBackToMBR = false
    /// diskutil's own words when setting it up failed.
    public var setupError: String?
    /// Why the wipe itself didn't happen or didn't finish.
    public var error: String?
    public var asAdministrator = false
    public init() {}
}

/// Live progress of an Entire Drive wipe.
public struct DiskEraseStatus: Equatable {
    public var step: DiskStep
    /// 0 to 100 within this step, when diskutil says.
    public var percent: Double?
    /// diskutil's own estimate of the time left in this step, such as 0:12:30.
    public var remaining: String?
    public var asAdministrator = false
}

/// Runs diskutil for an Entire Drive wipe. The real one is `SystemDiskRunner`; tests use a fake.
public protocol DiskCommandRunner: AnyObject {
    /// Runs diskutil with these arguments as this user, passing output on as it arrives. Returns the exit code and
    /// everything it printed.
    func run(_ arguments: [String], output: @escaping (String) -> Void) -> (code: Int32, text: String)
    /// Stops the command running now.
    func interrupt()
    /// Runs a shell script as the administrator, through macOS's own password prompt (one prompt). Returns the exit
    /// code and the error text ("Cancelled" when the prompt was cancelled).
    func runAsAdministrator(_ script: String) -> (code: Int32, error: String)
}

public enum DiskEraser {
    /// The commands for one drive: the overwrite passes (none for Quick), then either the setup (the chosen format) or,
    /// for Leave It Uninitialized, removing the volumes and zeroing the partition map.
    public static func steps(disk: String, level: WipeLevel, kind: MediaKind, method: WipeMethod, format: DiskFormat?) -> [DiskStep] {
        precondition(WipeDisks.isWholeDisk(disk), "Not a whole disk: \(disk)")
        var steps: [DiskStep] = []
        let passes = WipeMethods.drivePasses(level, kind: kind, method: method) ?? []
        for (index, pass) in passes.enumerated() {
            let arguments = pass.isRandom ? ["randomDisk", "1", disk] : ["zeroDisk", disk]
            steps.append(DiskStep(kind: .pass(index + 1, passes.count, pass.name), arguments: arguments))
        }
        if let format {
            steps.append(DiskStep(kind: .setup(format), arguments: setupArguments(disk: disk, format: format)))
        } else {
            steps.append(DiskStep(kind: .prepare, arguments: ["eraseDisk", "free", "EMPTY", "GPT", disk]))
            steps.append(DiskStep(kind: .clearMap, arguments: ["zeroDisk", "short", disk]))
        }
        return steps
    }

    public static func setupArguments(disk: String, format: DiskFormat) -> [String] {
        ["eraseDisk", format.fileSystem.personality, format.diskutilName, format.scheme, disk]
    }

    /// GPT is tried first; if setting the drive up fails, MBR is tried once, when MBR can hold this format and size.
    public static func fallback(for format: DiskFormat, size: Int64) -> DiskFormat? {
        guard format.gpt else { return nil }
        let mbr = format.with(gpt: false)
        return WipeMethods.formatProblem(size: size, format: mbr) == nil ? mbr : nil
    }

    /// diskutil said it needs to run as root (or couldn't open the disk, which is what it says without the rights).
    public static func needsAdministrator(_ output: String) -> Bool {
        let text = output.lowercased()
        return ["must be run as root", "requires root", "root privileges", "not privileged", "permission denied",
                "operation not permitted", "-69877", "couldn't open device", "could not open device"].contains { text.contains($0) }
    }

    /// The line where diskutil says what went wrong.
    public static func errorLine(_ output: String) -> String? {
        let lines = output.components(separatedBy: CharacterSet.newlines.union(CharacterSet(charactersIn: "\r")))
            .map { $0.trimmingCharacters(in: .whitespaces) }
            .filter { !$0.isEmpty && !$0.hasPrefix("@@") }
        let markers = ["error", "couldn't", "could not", "failed", "unable"]
        guard let line = lines.last(where: { line in markers.contains { line.lowercased().contains($0) } }) ?? lines.last else { return nil }
        return line.count > 240 ? String(line.prefix(240)) + "…" : line
    }

    /// diskutil's progress, such as "[ / 0%..10%..20%.......... ] 23% 0:41:02": the percent done and the time left.
    public static func progress(in output: String) -> (percent: Double?, remaining: String?) {
        let text = output as NSString
        let range = NSRange(location: 0, length: text.length)
        let bar = try! NSRegularExpression(pattern: "\\]\\s*(\\d{1,3})%(?:\\s+(\\d+:\\d{2}(?::\\d{2})?))?")
        if let match = bar.matches(in: output, range: range).last {
            let percent = Double(text.substring(with: match.range(at: 1)))
            let remaining = match.range(at: 2).location == NSNotFound ? nil : text.substring(with: match.range(at: 2))
            return (percent.map { min(100, $0) }, remaining)
        }
        let plain = try! NSRegularExpression(pattern: "(\\d{1,3})%")
        if let match = plain.matches(in: output, range: range).last {
            return (Double(text.substring(with: match.range(at: 1))).map { min(100, $0) }, nil)
        }
        return (nil, nil)
    }

    /// A shell word that stays one word whatever it holds.
    public static func shellQuote(_ word: String) -> String {
        "'" + word.replacingOccurrences(of: "'", with: "'\\''") + "'"
    }

    /// The administrator script for the steps from `start`: each step writes a marker and its output to the log, the
    /// passes are skipped once the stop file exists, a failed setup tries MBR once, and the first failure ends it.
    public static func administratorScript(_ steps: [DiskStep], start: Int, disk: String, size: Int64, log: String, stopFile: String) -> String {
        let diskutil = WipeDisks.diskutilPath
        let out = ">> " + shellQuote(log) + " 2>&1"
        func command(_ arguments: [String]) -> String { ([diskutil] + arguments).map(shellQuote).joined(separator: " ") + " " + out }
        var lines: [String] = []
        for index in start..<steps.count {
            let step = steps[index]
            var line = "echo '@@step \(index)' \(out); " + command(step.arguments) + "; s=$?; echo \"@@exit \(index) $s\" \(out)"
            if case .setup(let format) = step.kind, let mbr = fallback(for: format, size: size) {
                line += "; if [ $s -ne 0 ]; then echo '@@fallback' \(out); " + command(setupArguments(disk: disk, format: mbr))
                    + "; s=$?; echo \"@@exit fallback $s\" \(out); fi"
            }
            line += "; [ $s -eq 0 ] || exit $s"
            if step.isPass {
                line = "if [ -e \(shellQuote(stopFile)) ]; then echo '@@stopped' \(out); else \(line); fi"
            }
            lines.append(line)
        }
        return lines.joined(separator: "; ")
    }

    /// `do shell script "…" with administrator privileges`, with the script escaped for an AppleScript string.
    public static func appleScript(_ shell: String) -> String {
        let escaped = shell.replacingOccurrences(of: "\\", with: "\\\\").replacingOccurrences(of: "\"", with: "\\\"")
        return "do shell script \"\(escaped)\" with administrator privileges"
    }

    /// What the administrator script's log says: each step's exit code, whether MBR was tried and how it went,
    /// whether the passes were stopped, and the step running last with its output since.
    public struct LogState: Equatable {
        public var exits: [Int: Int32] = [:]
        public var fallbackExit: Int32?
        public var fellBack = false
        public var stopped = false
        public var current: Int?
        public var currentOutput = ""
    }

    public static func readLog(_ text: String) -> LogState {
        var state = LogState()
        for raw in text.components(separatedBy: "\n") {
            let line = raw.trimmingCharacters(in: .whitespaces)
            if line.hasPrefix("@@step ") {
                state.current = Int(line.dropFirst(7))
                state.currentOutput = ""
            } else if line.hasPrefix("@@exit fallback ") {
                state.fallbackExit = Int32(line.dropFirst(16))
            } else if line.hasPrefix("@@exit ") {
                let parts = line.dropFirst(7).split(separator: " ")
                if parts.count == 2, let step = Int(parts[0]), let code = Int32(parts[1]) { state.exits[step] = code }
            } else if line == "@@fallback" {
                state.fellBack = true
                state.currentOutput = ""
            } else if line == "@@stopped" {
                state.stopped = true
            } else {
                state.currentOutput += raw + "\n"
            }
        }
        return state
    }

    /// Erases one drive. Runs as this user first (diskutil erases external drives without the administrator
    /// password); if diskutil says it needs root, the rest of the job runs as the administrator with one password
    /// prompt. A stop ends the overwrite passes, then the drive is still set up so macOS can use it.
    public static func erase(disk: WipeDisk, steps: [DiskStep], runner: DiskCommandRunner, stop: WipeStop,
                             workFolder: String, report: @escaping (DiskEraseStatus) -> Void) -> DiskEraseResult {
        var result = DiskEraseResult()
        result.passes = steps.filter(\.isPass).count
        var index = 0
        while index < steps.count {
            let step = steps[index]
            if step.isPass && stop.isStopped {
                result.stopped = true
                index += 1
                continue
            }
            report(DiskEraseStatus(step: step))
            var seen = ""
            let ran = runner.run(step.arguments) { text in
                seen += text
                if seen.count > 8192 { seen = String(seen.suffix(4096)) }
                let found = progress(in: seen)
                if found.percent != nil { report(DiskEraseStatus(step: step, percent: found.percent, remaining: found.remaining)) }
            }
            if ran.code == 0 {
                finished(step, &result)
                index += 1
                continue
            }
            if step.isPass && stop.isStopped {
                result.stopped = true
                index += 1
                continue
            }
            if needsAdministrator(ran.text) {
                return eraseAsAdministrator(disk: disk, steps: steps, from: index, result: result, runner: runner, stop: stop,
                                            workFolder: workFolder, report: report)
            }
            switch step.kind {
            case .setup(let format):
                if let mbr = fallback(for: format, size: disk.size) {
                    let second = runner.run(setupArguments(disk: disk.id, format: mbr)) { _ in }
                    if second.code == 0 {
                        result.setUp = true
                        result.format = mbr
                        result.fellBackToMBR = true
                        return result
                    }
                    result.setupError = errorLine(second.text) ?? errorLine(ran.text)
                } else {
                    result.setupError = errorLine(ran.text)
                }
                return result
            case .prepare, .clearMap:
                result.setupError = errorLine(ran.text)
                return result
            case .pass:
                result.error = errorLine(ran.text) ?? "diskutil stopped with code \(ran.code)."
                return result
            }
        }
        return result
    }

    private static func finished(_ step: DiskStep, _ result: inout DiskEraseResult) {
        switch step.kind {
        case .pass: result.passesDone += 1
        case .prepare: break
        case .clearMap: result.setUp = true
        case .setup(let format): result.setUp = true; result.format = format
        }
    }

    private static func eraseAsAdministrator(disk: WipeDisk, steps: [DiskStep], from start: Int, result: DiskEraseResult,
                                             runner: DiskCommandRunner, stop: WipeStop, workFolder: String,
                                             report: @escaping (DiskEraseStatus) -> Void) -> DiskEraseResult {
        var result = result
        result.asAdministrator = true
        let manager = FileManager.default
        let folder = (workFolder as NSString).appendingPathComponent("kodiak-wipe-" + UUID().uuidString)
        try? manager.createDirectory(atPath: folder, withIntermediateDirectories: true, attributes: [.posixPermissions: 0o700])
        defer { try? manager.removeItem(atPath: folder) }
        let log = (folder as NSString).appendingPathComponent("diskutil.log")
        let stopFile = (folder as NSString).appendingPathComponent("stop")
        manager.createFile(atPath: log, contents: nil)
        let script = administratorScript(steps, start: start, disk: disk.id, size: disk.size, log: log, stopFile: stopFile)

        // Watch the log while the administrator's script runs: progress for the screen, and the stop file for Stop.
        let watching = WipeStop()
        let watcher = Thread {
            var stopWritten = false
            while !watching.isStopped {
                if stop.isStopped && !stopWritten {
                    manager.createFile(atPath: stopFile, contents: Data())
                    stopWritten = true
                }
                let text = (try? String(contentsOfFile: log, encoding: .utf8)) ?? ""
                let state = readLog(text)
                if let current = state.current, current < steps.count {
                    let found = progress(in: String(state.currentOutput.suffix(4096)))
                    report(DiskEraseStatus(step: steps[current], percent: found.percent, remaining: found.remaining, asAdministrator: true))
                }
                Thread.sleep(forTimeInterval: 0.5)
            }
            watching.finish()
        }
        watcher.start()
        let ran = runner.runAsAdministrator(script)
        watching.stop()
        watching.wait(seconds: 2)

        let state = readLog((try? String(contentsOfFile: log, encoding: .utf8)) ?? "")
        if state.exits.isEmpty && ran.code != 0 {
            result.error = ran.error == "Cancelled" ? "The administrator password prompt was cancelled, so it wasn't erased."
                : (errorLine(ran.error) ?? "macOS didn't run the erase as the administrator.")
            return result
        }
        if state.stopped { result.stopped = true }
        for index in start..<steps.count {
            let step = steps[index]
            guard let code = state.exits[index] else { continue }
            if code == 0 { finished(step, &result); continue }
            switch step.kind {
            case .setup(let format):
                if state.fellBack, state.fallbackExit == 0, let mbr = fallback(for: format, size: disk.size) {
                    result.setUp = true
                    result.format = mbr
                    result.fellBackToMBR = true
                } else {
                    result.setupError = errorLine(state.currentOutput)
                }
            case .prepare, .clearMap:
                result.setupError = errorLine(state.currentOutput)
            case .pass:
                result.error = errorLine(state.currentOutput) ?? "diskutil stopped with code \(code)." 
            }
        }
        return result
    }
}

/// Runs the real diskutil, and the administrator's script through osascript.
public final class SystemDiskRunner: DiskCommandRunner {
    private let lock = NSLock()
    private var process: Process?

    public init() {}

    public func run(_ arguments: [String], output: @escaping (String) -> Void) -> (code: Int32, text: String) {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: WipeDisks.diskutilPath)
        process.arguments = arguments
        process.standardInput = FileHandle.nullDevice
        let pipe = Pipe()
        process.standardOutput = pipe
        process.standardError = pipe
        let collected = Collector()
        pipe.fileHandleForReading.readabilityHandler = { handle in
            let data = handle.availableData
            guard !data.isEmpty else { return }
            let text = String(decoding: data, as: UTF8.self)
            collected.append(text)
            output(text)
        }
        do { try process.run() } catch {
            pipe.fileHandleForReading.readabilityHandler = nil
            return (-1, error.localizedDescription)
        }
        lock.lock(); self.process = process; lock.unlock()
        process.waitUntilExit()
        lock.lock(); self.process = nil; lock.unlock()
        pipe.fileHandleForReading.readabilityHandler = nil
        let rest = pipe.fileHandleForReading.readDataToEndOfFile()
        if !rest.isEmpty {
            let text = String(decoding: rest, as: UTF8.self)
            collected.append(text)
            output(text)
        }
        return (process.terminationStatus, collected.text)
    }

    public func interrupt() {
        lock.lock(); let running = process; lock.unlock()
        running?.interrupt()
    }

    public func runAsAdministrator(_ script: String) -> (code: Int32, error: String) {
        let result = Run.program("/usr/bin/osascript", ["-e", DiskEraser.appleScript(script)], timeout: 60 * 60 * 24 * 30)
        return (result.code, result.error.contains("-128") ? "Cancelled" : result.error)
    }

    private final class Collector: @unchecked Sendable {
        private let lock = NSLock()
        private var value = ""
        func append(_ text: String) { lock.lock(); value += text; lock.unlock() }
        var text: String { lock.lock(); defer { lock.unlock() }; return value }
    }
}
