import KodiakCore
import SwiftUI

/// Tools > Drive Wiper: overwrite a volume's free space, or erase whole external drives (Mac port of the Windows
/// edition's Drive Wiper, 0.8.112).
struct WiperView: View {
    @EnvironmentObject var model: AppModel
    @ObservedObject private var wiper = WiperModel.shared
    @Environment(\.theme) private var theme

    var body: some View {
        PageScroll {
            HStack(alignment: .bottom) {
                PageHeader(title: "Drive Wiper", subtitle: "Overwrite free space so deleted files can't be recovered, or securely erase whole drives you're giving away. Each drive type gets the right method.")
                Spacer()
                Button("Refresh") { wiper.refresh() }.kcButton(.secondary).disabled(wiper.busy || wiper.reading)
            }
            Card {
                HStack {
                    Text("Wipe").font(theme.display(17)).foregroundStyle(theme.text)
                    Spacer()
                    Segmented(options: [("free", "Free Space Only"), ("entire", "Entire Drive")], selection: $wiper.mode)
                        .disabled(wiper.busy)
                }
                if wiper.entireDrive { entireDrive } else { freeSpace }
                if !wiper.entireDrive || wiper.level == .secure {
                    label("Security")
                    Segmented(options: WipeMethod.allCases.map { ($0.rawValue, WipeMethods.name($0)) },
                              selection: Binding(get: { wiper.method.rawValue },
                                                 set: { wiper.method = WipeMethod(rawValue: $0) ?? .dod3 }))
                        .disabled(wiper.busy)
                }
                Caption(wiper.methodHint)
                if wiper.entireDrive { afterWiping }
                if !wiper.driveNote.isEmpty { WarnBox(text: wiper.driveNote).padding(.top, 6) }
                HStack(spacing: 10) {
                    Button { wiper.wipeClicked() } label: { Label("Wipe", systemImage: "xmark.bin") }
                        .kcButton(.danger).disabled(!wiper.canWipe)
                    if wiper.busy { Button("Stop") { wiper.stopClicked() }.kcButton(.secondary).disabled(wiper.stopping && !wiper.entireDrive) }
                }
                .padding(.top, 6)
                if !wiper.busy && !wiper.status.isEmpty { Caption(wiper.status).textSelection(.enabled) }
            }
            if wiper.busy { progressCard }
            howCard
        }
        .onAppear { if !wiper.loaded { wiper.refresh() } }
    }

    private func label(_ text: String) -> some View {
        Text(text).font(theme.body(13, weight: .semibold)).foregroundStyle(theme.text).padding(.top, 10)
    }

    // MARK: Free Space Only

    @ViewBuilder private var freeSpace: some View {
        label("Drive")
        if wiper.volumes.isEmpty {
            Caption(wiper.reading ? "Reading drives…" : "No drive with free space to wipe was found. Press Refresh to look again.")
        } else {
            VStack(alignment: .leading, spacing: 6) {
                ForEach(wiper.volumes) { volume in
                    Button { wiper.volumeID = volume.id } label: {
                        HStack(spacing: 10) {
                            Image(systemName: volume.isStartup ? "internaldrive" : "externaldrive")
                            VStack(alignment: .leading, spacing: 1) {
                                Text(volume.name).font(theme.body(13, weight: .semibold))
                                Text("\(volume.format.isEmpty ? volume.typeName : volume.format) · \(volume.kindName) · \(formatBytes(volume.wipeable)) free of \(formatBytes(volume.size))")
                                    .font(theme.body(12))
                            }
                            Spacer(minLength: 0)
                            if wiper.volumeID == volume.id { Image(systemName: "checkmark.circle.fill") }
                        }
                    }
                    .kcButton(wiper.volumeID == volume.id ? .accent : .secondary, fullWidth: true)
                    .frame(maxWidth: 520)
                    .disabled(wiper.busy)
                    .accessibilityAddTraits(wiper.volumeID == volume.id ? .isSelected : [])
                }
            }
        }
    }

    // MARK: Entire Drive

    @ViewBuilder private var entireDrive: some View {
        label("Drives")
        DiskTable()
        if !wiper.listNote.isEmpty { Caption(wiper.listNote) }
        label("Wipe Type")
        Segmented(options: WipeLevel.allCases.map { ($0.rawValue, WipeMethods.levelName($0)) },
                  selection: Binding(get: { wiper.level.rawValue }, set: { wiper.level = WipeLevel(rawValue: $0) ?? .secure }))
            .disabled(wiper.busy)
    }

