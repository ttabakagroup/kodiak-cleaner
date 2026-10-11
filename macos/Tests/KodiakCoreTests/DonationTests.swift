import XCTest
@testable import KodiakCore

/// The donation reminder: start count, when it can be turned off, the pop-up, the banner on every 10th start, and
/// Supporters seeing neither.
final class DonationTests: TempHomeTestCase {
    func testDefaultsAndSaving() {
        let fresh = AppSettings()
        XCTAssertEqual(fresh.launchCount, 0)
        XCTAssertTrue(fresh.showDonationPrompt)
        let path = home + "/s.json"
        var settings = fresh
        settings.countLaunch()
        settings.countLaunch()
        settings.showDonationPrompt = false
        settings.save(to: path)
        let loaded = AppSettings.load(from: path)
        XCTAssertEqual(loaded.launchCount, 2)
        XCTAssertFalse(loaded.showDonationPrompt)
        // An older Settings.json without these keys gets the defaults; a damaged count never goes below zero.
        FileManager.default.createFile(atPath: path, contents: Data(#"{"scheme": "honey"}"#.utf8))
        XCTAssertEqual(AppSettings.load(from: path).launchCount, 0)
        XCTAssertTrue(AppSettings.load(from: path).showDonationPrompt)
        FileManager.default.createFile(atPath: path, contents: Data(#"{"launchCount": -4, "showDonationPrompt": "no"}"#.utf8))
        XCTAssertEqual(AppSettings.load(from: path).launchCount, 0)
        XCTAssertTrue(AppSettings.load(from: path).showDonationPrompt)
    }

    func testCanOptOutAfterTenStarts() {
        XCTAssertFalse(Donation.canOptOut(launchCount: 0, isSupporter: false))
        XCTAssertFalse(Donation.canOptOut(launchCount: 9, isSupporter: false))
        XCTAssertTrue(Donation.canOptOut(launchCount: 10, isSupporter: false))
        XCTAssertTrue(Donation.canOptOut(launchCount: 37, isSupporter: false))
        XCTAssertTrue(Donation.canOptOut(launchCount: 1, isSupporter: true))
        XCTAssertEqual(Donation.launchesLeft(0), 10)
        XCTAssertEqual(Donation.launchesLeft(7), 3)
        XCTAssertEqual(Donation.launchesLeft(10), 0)
        XCTAssertEqual(Donation.launchesLeft(25), 0)
    }

    func testTurningItOffEarlyKeepsItOn() {
        var settings = AppSettings()
        for _ in 0..<9 { settings.countLaunch() }
        XCTAssertTrue(settings.setDonationPrompt(false, isSupporter: false), "Can't be turned off before 10 starts")
        XCTAssertTrue(settings.showDonationPrompt)
        settings.countLaunch()
        XCTAssertFalse(settings.setDonationPrompt(false, isSupporter: false))
        XCTAssertFalse(settings.showDonationPrompt)
        XCTAssertTrue(settings.setDonationPrompt(true, isSupporter: false))
    }

    func testShouldPrompt() {
        XCTAssertTrue(Donation.shouldPrompt(showDonationPrompt: true, isSupporter: false))
        XCTAssertFalse(Donation.shouldPrompt(showDonationPrompt: false, isSupporter: false))
        XCTAssertFalse(Donation.shouldPrompt(showDonationPrompt: true, isSupporter: true))
    }

    func testBannerOnEveryTenthStart() {
        let shown = (0...40).filter { Donation.shouldShowBanner(launchCount: $0, isSupporter: false) }
        XCTAssertEqual(shown, [10, 20, 30, 40])
        XCTAssertFalse(Donation.shouldShowBanner(launchCount: 20, isSupporter: true))
    }

    func testSupporterTurnsItOff() {
        var settings = AppSettings()
        settings.countLaunch()
        settings.applyDonationRules(isSupporter: true)
        XCTAssertFalse(settings.showDonationPrompt)
        XCTAssertFalse(Donation.shouldPrompt(showDonationPrompt: settings.showDonationPrompt, isSupporter: true))
        // Without a code again, before 10 starts it comes back on; after that it stays as it was set.
        settings.applyDonationRules(isSupporter: false)
        XCTAssertTrue(settings.showDonationPrompt)
        for _ in 0..<10 { settings.countLaunch() }
        settings.setDonationPrompt(false, isSupporter: false)
        settings.applyDonationRules(isSupporter: false)
        XCTAssertFalse(settings.showDonationPrompt)
    }

    func testResetStartsTheCountAgain() {
        var settings = AppSettings()
        for _ in 0..<12 { settings.countLaunch() }
        settings.setDonationPrompt(false, isSupporter: false)
        settings.supporterCode = "abc"
        let reset = settings.reset()
        XCTAssertEqual(reset.launchCount, 0)
        XCTAssertEqual(reset.supporterCode, "abc")
        XCTAssertTrue(reset.showDonationPrompt)
    }

    func testCaptions() {
        XCTAssertEqual(Donation.promptCaption(launchCount: 3, isSupporter: false),
                       "You can turn this reminder off after Kodiak Cleaner has started 10 times (7 more to go).")
        XCTAssertEqual(Donation.promptCaption(launchCount: 10, isSupporter: false),
                       "Turn this on to stop the reminder at startup. You can turn it back on in Settings > General.")
        XCTAssertEqual(Donation.settingsCaption(launchCount: 9, isSupporter: false),
                       "At startup, a short note asks you to consider donating. It can be turned off after Kodiak Cleaner has started 10 times (1 more to go).")
        XCTAssertEqual(Donation.settingsCaption(launchCount: 11, isSupporter: false),
                       "At startup, a short note asks you to consider donating to keep Kodiak Cleaner free and open source.")
        XCTAssertEqual(Donation.settingsCaption(launchCount: 2, isSupporter: true),
                       "Thank you for being a Supporter! The reminder is off for you.")
        XCTAssertEqual(AppInfo.kofiURL.absoluteString, "https://ko-fi.com/ttabaka")
    }

    /// Ted, 0.8.4: each start counts once, starting at 1, and the first start already asks.
    func testFirstStartCountsOneAndAsks() throws {
        var settings = AppSettings()
        settings.countLaunch()
        XCTAssertEqual(settings.launchCount, 1)
        XCTAssertTrue(Donation.shouldPrompt(showDonationPrompt: settings.showDonationPrompt, isSupporter: false))
        XCTAssertEqual(Donation.launchesLeft(settings.launchCount), 9)
        // Starts are counted in exactly one place in the app.
        let app = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent()
            .deletingLastPathComponent().appendingPathComponent("Sources/KodiakCleaner")
        var calls = 0
        for name in try FileManager.default.contentsOfDirectory(atPath: app.path) where name.hasSuffix(".swift") {
            let text = try String(contentsOf: app.appendingPathComponent(name), encoding: .utf8)
            calls += text.components(separatedBy: ".countLaunch()").count - 1
        }
        XCTAssertEqual(calls, 1)
    }
}
