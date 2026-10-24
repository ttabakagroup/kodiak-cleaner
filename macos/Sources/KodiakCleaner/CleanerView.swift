import AppKit
import KodiakCore
import SwiftUI

/// Cleaner Sort By choices; Default (Ted's standing order) first and chosen out of the box.
let sortOptions = [("default", "Default"), ("az", "A to Z"), ("largest", "Largest Found"), ("last", "Last Cleaned"),
                   ("freed", "Most Space Freed")]

/// An app's own icon, read from the app installed on this Mac (never a bundled brand logo).
struct AppIcon: View {
    var bundleID: String
    var fallback: String
    var size: CGFloat = 20
    @Environment(\.theme) private var theme

    var body: some View {
        if !bundleID.isEmpty, let url = NSWorkspace.shared.urlForApplication(withBundleIdentifier: bundleID) {
            Image(nsImage: NSWorkspace.shared.icon(forFile: url.path)).resizable().frame(width: size, height: size)
        } else {
            Image(systemName: fallback).foregroundStyle(theme.accentText).frame(width: size, height: size)
        }
    }
}

struct CleanerView: View {
    @EnvironmentObject var model: AppModel
    @Environment(\.theme) private var theme
    @FocusState private var searchFocused: Bool
    @StateObject private var drag = TickDrag()

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            PageHeader(title: "Cleaner", subtitle: "Tick what to clean, analyze to see what's there, then clean.")
            toolbar
            if !model.hasFullDiskAccess && model.targets.contains(where: { $0.needsFullDiskAccess && model.selected.contains($0.id) }) {
                HStack(spacing: 10) {
                    WarnBox(text: "Safari, Mail and the Trash need Full Disk Access. Allow Kodiak Cleaner in Privacy & Security, then open it again.",
                            icon: "lock.fill")
                    Button("Open Privacy Settings") { openLink(AppModel.fullDiskAccessURL) }.kcButton(.secondary)
                }
            }
            list
            footer
        }
        .padding(.horizontal, 28)
        .padding(.top, 28)
        .padding(.bottom, 18)
        .onAppear { model.resortTicked() }
        .onChange(of: model.findRequest) { searchFocused = true }
    }

    private var toolbar: some View {
        HStack(spacing: 10) {
            HStack(spacing: 6) {
                Image(systemName: "magnifyingglass").foregroundStyle(theme.muted)
                TextField("Search", text: $model.cleanerSearch, prompt: Text("Search"))
                    .textFieldStyle(.plain).foregroundStyle(theme.text).focused($searchFocused)
            }
            .padding(.horizontal, 10).padding(.vertical, 6)
            .frame(width: 220)
            .background(RoundedRectangle(cornerRadius: theme.radius).fill(theme.surface))
            .overlay(RoundedRectangle(cornerRadius: theme.radius).strokeBorder(theme.controlBorder, lineWidth: 1))
            Menu {
                ForEach(sortOptions, id: \.0) { key, label in
                    Button {
                        model.settings.sortBy = key
                        if key == "default" { model.resortTicked() }
                    } label: {
                        if model.settings.sortBy == key { Label(label, systemImage: "checkmark") } else { Text(label) }
                    }
                }
            } label: {
                Text("Sort By: \(sortOptions.first { $0.0 == model.settings.sortBy }?.1 ?? "Default")")
            }
            .menuStyle(.button)
            .kcButton(.secondary)
            .fixedSize()
            Spacer()
            Button("Select All") { model.selectAll(true) }.kcButton(.secondary)
            Button("Select None") { model.selectAll(false) }.kcButton(.secondary)
            Button("Reset to Default") { model.resetToDefault() }.kcButton(.secondary)
            Button(model.showAdvanced ? "Hide Advanced Cleaning" : "Advanced Cleaning") { model.toggleAdvanced() }
                .kcButton(model.showAdvanced ? .accent : .secondary)
        }
    }

    private var groups: [(String, [CleanTarget])] {
        var result: [(String, [CleanTarget])] = []
        for target in model.visibleTargets {
            if model.settings.sortBy == "default" || model.settings.sortBy == "az", let last = result.last, last.0 == target.group {
                result[result.count - 1].1.append(target)
            } else if model.settings.sortBy == "default" || model.settings.sortBy == "az" {
                result.append((target.group, [target]))
            } else {
                result.append(("", [target]))
            }
        }
        return result
    }

    private var list: some View {
        let grouped = model.settings.sortBy == "default" || model.settings.sortBy == "az"
        return VStack(spacing: 0) {
            HStack(spacing: 0) {
                Color.clear.frame(width: 48, height: 1)
                Text("Item").frame(maxWidth: .infinity, alignment: .leading)
                Text("Found").frame(width: 130, alignment: .trailing)
                Text("Last Cleaned").frame(width: 130, alignment: .trailing).padding(.trailing, 14)
            }
            .font(theme.body(12, weight: .semibold))
            .foregroundStyle(theme.muted)
            .padding(.vertical, 8)
            .background(theme.surfaceAlt)
            Rectangle().fill(theme.line).frame(height: 1)
            ScrollView {
                LazyVStack(spacing: 0) {
                    ForEach(Array(groups.enumerated()), id: \.offset) { _, group in
                        if grouped { GroupHeader(name: group.0, targets: group.1) }
                        ForEach(Array(group.1.enumerated()), id: \.element.id) { index, target in
                            CleanerRow(target: target, alternate: index % 2 == 1, showGroup: !grouped)
                                .tickDragRow(target.id, drag: drag, isTicked: { model.selected.contains($0) },
                                             apply: { model.paint($0, $1) })
                        }
                    }
                    if model.visibleTargets.isEmpty {
                        Caption("Nothing matches. Clear the search to see every item.").padding(24)
                    }
                }
                .tickDragOutline(drag, theme: theme)
            }
            .scrollContentBackground(.hidden)
        }
        .background(RoundedRectangle(cornerRadius: theme.radiusCard).fill(theme.surface))
        .clipShape(RoundedRectangle(cornerRadius: theme.radiusCard))
        .overlay(RoundedRectangle(cornerRadius: theme.radiusCard).strokeBorder(theme.line, lineWidth: 1))
    }

    private var footer: some View {
        HStack(spacing: 10) {
            if let working = model.working {
                ProgressView().controlSize(.small)
                Text(working).font(theme.body(13)).foregroundStyle(theme.text).lineLimit(1)
            } else {
                Text(model.summary.isEmpty ? "\(model.selected.intersection(Set(model.targets.map(\.id))).count) items ticked." : model.summary)
                    .font(theme.body(13)).foregroundStyle(theme.text)
            }
            Spacer()
            Button("Analyze") { model.analyze() }.kcButton(.secondary).disabled(model.working != nil)
            Button("Clean") { model.cleanClicked() }.kcButton(.primary).disabled(model.working != nil)
        }
    }
}

