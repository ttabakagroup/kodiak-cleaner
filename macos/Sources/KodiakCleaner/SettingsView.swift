import AppKit
import KodiakCore
import SwiftUI

/// Settings, with its own sidebar: General, Cleaning, Triggers, Appearance, Supporter, Contact & Feedback, then Help
/// (Changelog, Privacy Policy, Terms of Use), and Developer last in development builds.
struct SettingsView: View {
    @EnvironmentObject var model: AppModel
    @Environment(\.theme) private var theme

    private var entries: [(SettingsPage?, String, String)] {
        var list: [(SettingsPage?, String, String)] = [
            (nil, "Settings", ""),
            (.general, "General", "gearshape"),
            (.cleaning, "Cleaning", "sparkles"),
            (.triggers, "Triggers", "bolt.horizontal.circle"),
            (.appearance, "Appearance", "paintpalette"),
            (.supporter, "Supporter", "heart"),
            (.feedback, "Contact & Feedback", "envelope"),
            (nil, "Help", ""),
            (.changelog, "Changelog", "clock.arrow.circlepath"),
            (.privacy, "Privacy Policy", "hand.raised"),
            (.terms, "Terms of Use", "doc.text"),
        ]
        if AppInfo.hasDeveloperTools { list.append((.developer, "Developer", "hammer")) }
        return list
    }

    var body: some View {
        HStack(spacing: 0) {
            VStack(alignment: .leading, spacing: 3) {
                ForEach(Array(entries.enumerated()), id: \.offset) { _, entry in
                    if let page = entry.0 {
                        Button { model.settingsPage = page } label: { Label(entry.1, systemImage: entry.2).labelStyle(SidebarLabelStyle()) }
                            .kcButton(.nav(selected: model.settingsPage == page))
                            .accessibilityAddTraits(model.settingsPage == page ? .isSelected : [])
                    } else {
                        Text(entry.1).font(theme.body(11, weight: .semibold)).foregroundStyle(theme.muted)
                            .padding(.leading, 10).padding(.top, 10)
                    }
                }
                Spacer()
            }
            .padding(.horizontal, 12).padding(.vertical, 18)
            .frame(width: 210)
            .background(theme.surfaceAlt)
            Rectangle().fill(theme.line).frame(width: 1)
            PageScroll {
                page
                VStack(alignment: .leading, spacing: 2) {
                    Text("Copyright © \(String(Calendar.current.component(.year, from: Date()))) TED TABAKA.")
                    Text("Free and open source under the MIT License.")
                }
                .font(theme.body(11)).foregroundStyle(theme.muted).padding(.top, 10)
            }
        }
    }

    @ViewBuilder private var page: some View {
        switch model.settingsPage {
        case .general: GeneralSettings()
        case .cleaning: CleaningSettings()
        case .triggers: TriggersSettings()
        case .appearance: AppearanceSettings()
        case .supporter: SupporterSettings()
        case .feedback: FeedbackSettings()
        case .changelog:
            PageHeader(title: "Changelog", subtitle: "Every change, newest first.")
            MarkdownView(text: Changelog.text())
        case .privacy:
            PageHeader(title: "Privacy Policy", subtitle: "What Kodiak Cleaner does with your data: nothing leaves this Mac.")
            MarkdownView(text: Resources.text("PRIVACY.md"))
        case .terms:
            PageHeader(title: "Terms of Use", subtitle: "The rules for using Kodiak Cleaner.")
            MarkdownView(text: Resources.text("terms-of-use.md"))
        case .developer: DeveloperSettings()
        }
    }
}

private struct GeneralSettings: View {
    @EnvironmentObject var model: AppModel
    @Environment(\.theme) private var theme

