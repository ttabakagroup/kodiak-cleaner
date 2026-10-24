import AppKit
import KodiakCore
import SwiftUI

/// A confirmation that only goes ahead once the person types the expected word (Check 2 and Check 3 of an Entire
/// Drive wipe).
struct TypedCheck: Identifiable, Equatable {
    let id = UUID()
    var title: String
    var message: String
    var expected: String
    var confirm: String
    var action: () -> Void

    static func == (a: TypedCheck, b: TypedCheck) -> Bool { a.id == b.id }

    func matches(_ typed: String) -> Bool {
        typed.trimmingCharacters(in: .whitespacesAndNewlines).caseInsensitiveCompare(expected) == .orderedSame
    }
}

/// Drive Wiper's state (Tools > Drive Wiper). The logic lives in KodiakCore; this runs it in the background and
/// keeps the page up to date.
@MainActor
final class WiperModel: ObservableObject {
    static let shared = WiperModel()

    @Published var mode = "free"            // "free" (Free Space Only) or "entire" (Entire Drive)
    @Published var volumes: [WipeVolume] = []
    @Published var volumeID: String?
    @Published var disks: [WipeDisk] = []
    /// The ticked disks: a set changed in one step per tick or drag, never rebuilt from the rows (Ted's rule).
    @Published var ticked: Set<String> = []
    @Published var level: WipeLevel = .secure
    @Published var method: WipeMethod = .dod3
    @Published var leaveUninitialized = false
    @Published var fileSystem: WipeFileSystem = .exfat
    @Published var gpt = true
    @Published var label = "Wiped"
    @Published private(set) var recommendedFor: WipeDisk?
    @Published private(set) var recommended: DiskFormat?

    @Published var loaded = false
    @Published var reading = false
    @Published var busy = false
    @Published var percent: Double?
    @Published var status = ""
    @Published var listNote = ""
    @Published var rowStatus: [String: String] = [:]
    @Published var started: Date?
    @Published var remaining: String?
    /// Stop can only end an administrator's pass once it finishes.
    @Published var stopping = false

    private var stop: WipeStop?
    private var runner: SystemDiskRunner?
    private var wipingDisks = false

    var entireDrive: Bool { mode == "entire" }
    var selectedVolume: WipeVolume? { volumes.first { $0.id == volumeID } }
    var tickedDisks: [WipeDisk] { disks.filter { ticked.contains($0.id) && $0.canWipe } }
    var wipeable: [WipeDisk] { disks.filter(\.canWipe) }

    // MARK: Reading the drives

    func refresh() {
        guard !busy, !reading else { return }
        loaded = true
        reading = true
        listNote = "Reading drives…"
        let appPath = Bundle.main.bundlePath
        Task.detached(priority: .userInitiated) {
            let volumes = WipeDisks.volumes()
            let (disks, problem) = WipeDisks.list(protectedPaths: WipeDisks.defaultProtectedPaths(appPath: appPath))
            await MainActor.run { WiperModel.shared.show(volumes: volumes, disks: disks, problem: problem) }
        }
    }

    func show(volumes: [WipeVolume], disks: [WipeDisk], problem: String?) {
        self.volumes = volumes
        if selectedVolume == nil { volumeID = volumes.first?.id }
        self.disks = disks
        let ids = Set(disks.filter(\.canWipe).map(\.id))
        ticked = ticked.intersection(ids)
        if let shown = recommendedFor, !ids.contains(shown.id) { recommendedFor = nil; recommended = nil }
        rowStatus = rowStatus.filter { ids.contains($0.key) }
        reading = false
        listNote = problem.map { "Drives couldn't be read: " + $0 }
            ?? (disks.isEmpty ? "No drives found."
                : (disks.contains(where: \.canWipe) ? "" : "Only drives macOS or Kodiak Cleaner need are attached. Plug in the drive you want to erase and press Refresh."))
    }

    // MARK: Ticking

    func tick(_ id: String, _ on: Bool) { paint([id], on) }

