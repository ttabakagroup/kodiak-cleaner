import Foundation

/// CHANGELOG.md, read for the Changelog page, What's New and About.
public enum Changelog {
    public struct Release: Identifiable, Equatable {
        public var id: String { heading }
        public var version: String
        /// "0.8.0 Dev"
        public var heading: String
        public var date: String
        public var text: String
    }

    public static func text() -> String { Resources.text("CHANGELOG.md") }

    /// Every "## [0.8.0 Dev] - 2026-10-06" section, newest first as written.
    public static func parse(_ markdown: String? = nil) -> [Release] {
        var releases: [Release] = []
        var current: Release?
        var lines: [String] = []
        func finish() {
            if var release = current {
                release.text = lines.joined(separator: "\n").trimmingCharacters(in: .whitespacesAndNewlines)
                releases.append(release)
            }
            lines = []
        }
        for line in (markdown ?? text()).components(separatedBy: "\n") {
            if line.hasPrefix("## ["), let close = line.firstIndex(of: "]") {
                finish()
                let heading = String(line[line.index(line.startIndex, offsetBy: 4)..<close])
                let rest = line[line.index(after: close)...]
                let date = rest.trimmingCharacters(in: CharacterSet(charactersIn: " -"))
                current = Release(version: String(heading.split(separator: " ").first ?? ""), heading: heading,
                                  date: date, text: "")
            } else if current != nil {
                lines.append(line)
            }
        }
        finish()
        return releases
    }

    public static func latest() -> Release? { parse().first }

    /// Every release newer than the version run before, for What's New after an update.
    public static func newer(than previous: String, in releases: [Release]? = nil) -> [Release] {
        (releases ?? parse()).filter { AppInfo.isNewer($0.version, than: previous) }
    }
}

/// Contact & Feedback links. Nothing is sent automatically: every link opens the contact form on the website in the
/// person's own browser, with the topic and version filled in, to write and send there. No hidden data in links.
public enum Feedback {
    public static let kinds = ["feedback", "bug", "idea", "security"]
    /// The contact form's topics (its ?topic= values), by kind.
    public static let topics = [
        "feedback": "General Feedback",
        "bug": "Bug Report",
        "idea": "Feature Request",
        "security": "Security",
    ]

    /// What a bug report can include (Copy Details). Never files, folders, user names or findings.
    public static func details() -> String {
        [
            "\(AppInfo.name) \(AppInfo.versionName)",
            "\(AppInfo.osDescription) (\(AppInfo.processorDescription))",
            "Language: \(Locale.current.identifier)",
            "Installed As: " + (AppInfo.build.packaged ? "App" : "Source folder"),
        ].joined(separator: "\n")
    }

    static func encode(_ text: String) -> String {
        var allowed = CharacterSet.alphanumerics
        allowed.insert(charactersIn: "-._~")
        return text.addingPercentEncoding(withAllowedCharacters: allowed) ?? ""
    }

    /// "0.8.0 Dev (Mac)": the version the contact form fills into the message.
    public static var versionText: String { "\(AppInfo.versionName) (Mac)" }

    /// The contact form with the kind's topic chosen, e.g.
    /// https://www.kodiakcleaner.app/contact/?topic=Bug%20Report&version=0.8.0%20Dev%20%28Mac%29
    public static func link(kind: String) -> URL? {
        let topic = topics[kind] ?? "General Feedback"
        return URL(string: "\(AppInfo.contactURL.absoluteString)?topic=\(encode(topic))&version=\(encode(versionText))")
    }
}