    @ViewBuilder private var afterWiping: some View {
        SwitchRow(title: "Leave It Uninitialized Like a New Drive",
                  help: "Every wipe removes all partitions and the MBR or GPT. On: nothing is put back, so macOS says it can't read the drive and offers to initialize it, just like one fresh from the factory. Off: it's set up with the format you choose here.",
                  isOn: $wiper.leaveUninitialized)
            .disabled(wiper.busy)
            .padding(.top, 8)
        // Which format is recommended for the ticked drive's type, and why (Ted, as in Windows 0.8.112).
        HStack(alignment: .top, spacing: 10) {
            Image(systemName: "lightbulb").foregroundStyle(theme.accentText).padding(.top, 1)
            VStack(alignment: .leading, spacing: 4) {
                Text(wiper.recommendTitle).font(theme.body(13, weight: .semibold)).foregroundStyle(theme.text)
                    .fixedSize(horizontal: false, vertical: true)
                Text(wiper.recommendText).font(theme.body(13)).foregroundStyle(theme.text).fixedSize(horizontal: false, vertical: true)
            }
            Spacer(minLength: 0)
            if wiper.isCustomFormat {
                Button("Use Recommended") { wiper.useRecommended() }.kcButton(.secondary).disabled(wiper.busy)
            }
        }
        .padding(.horizontal, 14).padding(.vertical, 12)
        .background(RoundedRectangle(cornerRadius: theme.radius).fill(theme.accentTint))
        .overlay(RoundedRectangle(cornerRadius: theme.radius).strokeBorder(theme.accent, lineWidth: 1))
        .opacity(wiper.leaveUninitialized ? 0.45 : 1)
        .padding(.top, 6)
        // The format choices stay on screen, grayed out, while the drive is to be left uninitialized (Ted).
        VStack(alignment: .leading, spacing: 6) {
            label("Partition Style")
            RecommendedChoices(options: [("gpt", "GPT"), ("mbr", "MBR")],
                               recommended: wiper.recommended.map { $0.gpt ? "gpt" : "mbr" },
                               selection: Binding(get: { wiper.gpt ? "gpt" : "mbr" }, set: { wiper.gpt = $0 == "gpt" }))
            label("File System")
            RecommendedChoices(options: WipeFileSystem.allCases.map { ($0.rawValue, $0.label) },
                               recommended: wiper.recommended?.fileSystem.rawValue,
                               selection: Binding(get: { wiper.fileSystem.rawValue },
                                                  set: { wiper.fileSystem = WipeFileSystem(rawValue: $0) ?? .exfat }))
            label("Drive Name")
            TextField("Drive Name", text: $wiper.label, prompt: Text("Wiped")).kcField().frame(width: 220)
            Caption("GPT suits drives over 2 TB and every Mac and modern PC; MBR suits older PCs and devices. APFS for Macs only, Mac OS Extended for older Macs, exFAT to share with Windows PCs, TVs and cameras, FAT32 for old devices (drives up to 32 GB).")
        }
        .disabled(wiper.leaveUninitialized || wiper.busy)
        .opacity(wiper.leaveUninitialized ? 0.45 : 1)
    }

    // MARK: Progress

    private var progressCard: some View {
        Card {
            Text(wiper.status).font(theme.body(13)).foregroundStyle(theme.text).fixedSize(horizontal: false, vertical: true)
            if let percent = wiper.percent {
                ProgressView(value: min(100, max(0, percent)), total: 100).tint(theme.accent)
            } else {
                ProgressView().progressViewStyle(.linear).tint(theme.accent)
            }
            TimelineView(.periodic(from: .now, by: 1)) { context in
                Caption(clock(now: context.date))
            }
        }
    }

    /// Elapsed time, and the time left: diskutil's own estimate when it gives one, otherwise worked out from the percent.
    private func clock(now: Date) -> String {
        guard let started = wiper.started else { return "" }
        let elapsed = now.timeIntervalSince(started)
        var text = "Elapsed \(Self.time(elapsed))"
        if let remaining = wiper.remaining {
            text += " · About \(remaining) left in this pass"
        } else if let percent = wiper.percent, percent >= 1, percent < 100 {
            text += " · About \(Self.time(elapsed * (100 - percent) / percent)) left"
        }
        return text
    }

