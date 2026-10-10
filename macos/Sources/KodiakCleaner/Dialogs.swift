import AppKit
import KodiakCore
import SwiftUI

/// Every pop-up goes through here so it wears the theme. Risky choices use the danger style; Cancel stays the default.
struct DialogView: View {
    var request: DialogRequest
    @EnvironmentObject var model: AppModel
    @Environment(\.theme) private var theme

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack(spacing: 12) {
                LogoView(size: 40)
                Text(request.title).font(theme.display(20)).foregroundStyle(theme.text)
            }
            Text(request.message).font(theme.body(13)).foregroundStyle(theme.text).fixedSize(horizontal: false, vertical: true)
                .textSelection(.enabled)
            HStack(spacing: 8) {
                Spacer()
                if let cancel = request.cancel {
                    Button(cancel) { model.dialog = nil }.kcButton(.secondary).keyboardShortcut(.cancelAction)
                    Button(request.confirm) { finish() }.kcButton(request.danger ? .danger : .primary)
                } else {
                    Button(request.confirm) { finish() }.kcButton(.primary).keyboardShortcut(.defaultAction)
                }
            }
        }
        .padding(24)
        .frame(width: 460)
        .background(theme.bg)
    }

    private func finish() {
        let action = request.action
        model.dialog = nil
        DispatchQueue.main.async { action?() }
    }
}

/// Dev and Beta builds show this at every start until Don't Show This Again at Startup is on.
struct BetaNoticeView: View {
    @EnvironmentObject var model: AppModel
    @Environment(\.theme) private var theme

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack(spacing: 14) {
                LogoView(size: 56)
                Text("Beta Software").font(theme.display(22)).foregroundStyle(theme.text)
            }
            Text("You're using \(AppInfo.name) \(AppInfo.versionName), a test build. Use it at your own risk: more testing is needed before every feature can be confirmed to work as intended, and as cleaning software it can remove things you didn't mean to.")
                .font(theme.body(13)).foregroundStyle(theme.text).fixedSize(horizontal: false, vertical: true)
            Text("Analyze before you clean, read each caution, and keep your own backups of anything important.")
                .font(theme.body(13)).foregroundStyle(theme.text).fixedSize(horizontal: false, vertical: true)
            SwitchRow(title: "Don't Show This Again at Startup",
                      isOn: Binding(get: { !model.settings.showBetaNotice }, set: { model.settings.showBetaNotice = !$0 }))
            HStack {
                Spacer()
                Button("I Understand") { model.betaNoticeClosed() }.kcButton(.primary).keyboardShortcut(.defaultAction)
            }
        }
        .padding(24)
        .frame(width: 520)
        .background(theme.bg)
    }
}

/// The donation reminder at startup (Ted, 2026-10-10). Not Now, the close button and Esc close it; Donate on Ko-fi
/// opens Ko-fi in the browser and closes it. Don't Show This Again at Startup works once Kodiak Cleaner has started
/// 10 times, and applies however the pop-up closes.
struct DonationView: View {
    @EnvironmentObject var model: AppModel
    @Environment(\.theme) private var theme
    @State private var dontShowAgain = false

    var body: some View {
        let canOptOut = model.canOptOutOfDonation
        VStack(alignment: .leading, spacing: 14) {
            HStack(spacing: 12) {
                Image(systemName: "heart.fill").font(.system(size: 24)).foregroundStyle(theme.accentText)
                    .accessibilityHidden(true)
                Text(Donation.title).font(theme.display(22)).foregroundStyle(theme.text)
                    .accessibilityAddTraits(.isHeader)
                Spacer()
                Button { model.sheet = nil } label: {
                    Image(systemName: "xmark").font(.system(size: 12, weight: .semibold)).frame(width: 14, height: 14)
                }
                .kcButton(.secondary)
                .accessibilityLabel("Close")
                .help("Close")
            }
            Card {
                Text(Donation.paragraphs[0]).font(theme.body(13)).foregroundStyle(theme.text)
                    .fixedSize(horizontal: false, vertical: true)
                Text(Donation.paragraphs[1]).font(theme.body(13, weight: .semibold)).foregroundStyle(theme.text)
                    .fixedSize(horizontal: false, vertical: true)
                Caption(Donation.paragraphs[2])
            }
            SwitchRow(title: "Don't Show This Again at Startup",
                      help: Donation.promptCaption(launchCount: model.settings.launchCount, isSupporter: model.plan.isSupporter),
                      isOn: Binding(get: { canOptOut && dontShowAgain }, set: { dontShowAgain = canOptOut && $0 }))
                .disabled(!canOptOut)
            HStack(spacing: 8) {
                Spacer()
                Button("Not Now") { model.sheet = nil }.kcButton(.secondary).keyboardShortcut(.cancelAction)
                Button("Donate on Ko-fi") {
                    openLink(AppInfo.kofiURL)
                    model.sheet = nil
                }
                .kcButton(.primary).keyboardShortcut(.defaultAction)
            }
        }
        .padding(24)
        .frame(width: 520)
        .background(theme.bg)
        .onDisappear { model.donationPromptClosed(dontShowAgain: canOptOut && dontShowAgain) }
    }
}

