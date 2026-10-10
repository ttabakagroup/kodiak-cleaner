import Foundation

/// What the Cleaner can clean, how it measures it, and how it cleans.
///
/// Order rule (Ted): the macOS group first, then programs A to Z, Advanced Cleaning last, items A to Z inside every
/// group. `Cleaner.ordered` does the sorting; add new entries anywhere in `catalog()` and never hand-order them.
///
/// Safety rules: files are only removed inside the person's home folder and their own temporary folder; links are
/// removed, never followed; nothing outside the home folder is ever touched, and nothing needs the administrator
/// password.
public struct CleanTarget: Identifiable, Hashable {
    public var id: String
    public var group: String
    public var name: String
    public var description: String
    /// Globs under ~ (or $TMPDIR). Folders are emptied, files removed.
    public var patterns: [String] = []
    /// Names (globs) skipped directly inside each matched folder.
    public var exclude: [String] = []
    /// A command run as the person, e.g. brew cleanup. The first item is a tool name looked up with Run.which.
    public var command: [String] = []
    /// Paths measured before and after a command to see what it freed.
    public var measure: [String] = []
    public var enabledByDefault = false
    /// Caution text: confirm on tick, skipped by Select All.
    public var warning = ""
    /// Bundle IDs (or program names) that must be closed first.
    public var processes: [String] = []
    /// Paths or commands ("cmd:brew") that show the program is installed. Empty = always shown.
    public var present: [String] = []
    /// With Keep Temporary Files from the Last Day on, only older files go.
    public var minAgeDays = 0
    public var onlyOwnFiles = false
    /// macOS keeps this behind privacy protection: Kodiak Cleaner needs Full Disk Access to see it.
    public var needsFullDiskAccess = false
    /// The app whose icon the group shows.
    public var appBundleID = ""

    public var advanced: Bool { group == Cleaner.advancedGroup }
}

public struct Finding {
    public var target: CleanTarget
    public var size: Int64 = 0
    public var count = 0
    public var files: [String] = []
    public var note = ""
    public var skipped = ""
    public var unknownSize = false
    public var needsAccess = false
    /// Keep Temporary Files from the Last Day, as it was when this was found; cleaning checks each file's age again.
    public var keepRecent = true
}

public enum Cleaner {
    public static let systemGroup = "macOS"
    public static let advancedGroup = "Advanced Cleaning"
    public static let day: Double = 24 * 3600
    public static let needsAccessNote = "Needs Full Disk Access"

    // MARK: Browsers

    struct Browser {
        var name: String
        var bundleID: String
        var configs: [String]
        var caches: [String]
        /// Profile folder globs inside each config folder. Opera keeps one profile in the folder itself ("").
        var profiles: [String] = ["Default", "Profile *"]
    }

    static let chromiumBrowsers = [
        Browser(name: "Arc", bundleID: "company.thebrowser.Browser",
                configs: ["~/Library/Application Support/Arc/User Data"], caches: ["~/Library/Caches/Arc/User Data"]),
        Browser(name: "Brave", bundleID: "com.brave.Browser",
                configs: ["~/Library/Application Support/BraveSoftware/Brave-Browser"],
                caches: ["~/Library/Caches/BraveSoftware/Brave-Browser"]),
        Browser(name: "Chromium", bundleID: "org.chromium.Chromium",
                configs: ["~/Library/Application Support/Chromium"], caches: ["~/Library/Caches/Chromium"]),
        Browser(name: "Google Chrome", bundleID: "com.google.Chrome",
                configs: ["~/Library/Application Support/Google/Chrome"], caches: ["~/Library/Caches/Google/Chrome"]),
        Browser(name: "Helium", bundleID: "net.imput.helium",
                configs: ["~/Library/Application Support/net.imput.helium"], caches: ["~/Library/Caches/net.imput.helium"]),
        Browser(name: "Microsoft Edge", bundleID: "com.microsoft.edgemac",
                configs: ["~/Library/Application Support/Microsoft Edge"], caches: ["~/Library/Caches/Microsoft Edge"]),
        Browser(name: "Opera", bundleID: "com.operasoftware.Opera",
                configs: ["~/Library/Application Support/com.operasoftware.Opera"],
                caches: ["~/Library/Caches/com.operasoftware.Opera"], profiles: [""]),
        Browser(name: "Vivaldi", bundleID: "com.vivaldi.Vivaldi",
                configs: ["~/Library/Application Support/Vivaldi"], caches: ["~/Library/Caches/Vivaldi"]),
    ]