    var body: some View {
        PageHeader(title: "General", subtitle: "How Kodiak Cleaner starts and where it keeps its files.")
        // Cards A to Z with Reset last, and rows A to Z inside each card (Ted, 0.8.3).
        Card(title: "Full Disk Access", subtitle: "macOS keeps Safari, Mail and the Trash private. To clean them, allow Kodiak Cleaner under Full Disk Access in Privacy & Security, then open Kodiak Cleaner again. Everything else works without it.") {
            HStack(spacing: 10) {
                Label(model.hasFullDiskAccess ? "Allowed" : "Not Allowed",
                      systemImage: model.hasFullDiskAccess ? "checkmark.circle.fill" : "lock.fill")
                    .font(theme.body(13, weight: .semibold)).foregroundStyle(theme.text)
                Spacer()
                Button("Open Privacy Settings") { openLink(AppModel.fullDiskAccessURL) }.kcButton(.secondary)
            }
        }
        Card(title: "Startup") {
            // Greyed out until Kodiak Cleaner has started 10 times; Supporters have it off for good.
            SwitchRow(title: "Ask Me to Donate at Startup",
                      help: Donation.settingsCaption(launchCount: model.settings.launchCount, isSupporter: model.plan.isSupporter),
                      isOn: Binding(get: { model.settings.showDonationPrompt },
                                    set: { on in
                                        let isSupporter = model.plan.isSupporter
                                        model.settings.setDonationPrompt(on, isSupporter: isSupporter)
                                    }))
                .disabled(!model.canOptOutOfDonation || model.plan.isSupporter)
            if AppInfo.channel != "stable" {
                SwitchRow(title: "Show the Beta Notice at Startup", help: "Reminds you this is test software each time it opens.",
                          isOn: $model.settings.showBetaNotice)
            }
            SwitchRow(title: "Show What's New After Updates", help: "Opens the list of changes the first time a new version starts.",
                      isOn: $model.settings.showWhatsNew)
        }
        Card(title: "Your Files", subtitle: "Settings, backups and history stay in your own Library folder.") {
            fileRow("Backups and History", Paths.backups)
            fileRow("Settings", Paths.appSupport)
        }
        Card(title: "Reset", subtitle: "Puts every setting back to how it was at first. Your supporter code and history stay.") {
            Button("Reset All Settings") {
                model.ask(DialogRequest(title: "Reset All Settings?",
                                        message: "Every setting goes back to its default, and the start count for the donation reminder starts again. Your supporter code, cleaning history and Triggers History stay.",
                                        confirm: "Reset", danger: true) {
                    model.settings = model.settings.reset()
                    model.applyAppearance()
                })
            }
            .kcButton(.danger)
        }
    }

    private func fileRow(_ title: String, _ path: String) -> some View {
        HStack(spacing: 10) {
            VStack(alignment: .leading, spacing: 2) {
                Text(title).font(theme.body(13, weight: .semibold)).foregroundStyle(theme.text)
                Caption(Paths.display(path))
            }
            Spacer()
            Button("Show in Finder") { model.openFolder(path) }.kcButton(.secondary)
        }
    }
}

private struct CleaningSettings: View {
    @EnvironmentObject var model: AppModel

    var body: some View {
        PageHeader(title: "Cleaning", subtitle: "How the Cleaner works.")
        Card(title: "Before Cleaning") {
            SwitchRow(title: "Ask Before Cleaning", help: "Shows what will be removed and waits for you to agree.",
                      isOn: $model.settings.confirmBeforeCleaning)
            SwitchRow(title: "Keep Temporary Files from the Last Day", help: "Apps that are running may still need them.",
                      isOn: $model.settings.keepRecentTempFiles)
            SwitchRow(title: "Skip Programs That Are Open", help: "Leaves a browser or app alone while it's running, so nothing breaks.",
                      isOn: $model.settings.skipOpenPrograms)
        }
        Card(title: "What Kodiak Cleaner Touches", subtitle: "Only files in your own home folder and your own temporary folder. Nothing on the rest of the Mac, and never anything that needs your administrator password. Links are removed, never followed.") {
            EmptyView()
        }
    }
}

private struct TriggersSettings: View {
    @EnvironmentObject var model: AppModel

    var body: some View {
        PageHeader(title: "Triggers", subtitle: "How the Triggers page lists and changes entries.")
        Card(title: "Changes and Backups", subtitle: "Every change is recorded in Triggers History with a copy of the file first, and History can put it back. Launch agents for all users are turned off just for you. Launch daemons ask for your administrator password through macOS's own prompt, and Kodiak Cleaner never sees it.") {
            Caption("History is kept in \(Paths.display(Paths.triggersHistory))")
        }
        Card(title: "What to Show") {
            SwitchRow(title: "Hide Protected Items", help: "Hides Apple's own launch agents and daemons, which are part of macOS.",
                      isOn: $model.settings.hideProtected)
            SwitchRow(title: "Show System Triggers", help: "Includes launch agents and daemons that run for all users, and the system's shell files.",
                      isOn: $model.settings.showSystemTriggers)
        }
    }
}