    /// Ticks or unticks several rows in one step (a drag, or Select All). Ticking picks the format for the last drive.
    func paint(_ ids: [String], _ on: Bool) {
        let allowed = ids.filter { id in disks.contains { $0.id == id && $0.canWipe } }
        guard !allowed.isEmpty else { return }
        if on {
            ticked.formUnion(allowed)
            if let last = allowed.last, let disk = disks.first(where: { $0.id == last }) { recommend(disk) }
        } else {
            ticked.subtract(allowed)
        }
    }

    var allTicked: Bool { !wipeable.isEmpty && wipeable.allSatisfy { ticked.contains($0.id) } }

    func selectAll(_ on: Bool) {
        if on { paint(wipeable.map(\.id), true) } else { ticked.subtract(wipeable.map(\.id)) }
    }

    // MARK: Format

    /// Ticking a drive picks the format that suits its type (Ted). It can still be changed.
    func recommend(_ disk: WipeDisk) {
        recommendedFor = disk
        let format = WipeMethods.recommended(kind: disk.kind, size: disk.size, label: label)
        recommended = format
        useRecommended()
    }

    func useRecommended() {
        guard let recommended else { return }
        gpt = recommended.gpt
        fileSystem = recommended.fileSystem
    }

    var format: DiskFormat? { leaveUninitialized ? nil : DiskFormat(fileSystem, gpt: gpt, label: label) }
    var isCustomFormat: Bool { recommended.map { $0.gpt != gpt || $0.fileSystem != fileSystem } ?? false }

    var recommendTitle: String {
        if let disk = recommendedFor { return "Recommended for \(disk.name) (\(disk.kindName), \(formatBytes(disk.size)))" }
        return "Recommended Format for Each Drive Type"
    }

    var recommendText: String {
        if let disk = recommendedFor, recommended != nil {
            return WipeMethods.recommendedWhy(kind: disk.kind, size: disk.size) + (isCustomFormat
                ? " You changed it, so your own choice will be used."
                : " It was picked when you ticked the drive, and the choices marked Recommended show it. You can still change them.")
        }
        return "Ticking a drive picks its format for you: flash drives get MBR with FAT32 (32 GB or smaller) or exFAT; other external drives get GPT with exFAT."
    }

    // MARK: Words

    var methodHint: String {
        if entireDrive && level == .quick {
            return "Quick removes the partition map (MBR or GPT) in seconds. The drive looks empty, but recovery tools could still find the old files. Use it to reuse a drive you keep."
        }
        if entireDrive && level == .thorough {
            return "Thorough writes zeros over every sector of hard drives and flash drives (NIST SP 800-88 Clear), enough for any modern drive. External SSDs get one pass of random data, because macOS has no firmware erase for them."
        }
        var text: String
        switch method {
        case .zeros: text = "One pass of zeros (NIST SP 800-88 Clear) is enough for any modern hard drive and is the quickest."
        case .dod3: text = "DoD 5220.22-M: zeros, then ones, then random data."
        case .dod7: text = "DoD 5220.22-M ECE: the three DoD passes, a random pass, then the three DoD passes again. Takes about seven times as long."
        case .pass30: text = "30 passes: the DoD pattern (zeros, ones, random) ten times over. It isn't a published standard and no modern drive needs it; it takes about 30 times as long as one pass."
        }
        if entireDrive {
            if method != .zeros { text += " macOS's disk tool writes zeros or random data but not ones, so on a whole drive each ones pass is random data." }
            text += " External SSDs always get one pass of random data instead."
        }
        return text
    }