private struct GroupHeader: View {
    var name: String
    var targets: [CleanTarget]
    @EnvironmentObject var model: AppModel
    @Environment(\.theme) private var theme

    var body: some View {
        HStack(spacing: 10) {
            AppIcon(bundleID: targets.first?.appBundleID ?? "", fallback: icon)
            Text(name).font(theme.body(13, weight: .bold)).foregroundStyle(theme.text)
            Spacer()
        }
        .padding(.leading, 14)
        .padding(.vertical, 8)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(theme.surfaceAlt)
        .overlay(alignment: .bottom) { Rectangle().fill(theme.rowLine).frame(height: 1) }
    }

    private var icon: String {
        switch name {
        case Cleaner.systemGroup: return "desktopcomputer"
        case Cleaner.advancedGroup: return "exclamationmark.triangle"
        default: return "shippingbox"
        }
    }
}

private struct CleanerRow: View {
    var target: CleanTarget
    var alternate: Bool
    var showGroup: Bool
    @EnvironmentObject var model: AppModel
    @Environment(\.theme) private var theme
    @State private var hover = false

    var body: some View {
        let finding = model.findings[target.id]
        let result = model.results[target.id]
        HStack(spacing: 0) {
            Toggle(target.name, isOn: Binding(get: { model.selected.contains(target.id) }, set: { model.tick(target, $0) }))
                .toggleStyle(KCCheckboxStyle()).labelsHidden()
                .frame(width: 48)
            VStack(alignment: .leading, spacing: 2) {
                HStack(spacing: 6) {
                    Text(showGroup ? "\(target.group): \(target.name)" : target.name)
                        .font(theme.body(13, weight: .semibold)).foregroundStyle(theme.text)
                    if !target.warning.isEmpty { Chip(text: "Caution", icon: "exclamationmark.triangle.fill") }
                    if target.needsFullDiskAccess { Chip(text: "Full Disk Access", icon: "lock.fill") }
                }
                Caption(target.description)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(.vertical, 8)
            Text(foundText(finding, result)).font(theme.body(12)).foregroundStyle(theme.text)
                .multilineTextAlignment(.trailing).frame(width: 130, alignment: .trailing)
            Text(lastCleaned).font(theme.body(12)).foregroundStyle(theme.muted).frame(width: 130, alignment: .trailing)
                .padding(.trailing, 14)
        }
        .background(hover ? theme.rowHover : (alternate ? theme.rowAlt : theme.surface))
        .overlay(alignment: .bottom) { Rectangle().fill(theme.rowLine).frame(height: 1) }
        .onHover { hover = $0 }
    }

    private func foundText(_ finding: Finding?, _ result: Cleaner.Result?) -> String {
        if let result {
            return result.message.isEmpty ? "Freed \(formatBytes(result.freed))" : result.message
        }
        guard let finding else { return "" }
        if !finding.skipped.isEmpty { return finding.skipped }
        if finding.unknownSize { return finding.note }
        return finding.count == 0 ? "Nothing" : formatBytes(finding.size)
    }

    private var lastCleaned: String {
        guard let history = model.settings.cleanerHistory[target.id] else { return "Never" }
        let date = Date(timeIntervalSince1970: history.lastCleaned)
        return date.formatted(.relative(presentation: .named))
    }
}
