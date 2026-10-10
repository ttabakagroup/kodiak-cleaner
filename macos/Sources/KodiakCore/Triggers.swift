import Foundation
#if canImport(Darwin)
import Darwin
#else
import Glibc
#endif

/// Everything that starts by itself on a Mac: launch agents and daemons (launchd), cron jobs and shell startup files.
///
/// Every change goes through `Triggers.record` with a backup copy first, and Triggers History can put it back.
/// Apple's own items (in /System) are shown for reference only and never changed.
public struct Trigger: Identifiable, Hashable {
    public enum Kind: String, Codable, CaseIterable {
        case userAgent, globalAgent, daemon, cron, shellFile, systemShellFile, appleAgent, appleDaemon
    }

    public struct Note: Hashable {
        public var id: String
        public var text: String
        /// Counts toward Needs a Look.
        public var look: Bool
    }

    public var id: String
    public var kind: Kind
    public var label: String
    public var name: String
    public var path: String
    public var program = ""
    public var arguments: [String] = []
    public var schedule = ""
    /// nil when it can't be turned off (shell files).
    public var enabled: Bool?
    public var notes: [Note] = []
    public var modified: Double = 0
    public var cronLine = ""

    public var isProtected: Bool { kind == .appleAgent || kind == .appleDaemon }
    public var isSystem: Bool { [.globalAgent, .daemon, .systemShellFile, .appleAgent, .appleDaemon].contains(kind) }
    public var canToggle: Bool { [.userAgent, .globalAgent, .daemon, .cron].contains(kind) }
    /// Only the person's own entries can be removed.
    public var canRemove: Bool { kind == .userAgent || kind == .cron }
    public var needsAdmin: Bool { kind == .daemon }
    public var needsLook: Bool { notes.contains(where: \.look) }

    public var group: String { Triggers.groupNames[kind]! }
}

public enum Triggers {
    public static let groupOrder: [Trigger.Kind] = [.userAgent, .globalAgent, .daemon, .cron, .shellFile, .systemShellFile,
                                                    .appleAgent, .appleDaemon]
    public static let groupNames: [Trigger.Kind: String] = [
        .userAgent: "Launch Agents for You", .globalAgent: "Launch Agents for All Users", .daemon: "Launch Daemons",
        .cron: "Cron Jobs", .shellFile: "Shell Startup Files", .systemShellFile: "System Shell Startup Files",
        .appleAgent: "Apple Launch Agents", .appleDaemon: "Apple Launch Daemons",
    ]
    public static let groupBlurbs: [Trigger.Kind: String] = [
        .userAgent: "Start when you log in. Only you have them.",
        .globalAgent: "Start when anyone logs in. Turning one off here turns it off just for you.",
        .daemon: "Start with the Mac, before anyone logs in. Changes ask for your administrator password.",
        .cron: "Your own scheduled commands.",
        .shellFile: "Run each time Terminal opens a shell. Shown so you can check them.",
        .systemShellFile: "Run for everyone each time Terminal opens a shell. Shown so you can check them.",
        .appleAgent: "Part of macOS. Shown for reference only.",
        .appleDaemon: "Part of macOS. Shown for reference only.",
    ]

    static let folders: [(Trigger.Kind, String)] = [
        (.userAgent, "~/Library/LaunchAgents"), (.globalAgent, "/Library/LaunchAgents"), (.daemon, "/Library/LaunchDaemons"),
        (.appleAgent, "/System/Library/LaunchAgents"), (.appleDaemon, "/System/Library/LaunchDaemons"),
    ]
    static let shellFiles = ["~/.zshenv", "~/.zprofile", "~/.zshrc", "~/.zlogin", "~/.bash_profile", "~/.bashrc", "~/.profile"]
    static let systemShellFiles = ["/etc/zshenv", "/etc/zprofile", "/etc/zshrc", "/etc/profile", "/etc/bashrc"]
    public static let cronMarker = "#kodiak-off# "
    static let recentDays: Double = 7

    nonisolated(unsafe) public static var launchctl = "/bin/launchctl"
    nonisolated(unsafe) public static var crontab = "/usr/bin/crontab"

