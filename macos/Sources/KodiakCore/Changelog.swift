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

/// Contact & Feedback links. Nothing is sent automatically: every message opens in the person's own email app to read
/// and edit first. No hidden data in links.
public enum Feedback {
    public static let kinds = ["feedback", "bug", "idea", "security"]
    public static let subjects = [
        "feedback": "[Feedback] \(AppInfo.name) ",
        "bug": "[Bug Report] \(AppInfo.name) ",
        "idea": "[Feature Request] \(AppInfo.name) ",
        "security": "[Security] \(AppInfo.name) ",
    ]
    static let templates = [
        "feedback": "",
        "bug": "What I was doing:\n\n\nWhat I expected:\n\n\nWhat happened:\n\n\nSteps to repeat:\n1. \n",
        "idea": "What I'd like:\n\n\nWhy it would help:\n",
        "security": "What I found:\n\n\nSteps to repeat (please leave out personal data):\n1. \n",
    ]

    /// Only bug reports include the Details.
    public static func defaultBody(_ kind: String) -> String {
        var body = templates[kind] ?? ""
        if kind == "bug" { body += "\n\nDetails for Your Report:\n" + details() + "\n" }
        return body
    }

    /// What a bug report includes. Never files, folders, user names or findings.
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

    public static func link(kind: String, subject: String, body: String) -> URL? {
        let target = AppInfo.feedbackAddress
        if target.hasPrefix("mailto:") {
            return URL(string: "\(target)?subject=\(encode(subject))&body=\(encode(body))")
        }
        let joiner = target.contains("?") ? "&" : "?"
        return URL(string: "\(target)\(joiner)type=\(kind)&version=\(encode(AppInfo.versionName))")
    }
}
