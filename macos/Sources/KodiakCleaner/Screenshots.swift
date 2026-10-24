import AppKit
import KodiakCore
import SwiftUI

/// `KodiakCleaner --screenshots <folder>` draws every page in every color scheme, light and dark, to PNG files, so the
/// look can be checked on a Mac without anyone clicking through it. Development builds only use it for testing.
@MainActor
enum Screenshots {
    static let size = NSSize(width: 1180, height: 760)

    static func run(to folder: String) async {
        try? FileManager.default.createDirectory(atPath: folder, withIntermediateDirectories: true)
        let model = AppModel.shared
        let saved = model.settings
        model.sheet = nil
        model.dialog = nil
        model.settings.preview = "supporter"   // Classic, Glacier and Lagoon, through the Developer page's plan preview
        model.settings.previewName = "Jane Doe"
        model.settings.selected = nil
        model.resortTicked()
        // Real findings for the Cleaner screenshots, and the Triggers list.
        let running = AppModel.runningPrograms()
        for target in model.targets where model.selected.contains(target.id) {
            model.findings[target.id] = Cleaner.find(target, keepRecent: true, running: running)
        }
        let found = model.findings.values.reduce(Int64(0)) { $0 + $1.size }
        model.summary = "Found \(formatBytes(found)) in \(model.findings.values.reduce(0) { $0 + $1.count }) files."
        model.triggers = Triggers.list(includeSystem: true, includeApple: false)
        model.triggersLoaded = true
        // Drive Wiper: this Mac's own volumes for Free Space Only, and a sample external drive list for Entire Drive
        // (nothing is erased; the sample drives aren't real).
        let wiper = WiperModel.shared
        wiper.show(volumes: WipeDisks.volumes(), disks: Screenshots.sampleDisks, problem: nil)
        wiper.loaded = true

        let pages: [(String, Page, SettingsPage?)] = [
            ("home", .home, nil), ("cleaner", .cleaner, nil), ("triggers", .triggers, nil), ("wiper", .wiper, nil), ("about", .about, nil),
            ("settings-general", .settings, .general), ("settings-appearance", .settings, .appearance),
            ("settings-supporter", .settings, .supporter), ("settings-feedback", .settings, .feedback),
            ("settings-changelog", .settings, .changelog), ("settings-developer", .settings, .developer),
        ]
        for scheme in Themes.schemeOrder {
            model.settings.scheme = scheme
            for dark in [false, true] {
                let suffix = "\(scheme)-\(dark ? "dark" : "light")"
                for (name, page, settingsPage) in pages {
                    if scheme != "honey" && !["cleaner", "settings-appearance", "triggers"].contains(name) { continue }
                    model.page = page
                    if let settingsPage { model.settingsPage = settingsPage }
                    await capture(MainLayout(), dark: dark, to: "\(folder)/\(name)-\(suffix).png", model: model)
                }
                await capture(BetaNoticeView(), dark: dark, to: "\(folder)/beta-notice-\(suffix).png", model: model, fit: true)
                await capture(DonationView(), dark: dark, to: "\(folder)/donation-\(suffix).png", model: model, fit: true)
                await capture(DialogView(request: DialogRequest(title: "Clean Now?", message: "Kodiak Cleaner will remove 1,204 files (812 MB) from 9 items. Files it removes can't be brought back.", confirm: "Clean", danger: true)),
                              dark: dark, to: "\(folder)/dialog-\(suffix).png", model: model, fit: true)
            }
        }
        // Drive Wiper's Entire Drive mode, with a flash drive ticked and its recommended format, then the format changed.
        model.settings.scheme = "honey"
        model.page = .wiper
        wiper.mode = "entire"
        wiper.paint(["disk4"], true)
        for dark in [false, true] {
            await capture(MainLayout(), dark: dark, to: "\(folder)/wiper-entire-honey-\(dark ? "dark" : "light").png", model: model, tall: true)
        }
        wiper.fileSystem = .apfs
        wiper.gpt = true
        await capture(MainLayout(), dark: false, to: "\(folder)/wiper-entire-changed.png", model: model, tall: true)
        wiper.leaveUninitialized = true
        await capture(MainLayout(), dark: false, to: "\(folder)/wiper-entire-uninitialized.png", model: model, tall: true)
        await capture(TypedCheckView(check: TypedCheck(title: "Check 2 of 3: Confirm the Drive", message: "1 of 1 drives\n\nSanDisk Ultra (disk4)\n30.8 GB, Flash Drive, STICK\nSecure wipe: DoD 3 Passes\n\nAll data on this drive will be deleted and can't be recovered. Type its disk identifier (disk4) to confirm this is the right drive:", expected: "disk4", confirm: "Confirm") {}),
                      dark: false, to: "\(folder)/wiper-check.png", model: model, fit: true)
        wiper.leaveUninitialized = false
        wiper.selectAll(false)
        wiper.mode = "free"
        // Accent colors on Honey light.
        model.settings.scheme = "honey"
        model.page = .settings
        model.settingsPage = .appearance
        for accent in ["Teal", "Purple", Themes.systemAccent] {
            model.settings.accent = accent
            await capture(MainLayout(), dark: false, to: "\(folder)/accent-\(accent.lowercased()).png", model: model)
        }
        // A Free copy: Classic, Glacier and Lagoon locked.
        model.settings.accent = ""
        model.settings.preview = "free"
        await capture(MainLayout(), dark: false, to: "\(folder)/settings-appearance-free.png", model: model)
        // The donation banner (every 10th start) and the pop-up as a Free copy sees it before 10 starts.
        model.page = .home
        model.showDonationBanner = true
        await capture(MainLayout(), dark: false, to: "\(folder)/donation-banner.png", model: model)
        model.showDonationBanner = false
        model.settings.launchCount = 3
        await capture(DonationView(), dark: false, to: "\(folder)/donation-free.png", model: model, fit: true)
        model.settings = saved
    }