    // MARK: Reading

    /// Labels launchd has turned off or on, for one domain ("gui/501" or "system").
    public static func overrides(_ domain: String) -> [String: Bool] {
        parseOverrides(Run.program(launchctl, ["print-disabled", domain]).output)
    }

    /// "label" => disabled / enabled (older macOS: true / false). Returns label: turned on?
    public static func parseOverrides(_ text: String) -> [String: Bool] {
        var result: [String: Bool] = [:]
        for line in text.components(separatedBy: "\n") {
            let parts = line.components(separatedBy: "=>")
            guard parts.count == 2 else { continue }
            let label = parts[0].trimmingCharacters(in: .whitespaces).trimmingCharacters(in: CharacterSet(charactersIn: "\""))
            let value = parts[1].trimmingCharacters(in: .whitespaces).lowercased()
            if label.isEmpty { continue }
            result[label] = !(value == "disabled" || value == "true")
        }
        return result
    }

    static var uid: UInt32 { FS.userID }
    static var userDomain: String { "gui/\(uid)" }

    public static func list(includeSystem: Bool = true, includeApple: Bool = true) -> [Trigger] {
        let user = overrides(userDomain)
        let system = includeSystem ? overrides("system") : [:]
        var items: [Trigger] = []
        for (kind, folder) in folders {
            if !includeSystem && [.globalAgent, .daemon].contains(kind) { continue }
            if !includeApple && [.appleAgent, .appleDaemon].contains(kind) { continue }
            let path = Paths.expandTilde(folder)
            var denied = false
            for name in (FS.list(path, denied: &denied) ?? []).sorted() where name.hasSuffix(".plist") {
                let overrides = [.daemon, .appleDaemon].contains(kind) ? system : user
                if let item = launchItem(kind: kind, path: path + "/" + name, overrides: overrides) { items.append(item) }
            }
        }
        items += cronJobs()
        for file in shellFiles + (includeSystem ? systemShellFiles : []) {
            if let item = shellFile(Paths.expandTilde(file)) { items.append(item) }
        }
        return sorted(items)
    }

    /// Groups in their fixed order, entries A to Z inside each (Ted's rule).
    public static func sorted(_ items: [Trigger]) -> [Trigger] {
        items.sorted { a, b in
            let ga = groupOrder.firstIndex(of: a.kind)!, gb = groupOrder.firstIndex(of: b.kind)!
            if ga != gb { return ga < gb }
            let na = a.name.lowercased(), nb = b.name.lowercased()
            return na != nb ? na < nb : a.id < b.id
        }
    }

    static func appName(_ program: String) -> String? {
        guard let range = program.range(of: ".app/") else { return nil }
        let bundle = String(program[..<range.lowerBound])
        return (bundle as NSString).lastPathComponent
    }

    public static func launchItem(kind: Trigger.Kind, path: String, overrides: [String: Bool]) -> Trigger? {
        guard let data = FileManager.default.contents(atPath: path),
              let plist = try? PropertyListSerialization.propertyList(from: data, format: nil) as? [String: Any] else {
            let label = ((path as NSString).lastPathComponent as NSString).deletingPathExtension
            return Trigger(id: path, kind: kind, label: label, name: label, path: path, schedule: "Can't be read",
                           enabled: nil, notes: [Trigger.Note(id: "unreadable", text: "Its file can't be read", look: false)],
                           modified: FS.info(path)?.modified ?? 0)
        }
        let label = (plist["Label"] as? String) ?? ((path as NSString).lastPathComponent as NSString).deletingPathExtension
        let arguments = (plist["ProgramArguments"] as? [Any])?.map { "\($0)" } ?? []
        let program = (plist["Program"] as? String) ?? arguments.first ?? ""
        let disabledInFile = (plist["Disabled"] as? Bool) ?? false
        let enabled = overrides[label] ?? !disabledInFile
        var item = Trigger(id: path, kind: kind, label: label, name: appName(program) ?? label, path: path,
                           program: program, arguments: arguments, schedule: schedule(plist), enabled: enabled,
                           modified: FS.info(path)?.modified ?? 0)
        if !item.isProtected { item.notes = notes(for: item, keepAlive: plist["KeepAlive"]) }
        return item
    }

