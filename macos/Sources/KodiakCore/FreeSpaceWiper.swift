import Foundation

/// Asks a running wipe to stop, and says when it has finished cleaning up. Shared between threads.
public final class WipeStop: @unchecked Sendable {
    private let lock = NSLock()
    private var stopped = false
    private var done = false

    public init() {}

    public var isStopped: Bool { lock.lock(); defer { lock.unlock() }; return stopped }
    public var isDone: Bool { lock.lock(); defer { lock.unlock() }; return done }
    public func stop() { lock.lock(); stopped = true; lock.unlock() }
    public func finish() { lock.lock(); done = true; lock.unlock() }

    /// Waits (off the caller's run loop) until the wipe has cleaned up, or the time runs out.
    public func wait(seconds: TimeInterval) {
        let deadline = Date().addingTimeInterval(seconds)
        while !isDone && Date() < deadline { Thread.sleep(forTimeInterval: 0.05) }
    }
}

public struct WipeError: LocalizedError, Equatable {
    public var message: String
    public init(_ message: String) { self.message = message }
    public var errorDescription: String? { message }
}

/// Overwrites the free space of a volume so files deleted earlier can't be recovered from it (the Mac port of the
/// Windows and Linux editions' free space wiper).
///
/// Kodiak Cleaner makes a hidden folder (.kodiak-cleaner-wipe-*) in the chosen folder and fills it with files of the
/// pass's bytes, 4 MB at a time and 1 GB per file (FAT32 can't hold larger files), until the volume is full. Each
/// file is flushed to the disk, then every filler file is deleted, so the volume is full only for a moment before the
/// next pass. Stop, an error or quitting removes the filler files at once, and leftovers from a crash are removed the
/// next time a wipe starts. On the startup volume 1 GB stays free so macOS keeps running.
public enum FreeSpaceWiper {
    public static let prefix = ".kodiak-cleaner-wipe-"
    public static let chunk = 4 * 1024 * 1024
    public static let fileLimit: Int64 = 1024 * 1024 * 1024
    /// The most a pass writes past the free space the volume reported at the start.
    static let overshoot: Int64 = 256 * 1024 * 1024

    public struct Progress: Equatable {
        public var pass: Int
        public var passes: Int
        public var passName: String
        public var written: Int64
        public var total: Int64
        /// How far the whole wipe is, 0 to 1.
        public var fraction: Double {
            let inPass = total > 0 ? min(1, Double(written) / Double(total)) : 0
            return passes > 0 ? (Double(pass - 1) + inPass) / Double(passes) : 0
        }
    }

    public struct Outcome: Equatable {
        public var passesDone: Int
        public var bytes: Int64
        public var stopped: Bool
    }

    /// Bytes free for this user on the volume holding the folder.
    public static func freeBytes(_ folder: String) -> Int64 {
        if let values = try? URL(fileURLWithPath: folder).resourceValues(forKeys: [.volumeAvailableCapacityKey]),
           let free = values.volumeAvailableCapacity {
            return Int64(free)
        }
        let attributes = try? FileManager.default.attributesOfFileSystem(forPath: folder)
        return (attributes?[.systemFreeSize] as? NSNumber)?.int64Value ?? 0
    }

    /// Removes filler folders left by a wipe that didn't finish. Links are never followed. Returns how many it removed.
    @discardableResult
    public static func cleanup(_ folder: String) -> Int {
        let manager = FileManager.default
        var removed = 0
        for name in (try? manager.contentsOfDirectory(atPath: folder)) ?? [] where name.hasPrefix(prefix) {
            let path = (folder as NSString).appendingPathComponent(name)
            guard let type = (try? manager.attributesOfItem(atPath: path))?[.type] as? FileAttributeType,
                  type == .typeDirectory else { continue }
            if (try? manager.removeItem(atPath: path)) != nil { removed += 1 }
        }
        return removed
    }

