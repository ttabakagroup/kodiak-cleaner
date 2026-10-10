import AppKit
import KodiakCore
import SwiftUI

struct TriggersView: View {
    @EnvironmentObject var model: AppModel
    @Environment(\.theme) private var theme

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            PageHeader(title: "Triggers", subtitle: "Everything that starts by itself on this Mac. Every change is backed up first.")
            HStack(spacing: 10) {
                HStack(spacing: 6) {
                    Image(systemName: "magnifyingglass").foregroundStyle(theme.muted)
                    TextField("Search", text: $model.triggerSearch, prompt: Text("Search")).textFieldStyle(.plain).foregroundStyle(theme.text)
                }
                .padding(.horizontal, 10).padding(.vertical, 6)
                .frame(width: 220)
                .background(RoundedRectangle(cornerRadius: theme.radius).fill(theme.surface))
                .overlay(RoundedRectangle(cornerRadius: theme.radius).strokeBorder(theme.controlBorder, lineWidth: 1))
                Segmented(options: [("all", "All"), ("look", "Needs a Look"), ("off", "Turned Off")], selection: $model.triggerFilter)
                Spacer()
                Button("Login Items") { openLink(AppModel.loginItemsURL) }.kcButton(.secondary)
                    .help("Opens Login Items & Extensions in System Settings, where macOS lists apps that open at login")
                Button("History") { model.sheet = .triggersHistory }.kcButton(.secondary)
                Button("Refresh") { model.loadTriggers() }.kcButton(.secondary)
            }
            list
            HStack(spacing: 10) {
                if let working = model.working {
                    ProgressView().controlSize(.small)
                    Text(working).font(theme.body(13)).foregroundStyle(theme.text)
                } else {
                    let shown = model.visibleTriggers
                    Text("\(shown.count) shown, \(shown.filter(\.needsLook).count) worth a look, \(model.triggerSelection.count) ticked.")
                        .font(theme.body(13)).foregroundStyle(theme.text)
                }
                Spacer()
                Button("Turn Off") { model.changeTriggers(ticked, .turnedOff) }.kcButton(.secondary).disabled(ticked.isEmpty)
                Button("Turn On") { model.changeTriggers(ticked, .turnedOn) }.kcButton(.secondary).disabled(ticked.isEmpty)
                Button("Remove") { model.changeTriggers(ticked, .removed) }.kcButton(.danger).disabled(ticked.isEmpty)
            }
        }
        .padding(.horizontal, 28)
        .padding(.top, 28)
        .padding(.bottom, 18)
        .onAppear { if !model.triggersLoaded { model.loadTriggers() } }
        .onChange(of: model.settings.hideProtected) { model.loadTriggers() }
        .onChange(of: model.settings.showSystemTriggers) { model.loadTriggers() }
    }

    private var ticked: [Trigger] { model.triggers.filter { model.triggerSelection.contains($0.id) } }

    private var list: some View {
        let shown = model.visibleTriggers
        let groups = Triggers.groupOrder.compactMap { kind -> (Trigger.Kind, [Trigger])? in
            let items = shown.filter { $0.kind == kind }
            return items.isEmpty ? nil : (kind, items)
        }
        let selectable = shown.filter { $0.canToggle && !$0.isProtected }
        return VStack(spacing: 0) {
            HStack(spacing: 0) {
                Toggle("Select Everything Shown", isOn: Binding(
                    get: { !selectable.isEmpty && selectable.allSatisfy { model.triggerSelection.contains($0.id) } },
                    set: { on in
                        if on { model.triggerSelection.formUnion(selectable.map(\.id)) }
                        else { model.triggerSelection.subtract(selectable.map(\.id)) }
                    }))
                    .toggleStyle(KCCheckboxStyle()).labelsHidden().frame(width: 48)
                Text("Entry").frame(maxWidth: .infinity, alignment: .leading)
                Text("Runs").frame(width: 170, alignment: .leading)
                Text("Status").frame(width: 90, alignment: .leading)
                Color.clear.frame(width: 44, height: 1)
            }
            .font(theme.body(12, weight: .semibold))
            .foregroundStyle(theme.muted)
            .padding(.vertical, 8)
            .background(theme.surfaceAlt)
            Rectangle().fill(theme.line).frame(height: 1)
            ScrollView {
                LazyVStack(spacing: 0) {
                    ForEach(groups, id: \.0) { kind, items in
                        VStack(alignment: .leading, spacing: 1) {
                            Text(Triggers.groupNames[kind]!).font(theme.body(13, weight: .bold)).foregroundStyle(theme.text)
                            Caption(Triggers.groupBlurbs[kind]!)
                        }
                        .padding(.leading, 14).padding(.vertical, 8)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .background(theme.surfaceAlt)
                        .overlay(alignment: .bottom) { Rectangle().fill(theme.rowLine).frame(height: 1) }
                        ForEach(Array(items.enumerated()), id: \.element.id) { index, item in
                            TriggerRow(item: item, alternate: index % 2 == 1)
                        }
                    }
                    if model.triggersLoaded && shown.isEmpty {
                        Caption(model.triggerFilter == "look" ? "Nothing needs a look right now." : "Nothing matches.").padding(24)
                    }
                }
            }
            .scrollContentBackground(.hidden)
        }
        .background(RoundedRectangle(cornerRadius: theme.radiusCard).fill(theme.surface))
        .clipShape(RoundedRectangle(cornerRadius: theme.radiusCard))
        .overlay(RoundedRectangle(cornerRadius: theme.radiusCard).strokeBorder(theme.line, lineWidth: 1))
    }
}