    static func schedule(_ plist: [String: Any]) -> String {
        var parts: [String] = []
        if (plist["RunAtLoad"] as? Bool) == true { parts.append("At login or startup") }
        if let seconds = plist["StartInterval"] as? Int {
            parts.append(seconds % 3600 == 0 ? "Every \(seconds / 3600) hour\(seconds == 3600 ? "" : "s")"
                         : seconds % 60 == 0 ? "Every \(seconds / 60) minute\(seconds == 60 ? "" : "s")"
                         : "Every \(seconds) seconds")
        }
        if plist["StartCalendarInterval"] != nil { parts.append("On a schedule") }
        if plist["WatchPaths"] != nil || plist["QueueDirectories"] != nil { parts.append("When files change") }
        if plist["MachServices"] != nil || plist["Sockets"] != nil { parts.append("When asked for") }
        if let keep = plist["KeepAlive"], (keep as? Bool) != false { parts.append("Kept running") }
        return parts.isEmpty ? "When started" : parts.joined(separator: ", ")
    }

    // MARK: Notes

    public static let downloadsCode = try! NSRegularExpression(
        pattern: "(curl|wget)[^|;&\\n]*\\|\\s*(sudo\\s+)?(ba|z|da)?sh\\b|base64\\s+(-d|--decode|-D)", options: [.caseInsensitive])

    static func runsFromOddPlace(_ program: String) -> Bool {
        let home = Paths.home
        let odd = ["/tmp/", "/private/tmp/", "/var/folders/", "/private/var/folders/", "/var/tmp/", "/private/var/tmp/",
                   home + "/Downloads/"]
        if odd.contains(where: { program.hasPrefix($0) }) { return true }
        return program.split(separator: "/").dropLast().contains { $0.hasPrefix(".") && $0 != "." && $0 != ".." }
    }

    static func notes(for item: Trigger, keepAlive: Any? = nil, now: Double = Date().timeIntervalSince1970) -> [Trigger.Note] {
        var notes: [Trigger.Note] = []
        let program = item.program
        if program.hasPrefix("/") && !FS.exists(program) {
            notes.append(.init(id: "missing", text: "Its program is missing", look: true))
        }
        if program.hasPrefix("/") && runsFromOddPlace(program) {
            notes.append(.init(id: "oddplace", text: "Runs from a temporary, Downloads or hidden folder", look: true))
        }
        let command = ([program] + item.arguments.dropFirst()).joined(separator: " ")
        if downloadsCode.firstMatch(in: command, range: NSRange(command.startIndex..., in: command)) != nil {
            notes.append(.init(id: "download", text: "Downloads or unpacks code and runs it", look: true))
        }
        if item.modified > 0 && now - item.modified < recentDays * Cleaner.day {
            notes.append(.init(id: "recent", text: "Added or changed in the last 7 days", look: true))
        }
        if let keep = keepAlive, (keep as? Bool) != false {
            notes.append(.init(id: "keepalive", text: "Starts again whenever it stops", look: false))
        }
        return notes
    }

    // MARK: Cron and shell files

    public static func readCrontab() -> String? {
        guard FileManager.default.isExecutableFile(atPath: crontab) else { return nil }
        let result = Run.program(crontab, ["-l"])
        return result.code == 0 ? result.output : ""
    }