    var driveNote: String {
        if entireDrive {
            return "Everything on the ticked drives is destroyed and can't be recovered. The startup disk, internal disks and drives that hold macOS, Kodiak Cleaner or your home folder can't be ticked."
        }
        guard let volume = selectedVolume else { return "" }
        var text = "\(formatBytes(volume.wipeable)) of free space on \(volume.name) will be overwritten. Your files are not touched."
        if volume.isStartup { text += " 1 GB stays free so macOS keeps running." }
        if volume.isSSD {
            text += " This is an SSD: macOS already clears deleted data on it in the background (TRIM), so overwriting free space mostly adds wear."
        }
        if volume.isAPFS {
            text += " APFS writes new data to new places and can keep snapshots, so old copies may survive an overwrite."
        }
        return text
    }

    var afterText: String {
        if let format {
            return "Afterwards: one \(format.partitionStyle) partition, formatted \(format.fileSystem.label) and named \(format.diskutilName)."
        }
        return "Afterwards: no partitions and no MBR or GPT at all. macOS will say it can't read the drive and offer to initialize it, like a new drive."
    }

    func methodText(_ disk: WipeDisk) -> String { WipeMethods.methodText(level, kind: disk.kind, method: method) }

    var canWipe: Bool { !busy && !reading && (entireDrive ? !tickedDisks.isEmpty : selectedVolume != nil) }

    // MARK: Wiping

    func wipeClicked() {
        if entireDrive { askToWipeDisks() } else { askToWipeFreeSpace() }
    }

    func stopClicked() {
        stop?.stop()
        runner?.interrupt()
        stopping = true
        if !wipingDisks { status = "Stopping…" }
    }

    private func begin() {
        busy = true
        stopping = false
        percent = nil
        remaining = nil
        started = Date()
        status = "Starting…"
    }

    private func end() {
        busy = false
        stopping = false
        started = nil
        remaining = nil
        stop = nil
        runner = nil
        wipingDisks = false
    }

    private func askToWipeFreeSpace() {
        guard let volume = selectedVolume else { return }
        let method = self.method
        var message = "Overwrite the \(formatBytes(volume.wipeable)) of free space on \(volume.name) with \(WipeMethods.name(method))?\n\nThis takes a while and the drive will be full until each pass finishes."
        if volume.isSSD { message += " On SSDs, macOS already erases deleted data in the background, so this mostly adds wear." }
        AppModel.shared.ask(DialogRequest(title: "Wipe Free Space?", message: message, confirm: "Wipe") {
            self.wipeFreeSpace(volume, method)
        })
    }

    private func wipeFreeSpace(_ volume: WipeVolume, _ method: WipeMethod) {
        begin()
        let stop = WipeStop()
        self.stop = stop
        let passes = WipeMethods.passes(method)
        Task.detached(priority: .userInitiated) {
            let outcome: Result<FreeSpaceWiper.Outcome, Error> = Result {
                try FreeSpaceWiper.wipe(folder: volume.folder, passes: passes, reserve: volume.reserve, stop: stop) { progress in
                    Task { @MainActor in
                        let model = WiperModel.shared
                        guard model.busy else { return }
                        model.percent = progress.fraction * 100
                        model.status = "Pass \(progress.pass) of \(progress.passes) (\(progress.passName)): \(formatBytes(progress.written)) of \(formatBytes(progress.total))"
                    }
                }
            }
            await MainActor.run {
                let model = WiperModel.shared
                switch outcome {
                case .success(let done) where done.stopped:
                    model.status = "Stopped. Temporary filler files were removed."
                case .success:
                    model.percent = 100
                    model.status = "Free space on \(volume.name) has been wiped with \(WipeMethods.name(method))."
                case .failure(let error):
                    model.status = "Wipe failed: " + error.localizedDescription
                }
                model.end()
                model.refresh()
            }
        }
    }

