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
            // Cmd+F jumps to the page's search box (Ted, 2026-10-23, as Ctrl+F in the Windows edition).
            CommandGroup(after: .pasteboard) {
                Button("Find") { model.findRequest += 1 }.keyboardShortcut("f")
            }
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

    /// A free space wipe stops and removes its filler files before Kodiak Cleaner quits; a whole drive must be stopped first.
    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        let quit = MainActor.assumeIsolated { WiperModel.shared.shouldQuit() }
        return quit ? .terminateNow : .terminateCancel
    }
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
        for name in ["logo.png", "CHANGELOG.md", "PRIVACY.md", "terms-of-use.md", "Fonts/Fredoka-SemiBold.ttf"] {
            check("Bundled \(name)", Resources.path(name) != nil)
        }
        // build.sh ships Ted's key in place of the test key once it exists, so either one counts.
        let key = Resources.path("keys/supporter-public.pem") != nil ? "keys/supporter-public.pem" : "keys/supporter-test-public.pem"
        check("Bundled \(key)", Resources.path(key) != nil)
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
        // Drive Wiper: reads the disks and prints diskutil's own usage lines for the commands it uses (no disk is named,
        // so nothing is erased).
        let (disks, problem) = WipeDisks.list(protectedPaths: WipeDisks.defaultProtectedPaths(appPath: Bundle.main.bundlePath))
        check("Drive Wiper lists the disks", problem == nil && !disks.isEmpty, problem ?? "\(disks.count) disks")
        for disk in disks {
            print("      \(disk.name): \(disk.kindName), \(disk.size) bytes, \(disk.isInternal ? "internal" : "external"), bus \(disk.bus), volumes \(disk.volumesText): \(disk.reason ?? "can be erased")")
        }
        check("Drive Wiper refuses the startup disk", !disks.isEmpty && disks.contains { $0.reason == "macOS starts from this disk." })
        let volumes = WipeDisks.volumes()
        check("Drive Wiper lists volumes for free space", volumes.contains { $0.isStartup },
              volumes.map { volume in "\(volume.name) (\(volume.typeName), SSD \(volume.solidState.map { $0 ? "yes" : "no" } ?? "unknown"), \(volume.wipeable) bytes)" }.joined(separator: ", "))
        for verb in ["zeroDisk", "randomDisk", "eraseDisk"] {
            let usage = Run.program(WipeDisks.diskutilPath, [verb])
            print("      diskutil \(verb): " + (usage.output + usage.error).split(separator: "\n").prefix(6).joined(separator: " | "))
        }
        let filesystems = Run.program(WipeDisks.diskutilPath, ["listFilesystems"]).output
        print("      diskutil listFilesystems: " + filesystems.split(separator: "\n").joined(separator: " | "))
        check("diskutil knows the file systems", ["APFS", "HFS+", "ExFAT", "FAT32", "Free Space"].allSatisfy { filesystems.contains($0) })
        check("Fredoka font", NSFont(name: "Fredoka-SemiBold", size: 12) != nil || registerAndCheckFont())
        return ok
    }

    static func registerAndCheckFont() -> Bool {
        Theme.registerFonts()
        return NSFont(name: "Fredoka-SemiBold", size: 12) != nil
    }
}