    static let firefoxBrowsers = [
        Browser(name: "Firefox", bundleID: "org.mozilla.firefox",
                configs: ["~/Library/Application Support/Firefox/Profiles"], caches: ["~/Library/Caches/Firefox/Profiles"]),
        Browser(name: "Floorp", bundleID: "one.ablaze.floorp",
                configs: ["~/Library/Application Support/Floorp/Profiles"], caches: ["~/Library/Caches/Floorp/Profiles"]),
        Browser(name: "LibreWolf", bundleID: "org.mozilla.librewolf",
                configs: ["~/Library/Application Support/librewolf/Profiles"], caches: ["~/Library/Caches/librewolf/Profiles"]),
        Browser(name: "Thunderbird", bundleID: "org.mozilla.thunderbird",
                configs: ["~/Library/Thunderbird/Profiles"], caches: ["~/Library/Caches/Thunderbird/Profiles"]),
        Browser(name: "Zen", bundleID: "app.zen-browser.zen",
                configs: ["~/Library/Application Support/zen/Profiles"], caches: ["~/Library/Caches/zen/Profiles"]),
    ]

    static func key(_ name: String) -> String { name.lowercased().filter { $0.isLetter } }

    static func chromiumTargets() -> [CleanTarget] {
        chromiumBrowsers.flatMap { b -> [CleanTarget] in
            func each(_ folders: [String], _ tails: [String]) -> [String] {
                folders.flatMap { folder in
                    b.profiles.flatMap { profile in tails.map { tail in
                        profile.isEmpty ? "\(folder)/\(tail)" : "\(folder)/\(profile)/\(tail)" } }
                }
            }
            let k = key(b.name)
            return [
                CleanTarget(id: "\(k).cache", group: b.name, name: "Cache",
                            description: "Saved copies of web pages and images \(b.name) can download again.",
                            patterns: each(b.caches, ["Cache", "Code Cache"]) + each(b.configs, ["GPUCache", "Service Worker/CacheStorage"]),
                            enabledByDefault: true, processes: [b.bundleID], present: b.configs, appBundleID: b.bundleID),
                CleanTarget(id: "\(k).cookies", group: b.name, name: "Cookies", description: "Signs you out of websites.",
                            patterns: each(b.configs, ["Cookies", "Cookies-journal", "Network/Cookies", "Network/Cookies-journal"]),
                            processes: [b.bundleID], present: b.configs, appBundleID: b.bundleID),
                CleanTarget(id: "\(k).history", group: b.name, name: "History",
                            description: "The list of websites you visited. Bookmarks stay.",
                            patterns: each(b.configs, ["History", "History-journal", "Visited Links", "Top Sites", "Top Sites-journal"]),
                            processes: [b.bundleID], present: b.configs, appBundleID: b.bundleID),
                CleanTarget(id: "\(k).session", group: b.name, name: "Session", description: "Tabs and windows to reopen next time.",
                            patterns: each(b.configs, ["Sessions"]), processes: [b.bundleID], present: b.configs,
                            appBundleID: b.bundleID),
            ]
        }
    }

