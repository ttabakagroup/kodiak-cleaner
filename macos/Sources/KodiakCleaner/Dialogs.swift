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

/// The form the Contact & Feedback buttons open. Send Email opens the person's own email app; nothing is sent until
/// they send it there.
struct FeedbackForm: View {
    var kind: String
    @EnvironmentObject var model: AppModel
    @Environment(\.theme) private var theme
    @State private var subject = ""
    @State private var message = ""

    static let titles = ["feedback": "Send Feedback", "bug": "Report a Bug", "idea": "Request a Feature",
                         "security": "Report a Security Problem"]
    static let intros = [
        "feedback": "Tell us what's working, what isn't, and what you'd like next.",
        "bug": "Describe what went wrong. The details below help track it down; they never include files, folders or user names.",
        "idea": "Missing a feature you'd like? Let us know and we'll look into it.",
        "security": "Security problems are reported privately, never in public issues or reviews.",
    ]

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text(FeedbackForm.titles[kind] ?? "Send Feedback").font(theme.display(22)).foregroundStyle(theme.text)
            Caption(FeedbackForm.intros[kind] ?? "")
            Text("Subject").font(theme.body(13, weight: .semibold)).foregroundStyle(theme.text)
            TextField("Subject", text: $subject, prompt: Text("e.g. Cleaner skipped Firefox"))
                .kcField()
            Text("Message").font(theme.body(13, weight: .semibold)).foregroundStyle(theme.text)
            TextEditor(text: $message)
                .font(theme.body(13))
                .foregroundStyle(theme.text)
                .scrollContentBackground(.hidden)
                .padding(8)
                .background(RoundedRectangle(cornerRadius: theme.radius).fill(theme.surface))
                .overlay(RoundedRectangle(cornerRadius: theme.radius).strokeBorder(theme.controlBorder, lineWidth: 1))
                .accessibilityLabel("Message")
            Caption("Send Email opens your own email app with this message ready to \(AppInfo.email). Nothing is sent until you send it there.")
            HStack(spacing: 8) {
                Button("Copy Message") {
                    NSPasteboard.general.clearContents()
                    NSPasteboard.general.setString("To: \(AppInfo.email)\nSubject: \(subject)\n\n\(message)", forType: .string)
                }
                .kcButton(.secondary)
                Spacer()
                Button("Cancel") { model.sheet = nil }.kcButton(.secondary).keyboardShortcut(.cancelAction)
                Button("Send Email") {
                    openLink(Feedback.link(kind: kind, subject: subject, body: message))
                    model.sheet = nil
                }
                .kcButton(.primary)
            }
        }
        .padding(24)
        .frame(width: 620, height: 560)
        .background(theme.bg)
        .onAppear {
            subject = Feedback.subjects[kind] ?? ""
            message = Feedback.defaultBody(kind)
        }
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
