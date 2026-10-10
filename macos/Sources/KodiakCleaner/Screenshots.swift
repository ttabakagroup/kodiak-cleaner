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
        model.settings.preview = "supporter"   // Classic and Glacier, through the Developer page's plan preview
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

        let pages: [(String, Page, SettingsPage?)] = [
            ("home", .home, nil), ("cleaner", .cleaner, nil), ("triggers", .triggers, nil), ("about", .about, nil),
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
        // Accent colors on Honey light.
        model.settings.scheme = "honey"
        model.page = .settings
        model.settingsPage = .appearance
        for accent in ["Teal", "Purple", Themes.systemAccent] {
            model.settings.accent = accent
            await capture(MainLayout(), dark: false, to: "\(folder)/accent-\(accent.lowercased()).png", model: model)
        }
        // A Free copy: Classic and Glacier locked.
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

    static func capture<V: View>(_ view: V, dark: Bool, to path: String, model: AppModel, fit: Bool = false) async {
        model.forcedDark = dark
        let root = Themed { view }
            .environmentObject(model)
            .environment(\.colorScheme, dark ? .dark : .light)
        let hosting = NSHostingView(rootView: AnyView(fit ? AnyView(root.fixedSize()) : AnyView(root.frame(width: size.width, height: size.height))))
        hosting.appearance = NSAppearance(named: dark ? .darkAqua : .aqua)
        let frame = NSRect(origin: .zero, size: fit ? hosting.fittingSize : size)
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