    static func firefoxTargets() -> [CleanTarget] {
        firefoxBrowsers.flatMap { b -> [CleanTarget] in
            func each(_ folders: [String], _ tails: [String]) -> [String] {
                folders.flatMap { folder in tails.map { "\(folder)/*/\($0)" } }
            }
            let k = key(b.name)
            var targets = [
                CleanTarget(id: "\(k).cache", group: b.name, name: "Cache",
                            description: "Saved copies of web pages and images \(b.name) can download again.",
                            patterns: each(b.caches, ["cache2", "startupCache", "thumbnails"]),
                            enabledByDefault: true, processes: [b.bundleID], present: b.configs, appBundleID: b.bundleID),
            ]
            if b.name == "Thunderbird" { return targets }
            targets += [
                CleanTarget(id: "\(k).cookies", group: b.name, name: "Cookies", description: "Signs you out of websites.",
                            patterns: each(b.configs, ["cookies.sqlite", "cookies.sqlite-wal"]),
                            processes: [b.bundleID], present: b.configs, appBundleID: b.bundleID),
                CleanTarget(id: "\(k).forms", group: b.name, name: "Form History",
                            description: "Words you typed into forms and search boxes.",
                            patterns: each(b.configs, ["formhistory.sqlite"]), processes: [b.bundleID], present: b.configs,
                            appBundleID: b.bundleID),
                CleanTarget(id: "\(k).session", group: b.name, name: "Session", description: "Tabs and windows to reopen next time.",
                            patterns: each(b.configs, ["sessionstore.jsonlz4", "sessionstore-backups"]),
                            processes: [b.bundleID], present: b.configs, appBundleID: b.bundleID),
            ]
            return targets
        }
    }

    // MARK: The catalog