    public static func cronJobs(_ text: String? = nil) -> [Trigger] {
        guard let text = text ?? readCrontab() else { return [] }
        var items: [Trigger] = []
        for (index, raw) in text.components(separatedBy: "\n").enumerated() {
            var line = raw.trimmingCharacters(in: .whitespaces)
            var enabled = true
            if line.hasPrefix(cronMarker) {
                enabled = false
                line = String(line.dropFirst(cronMarker.count))
            }
            if line.isEmpty || line.hasPrefix("#") { continue }
            // crontab(5): fields are separated by spaces or tabs.
            let fields = line.split(omittingEmptySubsequences: true, whereSeparator: { $0 == " " || $0 == "\t" })
                .map(String.init)
            // Variables such as PATH=... aren't jobs.
            if let first = fields.first, first.contains("="), !first.hasPrefix("@") { continue }
            let commandStart = line.hasPrefix("@") ? 1 : 5
            guard fields.count > commandStart else { continue }
            let command = fields[commandStart...].joined(separator: " ")
            let when = line.hasPrefix("@reboot") ? "At startup" : (line.hasPrefix("@") ? fields[0] : fields[..<5].joined(separator: " "))
            var item = Trigger(id: "cron:\(index):\(line)", kind: .cron, label: command, name: (fields[commandStart] as NSString).lastPathComponent,
                               path: "crontab", program: fields[commandStart], arguments: Array(fields[commandStart...]),
                               schedule: when, enabled: enabled, cronLine: line)
            item.notes = notes(for: item)
            items.append(item)
        }
        return items
    }

    static func shellFile(_ path: String) -> Trigger? {
        guard let info = FS.info(path), info.kind == .file || info.kind == .link else { return nil }
        let kind: Trigger.Kind = path.hasPrefix("/etc/") ? .systemShellFile : .shellFile
        var item = Trigger(id: path, kind: kind, label: path, name: (path as NSString).lastPathComponent, path: path,
                           schedule: "When a shell starts", enabled: nil, modified: info.modified)
        let text = (try? String(contentsOfFile: path, encoding: .utf8)) ?? ""
        if downloadsCode.firstMatch(in: text, range: NSRange(text.startIndex..., in: text)) != nil {
            item.notes.append(.init(id: "download", text: "Downloads or unpacks code and runs it", look: true))
        }
        if kind == .shellFile && Date().timeIntervalSince1970 - info.modified < recentDays * Cleaner.day {
            item.notes.append(.init(id: "recent", text: "Added or changed in the last 7 days", look: true))
        }
        return item
    }

    // MARK: Changes and history

    public enum Action: String, Codable { case turnedOff, turnedOn, removed }

    public struct HistoryEntry: Codable, Identifiable, Equatable {
        public var id: String
        public var date: Double
        public var action: Action
        public var kind: Trigger.Kind
        public var label: String
        public var name: String
        public var path: String
        public var backup: String
        public var cronLine: String
        public var restored: Bool
    }

    static var historyFile: String { Paths.triggersHistory + "/history.json" }

    public static func history() -> [HistoryEntry] {
        guard let data = FileManager.default.contents(atPath: historyFile),
              let entries = try? JSONDecoder().decode([HistoryEntry].self, from: data) else { return [] }
        return entries.sorted { $0.date > $1.date }
    }

    static func saveHistory(_ entries: [HistoryEntry]) {
        try? FileManager.default.createDirectory(atPath: Paths.triggersHistory, withIntermediateDirectories: true)
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        if let data = try? encoder.encode(entries) { try? data.write(to: URL(fileURLWithPath: historyFile), options: .atomic) }
    }

    static func stamp() -> String {
        let format = DateFormatter()
        format.dateFormat = "yyyy-MM-dd HH-mm-ss"
        format.locale = Locale(identifier: "en_US_POSIX")
        return format.string(from: Date())
    }

    static func safeName(_ text: String) -> String {
        String(text.map { $0.isLetter || $0.isNumber || "._-".contains($0) ? $0 : "-" }.prefix(80))
    }

    /// Copies what's about to change into Triggers History. Returns the backup's path, or nil when it couldn't be saved
    /// (and then nothing is changed).
    static func backup(_ item: Trigger) -> String? {
        try? FileManager.default.createDirectory(atPath: Paths.triggersHistory, withIntermediateDirectories: true)
        let base = Paths.triggersHistory + "/" + stamp() + " " + safeName(item.kind == .cron ? "crontab" : item.label)
        var target = base + (item.kind == .cron ? ".txt" : ".plist")
        var counter = 2
        while FS.exists(target) {
            target = base + " \(counter)" + (item.kind == .cron ? ".txt" : ".plist")
            counter += 1
        }
        if item.kind == .cron {
            guard let text = readCrontab() else { return nil }
            return FileManager.default.createFile(atPath: target, contents: Data(text.utf8)) ? target : nil
        }
        do { try FileManager.default.copyItem(atPath: item.path, toPath: target) } catch { return nil }
        return target
    }