private struct TriggerRow: View {
    var item: Trigger
    var alternate: Bool
    @EnvironmentObject var model: AppModel
    @Environment(\.theme) private var theme
    @State private var hover = false

    var body: some View {
        HStack(alignment: .top, spacing: 0) {
            Group {
                if item.canToggle && !item.isProtected {
                    Toggle(item.name, isOn: Binding(
                        get: { model.triggerSelection.contains(item.id) },
                        set: { on in if on { model.triggerSelection.insert(item.id) } else { model.triggerSelection.remove(item.id) } }))
                        .toggleStyle(KCCheckboxStyle()).labelsHidden()
                } else {
                    Image(systemName: "lock").foregroundStyle(theme.muted).help("Shown for reference only")
                        .accessibilityLabel("Can't be changed here")
                }
            }
            .frame(width: 48)
            .padding(.top, 10)
            HStack(alignment: .top, spacing: 8) {
                AppIcon(bundleID: "", fallback: icon, size: 18).padding(.top, 9)
                VStack(alignment: .leading, spacing: 3) {
                    Text(item.name).font(theme.body(13, weight: .semibold)).foregroundStyle(theme.text)
                    if item.label != item.name { Caption(item.label) }
                    if !item.program.isEmpty { Caption(Paths.display(item.program)).lineLimit(1).truncationMode(.middle) }
                    if !item.notes.isEmpty {
                        HStack(spacing: 6) {
                            ForEach(item.notes, id: \.id) { note in
                                Chip(text: note.text, icon: note.look ? "exclamationmark.triangle.fill" : "info.circle")
                            }
                        }
                    }
                }
                .padding(.vertical, 8)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            Text(item.schedule).font(theme.body(12)).foregroundStyle(theme.text).frame(width: 170, alignment: .leading).padding(.top, 9)
            statusText.frame(width: 90, alignment: .leading).padding(.top, 9)
            Menu {
                if FileManager.default.fileExists(atPath: item.path) {
                    Button("Show in Finder") { model.reveal(item.path) }
                }
                if item.kind == .shellFile || item.kind == .systemShellFile {
                    Button("Open File") { NSWorkspace.shared.open(URL(fileURLWithPath: item.path)) }
                }
                if !item.program.isEmpty && FileManager.default.fileExists(atPath: item.program) {
                    Button("Show Program in Finder") { model.reveal(item.program) }
                }
                if item.canToggle && !item.isProtected {
                    Divider()
                    if item.enabled == false {
                        Button("Turn On") { model.changeTriggers([item], .turnedOn) }
                    } else {
                        Button("Turn Off") { model.changeTriggers([item], .turnedOff) }
                    }
                }
                if item.canRemove { Button("Remove") { model.changeTriggers([item], .removed) } }
            } label: {
                Image(systemName: "ellipsis.circle").foregroundStyle(theme.text)
            }
            .menuStyle(.button)
            .buttonStyle(.plain)
            .menuIndicator(.hidden)
            .accessibilityLabel("Actions for \(item.name)")
            .frame(width: 44)
            .padding(.top, 9)
        }
        .background(hover ? theme.rowHover : (alternate ? theme.rowAlt : theme.surface))
        .overlay(alignment: .bottom) { Rectangle().fill(theme.rowLine).frame(height: 1) }
        .onHover { hover = $0 }
        // Hover details: everything about the entry, one "Label: value" line each (Ted, 0.8.3).
        .help(DetailText.lines(item.detailFields))
        .contextMenu {
            if FileManager.default.fileExists(atPath: item.path) { Button("Show in Finder") { model.reveal(item.path) } }
        }
    }

    private var icon: String {
        switch item.kind {
        case .cron: return "clock"
        case .shellFile, .systemShellFile: return "terminal"
        case .daemon, .appleDaemon: return "gearshape.2"
        default: return "bolt.horizontal"
        }
    }

    @ViewBuilder private var statusText: some View {
        switch item.enabled {
        case .some(true): Label("On", systemImage: "checkmark.circle.fill").font(theme.body(12, weight: .semibold)).foregroundStyle(theme.text)
        case .some(false): Label("Off", systemImage: "minus.circle").font(theme.body(12, weight: .semibold)).foregroundStyle(theme.muted)
        case .none: Text("Always").font(theme.body(12)).foregroundStyle(theme.muted)
        }
    }
}

struct TriggersHistoryView: View {
    @EnvironmentObject var model: AppModel
    @Environment(\.theme) private var theme