    /// Every target Kodiak Cleaner knows. `available()` drops programs that aren't on this Mac.
    public static func catalog() -> [CleanTarget] {
        let safari = "com.apple.Safari"
        var targets = [
            // macOS
            CleanTarget(id: "macos.crashreports", group: systemGroup, name: "Crash Reports",
                        description: "Reports saved after programs crashed.",
                        patterns: ["~/Library/Logs/DiagnosticReports"], enabledByDefault: true),
            CleanTarget(id: "macos.logs", group: systemGroup, name: "Logs", description: "Log files programs keep in your Library folder.",
                        patterns: ["~/Library/Logs"], exclude: ["DiagnosticReports"], enabledByDefault: true, minAgeDays: 1),
            CleanTarget(id: "macos.quicklook", group: systemGroup, name: "Quick Look Thumbnails",
                        description: "Small previews of files, made again when needed.",
                        command: ["qlmanage", "-r", "cache"]),
            CleanTarget(id: "macos.savedstate", group: systemGroup, name: "Saved Window State",
                        description: "Windows that apps reopen where you left them.",
                        patterns: ["~/Library/Saved Application State"]),
            CleanTarget(id: "macos.shellhistory", group: systemGroup, name: "Shell History",
                        description: "Commands you typed in Terminal (zsh, Bash and fish).",
                        patterns: ["~/.zsh_history", "~/.zsh_sessions", "~/.bash_history", "~/.bash_sessions",
                                   "~/.local/share/fish/fish_history"]),
            CleanTarget(id: "macos.temp", group: systemGroup, name: "Temporary Files",
                        description: "Your own temporary files that programs left behind.",
                        patterns: ["$TMPDIR"], exclude: ["com.apple.*"], enabledByDefault: true, minAgeDays: 1, onlyOwnFiles: true),
            CleanTarget(id: "macos.trash", group: systemGroup, name: "Trash", description: "Empties the Trash in your home folder.",
                        patterns: ["~/.Trash"], enabledByDefault: true, needsFullDiskAccess: true),
            // Apple apps
            CleanTarget(id: "mail.downloads", group: "Mail", name: "Mail Downloads",
                        description: "Copies of attachments you opened in Mail. The emails keep their attachments.",
                        patterns: ["~/Library/Containers/com.apple.mail/Data/Library/Mail Downloads"],
                        processes: ["com.apple.mail"], present: ["~/Library/Containers/com.apple.mail"],
                        needsFullDiskAccess: true, appBundleID: "com.apple.mail"),
            CleanTarget(id: "safari.cache", group: "Safari", name: "Cache",
                        description: "Saved copies of web pages and images Safari can download again.",
                        patterns: ["~/Library/Caches/com.apple.Safari", "~/Library/Containers/com.apple.Safari/Data/Library/Caches"],
                        enabledByDefault: true, processes: [safari], needsFullDiskAccess: true, appBundleID: safari),
            CleanTarget(id: "safari.cookies", group: "Safari", name: "Cookies", description: "Signs you out of websites.",
                        patterns: ["~/Library/Containers/com.apple.Safari/Data/Library/Cookies/Cookies.binarycookies",
                                   "~/Library/Cookies/Cookies.binarycookies"],
                        processes: [safari], needsFullDiskAccess: true, appBundleID: safari),
            CleanTarget(id: "safari.history", group: "Safari", name: "History",
                        description: "The list of websites you visited. Bookmarks stay. With Safari in iCloud on, your other devices may bring it back.",
                        patterns: ["~/Library/Safari/History.db", "~/Library/Safari/History.db-lock",
                                   "~/Library/Safari/History.db-shm", "~/Library/Safari/History.db-wal"],
                        processes: [safari], needsFullDiskAccess: true, appBundleID: safari),
            // Programs
            CleanTarget(id: "cocoapods.cache", group: "CocoaPods", name: "Download Cache",
                        description: "Pods CocoaPods can download again.",
                        patterns: ["~/Library/Caches/CocoaPods"], enabledByDefault: true, present: ["~/Library/Caches/CocoaPods"]),
            CleanTarget(id: "discord.cache", group: "Discord", name: "Cache",
                        description: "Saved images and files Discord can download again.",
                        patterns: ["~/Library/Application Support/discord/Cache", "~/Library/Application Support/discord/Code Cache",
                                   "~/Library/Application Support/discord/GPUCache"],
                        enabledByDefault: true, processes: ["com.hnc.Discord"], present: ["~/Library/Application Support/discord"],
                        appBundleID: "com.hnc.Discord"),
            CleanTarget(id: "go.cache", group: "Go", name: "Build Cache", description: "Compiled files Go makes again when needed.",
                        patterns: ["~/Library/Caches/go-build"], enabledByDefault: true, present: ["~/Library/Caches/go-build"]),
            CleanTarget(id: "homebrew.cache", group: "Homebrew", name: "Download Cache",
                        description: "Downloads Homebrew already installed.",
                        patterns: ["~/Library/Caches/Homebrew"], enabledByDefault: true, present: ["cmd:brew"]),
            CleanTarget(id: "homebrew.old", group: "Homebrew", name: "Old Versions",
                        description: "Older versions Homebrew keeps after upgrades (brew cleanup).",
                        command: ["brew", "cleanup", "--prune=all"],
                        measure: ["/opt/homebrew/Cellar", "/usr/local/Cellar", "/opt/homebrew/Caskroom", "/usr/local/Caskroom"],
                        present: ["cmd:brew"]),
            CleanTarget(id: "npm.cache", group: "npm", name: "Package Cache", description: "Downloaded packages npm can fetch again.",
                        patterns: ["~/.npm/_cacache"], enabledByDefault: true, processes: ["npm", "node"], present: ["~/.npm"]),
            CleanTarget(id: "pip.cache", group: "pip", name: "Package Cache", description: "Downloaded packages pip can fetch again.",
                        patterns: ["~/Library/Caches/pip"], enabledByDefault: true, present: ["~/Library/Caches/pip"]),
            CleanTarget(id: "slack.cache", group: "Slack", name: "Cache", description: "Saved images and files Slack can download again.",
                        patterns: ["~/Library/Application Support/Slack/Cache", "~/Library/Application Support/Slack/Code Cache",
                                   "~/Library/Application Support/Slack/GPUCache",
                                   "~/Library/Containers/com.tinyspeck.slackmacgap/Data/Library/Application Support/Slack/Cache",
                                   "~/Library/Containers/com.tinyspeck.slackmacgap/Data/Library/Application Support/Slack/Code Cache"],
                        enabledByDefault: true, processes: ["com.tinyspeck.slackmacgap"],
                        present: ["~/Library/Application Support/Slack", "~/Library/Containers/com.tinyspeck.slackmacgap"],
                        appBundleID: "com.tinyspeck.slackmacgap"),
            CleanTarget(id: "spotify.cache", group: "Spotify", name: "Cache", description: "Songs and pictures Spotify saved to play faster.",
                        patterns: ["~/Library/Caches/com.spotify.client/Data", "~/Library/Application Support/Spotify/PersistentCache"],
                        enabledByDefault: true, processes: ["com.spotify.client"],
                        present: ["~/Library/Caches/com.spotify.client", "~/Library/Application Support/Spotify"],
                        appBundleID: "com.spotify.client"),
            CleanTarget(id: "vscode.cache", group: "Visual Studio Code", name: "Cache",
                        description: "Saved files Visual Studio Code makes again when needed.",
                        patterns: ["~/Library/Application Support/Code/Cache", "~/Library/Application Support/Code/CachedData",
                                   "~/Library/Application Support/Code/Code Cache", "~/Library/Application Support/Code/GPUCache",
                                   "~/Library/Application Support/Code/logs"],
                        enabledByDefault: true, processes: ["com.microsoft.VSCode"], present: ["~/Library/Application Support/Code"],
                        appBundleID: "com.microsoft.VSCode"),
            CleanTarget(id: "xcode.deriveddata", group: "Xcode", name: "Derived Data",
                        description: "Build files Xcode makes again the next time you build.",
                        patterns: ["~/Library/Developer/Xcode/DerivedData"], enabledByDefault: true,
                        processes: ["com.apple.dt.Xcode"], present: ["~/Library/Developer/Xcode"], appBundleID: "com.apple.dt.Xcode"),
            CleanTarget(id: "xcode.simulatorcaches", group: "Xcode", name: "Simulator Caches",
                        description: "Caches the Simulator makes again when needed.",
                        patterns: ["~/Library/Developer/CoreSimulator/Caches"], enabledByDefault: true,
                        processes: ["com.apple.iphonesimulator"], present: ["~/Library/Developer/CoreSimulator"],
                        appBundleID: "com.apple.dt.Xcode"),
            CleanTarget(id: "yarn.cache", group: "Yarn", name: "Package Cache", description: "Downloaded packages Yarn can fetch again.",
                        patterns: ["~/Library/Caches/Yarn"], enabledByDefault: true, present: ["~/Library/Caches/Yarn"]),
            // Advanced Cleaning: never ticked by default
            CleanTarget(id: "advanced.cachefolder", group: advancedGroup, name: "Entire Cache Folder",
                        description: "Everything in ~/Library/Caches.",
                        patterns: ["~/Library/Caches"],
                        warning: "This empties every app's cache in your Library folder. Apps start slower once while they "
                            + "rebuild it, some may sign you out, and apps that are open may misbehave until restarted."),
            CleanTarget(id: "advanced.xcodearchives", group: advancedGroup, name: "Xcode Archives",
                        description: "Builds you archived in Xcode.",
                        patterns: ["~/Library/Developer/Xcode/Archives"],
                        warning: "Archives are what you need to send an old build again or read its crash reports. "
                            + "Keep the ones for versions people still use.",
                        processes: ["com.apple.dt.Xcode"], present: ["~/Library/Developer/Xcode/Archives"]),
            CleanTarget(id: "advanced.devicesupport", group: advancedGroup, name: "Xcode Device Support",
                        description: "Files Xcode copied from iPhones and other devices, made again when a device connects.",
                        patterns: ["~/Library/Developer/Xcode/iOS DeviceSupport", "~/Library/Developer/Xcode/watchOS DeviceSupport",
                                   "~/Library/Developer/Xcode/tvOS DeviceSupport", "~/Library/Developer/Xcode/visionOS DeviceSupport"],
                        processes: ["com.apple.dt.Xcode"], present: ["~/Library/Developer/Xcode"]),
        ]
        targets += chromiumTargets() + firefoxTargets()
        return targets
    }

