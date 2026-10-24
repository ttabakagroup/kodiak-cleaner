import Foundation

/// The support reminder (Ted, 2026-10-10; the same as Windows 0.8.55; softer wording 0.8.13): a pop-up at startup inviting
/// the person to support Kodiak Cleaner on Ko-fi, which can be turned off once Kodiak Cleaner has started 10 times, and a small banner on every 10th start.
/// Supporters see neither. A start counts only when the main window opens. User-facing text never says "donate":
/// Supporter perks make it a purchase, not a gift (Ted, 2026-10-24).
public enum Donation {
    /// The reminder can be turned off after this many starts.
    public static let optOutAfter = 10
    /// The banner shows on every 10th start.
    public static let bannerEvery = 10

    public static func canOptOut(launchCount: Int, isSupporter: Bool) -> Bool {
        launchCount >= optOutAfter || isSupporter
    }

    public static func launchesLeft(_ launchCount: Int) -> Int { max(0, optOutAfter - launchCount) }

    public static func shouldPrompt(showDonationPrompt: Bool, isSupporter: Bool) -> Bool {
        showDonationPrompt && !isSupporter
    }

    /// Every 10th start, whatever the pop-up switch says.
    public static func shouldShowBanner(launchCount: Int, isSupporter: Bool) -> Bool {
        !isSupporter && launchCount > 0 && launchCount % bannerEvery == 0
    }

    // MARK: Wording (the same as Windows)

    public static let title = "Support Kodiak Cleaner"
    public static let paragraphs = [
        "Kodiak Cleaner is free and open source, with no ads, no tracking and nothing held back behind a paywall. It's built in spare time by one person.",
        "If it helps you, please consider supporting Kodiak Cleaner on Ko-fi. It helps pay for the next round of features. No pressure, and thank you for using it.",
        "Ko-fi opens in your browser. Supporters get a few extras as a thank-you, and this reminder turns off for good.",
    ]
    public static let bannerText = "Kodiak Cleaner is free. If it helps you, you can support it on Ko-fi."

    /// The caption under the pop-up's Don't Show This Again at Startup switch.
    public static func promptCaption(launchCount: Int, isSupporter: Bool) -> String {
        if canOptOut(launchCount: launchCount, isSupporter: isSupporter) {
            return "Turn this on to stop the reminder at startup. You can turn it back on in Settings > General."
        }
        return "You can turn this reminder off after Kodiak Cleaner has started \(optOutAfter) times (\(launchesLeft(launchCount)) more to go)."
    }

    /// The caption under Settings > General > Remind Me to Support Kodiak Cleaner at Startup.
    public static func settingsCaption(launchCount: Int, isSupporter: Bool) -> String {
        if isSupporter { return "Thank you for being a Supporter! The reminder is off for you." }
        if canOptOut(launchCount: launchCount, isSupporter: false) {
            return "At startup, a short note invites you to support Kodiak Cleaner on Ko-fi. Every feature stays free either way."
        }
        return "At startup, a short note invites you to support Kodiak Cleaner on Ko-fi. It can be turned off after Kodiak Cleaner has started \(optOutAfter) times (\(launchesLeft(launchCount)) more to go)."
    }
}

extension AppSettings {
    /// One more start (the main window opened).
    public mutating func countLaunch() {
        if launchCount < Int.max { launchCount += 1 }
    }

    /// At startup: Supporters have the reminder off; before 10 starts it can't be off.
    public mutating func applyDonationRules(isSupporter: Bool) {
        if isSupporter {
            showDonationPrompt = false
        } else if !Donation.canOptOut(launchCount: launchCount, isSupporter: false) {
            showDonationPrompt = true
        }
    }

    /// Turns the startup reminder on or off. Turning it off before it is allowed keeps it on. Returns the new value.
    @discardableResult
    public mutating func setDonationPrompt(_ on: Bool, isSupporter: Bool) -> Bool {
        if on || Donation.canOptOut(launchCount: launchCount, isSupporter: isSupporter) {
            showDonationPrompt = on
        }
        return showDonationPrompt
    }
}
