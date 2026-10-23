# Kodiak Cleaner for Mac Changelog

## [0.8.7 Dev] - 2026-10-23

### Changed
- **Supporter:** the Support Kodiak Cleaner card now says that a one-time $10 or more unlocks the Supporter color schemes.

## [0.8.6 Dev] - 2026-10-23

### Changed
- **Supporter codes:** now checked with Ted's own key, so real codes from Ko-fi work.

## [0.8.5 Dev] - 2026-10-22

### Added
- **Lagoon color scheme (Supporters):** deep blue and clear water, in light and dark, next to Honey, Classic and Glacier in Settings > Appearance.

## [0.8.4 Dev] - 2026-10-11

### Changed
- **Donation reminder:** it now also shows after What's New closes, instead of being skipped on the first start after an update. Each start still counts once, starting at 1 on the very first start (a new test checks this).

## [0.8.3 Dev] - 2026-10-10

### Added
- **Triggers:** hover over an entry to see everything about it: its name, kind, label, program and full command, cron line, file, when it runs, who it runs for, whether it's on, what can be changed, when it last changed, and any warnings or notes.

### Changed
- **Settings:** the cards on each page are in A to Z order, and so are the switches inside each card. Reset stays last on General. Every switch still changes the same setting.

## [0.8.2 Dev] - 2026-10-10

### Changed
- **Donation reminder:** Reset All Settings now starts its count again, so the reminder is back on and can be turned off after 10 more starts. Supporters keep their code and still never see it.
- **Donation reminder:** on a start with the Beta Software notice, the pop-up now shows after the notice is closed instead of being skipped.

## [0.8.1 Dev] - 2026-10-10

### Added
- **Donation reminder:** at startup, a short pop-up asks you to consider a donation on Ko-fi (Donate on Ko-fi opens it in your browser; Not Now, the close button or Esc close it). Its Don't Show This Again at Startup switch, and Ask Me to Donate at Startup in Settings > General, can turn it off once Kodiak Cleaner has started 10 times; until then they are greyed out and say how many starts are left. It never shows on a start that already has the Beta Software notice or What's New.
- **Donation banner:** on every 10th start, a slim banner at the top of the page asks for a donation, with Donate on Ko-fi and Dismiss.
- **Supporter:** Supporters see neither; entering a supporter code turns the reminder off for good. Reset All Settings keeps the start count.

### Changed
- **Contact & Feedback:** Send Feedback, Report a Bug, Request a Feature and Security open the contact form on the Kodiak Cleaner website with the topic and version filled in, instead of an email. The app and its documents no longer show an email address.
- **Switches:** a switch that can't be changed yet is greyed out, its name included.

### Fixed
- **Cleaner:** programs opened between Analyze and Clean are skipped, and files that changed or became someone else's since Analyze stay. A pattern's own folder that is a link stays in place.
- **Triggers:** cron lines separated by tabs are read correctly, and a second click on Restore never adds a cron job twice. Changes and restores happen one at a time.
- **Reliability:** commands Kodiak Cleaner runs can no longer hang it by filling their output, and a command that runs too long is stopped.

## [0.8.0 Dev] - 2026-10-06

### Added
- **Kodiak Cleaner for Mac:** a native Mac app (SwiftUI) for macOS 26 Tahoe and macOS 27 Golden Gate, on Apple silicon and Intel Macs that run them.
- **Cleaner:** crash reports, logs, temporary files, Quick Look thumbnails, saved window state, shell history and the Trash; browser cache, cookies, history and sessions for Arc, Brave, Chromium, Firefox, Floorp, Google Chrome, Helium, LibreWolf, Microsoft Edge, Opera, Safari, Thunderbird (cache), Vivaldi and Zen; Mail downloads; CocoaPods, Discord, Go, Homebrew, npm, pip, Slack, Spotify, Visual Studio Code, Xcode and Yarn caches.
- **Advanced Cleaning:** the entire cache folder, Xcode archives and Xcode device support files, hidden behind a warning, never ticked for you, each with a description and a caution where it matters.
- **Sort By:** Default (macOS first, then programs A to Z, Advanced Cleaning last, ticked items first), A to Z, Largest Found, Last Cleaned and Most Space Freed, with a Last Cleaned column.
- **Full Disk Access:** Safari, Mail and the Trash are marked, and Kodiak Cleaner explains how to allow it in Privacy & Security. Everything else works without it.
- **Triggers:** one list of launch agents (yours and everyone's), launch daemons, cron jobs and shell startup files, with Apple's own items available for reference. Notes flag missing programs, programs in temporary, Downloads or hidden folders, code that is downloaded and run, recent changes and items that restart themselves.
- **Triggers changes:** turn launch agents, launch daemons and cron jobs off or on, and remove your own launch agents and cron jobs. Every change is backed up first and can be restored from Triggers History. Launch daemons ask for the administrator password once per batch, through macOS's own prompt.
- **Look and feel:** Honey (default), Classic and Glacier color schemes, light or dark or match device, accent colors (including one that follows the Mac's own accent color) and an Animations switch.
- **Settings, About, Contact & Feedback, Changelog, Privacy Policy and Terms of Use** pages, a Beta Software notice and What's New after updates.
- **Supporter:** offline supporter codes (the same codes as the Windows and Linux editions) unlock Classic, Glacier and the badge.
- **Downloads:** an app in a ZIP and a disk image (DMG), each with the Docs folder.