    public static func isPresent(_ target: CleanTarget) -> Bool {
        if target.present.isEmpty { return true }
        for marker in target.present {
            if marker.hasPrefix("cmd:") {
                if Run.which(String(marker.dropFirst(4))) != nil { return true }
            } else if !Glob.expand(marker).paths.isEmpty {
                return true
            }
        }
        return false
    }

    public static func available() -> [CleanTarget] { catalog().filter(isPresent) }

    static func groupRank(_ group: String) -> Int {
        group == systemGroup ? 0 : (group == advancedGroup ? 2 : 1)
    }

    public struct History: Codable, Equatable {
        public var lastCleaned: Double
        public var freed: Int64
        public init(lastCleaned: Double, freed: Int64) { self.lastCleaned = lastCleaned; self.freed = freed }
    }

    /// Sorts the list. "default": macOS first, programs A to Z, Advanced Cleaning last, ticked items first in each
    /// group then A to Z. "az": plain A to Z by group and name. "largest", "last" and "freed" sort by numbers first.
    public static func ordered(_ targets: [CleanTarget], mode: String = "default", selected: Set<String> = [],
                               history: [String: History] = [:], sizes: [String: Int64] = [:]) -> [CleanTarget] {
        func az(_ a: CleanTarget, _ b: CleanTarget) -> Bool {
            let ga = a.group.lowercased(), gb = b.group.lowercased()
            return ga != gb ? ga < gb : a.name.lowercased() < b.name.lowercased()
        }
        func standard(_ a: CleanTarget, _ b: CleanTarget) -> Bool {
            let ra = groupRank(a.group), rb = groupRank(b.group)
            if ra != rb { return ra < rb }
            return az(a, b)
        }
        switch mode {
        case "az":
            return targets.sorted(by: az)
        case "largest", "last", "freed":
            func number(_ t: CleanTarget) -> Double {
                switch mode {
                case "largest": return Double(sizes[t.id] ?? 0)
                case "last": return history[t.id]?.lastCleaned ?? 0
                default: return Double(history[t.id]?.freed ?? 0)
                }
            }
            return targets.sorted { a, b in
                let na = number(a), nb = number(b)
                return na != nb ? na > nb : standard(a, b)
            }
        default:
            return targets.sorted { a, b in
                let ra = groupRank(a.group), rb = groupRank(b.group)
                if ra != rb { return ra < rb }
                let ga = a.group.lowercased(), gb = b.group.lowercased()
                if ga != gb { return ga < gb }
                let sa = selected.contains(a.id), sb = selected.contains(b.id)
                if sa != sb { return sa }
                return a.name.lowercased() < b.name.lowercased()
            }
        }
    }