private struct AppearanceSettings: View {
    @EnvironmentObject var model: AppModel
    @Environment(\.theme) private var theme

    var body: some View {
        PageHeader(title: "Appearance", subtitle: "Colors, light or dark, and motion.")
        Card(title: "Accent Color", subtitle: "Recolors buttons, switches, links and highlights. The first swatch keeps the color scheme's own accent; macOS follows the accent color in System Settings.") {
            HStack(spacing: 12) {
                let scheme = model.scheme
                let current = Themes.effectiveAccent(scheme, model.settings.accent)
                ForEach(Themes.accentNames(scheme), id: \.self) { name in
                    swatch(name, scheme: scheme, chosen: current == name)
                }
            }
        }
        Card(title: "Color Scheme") {
            HStack(spacing: 10) {
                ForEach(Themes.schemeOrder, id: \.self) { key in schemeTile(key) }
            }
        }
        Card(title: "Light or Dark") {
            Segmented(options: [("system", "Match Device"), ("light", "Light"), ("dark", "Dark")], selection: $model.settings.mode)
        }
        Card(title: "Motion") {
            SwitchRow(title: "Animations", help: "Fades and slides between pages. Turn off for a calmer, faster feel.",
                      isOn: $model.settings.animations)
        }
    }

    private func schemeTile(_ key: String) -> some View {
        let chosen = model.scheme == key
        let locked = Themes.supporterSchemes.contains(key) && !model.plan.isSupporter
        return Button {
            if locked {
                let name = Themes.schemeNames[key]!
                model.tell("\(name) Is a Supporter Theme", "\(name) is a thank-you for supporters. Enter a supporter code under Supporter to unlock it.")
            } else {
                model.settings.scheme = key
            }
        } label: {
            VStack(alignment: .leading, spacing: 6) {
                HStack(spacing: 4) {
                    ForEach(Themes.schemePreview(key, dark: theme.dark), id: \.self) { color in
                        Circle().fill(Theme.color(color)).frame(width: 16, height: 16)
                            .overlay(Circle().strokeBorder(theme.line, lineWidth: 1))
                    }
                }
                Text(Themes.schemeNames[key]!).font(theme.body(13, weight: .semibold)).foregroundStyle(theme.text)
                Caption(Themes.schemeBlurbs[key]!)
                if locked { Chip(text: "Supporter Theme", icon: "lock.fill") }
            }
            .padding(12)
            .frame(width: 190, alignment: .leading)
            .frame(minHeight: 96, alignment: .topLeading)
            .background(RoundedRectangle(cornerRadius: theme.radius).fill(chosen ? theme.accentTint : theme.surface))
            .overlay(RoundedRectangle(cornerRadius: theme.radius).strokeBorder(chosen ? theme.accent : theme.controlBorder, lineWidth: chosen ? 2 : 1))
        }
        .buttonStyle(TileStyle(radius: theme.radius))
        .accessibilityLabel("\(Themes.schemeNames[key]!) color scheme\(locked ? ", Supporter theme" : "")")
        .accessibilityAddTraits(chosen ? .isSelected : [])
    }

