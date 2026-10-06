import AppKit
import KodiakCore
import SwiftUI

// Shared building blocks. Every color comes from the theme; every setting is a switch, and checkmarks are only for
// batch choices (Cleaner items, Triggers rows).

enum ButtonKind: Equatable {
    case primary, secondary, danger, accent, nav(selected: Bool), link
}

struct KCButtonStyle: ButtonStyle {
    var kind: ButtonKind = .secondary
    var fullWidth = false

    func makeBody(configuration: Configuration) -> some View {
        KCButtonBody(configuration: configuration, kind: kind, fullWidth: fullWidth)
    }
}

private struct KCButtonBody: View {
    let configuration: ButtonStyleConfiguration
    let kind: ButtonKind
    let fullWidth: Bool
    @Environment(\.theme) private var theme
    @Environment(\.isFocused) private var focused
    @Environment(\.isEnabled) private var enabled
    @State private var hover = false

    var body: some View {
        let colors = palette
        configuration.label
            .font(theme.body(13, weight: kind == .link ? .regular : .medium))
            .lineLimit(1)
            .padding(.horizontal, isNav ? 10 : (kind == .link ? 0 : 14))
            .padding(.vertical, isNav ? 7 : (kind == .link ? 2 : 6))
            .frame(maxWidth: fullWidth || isNav ? .infinity : nil, alignment: isNav ? .leading : .center)
            .foregroundStyle(colors.fg)
            .background(RoundedRectangle(cornerRadius: theme.radius).fill(colors.bg))
            .overlay(RoundedRectangle(cornerRadius: theme.radius).strokeBorder(colors.border, lineWidth: colors.border == .clear ? 0 : 1))
            .overlay(FocusRing(radius: theme.radius, shown: focused))
            .opacity(enabled ? (configuration.isPressed ? 0.82 : 1) : 0.45)
            .contentShape(RoundedRectangle(cornerRadius: theme.radius))
            .onHover { hover = $0 }
            .underline(kind == .link && hover)
    }

    private var isNav: Bool { if case .nav = kind { return true } else { return false } }

    private var palette: (bg: Color, fg: Color, border: Color) {
        switch kind {
        case .primary: return (theme.primary, theme.onPrimary, theme.primary)
        case .secondary: return (hover ? theme.surfaceAlt : theme.surface, theme.text, theme.controlBorder)
        case .danger: return (theme.dangerBg, theme.dangerFg, theme.dangerBorder)
        case .accent: return (theme.accentTint, theme.accentText, theme.accent)
        case .nav(let selected): return (selected ? theme.accentTint : (hover ? theme.rowHover : .clear), theme.text, .clear)
        case .link: return (.clear, theme.link, .clear)
        }
    }
}

/// The keyboard focus ring, in the theme's own focus color.
struct FocusRing: View {
    var radius: CGFloat
    var shown: Bool
    @Environment(\.theme) private var theme

    var body: some View {
        RoundedRectangle(cornerRadius: radius + 3)
            .strokeBorder(theme.focusRing, lineWidth: 2)
            .padding(-3)
            .opacity(shown ? 1 : 0)
            .allowsHitTesting(false)
    }
}

extension View {
    func kcButton(_ kind: ButtonKind = .secondary, fullWidth: Bool = false) -> some View {
        buttonStyle(KCButtonStyle(kind: kind, fullWidth: fullWidth)).focusEffectDisabled()
    }
}

/// A switch: the only control for settings.
struct KCSwitchStyle: ToggleStyle {
    func makeBody(configuration: Configuration) -> some View { KCSwitch(configuration: configuration) }
}

private struct KCSwitch: View {
    let configuration: ToggleStyleConfiguration
    @Environment(\.theme) private var theme
    @Environment(\.isFocused) private var focused
    @Environment(\.isEnabled) private var enabled

    var body: some View {
        let on = configuration.isOn
        Button { configuration.isOn.toggle() } label: {
            ZStack(alignment: on ? .trailing : .leading) {
                Capsule().fill(on ? theme.toggleOn : theme.surfaceAlt)
                Capsule().strokeBorder(on ? theme.toggleOn : theme.controlBorder, lineWidth: 1.5)
                Circle().fill(on ? theme.surface : theme.controlBorder).frame(width: 16, height: 16).padding(3)
            }
            .frame(width: 40, height: 22)
            .overlay(FocusRing(radius: 11, shown: focused))
        }
        .buttonStyle(.plain)
        .focusEffectDisabled()
        .opacity(enabled ? 1 : 0.45)
        .accessibilityRepresentation { Toggle(isOn: configuration.$isOn) { configuration.label } }
    }
}

/// A checkmark box for batch choices.
struct KCCheckboxStyle: ToggleStyle {
    func makeBody(configuration: Configuration) -> some View { KCCheckbox(configuration: configuration) }
}