    static func time(_ seconds: TimeInterval) -> String {
        let total = Int(max(0, seconds))
        return String(format: "%d:%02d:%02d", total / 3600, total / 60 % 60, total % 60)
    }

    // MARK: How Each Drive Type Is Erased

    private var howCard: some View {
        Card(title: "How Each Drive Type Is Erased") {
            Grid(alignment: .topLeading, horizontalSpacing: 14, verticalSpacing: 12) {
                GridRow {
                    Chip(text: "Flash Drive")
                    Text("USB sticks and memory cards are overwritten with the passes you choose. macOS can't run a firmware erase on them, so for top-secret data, destroy the stick.")
                        .font(theme.body(13)).foregroundStyle(theme.text).fixedSize(horizontal: false, vertical: true)
                }
                GridRow {
                    Chip(text: "Hard Drive")
                    Text("Every sector is overwritten with the passes you choose (the DoD 5220.22-M pattern, with random data in place of ones).")
                        .font(theme.body(13)).foregroundStyle(theme.text).fixedSize(horizontal: false, vertical: true)
                }
                GridRow {
                    Chip(text: "SSD")
                    Text("macOS has no firmware erase for external SSDs, so they get one pass of random data, the most macOS can do for them. The Mac's own internal SSD is erased with Erase All Content and Settings in System Settings.")
                        .font(theme.body(13)).foregroundStyle(theme.text).fixedSize(horizontal: false, vertical: true)
                }
            }
        }
    }
}

/// One-of-several choices where one may be tagged Recommended (Partition Style and File System).
struct RecommendedChoices: View {
    var options: [(String, String)]
    var recommended: String?
    @Binding var selection: String
    @Environment(\.theme) private var theme

    var body: some View {
        HStack(spacing: 6) {
            ForEach(options, id: \.0) { key, name in
                Button { selection = key } label: {
                    HStack(spacing: 6) {
                        Text(name)
                        if recommended == key {
                            Text("Recommended").font(theme.body(11)).padding(.horizontal, 6).padding(.vertical, 1)
                                .background(Capsule().fill(selection == key ? theme.onPrimary.opacity(0.22) : theme.chipBg))
                        }
                    }
                }
                .kcButton(selection == key ? .primary : .secondary)
                .accessibilityLabel(recommended == key ? "\(name), Recommended" : name)
                .accessibilityAddTraits(selection == key ? .isSelected : [])
            }
        }
    }
}

/// The physical disks, tick the ones to erase. Only drives that can be erased take a tick; the others show why.
private struct DiskTable: View {
    @ObservedObject private var wiper = WiperModel.shared
    @Environment(\.theme) private var theme
    @StateObject private var drag = TickDrag()

    var body: some View {
        VStack(spacing: 0) {
            HStack(spacing: 0) {
                Toggle("Select All", isOn: Binding(get: { wiper.allTicked }, set: { wiper.selectAll($0) }))
                    .toggleStyle(KCCheckboxStyle()).labelsHidden().frame(width: 44)
                    .disabled(wiper.wipeable.isEmpty || wiper.busy)
                Text("Drive").frame(maxWidth: .infinity, alignment: .leading)
                Text("Type").frame(width: 100, alignment: .leading)
                Text("Size").frame(width: 80, alignment: .leading)
                Text("Method").frame(width: 120, alignment: .leading)
                Text("Status").frame(width: 150, alignment: .leading)
            }
            .font(theme.body(12, weight: .semibold))
            .foregroundStyle(theme.muted)
            .padding(.vertical, 8)
            .background(theme.surfaceAlt)
            Rectangle().fill(theme.line).frame(height: 1)
            LazyVStack(spacing: 0) {
                ForEach(Array(wiper.disks.enumerated()), id: \.element.id) { index, disk in
                    DiskRow(disk: disk, alternate: index % 2 == 1)
                        .tickDragRow(disk.id, drag: drag, isTicked: { wiper.ticked.contains($0) },
                                     apply: { ids, on in if !wiper.busy { wiper.paint(ids, on) } })
                }
                if wiper.disks.isEmpty {
                    Caption(wiper.reading ? "Reading drives…" : "Drives that can be wiped show here. Press Refresh to look again.").padding(20)
                }
            }
            .tickDragOutline(drag, theme: theme)
        }
        .background(RoundedRectangle(cornerRadius: theme.radiusCard).fill(theme.surface))
        .clipShape(RoundedRectangle(cornerRadius: theme.radiusCard))
        .overlay(RoundedRectangle(cornerRadius: theme.radiusCard).strokeBorder(theme.line, lineWidth: 1))
    }
}