    /// Records a change that was made. Every change type must call this with a backup taken first.
    static func record(_ action: Action, _ item: Trigger, backup: String) {
        var entries = history()
        entries.append(HistoryEntry(id: UUID().uuidString, date: Date().timeIntervalSince1970, action: action, kind: item.kind,
                                    label: item.label, name: item.name, path: item.path, backup: backup,
                                    cronLine: item.cronLine, restored: false))
        saveHistory(entries)
    }

    static let labelPattern = try! NSRegularExpression(pattern: "^[A-Za-z0-9][A-Za-z0-9._-]{0,200}$")
    static let pathPattern = try! NSRegularExpression(pattern: "^/Library/LaunchDaemons/[A-Za-z0-9._-]+\\.plist$")

    static func matches(_ regex: NSRegularExpression, _ text: String) -> Bool {
        regex.firstMatch(in: text, range: NSRange(text.startIndex..., in: text)) != nil
    }

    /// Runs commands as the administrator through macOS's own password prompt, once for the whole batch. Only the
    /// fixed launchctl forms below, with labels and paths checked first, ever reach it.
    nonisolated(unsafe) public static var runAsAdmin: ([String]) -> (Bool, String) = { commands in
        let script = commands.joined(separator: "; ")
        let escaped = script.replacingOccurrences(of: "\\", with: "\\\\").replacingOccurrences(of: "\"", with: "\\\"")
        let result = Run.program("/usr/bin/osascript", ["-e", "do shell script \"\(escaped)\" with administrator privileges"],
                                 timeout: 600)
        return (result.code == 0, result.error.contains("-128") ? "Cancelled" : result.error)
    }

    /// One change or restore at a time: each reads and rewrites the crontab and history.json, so two at once (a
    /// double-clicked Restore, or a second change while the administrator prompt is up) could lose one.
    static let changeLock = NSRecursiveLock()

    /// Turns entries on or off, or removes them. Returns error messages by trigger id (empty when all went well).
    public static func change(_ items: [Trigger], to action: Action) -> [String: String] {
        changeLock.lock()
        defer { changeLock.unlock() }
        var errors: [String: String] = [:]
        var adminCommands: [String] = []
        var adminItems: [(Trigger, String)] = []
        var crontabText = readCrontab()
        var crontabChanged: [(Trigger, String)] = []
        for item in items {
            if item.isProtected || !item.canToggle || (action == .removed && !item.canRemove) {
                errors[item.id] = "Kodiak Cleaner doesn't change this one"
                continue
            }
            if item.kind != .cron && !matches(labelPattern, item.label) {
                errors[item.id] = "Its name has characters Kodiak Cleaner won't pass on"
                continue
            }
            guard let saved = backup(item) else {
                errors[item.id] = "The backup copy couldn't be saved, so nothing was changed"
                continue
            }
            switch item.kind {
            case .userAgent, .globalAgent:
                let service = "\(userDomain)/\(item.label)"
                switch action {
                case .turnedOff:
                    let result = Run.program(launchctl, ["disable", service])
                    _ = Run.program(launchctl, ["bootout", service])
                    if result.code != 0 { errors[item.id] = result.error.trimmingCharacters(in: .whitespacesAndNewlines) }
                case .turnedOn:
                    let result = Run.program(launchctl, ["enable", service])
                    _ = Run.program(launchctl, ["bootstrap", userDomain, item.path])
                    if result.code != 0 { errors[item.id] = result.error.trimmingCharacters(in: .whitespacesAndNewlines) }
                case .removed:
                    _ = Run.program(launchctl, ["bootout", service])
                    if (try? FileManager.default.removeItem(atPath: item.path)) == nil {
                        errors[item.id] = "The file couldn't be removed"
                    }
                }
                if errors[item.id] == nil { record(action, item, backup: saved) }
            case .daemon:
                guard matches(pathPattern, item.path) else {
                    errors[item.id] = "Its file isn't where Kodiak Cleaner expects launch daemons"
                    continue
                }
                let service = "system/\(item.label)"
                if action == .turnedOff {
                    adminCommands.append("/bin/launchctl disable \(service)")
                    adminCommands.append("/bin/launchctl bootout \(service) 2>/dev/null || true")
                } else {
                    adminCommands.append("/bin/launchctl enable \(service)")
                    adminCommands.append("/bin/launchctl bootstrap system '\(item.path)' 2>/dev/null || true")
                }
                adminItems.append((item, saved))
            case .cron:
                guard var text = crontabText else {
                    errors[item.id] = "The crontab couldn't be read"
                    continue
                }
                var lines = text.components(separatedBy: "\n")
                let target = item.enabled == false ? cronMarker + item.cronLine : item.cronLine
                guard let index = lines.firstIndex(where: { $0.trimmingCharacters(in: .whitespaces) == target }) else {
                    errors[item.id] = "It changed since the list was read. Refresh and try again."
                    continue
                }
                switch action {
                case .turnedOff: lines[index] = cronMarker + item.cronLine
                case .turnedOn: lines[index] = item.cronLine
                case .removed: lines.remove(at: index)
                }
                text = lines.joined(separator: "\n")
                crontabText = text
                crontabChanged.append((item, saved))
            default:
                errors[item.id] = "Kodiak Cleaner doesn't change this one"
            }
        }
        if !adminCommands.isEmpty {
            let (ok, message) = runAsAdmin(adminCommands)
            for (item, saved) in adminItems {
                if ok { record(action, item, backup: saved) } else { errors[item.id] = message.isEmpty ? "Didn't finish" : message }
            }
        }
        if !crontabChanged.isEmpty, let text = crontabText {
            let body = text.hasSuffix("\n") ? text : text + "\n"
            let result = Run.program(crontab, ["-"], input: body)
            for (item, saved) in crontabChanged {
                if result.code == 0 { record(action, item, backup: saved) } else { errors[item.id] = result.error }
            }
        }
        return errors
    }