private struct KCCheckbox: View {
    let configuration: ToggleStyleConfiguration
    @Environment(\.theme) private var theme
    @Environment(\.isFocused) private var focused

    var body: some View {
        Button { configuration.isOn.toggle() } label: {
            HStack(spacing: 8) {
                ZStack {
                    RoundedRectangle(cornerRadius: 4).fill(configuration.isOn ? theme.primary : theme.surface)
                    RoundedRectangle(cornerRadius: 4).strokeBorder(configuration.isOn ? theme.primary : theme.controlBorder, lineWidth: 1.5)
                    if configuration.isOn {
                        Image(systemName: "checkmark").font(.system(size: 10, weight: .bold)).foregroundStyle(theme.onPrimary)
                    }
                }
                .frame(width: 17, height: 17)
                .overlay(FocusRing(radius: 4, shown: focused))
                configuration.label
            }
        }
        .buttonStyle(.plain)
        .focusEffectDisabled()
        .accessibilityRepresentation { Toggle(isOn: configuration.$isOn) { configuration.label } }
    }
}

struct SwitchRow: View {
    var title: String
    var help: String? = nil
    @Binding var isOn: Bool
    @Environment(\.theme) private var theme

    var body: some View {
        HStack(alignment: .center, spacing: 16) {
            VStack(alignment: .leading, spacing: 2) {
                Text(title).font(theme.body(13, weight: .semibold)).foregroundStyle(theme.text)
                if let help { Text(help).font(theme.body(12)).foregroundStyle(theme.muted).fixedSize(horizontal: false, vertical: true) }
            }
            Spacer(minLength: 12)
            Toggle(title, isOn: $isOn).toggleStyle(KCSwitchStyle()).labelsHidden()
        }
        .padding(.vertical, 4)
    }
}

struct Card<Content: View>: View {
    var title: String? = nil
    var subtitle: String? = nil
    @ViewBuilder var content: Content
    @Environment(\.theme) private var theme

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            if let title { Text(title).font(theme.display(17)).foregroundStyle(theme.text) }
            if let subtitle { Caption(subtitle) }
            content
        }
        .padding(18)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(RoundedRectangle(cornerRadius: theme.radiusCard).fill(theme.surface))
        .overlay(RoundedRectangle(cornerRadius: theme.radiusCard).strokeBorder(theme.line, lineWidth: 1))
    }
}

struct Caption: View {
    var text: String
    init(_ text: String) { self.text = text }
    @Environment(\.theme) private var theme

    var body: some View {
        Text(text).font(theme.body(12)).foregroundStyle(theme.muted).fixedSize(horizontal: false, vertical: true)
    }
}

struct PageHeader: View {
    var title: String
    var subtitle: String
    @Environment(\.theme) private var theme

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(title).font(theme.display(26)).foregroundStyle(theme.text)
            Text(subtitle).font(theme.body(13)).foregroundStyle(theme.muted)
        }
        .padding(.bottom, 4)
    }
}

struct Chip: View {
    var text: String
    var icon: String? = nil
    @Environment(\.theme) private var theme

    var body: some View {
        HStack(spacing: 4) {
            if let icon { Image(systemName: icon).font(.system(size: 10, weight: .semibold)) }
            Text(text).font(theme.body(11, weight: .semibold))
        }
        .foregroundStyle(theme.chipText)
        .padding(.horizontal, 8).padding(.vertical, 3)
        .background(Capsule().fill(theme.chipBg))
    }
}

/// A caution: always an icon and words, never color alone.
struct WarnBox: View {
    var text: String
    var icon = "exclamationmark.triangle.fill"
    @Environment(\.theme) private var theme

    var body: some View {
        HStack(alignment: .top, spacing: 10) {
            Image(systemName: icon).foregroundStyle(theme.warnText)
            Text(text).font(theme.body(13)).foregroundStyle(theme.warnText).fixedSize(horizontal: false, vertical: true)
            Spacer(minLength: 0)
        }
        .padding(12)
        .background(RoundedRectangle(cornerRadius: theme.radius).fill(theme.warnBg))
        .overlay(RoundedRectangle(cornerRadius: theme.radius).strokeBorder(theme.warnBorder, lineWidth: 1))
    }
}

/// One-of-several choices (Light or Dark, Sort By, Developer preview).
struct Segmented: View {
    var options: [(String, String)]
    @Binding var selection: String
    @Environment(\.theme) private var theme

    var body: some View {
        HStack(spacing: 6) {
            ForEach(options, id: \.0) { key, label in
                Button(label) { selection = key }
                    .kcButton(selection == key ? .primary : .secondary)
                    .accessibilityAddTraits(selection == key ? .isSelected : [])
            }
        }
    }
}

