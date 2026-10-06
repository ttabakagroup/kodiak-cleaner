import Foundation

/// Names, version, channel and addresses. A built app reads its version and channel from Info.plist (written by
/// build.sh from version.json); running from the source folder reads version.json and counts as a development build.
public enum AppInfo {
    public static let name = "Kodiak Cleaner for Mac"
    public static let shortName = "Kodiak Cleaner"
    public static let slogan = "Not a Crap Cleaner. A Cleaner App, Built Around What Matters."
    public static let developer = "Ted Tabaka"
    public static let email = "www.kodiakcleaner.app/contact"
    public static let bundleID = "app.kodiakcleaner.Mac"

    public static let repository = "ttabakagroup/kodiakcleaner"
    public static let projectURL = URL(string: "https://github.com/\(repository)")!
    public static let releasesURL = URL(string: "https://github.com/\(repository)/releases")!
    public static let homepageURL = URL(string: "https://www.kodiakcleaner.app/")!
    public static let kofiURL = URL(string: "https://ko-fi.com/ttabaka")!

    /// Where Contact & Feedback messages go: mailto: or https:.
    public static let feedbackAddress = "mailto:" + email

    public static let channelLabels = ["dev": "Dev", "beta": "Beta", "stable": "Stable"]

    public struct Build {
        public var version: String
        public var channel: String
        public var release: Bool
        public var packaged: Bool
    }

    public static let build: Build = loadBuild()

    public static var version: String { build.version }
    public static var channel: String { build.channel }
    /// Development builds have the Developer page; release builds leave it out.
    public static var hasDeveloperTools: Bool { build.channel == "dev" && !build.release }
    public static var versionName: String { versionName(build.version, channel: build.channel) }

    static func loadBuild() -> Build {
        let info = Bundle.main.infoDictionary ?? [:]
        if let version = info["CFBundleShortVersionString"] as? String, let channel = info["KCChannel"] as? String {
            return Build(version: version, channel: channel, release: (info["KCReleaseBuild"] as? Bool) ?? false, packaged: true)
        }
        // Running from the source folder: version.json sits two folders above this file's folder.
        let source = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent()
            .deletingLastPathComponent().appendingPathComponent("version.json")
        if let data = try? Data(contentsOf: source),
           let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any] {
            return Build(version: json["version"] as? String ?? "0.8.0", channel: json["channel"] as? String ?? "dev",
                         release: false, packaged: false)
        }
        return Build(version: "0.8.0", channel: "dev", release: false, packaged: false)
    }

    public static func channelLabel(_ channel: String? = nil) -> String {
        channelLabels[channel ?? build.channel] ?? "Dev"
    }

    /// "0.8.0 Dev", "0.9.4 Beta" or "1.0.2". Use this wherever a version is shown or sent.
    public static func versionName(_ version: String, channel: String) -> String {
        channel == "stable" ? version : "\(version) \(channelLabel(channel))"
    }

    public static func parseVersion(_ text: String) -> [Int] {
        var parts = text.split(separator: ".").map { piece -> Int in
            Int(piece.filter(\.isNumber)) ?? 0
        }
        while parts.count < 3 { parts.append(0) }
        return Array(parts.prefix(3))
    }

    public static func isNewer(_ a: String, than b: String) -> Bool {
        parseVersion(b).lexicographicallyPrecedes(parseVersion(a))
    }

    /// "macOS 27.0.1". Never anything personal.
    public static var osDescription: String {
        let v = ProcessInfo.processInfo.operatingSystemVersion
        let patch = v.patchVersion > 0 ? ".\(v.patchVersion)" : ""
        return "macOS \(v.majorVersion).\(v.minorVersion)\(patch)"
    }

    public static var processorDescription: String {
        #if arch(arm64)
        return "Apple silicon"
        #else
        return "Intel"
        #endif
    }
}
