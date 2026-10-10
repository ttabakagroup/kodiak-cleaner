import Foundation

/// The donation reminder (Ted, 2026-10-10; the same as Windows 0.8.55): a pop-up at startup asking for a donation on
/// Ko-fi, which can be turned off once Kodiak Cleaner has started 10 times, and a small banner on every 10th start.
/// Supporters see neither. A start counts only when the main window opens.
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

    public static let title = "Help Keep Kodiak Cleaner Free"
    public static let paragraphs = [
        "Kodiak Cleaner is free and open source, with no ads, no tracking and nothing held back behind a paywall. It's built in spare time by one person.",
        "If it has saved you time or space, please consider a small donation. Even the price of a coffee keeps it free for everyone and pays for the next round of features.",
        "Donations are made on Ko-fi and open in your browser. Supporters get a few extras as a thank-you, and this reminder turns off for good.",
    ]
    public static let bannerText = "Kodiak Cleaner is made for free in spare time. A donation, however small, really keeps the work going."

    /// The caption under the pop-up's Don't Show This Again at Startup switch.
    public static func promptCaption(launchCount: Int, isSupporter: Bool) -> String {
        if canOptOut(launchCount: launchCount, isSupporter: isSupporter) {
            return "Turn this on to stop the reminder at startup. You can turn it back on in Settings > General."
        }
        return "You can turn this reminder off after Kodiak Cleaner has started \(optOutAfter) times (\(launchesLeft(launchCount)) more to go)."
    }

    /// The caption under Settings > General > Ask Me to Donate at Startup.
    public static func settingsCaption(launchCount: Int, isSupporter: Bool) -> String {
        if isSupporter { return "Thank you for being a Supporter! The reminder is off for you." }
        if canOptOut(launchCount: launchCount, isSupporter: false) {
            return "At startup, a short note asks you to consider donating to keep Kodiak Cleaner free and open source."
        }
        return "At startup, a short note asks you to consider donating. It can be turned off after Kodiak Cleaner has started \(optOutAfter) times (\(launchesLeft(launchCount)) more to go)."
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