/// The slim bar at the top of the page on every 10th start (never for Supporters). Dismiss hides it until next time.
struct DonationBanner: View {
    @EnvironmentObject var model: AppModel
    @Environment(\.theme) private var theme

    var body: some View {
        HStack(spacing: 10) {
            Image(systemName: "heart.fill").foregroundStyle(theme.accentText).accessibilityHidden(true)
            Text(Donation.bannerText).font(theme.body(13)).foregroundStyle(theme.text)
                .fixedSize(horizontal: false, vertical: true)
            Spacer(minLength: 8)
            Button("Donate on Ko-fi") { openLink(AppInfo.kofiURL) }.kcButton(.link)
            Button("Dismiss") { model.showDonationBanner = false }.kcButton(.secondary)
        }
        .padding(.horizontal, 14).padding(.vertical, 8)
        .background(RoundedRectangle(cornerRadius: theme.radius).fill(theme.accentTint))
        .overlay(RoundedRectangle(cornerRadius: theme.radius).strokeBorder(theme.accent, lineWidth: 1))
        .accessibilityElement(children: .contain)
    }
}

/// What's New since the previous version (with View Full Changelog), or the whole changelog.
struct ChangesView: View {
    var previous: String?
    @EnvironmentObject var model: AppModel
    @Environment(\.theme) private var theme
    @State private var full = false

    var body: some View {
        let releases = previous == nil || full ? Changelog.parse() : Changelog.newer(than: previous!)
        VStack(alignment: .leading, spacing: 12) {
            Text(previous != nil && !full ? "What's New in \(AppInfo.versionName)" : "Full Changelog")
                .font(theme.display(22)).foregroundStyle(theme.text)
            if let previous, !full { Caption("Everything that changed since \(previous).") }
            ScrollView {
                VStack(alignment: .leading, spacing: 14) {
                    ForEach(releases) { release in
                        VStack(alignment: .leading, spacing: 6) {
                            Text(release.heading).font(theme.display(18)).foregroundStyle(theme.text)
                            if !release.date.isEmpty { Caption(release.date) }
                            MarkdownView(text: release.text)
                        }
                    }
                }
                .frame(maxWidth: .infinity, alignment: .leading)
            }
            HStack(spacing: 8) {
                Spacer()
                if previous != nil && !full { Button("View Full Changelog") { full = true }.kcButton(.secondary) }
                Button("Close") { model.sheet = nil }.kcButton(.primary).keyboardShortcut(.defaultAction)
            }
        }
        .padding(24)
        .frame(width: 680, height: 600)
        .background(theme.bg)
    }
}

/// Text boxes: the theme's surface with the control border, so their edges show on any card.
struct KCField: ViewModifier {
    @Environment(\.theme) private var theme

    func body(content: Content) -> some View {
        content
            .textFieldStyle(.plain)
            .font(theme.body(13))
            .foregroundStyle(theme.text)
            .padding(.horizontal, 10).padding(.vertical, 7)
            .background(RoundedRectangle(cornerRadius: theme.radius).fill(theme.surface))
            .overlay(RoundedRectangle(cornerRadius: theme.radius).strokeBorder(theme.controlBorder, lineWidth: 1))
    }
}

extension View {
    func kcField() -> some View { modifier(KCField()) }
}
