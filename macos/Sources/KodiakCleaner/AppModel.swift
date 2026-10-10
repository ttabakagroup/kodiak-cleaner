import AppKit
import KodiakCore
import SwiftUI

enum Page: String, CaseIterable { case home, cleaner, triggers, settings, about }

enum SettingsPage: String, CaseIterable {
    case general, cleaning, triggers, appearance, supporter, feedback, changelog, privacy, terms, developer
}

/// A themed pop-up. Risky choices use the danger style, and Cancel stays the default.
struct DialogRequest: Identifiable {
    let id = UUID()
    var title: String
    var message: String
    var confirm: String = "OK"
    var cancel: String? = "Cancel"
    var danger = false
    var action: (() -> Void)? = nil
}

enum SheetKind: Identifiable, Equatable {
    case beta, whatsNew(previous: String), changelog, triggersHistory, donation
    var id: String {
        switch self {
        case .beta: return "beta"
        case .donation: return "donation"
        case .whatsNew(let previous): return "whatsnew-\(previous)"
        case .changelog: return "changelog"
        case .triggersHistory: return "history"
        }
    }
}

/// Everything the pages share. Settings save on every change and apply at once in every open window.
@MainActor
final class AppModel: ObservableObject {
    static let shared = AppModel()

    @Published var settings: AppSettings {
        didSet {
            // A Supporter (a code just entered, or the Developer page's preview) never gets the donation reminder.
            if settings.showDonationPrompt && plan.isSupporter { settings.showDonationPrompt = false }
            if settings != oldValue { settings.save() }
        }
    }
    @Published var page: Page = .home
    @Published var settingsPage: SettingsPage = .general
    @Published var dialog: DialogRequest?
    @Published var sheet: SheetKind?
    @Published var systemAccent: String? = Theme.systemAccentHex()
    /// The donation banner at the top of the page, on every 10th start until Dismiss.
    @Published var showDonationBanner = false
    /// Fixes the look for screenshots (nil follows Light or Dark).
    @Published var forcedDark: Bool?

    // Cleaner
    @Published var targets: [CleanTarget] = []
    @Published var findings: [String: Finding] = [:]
    @Published var results: [String: Cleaner.Result] = [:]
    @Published var working: String?
    @Published var showAdvanced = false
    @Published var cleanerSearch = ""
    @Published var summary = ""

    // Triggers
    @Published var triggers: [Trigger] = []
    @Published var triggerSelection: Set<String> = []
    @Published var triggerFilter = "all"
    @Published var triggerSearch = ""
    @Published var triggersLoaded = false
    @Published var history: [Triggers.HistoryEntry] = []

    init() {
        settings = AppSettings.load()
        targets = Cleaner.available()
        NotificationCenter.default.addObserver(forName: NSColor.systemColorsDidChangeNotification, object: nil, queue: .main) { _ in
            Task { @MainActor in AppModel.shared.systemAccent = Theme.systemAccentHex() }
        }
    }

    var plan: Supporter.Plan { Supporter.Plan(settings: settings) }
    /// The scheme in use: Classic and Glacier fall back to Honey without a supporter code.
    var scheme: String { plan.scheme(settings.scheme) }

    func theme(dark: Bool) -> Theme {
        Theme(scheme: scheme, dark: forcedDark ?? dark, accent: settings.accent, systemAccent: systemAccent)
    }

    func applyAppearance() {
        switch settings.mode {
        case "light": NSApp.appearance = NSAppearance(named: .aqua)
        case "dark": NSApp.appearance = NSAppearance(named: .darkAqua)
        default: NSApp.appearance = nil
        }
    }

    func showSettings(_ page: SettingsPage) {
        settingsPage = page
        self.page = .settings
    }

    func ask(_ request: DialogRequest) { dialog = request }

    func tell(_ title: String, _ message: String) {
        dialog = DialogRequest(title: title, message: message, confirm: "OK", cancel: nil)
    }

    // MARK: Startup

    /// `--screenshots` opens the window too, but isn't a start the person made: it isn't counted and shows no reminder.
    static var isAutomatedRun: Bool {
        CommandLine.arguments.contains("--screenshots") || CommandLine.arguments.contains("--self-test")
    }