    private func swatch(_ name: String, scheme: String, chosen: Bool) -> some View {
        let own = Themes.schemeSwatch[scheme]
        let title = name.isEmpty ? (own != nil ? "Theme Accent (\(own!))" : "Theme Accent") : name
        let hex = Themes.swatchColor(name, scheme: scheme, dark: theme.dark, systemAccentColor: model.systemAccent)
        let color = Theme.color(hex)
        // The mark on a swatch uses whichever of the surface or text color stands out more on it.
        let mark = Themes.contrast(theme.tokens["surface"]!, hex) >= Themes.contrast(theme.tokens["text"]!, hex) ? theme.surface : theme.text
        return Button { model.settings.accent = name } label: {
            ZStack {
                Circle().fill(color).frame(width: 30, height: 30)
                Circle().strokeBorder(theme.controlBorder, lineWidth: 1).frame(width: 30, height: 30)
                if chosen {
                    Circle().strokeBorder(theme.text, lineWidth: 2).frame(width: 38, height: 38)
                    Image(systemName: "checkmark").font(.system(size: 12, weight: .bold)).foregroundStyle(mark)
                }
                if name == Themes.systemAccent && !chosen {
                    Image(systemName: "apple.logo").font(.system(size: 11)).foregroundStyle(mark)
                }
            }
            .frame(width: 40, height: 40)
        }
        .buttonStyle(TileStyle(radius: 20))
        .help(title)
        .accessibilityLabel(title)
        .accessibilityAddTraits(chosen ? .isSelected : [])
    }
}

private struct TileStyle: ButtonStyle {
    var radius: CGFloat
    func makeBody(configuration: Configuration) -> some View { TileBody(configuration: configuration, radius: radius) }
}

private struct TileBody: View {
    let configuration: ButtonStyleConfiguration
    let radius: CGFloat
    @Environment(\.isFocused) private var focused

    var body: some View {
        configuration.label
            .overlay(FocusRing(radius: radius, shown: focused))
            .opacity(configuration.isPressed ? 0.85 : 1)
            .contentShape(Rectangle())
            .focusEffectDisabled()
    }
}

private struct SupporterSettings: View {
    @EnvironmentObject var model: AppModel
    @Environment(\.theme) private var theme
    @State private var code = ""
    @State private var status = ""

    var body: some View {
        PageHeader(title: "Supporter", subtitle: "Free has everything that matters. Supporter is a thank-you.")
        if Supporter.publicKey.isTest { Chip(text: "Test Signing Key in Use", icon: "hammer") }
        HStack(alignment: .top, spacing: 14) {
            Card(title: "Free") {
                Text("Free forever").font(theme.body(13, weight: .medium)).foregroundStyle(theme.accentText)
                ForEach(["Every cleaning feature", "Every Triggers feature", "The Honey color scheme", "All accent colors"], id: \.self) {
                    Label($0, systemImage: "checkmark").foregroundStyle(theme.text)
                }
            }
            Card(title: "Supporter") {
                Text("One-time thank-you. Yours to keep.").font(theme.body(13, weight: .medium)).foregroundStyle(theme.accentText)
                ForEach(["Everything in Free", "The Classic and Glacier color schemes", "A Supporter Badge (you can hide it)",
                         "The support link and donation reminder hidden"], id: \.self) {
                    Label($0, systemImage: "checkmark").foregroundStyle(theme.text)
                }
            }
        }
        if !model.plan.isSupporter {
            Card(title: "Support Kodiak Cleaner", subtitle: "Supporting is optional. Every cleaning feature works without it.") {
                Button("Support on Ko-fi ♥") { openLink(AppInfo.kofiURL) }.kcButton(.secondary)
            }
        }
        Card(title: "Supporter Code", subtitle: "Codes are checked on this Mac; nothing is sent anywhere. The same code works in the Windows and Linux editions.") {
            if model.plan.isSupporter {
                Label("Thank you\(model.plan.supporterName.map { ", \($0)" } ?? "")! Supporter perks are on.", systemImage: "heart.fill")
                    .foregroundStyle(theme.text)
            }
            TextField("Supporter Code", text: $code, prompt: Text("e.g. eyJpZCI6Ij…")).kcField()
            HStack(spacing: 8) {
                Button("Apply Code") {
                    let (result, _) = Supporter.verify(code)
                    if result == .valid {
                        model.settings.supporterCode = code.filter { !$0.isWhitespace }
                        code = ""
                        status = Supporter.messages[.valid]!
                    } else {
                        status = Supporter.messages[result]!
                    }
                }
                .kcButton(.primary)
                if model.plan.license != nil {
                    Button("Remove Code") { model.settings.supporterCode = ""; status = "" }.kcButton(.secondary)
                }
            }
            if !status.isEmpty { Text(status).font(theme.body(13)).foregroundStyle(theme.text) }
            SwitchRow(title: "Show Supporter Badge", help: "Shows the badge in the sidebar.", isOn: $model.settings.showBadge)
        }
    }
}

