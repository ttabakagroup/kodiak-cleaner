import Foundation
#if canImport(Darwin)
import Darwin
#else
import Glibc
#endif

/// Where Kodiak Cleaner keeps its own files, all inside the person's Library folder. Tests point `home` at a
/// throwaway folder.
public enum Paths {
    nonisolated(unsafe) public static var home: String = FileManager.default.homeDirectoryForCurrentUser.path
    nonisolated(unsafe) public static var temporary: String = NSTemporaryDirectory()

    public static var appSupport: String { home + "/Library/Application Support/Kodiak Cleaner" }
    public static var settingsFile: String { appSupport + "/Settings.json" }
    public static var backups: String { appSupport + "/Backups" }
    public static var triggersHistory: String { backups + "/Triggers History" }

    public static func expandTilde(_ path: String) -> String {
        if path == "~" { return home }
        if path.hasPrefix("~/") { return home + String(path.dropFirst(1)) }
        if path.hasPrefix("$TMPDIR") { return temporary.trimmingSlash + String(path.dropFirst(7)) }
        return path
    }

    /// The path with links resolved (/var becomes /private/var), or the path itself when it doesn't exist.
    public static func real(_ path: String) -> String {
        var buffer = [CChar](repeating: 0, count: Int(PATH_MAX) + 1)
        guard realpath(path, &buffer) != nil else { return path }
        return String(cString: buffer)
    }

    /// The home folder with ~ instead, for showing a path.
    public static func display(_ path: String) -> String {
        path.hasPrefix(home + "/") ? "~" + path.dropFirst(home.count) : path
    }
}

extension String {
    var trimmingSlash: String { hasSuffix("/") && count > 1 ? String(dropLast()) : self }
}

/// What lstat says about a path, never following a link.
public struct FileInfo {
    public enum Kind { case file, directory, link, other }
    public var kind: Kind
    public var size: Int64
    public var modified: Double
    public var owner: UInt32
}

public enum FS {
    public static func info(_ path: String) -> FileInfo? {
        var st = stat()
        guard lstat(path, &st) == 0 else { return nil }
        let type = st.st_mode & S_IFMT
        let kind: FileInfo.Kind
        switch type {
        case S_IFREG: kind = .file
        case S_IFDIR: kind = .directory
        case S_IFLNK: kind = .link
        default: kind = .other
        }
        #if canImport(Darwin)
        let modified = Double(st.st_mtimespec.tv_sec)
        #else
        let modified = Double(st.st_mtim.tv_sec)
        #endif
        return FileInfo(kind: kind, size: Int64(st.st_size), modified: modified, owner: UInt32(st.st_uid))
    }

    /// True when the last lstat or listing failed because macOS privacy protection (or file permissions) said no.
    public static func isDenied(_ path: String) -> Bool {
        var st = stat()
        if lstat(path, &st) != 0 { return errno == EPERM || errno == EACCES }
        if (st.st_mode & S_IFMT) == S_IFDIR {
            do { _ = try FileManager.default.contentsOfDirectory(atPath: path) } catch {
                return (error as NSError).code == NSFileReadNoPermissionError
            }
        }
        return false
    }

    public static func exists(_ path: String) -> Bool { info(path) != nil }

    /// Lists a folder: nil when it can't be read, with `denied` set when permission was the reason.
    public static func list(_ path: String, denied: inout Bool) -> [String]? {
        do {
            return try FileManager.default.contentsOfDirectory(atPath: path)
        } catch {
            let code = (error as NSError).code
            if code == NSFileReadNoPermissionError { denied = true }
            return nil
        }
    }

    public static var userID: UInt32 { UInt32(getuid()) }

    /// Total size of a file or folder, never following links.
    public static func treeSize(_ path: String) -> Int64 {
        guard let info = info(path) else { return 0 }
        guard info.kind == .directory else { return info.size }
        var denied = false
        var total: Int64 = 0
        for name in list(path, denied: &denied) ?? [] {
            total += treeSize(path + "/" + name)
        }
        return total
    }

    @discardableResult
    public static func remove(_ path: String) -> Bool { unlink(path) == 0 }

    @discardableResult
    public static func removeEmptyFolder(_ path: String) -> Bool { rmdir(path) == 0 }
}

/// Expands patterns such as "~/Library/Caches/Firefox/Profiles/*/cache2" without following the last link.
public enum Glob {
    public static func matches(_ pattern: String, _ name: String) -> Bool {
        fnmatch(pattern, name, 0) == 0
    }

    public static func expand(_ pattern: String) -> (paths: [String], denied: Bool) {
        let path = Paths.expandTilde(pattern)
        var denied = false
        let parts = path.split(separator: "/").map(String.init)
        var current = [""]
        for part in parts {
            var next: [String] = []
            let wild = part.contains("*") || part.contains("?") || part.contains("[")
            for base in current {
                if wild {
                    guard let names = FS.list(base.isEmpty ? "/" : base, denied: &denied) else { continue }
                    for name in names.sorted() where matches(part, name) {
                        next.append(base + "/" + name)
                    }
                } else {
                    let candidate = base + "/" + part
                    if FS.exists(candidate) {
                        next.append(candidate)
                    } else if errno == EPERM || errno == EACCES {
                        denied = true
                    }
                }
            }
            current = next
            if current.isEmpty { break }
        }
        return (current.filter { !$0.isEmpty }, denied)
    }
}

/// Runs a program and waits. Never through a shell.
public enum Run {
    public struct Result {
        public var code: Int32
        public var output: String
        public var error: String
    }

    @discardableResult
    public static func program(_ path: String, _ arguments: [String], input: String? = nil, timeout: TimeInterval = 120) -> Result {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: path)
        process.arguments = arguments
        let out = Pipe(), err = Pipe()
        process.standardOutput = out
        process.standardError = err
        let inPipe = Pipe()
        process.standardInput = input == nil ? FileHandle.nullDevice : inPipe
        do { try process.run() } catch {
            return Result(code: -1, output: "", error: error.localizedDescription)
        }
        if let input {
            inPipe.fileHandleForWriting.write(input.data(using: .utf8) ?? Data())
            try? inPipe.fileHandleForWriting.close()
        }
        // Read before waiting so a full pipe never blocks the program.
        let outData = out.fileHandleForReading.readDataToEndOfFile()
        let errData = err.fileHandleForReading.readDataToEndOfFile()
        let deadline = Date().addingTimeInterval(timeout)
        while process.isRunning && Date() < deadline { Thread.sleep(forTimeInterval: 0.02) }
        if process.isRunning { process.terminate() }
        process.waitUntilExit()
        return Result(code: process.terminationStatus,
                      output: String(decoding: outData, as: UTF8.self),
                      error: String(decoding: errData, as: UTF8.self))
    }

    /// The full path of a command-line tool, looking where Homebrew and the system put them (apps opened from
    /// Finder get a short PATH).
    public static func which(_ name: String) -> String? {
        let fromEnvironment = (ProcessInfo.processInfo.environment["PATH"] ?? "").split(separator: ":").map(String.init)
        for folder in ["/opt/homebrew/bin", "/usr/local/bin", "/usr/bin", "/bin", "/usr/sbin", "/sbin"] + fromEnvironment {
            let path = folder + "/" + name
            if FileManager.default.isExecutableFile(atPath: path) { return path }
        }
        return nil
    }
}