private struct DiskRow: View {
    var disk: WipeDisk
    var alternate: Bool
    @ObservedObject private var wiper = WiperModel.shared
    @Environment(\.theme) private var theme
    @State private var hover = false

    var body: some View {
        HStack(alignment: .top, spacing: 0) {
            Group {
                if disk.canWipe {
                    Toggle(disk.name, isOn: Binding(get: { wiper.ticked.contains(disk.id) }, set: { wiper.tick(disk.id, $0) }))
                        .toggleStyle(KCCheckboxStyle()).labelsHidden().disabled(wiper.busy)
                } else {
                    Image(systemName: "lock").foregroundStyle(theme.muted).help(disk.reason ?? "")
                        .accessibilityLabel("Can't be erased")
                }
            }
            .frame(width: 44)
            .padding(.top, 10)
            VStack(alignment: .leading, spacing: 2) {
                Text(disk.name).font(theme.body(13, weight: .semibold)).foregroundStyle(theme.text).lineLimit(1).truncationMode(.middle)
                Caption(disk.volumesText).lineLimit(1)
            }
            .padding(.vertical, 8)
            .frame(maxWidth: .infinity, alignment: .leading)
            Chip(text: disk.kindName).frame(width: 100, alignment: .leading).padding(.top, 9)
            Text(formatBytes(disk.size)).font(theme.body(12)).foregroundStyle(theme.text).frame(width: 80, alignment: .leading).padding(.top, 10)
            Text(disk.canWipe ? wiper.methodText(disk) : "").font(theme.body(12)).foregroundStyle(theme.text)
                .frame(width: 120, alignment: .leading).padding(.top, 10)
            Text(disk.reason ?? wiper.rowStatus[disk.id] ?? "Ready").font(theme.body(12))
                .foregroundStyle(disk.canWipe ? theme.text : theme.muted)
                .lineLimit(3).fixedSize(horizontal: false, vertical: true)
                .frame(width: 150, alignment: .leading).padding(.vertical, 10)
        }
        .padding(.trailing, 8)
        .background(hover ? theme.rowHover : (alternate ? theme.rowAlt : theme.surface))
        .overlay(alignment: .bottom) { Rectangle().fill(theme.rowLine).frame(height: 1) }
        .onHover { hover = $0 }
        .help(disk.reason ?? "\(disk.name): \(disk.kindName), \(formatBytes(disk.size)), \(disk.bus.isEmpty ? "" : disk.bus + ", ")\(disk.volumesText)")
    }
}

/// Check 2 and Check 3: type the disk identifier, then ERASE. Cancel stays the default.
struct TypedCheckView: View {
    var check: TypedCheck
    @EnvironmentObject var model: AppModel
    @Environment(\.theme) private var theme
    @State private var typed = ""
    @FocusState private var focused: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack(spacing: 12) {
                LogoView(size: 40)
                Text(check.title).font(theme.display(20)).foregroundStyle(theme.text)
            }
            Text(check.message).font(theme.body(13)).foregroundStyle(theme.text).fixedSize(horizontal: false, vertical: true)
                .textSelection(.enabled)
            TextField(check.expected, text: $typed, prompt: Text(check.expected)).kcField().focused($focused)
                .accessibilityLabel("Type \(check.expected)")
            HStack(spacing: 8) {
                Spacer()
                Button("Cancel") { model.sheet = nil }.kcButton(.secondary).keyboardShortcut(.cancelAction)
                Button(check.confirm) {
                    let action = check.action
                    model.sheet = nil
                    DispatchQueue.main.async { action() }
                }
                .kcButton(.danger)
                .disabled(!check.matches(typed))
            }
        }
        .padding(24)
        .frame(width: 480)
        .background(theme.bg)
        .onAppear { focused = true }
    }
}