private struct FeedbackSettings: View {
    @Environment(\.theme) private var theme

    var body: some View {
        PageHeader(title: "Contact & Feedback", subtitle: "Questions, bugs and ideas go straight to Ted.")
        VStack(alignment: .leading, spacing: 12) {
            if AppInfo.channel == "beta" {
                Text("You're using a beta. Your reports shape the next release.").foregroundStyle(theme.accentText)
            } else if AppInfo.channel == "dev" {
                Chip(text: "Internal Test Build")
            }
            Text("Tell us what's working, what isn't, and what you'd like next. Missing a feature you'd like? Let us know and we'll look into it.")
                .font(theme.body(13)).foregroundStyle(theme.text)
            HStack(spacing: 8) {
                ForEach([("feedback", "Send Feedback"), ("bug", "Report a Bug"), ("idea", "Request a Feature"), ("security", "Security")],
                        id: \.0) { kind, title in
                    Button(title) { openLink(Feedback.link(kind: kind)) }.kcButton(.accent)
                }
            }
            Caption("Each button opens the contact form on the Kodiak Cleaner website in your browser, with the topic and version already filled in. Nothing is sent until you send it there.")
        }
        Card(title: "Details for Your Report", subtitle: "Copy these into a bug report if they help. They never include files, folders, user names or what was found.") {
            Text(Feedback.details()).font(.system(size: 12, design: .monospaced)).foregroundStyle(theme.text).textSelection(.enabled)
                .padding(10).frame(maxWidth: .infinity, alignment: .leading)
                .background(RoundedRectangle(cornerRadius: theme.radius).fill(theme.surfaceAlt))
            Button("Copy Details") {
                NSPasteboard.general.clearContents()
                NSPasteboard.general.setString(Feedback.details(), forType: .string)
            }
            .kcButton(.secondary)
        }
        Card(title: "Rate Kodiak Cleaner") {
            Text("★★★★★").foregroundStyle(theme.accentText).accessibilityLabel("Five stars")
            Caption("If Kodiak Cleaner has earned it, please star it on GitHub. There's no store listing yet.")
            HStack(spacing: 12) {
                Button("Star on GitHub") { openLink(AppInfo.projectURL) }.kcButton(.primary)
                Caption("Before leaving a negative review, please contact us first so we can address any issues and meet your needs.")
            }
        }
    }
}

private struct DeveloperSettings: View {
    @EnvironmentObject var model: AppModel
    @Environment(\.theme) private var theme

    var body: some View {
        PageHeader(title: "Developer", subtitle: "Test tools for development builds.")
        Card(title: "Developer Mode", subtitle: "Preview Kodiak Cleaner as each plan. Only development builds have this page.") {
            Segmented(options: [("free", "Free"), ("supporter", "Supporter")], selection: $model.settings.preview)
            Text("Preview Name").font(theme.body(13, weight: .semibold)).foregroundStyle(theme.text)
            TextField("Preview Name", text: $model.settings.previewName, prompt: Text("e.g. Jane Doe")).kcField()
        }
        Card(title: "Status") {
            row("Full Disk Access", model.hasFullDiskAccess ? "Allowed" : "Not allowed")
            row("Installed As", AppInfo.build.packaged ? "App" : "Source folder")
            row("Perks Active", model.plan.isSupporter ? "Classic, Glacier, badge, support link and donation reminder hidden" : "None")
            row("Preview", model.settings.preview.capitalized)
            row("Signing Key", Supporter.publicKey.isTest ? "Test key" : "Ted's key")
            row("Starts Counted", "\(model.settings.launchCount)")
            row("Supporter Code", model.plan.license.map { "Valid (\($0.id))" } ?? "None")
            Button("Reset Supporter Testing") {
                model.settings.preview = "free"
                model.settings.previewName = ""
                model.settings.supporterCode = ""
            }
            .kcButton(.secondary)
        }
    }

    private func row(_ title: String, _ value: String) -> some View {
        HStack {
            Text(title).font(theme.body(13, weight: .semibold)).foregroundStyle(theme.text)
            Spacer()
            Text(value).font(theme.body(13)).foregroundStyle(theme.muted)
        }
    }
}