    /// Sample external drives for the Entire Drive screenshots, beside a refused internal disk.
    static let sampleDisks: [WipeDisk] = [
        WipeDisk(id: "disk0", mediaName: "APPLE SSD AP0512Z", size: 500_277_790_720, kind: .ssd, isInternal: true, isRemovable: false,
                 bus: "Apple Fabric", reason: "macOS starts from this disk."),
        WipeDisk(id: "disk4", mediaName: "SanDisk Ultra", size: 30_765_219_840, kind: .flashDrive, isInternal: false, isRemovable: true,
                 bus: "USB", volumes: ["STICK"]),
        WipeDisk(id: "disk6", mediaName: "Samsung T7", size: 1_000_204_886_016, kind: .ssd, isInternal: false, isRemovable: true,
                 bus: "USB", volumes: ["Backup"]),
        WipeDisk(id: "disk7", mediaName: "WD Elements", size: 4_000_787_030_016, kind: .hardDrive, isInternal: false, isRemovable: true,
                 bus: "USB", volumes: ["Archive"]),
    ]

    static func capture<V: View>(_ view: V, dark: Bool, to path: String, model: AppModel, fit: Bool = false, tall: Bool = false) async {
        model.forcedDark = dark
        let root = Themed { view }
            .environmentObject(model)
            .environment(\.colorScheme, dark ? .dark : .light)
        let height = tall ? 1500 : size.height
        let hosting = NSHostingView(rootView: AnyView(fit ? AnyView(root.fixedSize()) : AnyView(root.frame(width: size.width, height: height))))
        hosting.appearance = NSAppearance(named: dark ? .darkAqua : .aqua)
        let frame = NSRect(origin: .zero, size: fit ? hosting.fittingSize : NSSize(width: size.width, height: height))
        hosting.frame = frame
        let window = NSWindow(contentRect: frame, styleMask: [.borderless], backing: .buffered, defer: false)
        window.appearance = hosting.appearance
        window.contentView = hosting
        window.setFrameOrigin(NSPoint(x: 40, y: 40))
        window.orderFrontRegardless()
        hosting.layoutSubtreeIfNeeded()
        try? await Task.sleep(nanoseconds: 600_000_000)
        hosting.layoutSubtreeIfNeeded()
        hosting.display()
        if let rep = hosting.bitmapImageRepForCachingDisplay(in: hosting.bounds) {
            hosting.cacheDisplay(in: hosting.bounds, to: rep)
            if let data = rep.representation(using: .png, properties: [:]) {
                try? data.write(to: URL(fileURLWithPath: path))
                print("Saved \(path)")
            }
        }
        window.orderOut(nil)
        model.forcedDark = nil
    }
}