    // MARK: Finding and cleaning

    /// The only places files are ever removed: the home folder and the person's own temporary folder.
    static func allowedRoots() -> [String] {
        [Paths.real(Paths.home), Paths.real(Paths.temporary.trimmingSlash)]
    }

    static func allowed(_ path: String) -> Bool {
        let folder = Paths.real((path as NSString).deletingLastPathComponent)
        return allowedRoots().contains { root in folder == root || folder.hasPrefix(root + "/") }
    }

    static func walk(_ start: String, target: CleanTarget, now: Double, keepRecent: Bool, top: Bool,
                     denied: inout Bool, yield: (String, Int64) -> Void) {
        guard let info = FS.info(start) else { return }
        // A pattern's own folder that is a link stays, like every folder a pattern points at.
        if top && info.kind == .link && FS.isDirectoryFollowingLinks(start) { return }
        let minAge = keepRecent ? Double(target.minAgeDays) * day : 0
        if info.kind != .directory {
            // Sockets, pipes and devices belong to running programs; only plain files and links go.
            guard info.kind == .file || info.kind == .link else { return }
            if target.onlyOwnFiles && info.owner != FS.userID { return }
            if minAge > 0 && now - info.modified < minAge { return }
            yield(start, info.size)
            return
        }
        guard let names = FS.list(start, denied: &denied) else { return }
        for name in names.sorted() {
            if top && target.exclude.contains(where: { Glob.matches($0, name) }) { continue }
            walk(start + "/" + name, target: target, now: now, keepRecent: keepRecent, top: false, denied: &denied, yield: yield)
        }
    }