    /// Three checks before anything is erased (Ted): the summary, then each drive by its identifier, then the word ERASE.
    private func askToWipeDisks() {
        let chosen = tickedDisks
        guard !chosen.isEmpty else { return }
        let format = self.format
        for disk in chosen {
            if let problem = WipeMethods.formatProblem(size: disk.size, format: format) {
                AppModel.shared.tell("Choose Another Format", "\(disk.name) (\(formatBytes(disk.size))): \(problem)")
                return
            }
        }
        let levelName = WipeMethods.levelName(level)
        let list = chosen.map { "•  \($0.name) (\(formatBytes($0.size)), \($0.kindName), \($0.volumesText)): \(methodText($0))" }
            .joined(separator: "\n")
        let what = chosen.count == 1 ? "this drive" : "these \(chosen.count) drives"
        var message = "\(levelName) wipe of \(what)?\n\n\(list)\n\n\(afterText)\n\nEvery file and every partition on them is deleted, and nothing can be recovered afterwards."
        if level == .quick { message += " Quick only removes the partition map, so recovery tools might still find old files." }
        AppModel.shared.ask(DialogRequest(title: "Check 1 of 3: Erase Entire Drive", message: message, confirm: "Continue", danger: true) {
            self.after { self.askIdentifier(chosen, 0, format) }
        })
    }

    private func askIdentifier(_ chosen: [WipeDisk], _ index: Int, _ format: DiskFormat?) {
        let disk = chosen[index]
        let message = "\(index + 1) of \(chosen.count) drives\n\n\(disk.name)\n\(formatBytes(disk.size)), \(disk.kindName), \(disk.volumesText)\n\(WipeMethods.levelName(level)) wipe: \(methodText(disk))\n\nAll data on this drive will be deleted and can't be recovered. Type its disk identifier (\(disk.id)) to confirm this is the right drive:"
        AppModel.shared.sheet = .typedCheck(TypedCheck(title: "Check 2 of 3: Confirm the Drive", message: message, expected: disk.id,
                                                      confirm: "Confirm") {
            self.after {
                if index + 1 < chosen.count { self.askIdentifier(chosen, index + 1, format) } else { self.askErase(chosen, format) }
            }
        })
    }

    private func askErase(_ chosen: [WipeDisk], _ format: DiskFormat?) {
        let message = "\(chosen.count) \(chosen.count == 1 ? "drive" : "drives") will be wiped (\(WipeMethods.levelName(level))) and the data can never be recovered.\n\nType ERASE to start:"
        AppModel.shared.sheet = .typedCheck(TypedCheck(title: "Check 3 of 3: Start the Wipe", message: message, expected: "ERASE",
                                                      confirm: "Erase") {
            self.wipeDisks(chosen, format)
        })
    }

