import Foundation

/// Every setting, saved as Settings.json in ~/Library/Application Support/Kodiak Cleaner. A missing or unreadable
/// value falls back to its default, so an old or damaged file never stops Kodiak Cleaner from starting.
public struct AppSettings: Codable, Equatable {
    // General
    public var showBetaNotice = true
    public var showWhatsNew = true
    public var lastVersionRun = ""
    /// How many times the main window has opened (the donation reminder counts these). Reset keeps it.
    public var launchCount = 0
    /// The donation reminder at startup. It can be turned off after 10 starts; Supporters never see it.
    public var showDonationPrompt = true
    // Cleaning
    public var confirmBeforeCleaning = true
    public var skipOpenPrograms = true
    public var keepRecentTempFiles = true
    /// The ticked Cleaner items; nil until the person changes them (then the defaults apply).
    public var selected: [String]? = nil
    public var sortBy = "default"
    public var cleanerHistory: [String: Cleaner.History] = [:]
    // Triggers
    public var hideProtected = true
    public var showSystemTriggers = true
    // Appearance
    public var scheme = "honey"
    /// "system", "light" or "dark"
    public var mode = "system"
    public var accent = ""
    public var animations = true
    // Supporter
    public var supporterCode = ""
    public var showBadge = true
    // Developer (development builds only)
    public var preview = "free"
    public var previewName = ""

    public init() {}

    public init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        let d = AppSettings()
        func get<T: Decodable>(_ key: CodingKeys, _ fallback: T) -> T {
            (try? c.decodeIfPresent(T.self, forKey: key)) ?? fallback
        }
        showBetaNotice = get(.showBetaNotice, d.showBetaNotice)
        showWhatsNew = get(.showWhatsNew, d.showWhatsNew)
        lastVersionRun = get(.lastVersionRun, d.lastVersionRun)
        launchCount = max(0, get(.launchCount, d.launchCount))
        showDonationPrompt = get(.showDonationPrompt, d.showDonationPrompt)
        confirmBeforeCleaning = get(.confirmBeforeCleaning, d.confirmBeforeCleaning)
        skipOpenPrograms = get(.skipOpenPrograms, d.skipOpenPrograms)
        keepRecentTempFiles = get(.keepRecentTempFiles, d.keepRecentTempFiles)
        selected = (try? c.decodeIfPresent([String].self, forKey: .selected)) ?? nil
        sortBy = get(.sortBy, d.sortBy)
        cleanerHistory = get(.cleanerHistory, d.cleanerHistory)
        hideProtected = get(.hideProtected, d.hideProtected)
        showSystemTriggers = get(.showSystemTriggers, d.showSystemTriggers)
        scheme = get(.scheme, d.scheme)
        mode = get(.mode, d.mode)
        accent = get(.accent, d.accent)
        animations = get(.animations, d.animations)
        supporterCode = get(.supporterCode, d.supporterCode)
        showBadge = get(.showBadge, d.showBadge)
        preview = get(.preview, d.preview)
        previewName = get(.previewName, d.previewName)
        if !Themes.schemeOrder.contains(scheme) { scheme = "honey" }
        if !["system", "light", "dark"].contains(mode) { mode = "system" }
    }

    public static func load(from path: String = Paths.settingsFile) -> AppSettings {
        guard let data = FileManager.default.contents(atPath: path),
              let settings = try? JSONDecoder().decode(AppSettings.self, from: data) else { return AppSettings() }
        return settings
    }

    public func save(to path: String = Paths.settingsFile) {
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        guard let data = try? encoder.encode(self) else { return }
        try? FileManager.default.createDirectory(atPath: (path as NSString).deletingLastPathComponent,
                                                 withIntermediateDirectories: true)
        try? data.write(to: URL(fileURLWithPath: path), options: .atomic)
    }

    /// Reset All Settings: everything back to its default, keeping the supporter code and the cleaning history. The
    /// donation reminder's start count starts again (Ted, 0.8.2); AppModel turns the reminder off again for Supporters.
    public func reset() -> AppSettings {
        var fresh = AppSettings()
        fresh.supporterCode = supporterCode
        fresh.cleanerHistory = cleanerHistory
        fresh.lastVersionRun = lastVersionRun
        return fresh
    }
}