    /// Looks at one target without changing anything. `running` holds the lowercased bundle IDs and names of the
    /// programs that are open, or nil to skip that check.
    public static func find(_ target: CleanTarget, keepRecent: Bool = true, running: Set<String>? = nil) -> Finding {
        var finding = Finding(target: target)
        finding.keepRecent = keepRecent
        if let running, !target.processes.isEmpty, target.processes.contains(where: { running.contains($0.lowercased()) }) {
            finding.skipped = "Close \(target.group) first"
            return finding
        }
        if !target.patterns.isEmpty {
            let now = Date().timeIntervalSince1970
            var seen = Set<String>()
            var denied = false
            for pattern in target.patterns {
                let expanded = Glob.expand(pattern)
                denied = denied || expanded.denied
                for match in expanded.paths where allowed(match + "/x") {
                    walk(match, target: target, now: now, keepRecent: keepRecent, top: true, denied: &denied) { path, size in
                        if seen.insert(path).inserted {
                            finding.files.append(path)
                            finding.size += size
                        }
                    }
                }
            }
            finding.count = finding.files.count
            if denied && target.needsFullDiskAccess {
                finding.needsAccess = true
                if finding.files.isEmpty { finding.skipped = needsAccessNote }
            }
            return finding
        }
        if !target.command.isEmpty {
            finding.unknownSize = true
            finding.note = "Size shown after cleaning"
        }
        return finding
    }

    static func removeFiles(_ finding: Finding) -> Int64 {
        var freed: Int64 = 0
        var folders = Set<String>()
        let target = finding.target
        let minAge = finding.keepRecent ? Double(target.minAgeDays) * day : 0
        let now = Date().timeIntervalSince1970
        for path in finding.files where allowed(path) {
            guard let info = FS.info(path), info.kind == .file || info.kind == .link else { continue }
            // The list can be minutes old: a file made or changed since then, or now someone else's, stays.
            if target.onlyOwnFiles && info.owner != FS.userID { continue }
            if minAge > 0 && now - info.modified < minAge { continue }
            if FS.remove(path) {
                freed += info.size
                folders.insert((path as NSString).deletingLastPathComponent)
            }
        }
        // Remove folders left empty, but never the folders the patterns pointed at.
        let roots = Set(finding.target.patterns.flatMap { Glob.expand($0).paths.map(Paths.real) })
        for start in folders.sorted(by: { $0.count > $1.count }) {
            var folder = start
            while !folder.isEmpty, folder != "/", !roots.contains(Paths.real(folder)), allowed(folder) {
                if !FS.removeEmptyFolder(folder) { break }
                folder = (folder as NSString).deletingLastPathComponent
            }
        }
        return freed
    }

    static func measured(_ paths: [String]) -> Int64 {
        paths.reduce(0) { total, pattern in
            total + Glob.expand(pattern).paths.reduce(0) { $0 + FS.treeSize($1) }
        }
    }

    public struct Result {
        public var freed: Int64
        public var message: String
    }

    /// Cleans what was found. Returns what each target freed. `running` (lowercased bundle IDs and names of the
    /// programs open now, or nil to skip that check) is checked again here, because a program may have been opened
    /// since the findings were made.
    public static func clean(_ findings: [Finding], running: Set<String>? = nil,
                             progress: ((CleanTarget) -> Void)? = nil) -> [String: Result] {
        var results: [String: Result] = [:]
        for finding in findings {
            let target = finding.target
            if !finding.skipped.isEmpty {
                results[target.id] = Result(freed: 0, message: finding.skipped)
                continue
            }
            if let running, target.processes.contains(where: { running.contains($0.lowercased()) }) {
                results[target.id] = Result(freed: 0, message: "Close \(target.group) first")
                continue
            }
            progress?(target)
            if !target.patterns.isEmpty {
                results[target.id] = Result(freed: removeFiles(finding), message: "")
            } else if let tool = target.command.first {
                guard let path = Run.which(tool) else {
                    results[target.id] = Result(freed: 0, message: "\(tool) wasn't found")
                    continue
                }
                let before = measured(target.measure)
                let run = Run.program(path, Array(target.command.dropFirst()), timeout: 1800)
                let after = measured(target.measure)
                let message = run.code == 0 ? "" : String(run.error.trimmingCharacters(in: .whitespacesAndNewlines).suffix(200))
                results[target.id] = Result(freed: max(0, before - after), message: run.code == 0 ? "" : (message.isEmpty ? "Didn't finish" : message))
            }
        }
        return results
    }
}