struct LogoView: View {
    var size: CGFloat
    var body: some View {
        if let path = Resources.path("logo.png"), let image = NSImage(contentsOfFile: path) {
            Image(nsImage: image).resizable().interpolation(.high).frame(width: size, height: size)
                .accessibilityLabel("Kodiak Cleaner logo")
        }
    }
}

/// The two-tone name, bigger than any other text.
struct Wordmark: View {
    var size: CGFloat
    var withEdition = false
    @Environment(\.theme) private var theme

    var body: some View {
        HStack(spacing: size * 0.25) {
            Text("Kodiak").foregroundStyle(theme.brand1)
            Text("Cleaner").foregroundStyle(theme.brand2)
            if withEdition { Text("for Mac").foregroundStyle(theme.muted) }
        }
        .font(theme.display(size))
        .accessibilityElement(children: .combine)
    }
}

/// A page that scrolls, with the content kept to a readable width.
struct PageScroll<Content: View>: View {
    @ViewBuilder var content: Content
    @Environment(\.theme) private var theme

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) { content }
                .padding(28)
                .frame(maxWidth: 1000, alignment: .leading)
                .frame(maxWidth: .infinity, alignment: .leading)
        }
        .scrollContentBackground(.hidden)
        .background(theme.bg)
    }
}

/// Simple Markdown for the Changelog, Privacy Policy and Terms of Use: headings, bullets, paragraphs, bold, code and links.
struct MarkdownView: View {
    var text: String
    var skipTitle = true
    @Environment(\.theme) private var theme

    private enum Block: Hashable { case h1(String), h2(String), h3(String), bullet(String), paragraph(String) }

    private var blocks: [Block] {
        var blocks: [Block] = []
        var paragraph: [String] = []
        func flush() {
            if !paragraph.isEmpty { blocks.append(.paragraph(paragraph.joined(separator: " "))) }
            paragraph = []
        }
        for raw in text.components(separatedBy: "\n") {
            let line = raw.trimmingCharacters(in: .whitespaces)
            if line.hasPrefix("<!--") { continue }
            if line.hasPrefix("# ") { flush(); if !skipTitle { blocks.append(.h1(String(line.dropFirst(2)))) } }
            else if line.hasPrefix("## ") { flush(); blocks.append(.h2(String(line.dropFirst(3)))) }
            else if line.hasPrefix("### ") { flush(); blocks.append(.h3(String(line.dropFirst(4)))) }
            else if line.hasPrefix("- ") || line.hasPrefix("* ") { flush(); blocks.append(.bullet(String(line.dropFirst(2)))) }
            else if line.isEmpty { flush() }
            else if case .bullet(let previous)? = blocks.last, paragraph.isEmpty, raw.hasPrefix("  ") {
                blocks[blocks.count - 1] = .bullet(previous + " " + line)
            } else { paragraph.append(line) }
        }
        flush()
        return blocks
    }

    private func inline(_ text: String) -> AttributedString {
        var result = (try? AttributedString(markdown: text, options: .init(interpretedSyntax: .inlineOnlyPreservingWhitespace)))
            ?? AttributedString(text)
        let links = result.runs.filter { $0.link != nil }.map(\.range)
        for range in links {
            result[range].foregroundColor = theme.link
            result[range].underlineStyle = .single
        }
        return result
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            ForEach(Array(blocks.enumerated()), id: \.offset) { _, block in
                switch block {
                case .h1(let t): Text(inline(t)).font(theme.display(22)).foregroundStyle(theme.text)
                case .h2(let t): Text(inline(t)).font(theme.display(18)).foregroundStyle(theme.text).padding(.top, 8)
                case .h3(let t): Text(inline(t)).font(theme.body(14, weight: .semibold)).foregroundStyle(theme.accentText).padding(.top, 4)
                case .bullet(let t):
                    HStack(alignment: .firstTextBaseline, spacing: 8) {
                        Text("•").foregroundStyle(theme.muted)
                        Text(inline(t)).font(theme.body(13)).foregroundStyle(theme.text).fixedSize(horizontal: false, vertical: true)
                    }
                case .paragraph(let t):
                    Text(inline(t)).font(theme.body(13)).foregroundStyle(theme.text).fixedSize(horizontal: false, vertical: true)
                }
            }
        }
        .textSelection(.enabled)
        .tint(theme.link)
    }
}

/// Opens a web page or email in the person's own browser or email app.
@MainActor
func openLink(_ url: URL?) {
    if let url { NSWorkspace.shared.open(url) }
}

func formatBytes(_ bytes: Int64) -> String {
    ByteCountFormatter.string(fromByteCount: bytes, countStyle: .file)
}