    /// Puts a change back. Returns an error message, or nil when it worked.
    public static func restore(_ entry: HistoryEntry) -> String? {
        changeLock.lock()
        defer { changeLock.unlock() }
        // The entry may be stale (a second click on Restore): put a change back only once.
        if history().first(where: { $0.id == entry.id })?.restored == true { return nil }
        let item: Trigger
        switch entry.action {
        case .removed:
            if entry.kind == .cron {
                guard let text = readCrontab() else { return "The crontab couldn't be read" }
                if text.components(separatedBy: "\n").contains(where: { $0.trimmingCharacters(in: .whitespaces) == entry.cronLine }) {
                    markRestored(entry)
                    return nil
                }
                let body = (text.isEmpty || text.hasSuffix("\n") ? text : text + "\n") + entry.cronLine + "\n"
                let result = Run.program(crontab, ["-"], input: body)
                if result.code != 0 { return result.error }
            } else {
                if FS.exists(entry.path) { return "A file with that name is already there" }
                do {
                    try FileManager.default.createDirectory(atPath: (entry.path as NSString).deletingLastPathComponent,
                                                            withIntermediateDirectories: true)
                    try FileManager.default.copyItem(atPath: entry.backup, toPath: entry.path)
                } catch { return "The backup copy couldn't be put back" }
                _ = Run.program(launchctl, ["bootstrap", userDomain, entry.path])
            }
            markRestored(entry)
            return nil
        case .turnedOff, .turnedOn:
            item = Trigger(id: entry.path, kind: entry.kind, label: entry.label, name: entry.name, path: entry.path,
                           enabled: entry.action == .turnedOn, cronLine: entry.cronLine)
        }
        let errors = change([item], to: entry.action == .turnedOff ? .turnedOn : .turnedOff)
        if let error = errors.values.first { return error }
        markRestored(entry)
        return nil
    }

    static func markRestored(_ entry: HistoryEntry) {
        var entries = history()
        if let index = entries.firstIndex(where: { $0.id == entry.id }) { entries[index].restored = true }
        saveHistory(entries)
    }
}