    /// Overwrites the free space of the volume holding `folder` once per pass. `freeSpace` defaults to the volume's
    /// real free space; tests pass a fake. `reserve` bytes stay free. The filler folder is always removed.
    public static func wipe(folder: String, passes: [WipePass], reserve: Int64 = 0, freeSpace: ((String) -> Int64)? = nil,
                            stop: WipeStop, progress: @escaping (Progress) -> Void) throws -> Outcome {
        defer { stop.finish() }
        guard !passes.isEmpty else { throw WipeError("No passes were chosen.") }
        let measure = freeSpace ?? freeBytes
        cleanup(folder)
        let work = (folder as NSString).appendingPathComponent(prefix + UUID().uuidString)
        do {
            try FileManager.default.createDirectory(atPath: work, withIntermediateDirectories: false,
                                                    attributes: [.posixPermissions: 0o700])
        } catch {
            throw WipeError("Kodiak Cleaner can't write to \(folder): \(error.localizedDescription)")
        }
        defer { try? FileManager.default.removeItem(atPath: work) }
        var outcome = Outcome(passesDone: 0, bytes: 0, stopped: false)
        var buffer = [UInt8](repeating: 0, count: chunk)
        var generator = FastRandom()
        for (index, pass) in passes.enumerated() {
            if let fill = pass.fill { for i in buffer.indices { buffer[i] = fill } }
            let budget = max(1, measure(folder) - reserve)
            var last = Date.distantPast
            let report: (Int64) -> Void = { written in
                let now = Date()
                if now.timeIntervalSince(last) >= 0.2 {
                    last = now
                    progress(Progress(pass: index + 1, passes: passes.count, passName: pass.name, written: written, total: budget))
                }
            }
            report(0)
            let written: Int64
            do {
                written = try fill(work: work, folder: folder, random: pass.isRandom, buffer: &buffer, generator: &generator,
                                   budget: budget, reserve: reserve, measure: measure, stop: stop, report: report)
            } catch is Stopped {
                outcome.stopped = true
                return outcome
            }
            if index == 0 { outcome.bytes = written }
            try empty(work)
            outcome.passesDone += 1
            progress(Progress(pass: index + 1, passes: passes.count, passName: pass.name, written: budget, total: budget))
            if stop.isStopped {
                outcome.stopped = index + 1 < passes.count
                return outcome
            }
        }
        return outcome
    }

    private struct Stopped: Error {}

    private static func fill(work: String, folder: String, random: Bool, buffer: inout [UInt8], generator: inout FastRandom,
                             budget: Int64, reserve: Int64, measure: (String) -> Int64, stop: WipeStop,
                             report: (Int64) -> Void) throws -> Int64 {
        var written: Int64 = 0
        var index = 0
        let limit = budget + budget / 10 + overshoot
        while written < limit {
            index += 1
            let path = (work as NSString).appendingPathComponent(String(format: "fill-%05d", index))
            let fd = open(path, O_WRONLY | O_CREAT | O_EXCL | O_NOFOLLOW, 0o600)
            guard fd >= 0 else { throw WipeError("Couldn't make a filler file: \(String(cString: strerror(errno)))") }
            var full = false
            var inFile: Int64 = 0
            defer { close(fd) }
            while inFile < fileLimit {
                if stop.isStopped { throw Stopped() }
                let room = measure(folder) - reserve
                if room <= 0 { full = true; break }
                let size = Int(min(Int64(chunk), room, fileLimit - inFile, limit - written))
                if size <= 0 { full = true; break }
                if random { generator.fill(&buffer) }
                let done = buffer.withUnsafeBytes { write(fd, $0.baseAddress, size) }
                if done < 0 {
                    let code = errno
                    if code == ENOSPC || code == EDQUOT { full = true; break }
                    if code == EFBIG { break }
                    throw WipeError("Writing failed: \(String(cString: strerror(code)))")
                }
                if done == 0 { full = true; break }
                inFile += Int64(done)
                written += Int64(done)
                report(written)
            }
            if fsync(fd) != 0 && errno != ENOSPC && errno != EDQUOT {
                throw WipeError("Writing failed: \(String(cString: strerror(errno)))")
            }
            if full || inFile == 0 { break }
        }
        return written
    }

    private static func empty(_ work: String) throws {
        for name in (try? FileManager.default.contentsOfDirectory(atPath: work)) ?? [] {
            try FileManager.default.removeItem(atPath: (work as NSString).appendingPathComponent(name))
        }
        let fd = open(work, O_RDONLY)
        if fd >= 0 { _ = fsync(fd); close(fd) }
    }
}

/// Random-looking filler, fast enough to keep up with a disk: xoshiro256** seeded from the system's random numbers.
struct FastRandom {
    private var s: (UInt64, UInt64, UInt64, UInt64)

    init() {
        var system = SystemRandomNumberGenerator()
        s = (system.next(), system.next(), system.next(), system.next() | 1)
    }

    private static func rotl(_ x: UInt64, _ k: UInt64) -> UInt64 { (x << k) | (x >> (64 - k)) }

    mutating func next() -> UInt64 {
        let result = FastRandom.rotl(s.1 &* 5, 7) &* 9
        let t = s.1 << 17
        s.2 ^= s.0
        s.3 ^= s.1
        s.1 ^= s.2
        s.0 ^= s.3
        s.2 ^= t
        s.3 = FastRandom.rotl(s.3, 45)
        return result
    }

    mutating func fill(_ buffer: inout [UInt8]) {
        buffer.withUnsafeMutableBytes { raw in
            let words = raw.bindMemory(to: UInt64.self)
            for i in words.indices { words[i] = next() }
        }
    }
}