    /// The Beta Software notice (Dev and Beta builds), then What's New after an update. On a start with neither, the
    /// donation reminder (never stacked on another pop-up). Every 10th start also shows the donation banner.
    func startup() {
        applyAppearance()
        guard !startedUp else { return }
        startedUp = true
        let previous = settings.lastVersionRun
        settings.lastVersionRun = AppInfo.version
        let counted = !AppModel.isAutomatedRun
        let isSupporter = plan.isSupporter
        if counted {
            settings.countLaunch()
            settings.applyDonationRules(isSupporter: isSupporter)
            showDonationBanner = Donation.shouldShowBanner(launchCount: settings.launchCount, isSupporter: isSupporter)
        }
        if AppInfo.channel != "stable" && settings.showBetaNotice {
            sheet = .beta
            pendingWhatsNew = previous
            // The donation pop-up waits until the Beta Software notice closes, as on Windows (never stacked).
            pendingDonation = counted && Donation.shouldPrompt(showDonationPrompt: settings.showDonationPrompt, isSupporter: isSupporter)
        } else if !showWhatsNewIfUpdated(previous) && counted
                    && Donation.shouldPrompt(showDonationPrompt: settings.showDonationPrompt, isSupporter: isSupporter) {
            sheet = .donation
        }
    }

    private var startedUp = false
    private var pendingWhatsNew: String?
    private var pendingDonation = false

    var canOptOutOfDonation: Bool {
        Donation.canOptOut(launchCount: settings.launchCount, isSupporter: plan.isSupporter)
    }

    /// However the donation pop-up closes: Don't Show This Again at Startup turns the reminder off once that's allowed.
    func donationPromptClosed(dontShowAgain: Bool) {
        guard dontShowAgain else { return }
        let isSupporter = plan.isSupporter
        settings.setDonationPrompt(false, isSupporter: isSupporter)
    }