    static let actionNames: [Triggers.Action: String] = [.turnedOff: "Turned Off", .turnedOn: "Turned On", .removed: "Removed"]

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Triggers History").font(theme.display(22)).foregroundStyle(theme.text)
            Caption("Every change Kodiak Cleaner made, newest first, with the backup copy it took before. Restore puts a change back.")
            ScrollView {
                LazyVStack(spacing: 0) {
                    ForEach(model.history) { entry in
                        HStack(spacing: 10) {
                            VStack(alignment: .leading, spacing: 2) {
                                Text("\(TriggersHistoryView.actionNames[entry.action]!): \(entry.name)")
                                    .font(theme.body(13, weight: .semibold)).foregroundStyle(theme.text)
                                Caption("\(Date(timeIntervalSince1970: entry.date).formatted(date: .abbreviated, time: .shortened)) · \(Triggers.groupNames[entry.kind]!)")
                            }
                            Spacer()
                            if entry.restored {
                                Chip(text: "Restored", icon: "arrow.uturn.backward")
                            } else {
                                Button("Restore") { model.restore(entry) }.kcButton(.secondary)
                            }
                            Button("Show Backup") { model.reveal(entry.backup) }.kcButton(.secondary)
                        }
                        .padding(10)
                        .overlay(alignment: .bottom) { Rectangle().fill(theme.rowLine).frame(height: 1) }
                    }
                    if model.history.isEmpty { Caption("No changes yet.").padding(20) }
                }
            }
            .background(RoundedRectangle(cornerRadius: theme.radiusCard).fill(theme.surface))
            HStack {
                Button("Open History Folder") { model.openFolder(Paths.triggersHistory) }.kcButton(.secondary)
                Spacer()
                Button("Close") { model.sheet = nil }.kcButton(.primary).keyboardShortcut(.defaultAction)
            }
        }
        .padding(24)
        .frame(width: 680, height: 540)
        .background(theme.bg)
        .onAppear { model.history = Triggers.history() }
    }
}