    /// Runs the next pop-up once the one before has closed.
    private func after(_ next: @escaping () -> Void) {
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.35, execute: next)
    }

    private func wipeDisks(_ chosen: [WipeDisk], _ format: DiskFormat?) {
        begin()
        wipingDisks = true
        let stop = WipeStop()
        let runner = SystemDiskRunner()
        self.stop = stop
        self.runner = runner
        let level = self.level, method = self.method
        let appPath = Bundle.main.bundlePath
        let work = Paths.temporary
        for disk in chosen { rowStatus[disk.id] = "Waiting" }
        Task.detached(priority: .userInitiated) {
            var lines: [String] = []
            for (index, disk) in chosen.enumerated() {
                if stop.isStopped { break }
                await MainActor.run {
                    let model = WiperModel.shared
                    model.rowStatus[disk.id] = "Checking…"
                    model.status = "\(disk.name): checking it's the same drive…"
                    model.percent = nil
                    model.remaining = nil
                }
                // Look again right before erasing: if the drive was swapped and another took its identifier, stop.
                let now = WipeDisks.list(protectedPaths: WipeDisks.defaultProtectedPaths(appPath: appPath)).disks.first { $0.id == disk.id }
                guard let now, now.canWipe, now.isSameDrive(as: disk) else {
                    let why = now?.reason ?? "The drive changed since it was listed. Refresh the list and choose it again."
                    lines.append("\(disk.name) wasn't erased: \(why)")
                    await MainActor.run { WiperModel.shared.rowStatus[disk.id] = "Not Erased" }
                    continue
                }
                let steps = DiskEraser.steps(disk: disk.id, level: level, kind: disk.kind, method: method, format: format)
                let result = DiskEraser.erase(disk: disk, steps: steps, runner: runner, stop: stop, workFolder: work) { status in
                    Task { @MainActor in WiperModel.shared.show(status, disk: disk, index: index, of: chosen.count) }
                }
                let line = WiperModel.summary(disk, result, level: level, method: method)
                lines.append(line)
                await MainActor.run {
                    WiperModel.shared.rowStatus[disk.id] = result.error != nil ? "Not Erased"
                        : (result.stopped ? "Stopped" : (result.setUp ? "Erased" : "Erased, Not Set Up"))
                }
            }
            await MainActor.run {
                let model = WiperModel.shared
                model.status = lines.joined(separator: "\n")
                model.percent = nil
                model.end()
                model.ticked = []
                model.refresh()
                if !lines.isEmpty { AppModel.shared.tell("Drive Wiper", lines.joined(separator: "\n\n")) }
            }
        }
    }

    private func show(_ status: DiskEraseStatus, disk: WipeDisk, index: Int, of count: Int) {
        guard busy else { return }
        let step = status.step
        if case .pass(let number, let passes, _) = step.kind {
            let fraction = (Double(number - 1) + (status.percent ?? 0) / 100) / Double(passes)
            percent = status.percent == nil && number == 1 ? nil : 100 * (Double(index) + fraction) / Double(count)
            rowStatus[disk.id] = "\(Int(fraction * 100))%"
        } else {
            percent = nil
            rowStatus[disk.id] = "Setting Up…"
        }
        remaining = status.remaining
        var text = "\(disk.name): \(step.title)"
        if let found = status.percent, step.isPass { text += ", \(Int(found))%" }
        if status.asAdministrator && stopping { text += ". Stopping once this pass finishes (it runs as the administrator)." }
        self.status = text
    }

    nonisolated static func summary(_ disk: WipeDisk, _ result: DiskEraseResult, level: WipeLevel, method: WipeMethod) -> String {
        if let error = result.error {
            return result.passesDone > 0
                ? "\(disk.name) was only partly erased (\(result.passesDone) of \(result.passes) passes): \(error)"
                : "\(disk.name) wasn't erased: \(error)"
        }
        if result.stopped {
            return "\(disk.name) was stopped part way. It's only partly erased; erase it again before giving it away."
                + (result.setUp ? "" : setupFailure(result))
        }
        let how: String
        if level == .quick { how = "cleared of its partition map (Quick)" }
        else if disk.kind == .ssd { how = "overwritten with random data (macOS has no firmware erase for external SSDs)" }
        else { how = "erased with \(WipeMethods.methodText(level, kind: disk.kind, method: method))" }
        guard result.setUp else { return "\(disk.name) was \(how)" + setupFailure(result) }
        if let format = result.format {
            return "\(disk.name) was \(how). It's now an empty \(format.partitionStyle) drive formatted \(format.fileSystem.label)"
                + (result.fellBackToMBR ? " (macOS refused GPT on it, so it was set up as MBR)." : ".")
        }
        return "\(disk.name) was \(how). It has no partitions or MBR/GPT left; macOS will offer to initialize it, like a new drive."
    }

    nonisolated private static func setupFailure(_ result: DiskEraseResult) -> String {
        ". macOS couldn't finish setting it up afterwards" + (result.setupError.map { " (\($0))" } ?? "")
            + "; open Disk Utility to erase it with a format."
    }

    /// Quitting while a wipe runs: free space wipes stop and remove their filler files first; a whole drive asks to
    /// be stopped or finished.
    func shouldQuit() -> Bool {
        guard busy else { return true }
        if wipingDisks {
            AppModel.shared.tell("A Drive Is Being Wiped", "Stop the wipe, or wait for it to finish, then quit Kodiak Cleaner.")
            return false
        }
        let stop = self.stop
        stop?.stop()
        stop?.wait(seconds: 15)
        return true
    }
}