    func betaNoticeClosed() {
        sheet = nil
        let previous = pendingWhatsNew ?? ""
        let donation = pendingDonation
        pendingWhatsNew = nil
        pendingDonation = false
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.3) {
            // What's New first; the donation pop-up only when nothing else shows.
            if !self.showWhatsNewIfUpdated(previous) && donation { self.sheet = .donation }
        }
    }

    /// Returns true when What's New opened.
    @discardableResult
    private func showWhatsNewIfUpdated(_ previous: String) -> Bool {
        if !previous.isEmpty && settings.showWhatsNew && AppInfo.isNewer(AppInfo.version, than: previous) {
            sheet = .whatsNew(previous: previous)
            return true
        }
        return false
    }

    // MARK: Cleaner

    var selected: Set<String> {
        Set(settings.selected ?? Cleaner.catalog().filter(\.enabledByDefault).map(\.id))
    }

    func setSelected(_ id: String, _ on: Bool) {
        var set = selected
        if on { set.insert(id) } else { set.remove(id) }
        settings.selected = set.sorted()
    }

    func tick(_ target: CleanTarget, _ on: Bool) {
        if on && !target.warning.isEmpty {
            ask(DialogRequest(title: "\(target.name)?", message: target.warning, confirm: "Tick It", danger: true) {
                self.setSelected(target.id, true)
            })
        } else {
            setSelected(target.id, on)
        }
    }

    var visibleTargets: [CleanTarget] {
        let query = cleanerSearch.trimmingCharacters(in: .whitespaces).lowercased()
        let shown = targets.filter { (showAdvanced || !$0.advanced) &&
            (query.isEmpty || "\($0.group) \($0.name) \($0.description)".lowercased().contains(query)) }
        return Cleaner.ordered(shown, mode: settings.sortBy, selected: sortSnapshot ?? selected,
                               history: settings.cleanerHistory, sizes: findings.mapValues(\.size))
    }

    /// Default sorts ticked items first, but only when the list opens, on Reset and when Default is picked, so rows
    /// don't jump while you tick.
    @Published var sortSnapshot: Set<String>?

    func resortTicked() { sortSnapshot = selected }

    func selectAll(_ on: Bool) {
        var set = selected
        for target in visibleTargets {
            if on && !target.warning.isEmpty { continue }
            if on { set.insert(target.id) } else { set.remove(target.id) }
        }
        settings.selected = set.sorted()
    }

    func resetToDefault() {
        ask(DialogRequest(title: "Reset to Default?", message: "Ticks the basic items Kodiak Cleaner starts with and unticks the rest.",
                          confirm: "Reset") {
            self.settings.selected = Cleaner.catalog().filter(\.enabledByDefault).map(\.id).sorted()
            self.resortTicked()
        })
    }

    func toggleAdvanced() {
        if showAdvanced {
            showAdvanced = false
            // Advanced items never stay ticked while hidden.
            settings.selected = selected.filter { id in !targets.contains { $0.id == id && $0.advanced } }.sorted()
            return
        }
        ask(DialogRequest(title: "Show Advanced Cleaning?",
                          message: "These items free more space but can sign you out, slow apps down once or remove things you may want later. Nothing is ticked for you; read each description before you tick it.",
                          confirm: "Show Them", danger: true) {
            self.showAdvanced = true
        })
    }

    static func runningPrograms() -> Set<String> {
        var names = Set<String>()
        for app in NSWorkspace.shared.runningApplications {
            if let id = app.bundleIdentifier { names.insert(id.lowercased()) }
            if let name = app.localizedName { names.insert(name.lowercased()) }
            if let exe = app.executableURL?.lastPathComponent { names.insert(exe.lowercased()) }
        }
        return names
    }

    private func chosenTargets() -> [CleanTarget] {
        let set = selected
        return targets.filter { set.contains($0.id) && (showAdvanced || !$0.advanced) }
    }

    func analyze(then: (() -> Void)? = nil) {
        let chosen = chosenTargets()
        if chosen.isEmpty { tell("Nothing Ticked", "Tick at least one item first."); return }
        working = "Analyzing…"
        results = [:]
        let keepRecent = settings.keepRecentTempFiles
        let running = settings.skipOpenPrograms ? AppModel.runningPrograms() : nil
        Task.detached(priority: .userInitiated) {
            var found: [String: Finding] = [:]
            for target in chosen {
                await MainActor.run { AppModel.shared.working = "Analyzing \(target.group): \(target.name)…" }
                found[target.id] = Cleaner.find(target, keepRecent: keepRecent, running: running)
            }
            await MainActor.run {
                let model = AppModel.shared
                model.findings = found
                model.working = nil
                let total = found.values.reduce(Int64(0)) { $0 + $1.size }
                let files = found.values.reduce(0) { $0 + $1.count }
                model.summary = "Found \(formatBytes(total)) in \(files) file\(files == 1 ? "" : "s")."
                then?()
            }
        }
    }

    func cleanClicked() {
        let chosen = chosenTargets()
        if chosen.isEmpty { tell("Nothing Ticked", "Tick at least one item first."); return }
        let go = {
            if self.settings.confirmBeforeCleaning {
                let ready = chosen.compactMap { self.findings[$0.id] }.filter { $0.skipped.isEmpty }
                let total = ready.reduce(Int64(0)) { $0 + $1.size }
                let files = ready.reduce(0) { $0 + $1.count }
                self.ask(DialogRequest(title: "Clean Now?",
                                       message: "Kodiak Cleaner will remove \(files) file\(files == 1 ? "" : "s") (\(formatBytes(total))) from \(ready.count) item\(ready.count == 1 ? "" : "s"). Files it removes can't be brought back.",
                                       confirm: "Clean", danger: true) { self.clean(chosen) })
            } else {
                self.clean(chosen)
            }
        }
        if chosen.allSatisfy({ findings[$0.id] != nil }) { go() } else { analyze(then: go) }
    }

    private func clean(_ chosen: [CleanTarget]) {
        let findings = chosen.compactMap { self.findings[$0.id] }
        // Programs may have been opened since Analyze: check again right before cleaning.
        let running = settings.skipOpenPrograms ? AppModel.runningPrograms() : nil
        working = "Cleaning…"
        Task.detached(priority: .userInitiated) {
            let results = Cleaner.clean(findings, running: running) { target in
                Task { @MainActor in AppModel.shared.working = "Cleaning \(target.group): \(target.name)…" }
            }
            await MainActor.run {
                let model = AppModel.shared
                model.results = results
                model.working = nil
                let now = Date().timeIntervalSince1970
                for (id, result) in results where result.message.isEmpty {
                    let before = model.settings.cleanerHistory[id]?.freed ?? 0
                    model.settings.cleanerHistory[id] = Cleaner.History(lastCleaned: now, freed: before + result.freed)
                }
                let freed = results.values.reduce(Int64(0)) { $0 + $1.freed }
                model.summary = "Freed \(formatBytes(freed))."
                model.findings = [:]
            }
        }
    }

    // MARK: Triggers

    func loadTriggers() {
        working = "Reading Triggers…"
        let includeSystem = settings.showSystemTriggers
        let includeApple = !settings.hideProtected
        Task.detached(priority: .userInitiated) {
            let items = Triggers.list(includeSystem: includeSystem, includeApple: includeApple)
            let history = Triggers.history()
            await MainActor.run {
                let model = AppModel.shared
                model.triggers = items
                model.history = history
                model.triggerSelection = model.triggerSelection.filter { id in items.contains { $0.id == id } }
                model.triggersLoaded = true
                model.working = nil
            }
        }
    }

    var visibleTriggers: [Trigger] {
        let query = triggerSearch.trimmingCharacters(in: .whitespaces).lowercased()
        return triggers.filter { item in
            if settings.hideProtected && item.isProtected && !item.needsLook { return false }
            if !settings.showSystemTriggers && item.isSystem { return false }
            switch triggerFilter {
            case "look": if !item.needsLook { return false }
            case "off": if item.enabled != false { return false }
            default: break
            }
            return query.isEmpty || "\(item.name) \(item.label) \(item.program)".lowercased().contains(query)
        }
    }

    func changeTriggers(_ items: [Trigger], _ action: Triggers.Action) {
        let items = items.filter { action == .removed ? $0.canRemove : $0.canToggle && !$0.isProtected }
        if items.isEmpty {
            tell("Nothing to Change", action == .removed ? "Only your own launch agents and cron jobs can be removed."
                                                         : "None of the ticked entries can be turned on or off here.")
            return
        }
        let verb = action == .turnedOff ? "Turn Off" : (action == .turnedOn ? "Turn On" : "Remove")
        let names = items.prefix(5).map(\.name).joined(separator: ", ") + (items.count > 5 ? " and \(items.count - 5) more" : "")
        var message = "\(names).\n\nA backup copy goes into Triggers History first, so you can put it back."
        if items.contains(where: \.needsAdmin) { message += " Launch daemons ask for your administrator password once." }
        ask(DialogRequest(title: "\(verb) \(items.count) Entr\(items.count == 1 ? "y" : "ies")?", message: message,
                          confirm: verb, danger: action == .removed) {
            self.working = "Saving backups and making changes…"
            Task.detached(priority: .userInitiated) {
                let errors = Triggers.change(items, to: action)
                await MainActor.run {
                    let model = AppModel.shared
                    model.working = nil
                    model.triggerSelection = []
                    if !errors.isEmpty {
                        let lines = items.filter { errors[$0.id] != nil }.map { "\($0.name): \(errors[$0.id]!)" }
                        model.tell("Some Changes Didn't Happen", lines.joined(separator: "\n"))
                    }
                    model.loadTriggers()
                }
            }
        })
    }

    func restore(_ entry: Triggers.HistoryEntry) {
        Task.detached(priority: .userInitiated) {
            let error = Triggers.restore(entry)
            await MainActor.run {
                let model = AppModel.shared
                if let error { model.tell("Couldn't Restore", error) }
                model.loadTriggers()
            }
        }
    }

    // MARK: Files

    func reveal(_ path: String) {
        if FileManager.default.fileExists(atPath: path) {
            NSWorkspace.shared.activateFileViewerSelecting([URL(fileURLWithPath: path)])
        }
    }

    func openFolder(_ path: String) {
        try? FileManager.default.createDirectory(atPath: path, withIntermediateDirectories: true)
        NSWorkspace.shared.open(URL(fileURLWithPath: path))
    }

    static let fullDiskAccessURL = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_AllFiles")
    static let loginItemsURL = URL(string: "x-apple.systempreferences:com.apple.LoginItems-Settings.extension")

    /// macOS only lets Kodiak Cleaner into Safari, Mail and the Trash with Full Disk Access.
    var hasFullDiskAccess: Bool { !FS.isDenied(Paths.home + "/Library/Safari") }
}
