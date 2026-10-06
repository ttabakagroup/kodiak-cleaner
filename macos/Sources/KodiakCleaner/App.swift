import AppKit
import KodiakCore
import SwiftUI

@main
enum Launcher {
    static func main() {
        let arguments = CommandLine.arguments
        if arguments.contains("--self-test") {
            exit(SelfTest.run() ? 0 : 1)
        }
        KodiakCleanerApp.main()
    }
}

struct KodiakCleanerApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) var delegate
    @StateObject private var model = AppModel.shared

    init() { Theme.registerFonts() }

    var body: some Scene {
        Window(AppInfo.shortName, id: "main") {
            RootView().environmentObject(model)
        }
        .defaultSize(width: 1180, height: 760)
        .commands {
            CommandGroup(replacing: .appInfo) {
                Button("About Kodiak Cleaner") { model.page = .about }
            }
            CommandGroup(replacing: .appSettings) {
                Button("Settings…") { model.page = .settings }.keyboardShortcut(",")
            }
            CommandGroup(replacing: .newItem) {}
            CommandGroup(replacing: .help) {
                Button("Getting Started") { model.page = .home }
                Button("Changelog") { model.showSettings(.changelog) }
                Button("Contact & Feedback") { model.showSettings(.feedback) }
            }
        }
    }
}

final class AppDelegate: NSObject, NSApplicationDelegate {
    func applicationDidFinishLaunching(_ notification: Notification) {
        let arguments = CommandLine.arguments
        if let index = arguments.firstIndex(of: "--screenshots"), index + 1 < arguments.count {
            let folder = arguments[index + 1]
            Task { @MainActor in
                await Screenshots.run(to: folder)
                NSApp.terminate(nil)
            }
        }
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }
}

/// `KodiakCleaner --self-test` checks the parts that need a real Mac without changing anything, for the build.
enum SelfTest {
    static func run() -> Bool {
        var ok = true
        func check(_ name: String, _ passed: Bool, _ detail: String = "") {
            print("\(passed ? "PASS" : "FAIL")  \(name)\(detail.isEmpty ? "" : ": \(detail)")")
            if !passed { ok = false }
        }
        print("\(AppInfo.name) \(AppInfo.versionName) on \(AppInfo.osDescription) (\(AppInfo.processorDescription))")
        check("Version from the app's Info.plist", AppInfo.build.packaged || CommandLine.arguments.contains("--source"),
              AppInfo.versionName)
        for name in ["logo.png", "CHANGELOG.md", "PRIVACY.md", "terms-of-use.md", "Fonts/Fredoka-SemiBold.ttf",
                     "keys/supporter-test-public.pem"] {
            check("Bundled \(name)", Resources.path(name) != nil)
        }
        check("Changelog has this version", Changelog.parse().first?.version == AppInfo.version, Changelog.parse().first?.heading ?? "none")
        let available = Cleaner.available()
        check("Cleaner catalog", !available.isEmpty, "\(available.count) of \(Cleaner.catalog().count) items on this Mac")
        var total: Int64 = 0
        for target in available {
            let finding = Cleaner.find(target, keepRecent: true, running: [])
            total += finding.size
            let state = finding.skipped.isEmpty ? (finding.unknownSize ? finding.note : "\(finding.count) files, \(finding.size) bytes") : finding.skipped
            print("      \(target.group) / \(target.name): \(state)")
        }
        check("Analyze every item", true, "\(total) bytes found")
        let triggers = Triggers.list(includeSystem: true, includeApple: true)
        let counts = Dictionary(grouping: triggers, by: \.kind).map { "\(Triggers.groupNames[$0.key]!): \($0.value.count)" }.sorted()
        check("Triggers list", triggers.contains { $0.kind == .appleDaemon }, counts.joined(separator: ", "))
        for item in triggers where item.needsLook {
            print("      Worth a look: \(item.name): \(item.notes.map(\.text).joined(separator: "; "))")
        }
        check("launchctl print-disabled", !Run.program(Triggers.launchctl, ["print-disabled", "gui/\(FS.userID)"]).output.isEmpty)
        check("Full Disk Access check runs", true, FS.isDenied(Paths.home + "/Library/Safari") ? "not allowed" : "allowed")
        check("Fredoka font", NSFont(name: "Fredoka-SemiBold", size: 12) != nil || registerAndCheckFont())
        return ok
    }

    static func registerAndCheckFont() -> Bool {
        Theme.registerFonts()
        return NSFont(name: "Fredoka-SemiBold", size: 12) != nil
    }
}
