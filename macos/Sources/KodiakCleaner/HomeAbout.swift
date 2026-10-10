import KodiakCore
import SwiftUI

/// Getting Started's feature list: kept A to Z and current.
let features: [(String, String)] = [
    ("Advanced Cleaning", "The whole cache folder, Xcode archives and device support files, each with a caution."),
    ("Browser Cleaning", "Cache, cookies, history and sessions for Arc, Brave, Chrome, Chromium, Edge, Firefox, Floorp, Helium, "
        + "LibreWolf, Opera, Safari, Vivaldi and Zen."),
    ("Developer Caches", "Homebrew, npm, pip, Yarn, Go, CocoaPods and Xcode Derived Data."),
    ("Private by Design", "No telemetry, no ads, no upsells. Nothing leaves this Mac."),
    ("System Junk", "Crash reports, logs, temporary files, Quick Look thumbnails, saved window state and the Trash."),
    ("Triggers", "Every launch agent, launch daemon, cron job and shell startup file in one list, with notes on anything "
        + "worth a look."),
    ("Triggers History", "Every change is backed up first and can be restored."),
]

struct HomeView: View {
    @EnvironmentObject var model: AppModel
    @Environment(\.theme) private var theme

    var body: some View {
        PageScroll {
            HStack(spacing: 18) {
                LogoView(size: 96)
                VStack(alignment: .leading, spacing: 6) {
                    Wordmark(size: 34, withEdition: true)
                    Text(AppInfo.slogan).font(theme.body(15)).foregroundStyle(theme.muted)
                }
            }
            if AppInfo.channel != "stable" {
                WarnBox(text: "\(AppInfo.name) is beta software: use it at your own risk. Analyze before you clean and keep your own backups of anything important.")
            }
            Card(title: "Start Here", subtitle: "Two pages do the work. Both show you everything before they change anything.") {
                HStack(spacing: 10) {
                    Button("Open the Cleaner") { model.page = .cleaner }.kcButton(.primary)
                    Button("Look at Triggers") { model.page = .triggers }.kcButton(.secondary)
                    Button("About") { model.page = .about }.kcButton(.secondary)
                }
            }
            if !model.hasFullDiskAccess {
                Card(title: "Full Disk Access", subtitle: "macOS keeps Safari, Mail and the Trash private until you allow Kodiak Cleaner in. Everything else works without it.") {
                    Button("Open Privacy Settings") { openLink(AppModel.fullDiskAccessURL) }.kcButton(.secondary)
                }
            }
            Card(title: "Features") {
                LazyVGrid(columns: [GridItem(.flexible(), spacing: 18, alignment: .topLeading),
                                    GridItem(.flexible(), spacing: 18, alignment: .topLeading)], alignment: .leading, spacing: 12) {
                    ForEach(features, id: \.0) { name, text in
                        VStack(alignment: .leading, spacing: 2) {
                            Text(name).font(theme.body(13, weight: .semibold)).foregroundStyle(theme.text)
                            Caption(text)
                        }
                    }
                }
            }
        }
    }
}

struct AboutView: View {
    @EnvironmentObject var model: AppModel
    @Environment(\.theme) private var theme

    static let credits: [(String, String, String, String)] = [
        // (name, what it does here, license, address)
        ("Fredoka", "Heading font in the Honey theme, built into Kodiak Cleaner", "SIL Open Font License 1.1",
         "https://github.com/hafontia/Fredoka-One"),
        ("SF Symbols", "Icons, from macOS itself (not bundled)", "Apple's license for macOS", "https://developer.apple.com/sf-symbols/"),
        ("Swift and SwiftUI", "The language and interface, from Apple (not bundled)", "Apache 2.0 (Swift) and Apple's license for macOS",
         "https://www.swift.org"),
    ]

    var body: some View {
        PageScroll {
            HStack(spacing: 18) {
                LogoView(size: 88)
                VStack(alignment: .leading, spacing: 6) {
                    Text(AppInfo.name).font(theme.display(30)).foregroundStyle(theme.text)
                    HStack(spacing: 8) {
                        Chip(text: AppInfo.versionName)
                        if model.plan.isSupporter && model.settings.showBadge { Chip(text: model.plan.badgeText, icon: "heart.fill") }
                    }
                    Text(AppInfo.slogan).font(theme.body(14)).foregroundStyle(theme.muted)
                    Caption("Made by \(AppInfo.developer), an independent developer.")
                }
            }
            Card(title: "Your Plan") {
                HStack(spacing: 10) {
                    Text(model.plan.name).font(theme.body(13, weight: .semibold)).foregroundStyle(theme.text)
                    if model.plan.howUnlocked != "None" { Caption("Unlocked by: \(model.plan.howUnlocked)") }
                    Spacer()
                    Button("Supporter") { model.showSettings(.supporter) }.kcButton(.secondary)
                }
            }
            Card(title: "What's New") {
                if let release = Changelog.latest() {
                    Text(release.heading).font(theme.body(13, weight: .semibold)).foregroundStyle(theme.text)
                    MarkdownView(text: release.text)
                }
                Button("See All Changes") { model.sheet = .changelog }.kcButton(.secondary)
            }
            Card(title: "Privacy") {
                Text("Kodiak Cleaner works only on this Mac. It has no telemetry, no ads and no accounts, and it never goes online by itself. Links you click open in your own browser. Settings and history stay in your Library folder.")
                    .font(theme.body(13)).foregroundStyle(theme.text).fixedSize(horizontal: false, vertical: true)
                Button("Privacy Policy") { model.showSettings(.privacy) }.kcButton(.secondary)
            }
            Card(title: "Credits and Licenses") {
                ForEach(AboutView.credits, id: \.0) { name, use, license, url in
                    VStack(alignment: .leading, spacing: 1) {
                        HStack(spacing: 6) {
                            Button(name) { openLink(URL(string: url)) }.kcButton(.link)
                            Text("·  \(license)").font(theme.body(13)).foregroundStyle(theme.text)
                        }
                        Caption(use)
                    }
                }
                Caption("\(AppInfo.name) is free and open source under the MIT License. Copyright © 2026 Ted Tabaka.")
            }
            Card(title: "Learn More") {
                HStack(spacing: 8) {
                    Button("Website") { openLink(AppInfo.homepageURL) }.kcButton(.secondary)
                    Button("Source Code") { openLink(AppInfo.projectURL) }.kcButton(.secondary)
                    Button("Downloads") { openLink(AppInfo.releasesURL) }.kcButton(.secondary)
                }
                HStack(spacing: 8) {
                    Button("Contact & Feedback") { model.showSettings(.feedback) }.kcButton(.secondary)
                    Button("Report a Bug") { openLink(Feedback.link(kind: "bug")) }.kcButton(.secondary)
                    Button("Request a Feature") { openLink(Feedback.link(kind: "idea")) }.kcButton(.secondary)
                    Button("Getting Started") { model.page = .home }.kcButton(.secondary)
                }
            }
        }
    }
}
