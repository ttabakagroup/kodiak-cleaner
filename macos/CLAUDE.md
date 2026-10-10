# Kodiak Cleaner for Mac: Project Rules

Kodiak Cleaner for Mac is Ted Tabaka's Mac cleaner and the Mac edition of Kodiak Cleaner (Windows: `/mnt/project-files/ccleaner-alt/`,
Linux: `/mnt/project-files/kodiak-linux/`; leave those alone from here). Swift with SwiftUI, a Swift package with no Xcode project:
logic in `Sources/KodiakCore` (Foundation only, unit tested), the app in `Sources/KodiakCleaner`. It follows Ted's app standard
(the appbuilder skill): privacy first, local first, security paramount, convenient, practical and useful to everyone.

## Why This Stack
SwiftUI is the Mac's own toolkit, so Kodiak Cleaner looks and behaves like a Mac app, starts fast and needs nothing installed.
A plain Swift package (no .xcodeproj) builds the same way from Terminal and on GitHub's Macs. Don't switch toolkits without Ted.

## Target
- macOS 26 Tahoe and newer (`LSMinimumSystemVersion` 26.0, `.macOS(.v26)`), built for macOS 27 Golden Gate (the newest, released
  2026-09-14). Universal binary (Apple silicon and Intel). Raise the minimum only when Ted asks.
- Builds need Xcode 26 or newer. This cloud container can't build Swift for the Mac: push to the `macos-dev` branch of
  ttabakagroup/kodiakcleaner (folder `macos/`) and the workflow builds, tests, self-tests, screenshots and packages it on a
  GitHub Mac, then force-pushes the results (ZIP, DMG, screenshots, logs) to the `macos-dev-builds` branch.

## Privacy and Safety
- No telemetry, analytics, crash reporting, ads, upsells, accounts or background services. No network code at all
  (`tests/check_source.py` fails on URLSession and friends). Links open in the person's own browser.
- The one exception to "no nags" is the donation reminder (Ted, 2026-10-10; same as Windows 0.8.55): see Interface.
  Nothing else may ask for money or nag.
- No remote resources. Fredoka is bundled and registered for this process only; icons are SF Symbols or the apps' own icons.
- Settings: `~/Library/Application Support/Kodiak Cleaner/Settings.json`. Backups and Triggers History: `.../Backups`.
- The Cleaner only removes files inside the home folder and the person's own temporary folder (`Cleaner.allowed`), never
  follows links, skips sockets, pipes and devices, and leaves the folders the patterns point at in place. Every pattern starts
  with `~/` or `$TMPDIR` (a test checks). Nothing in the Cleaner needs the administrator password.
- Items macOS protects (Safari, Mail, Trash) are marked `needsFullDiskAccess`; without access they show "Needs Full Disk
  Access" instead of failing. Never ask for Full Disk Access by any trick; point to System Settings.
- Administrator work (launch daemons only) goes through `osascript ... with administrator privileges`, one prompt per batch,
  with fixed `launchctl` commands, a strict label pattern and a /Library/LaunchDaemons path pattern. Apple's own items
  (/System) are never changed.

## Cleaner
- Order (Ted's rule): `Cleaner.ordered` sorts macOS first, then programs A to Z, Advanced Cleaning last, items A to Z in every group
  (Default puts ticked items first, re-sorted only when the page opens, on Reset and when Default is picked). Add targets
  anywhere in `catalog()`; never hand-order them. Tests check it.
- Sort By: Default (first, selected out of the box), A to Z, Largest Found, Last Cleaned, Most Space Freed.
- Only programs found on the Mac are listed (`present`). Open programs are skipped while Skip Programs That Are Open is on
  (`processes` are bundle IDs).
- Advanced Cleaning: behind a warning each time it opens, never ticked by default, unticked when hidden. `warning` items
  confirm on tick and are skipped by Select All.
- Never delete bookmarks or anything a browser needs to start.

## Triggers
- Sources: ~/Library/LaunchAgents, /Library/LaunchAgents, /Library/LaunchDaemons, the user's crontab, shell startup files
  (yours and /etc), and Apple's /System/Library/Launch* for reference (hidden while Hide Protected Items is on).
- Off/on: launch agents with `launchctl disable|enable gui/<uid>/<label>` (all-users agents are turned off just for this
  person), daemons with `system/<label>` through the admin prompt, cron lines with the `#kodiak-off# ` marker (same as Linux).
  Remove: only the person's own launch agents and cron lines.
- Every change goes through `Triggers.record` with a backup copy first (`Triggers.backup`); History restores it. New change
  types must do the same.
- Notes: missing program, temporary/Downloads/hidden folder, downloads or unpacks code and runs it, changed in the last 7 days
  (all count toward Needs a Look), and "starts again whenever it stops" (information only).
- Hover details (Ted, 0.8.3; same as Windows 0.8.63): each row's `.help` is `DetailText.lines(item.detailFields)`, one
  "Label: value" line per field with empty ones skipped (`Sources/KodiakCore/DetailText.swift`, tested). New Trigger fields go
  into `detailFields` (an extension at the end of Triggers.swift, outside the groupNames block the Title Case check reads).

## Interface
- Colors only in `Sources/KodiakCore/Themes.swift` (the standard's exact tokens, same tables as Linux) and turned into SwiftUI
  colors in `Sources/KodiakCleaner/Theme.swift`. Pages use theme names only; `tests/check_source.py` fails on any other color.
- Accents in the standard's order: scheme's own, macOS (follows the Mac's accent color; the Mac's equivalent of the Windows
  swatch), Blue, Teal, Green, Purple, Pink, Orange, Slate, shaded by `accentTokens` (port of accent.js, checked against the
  Linux values). Classic hides its duplicate Blue. `ThemeTests` checks WCAG 2.2 AA for every scheme, mode and accent.
- Classic and Glacier are Supporter themes (fall back to Honey without a code); Honey and all accents are free.
- Settings order (Ted, 0.8.3; same as Windows 0.8.63): on every Settings page the cards are A to Z by title and the switches
  inside each card are A to Z; a Reset card stays last. Supporter's Free and Supporter cards stay side by side as one comparison.
  Add new cards and switches in their A to Z place; `tests/check_source.py` checks it. Captions never point "above" or "below".
- Every setting is a switch (`SwitchRow`/`KCSwitchStyle`); checkmarks (`KCCheckboxStyle`) only for batch selections. One-of-several
  choices use tiles, `Segmented` or swatches. The focus ring is drawn in the theme's focus color (`FocusRing`).
- Title Case for page names, card titles, headings, labels, column headers, buttons, switches, options and chips; help text
  stays sentence case. `tests/check_source.py` checks it.
- Pop-ups go through `DialogRequest`/`DialogView` (themed); risky choices use the danger style and Cancel stays the default.
- Main sidebar: logo and two-tone wordmark (home), Cleaning heading (Cleaner, Triggers), then Settings and About. Settings has
  its own sidebar: General, Cleaning, Triggers, Appearance, Supporter, Contact & Feedback, Help (Changelog, Privacy Policy,
  Terms of Use), Developer last in development builds only.
- Settings footer: `Copyright © <year> TED TABAKA.` then `Free and open source under the MIT License.`
- Name: "Kodiak Cleaner for Mac" in full, "Kodiak Cleaner" in running text and as the app's name in Finder and the Dock; never
  with the channel. "Ted Tabaka" only. Plain hyphens. Never mention Ted's other apps.
- Logo: Ted-approved bear logo (`Resources/logo.svg`); the app icon puts it on a Honey cream rounded square for macOS's icon
  shape. Don't redesign it.
- Slogan (Ted's exact words): "Not a Crap Cleaner. A Cleaner App, Built Around What Matters."
- Dev and Beta builds show the Beta Software notice at each start until Don't Show This Again at Startup is on. What's New opens
  after an update.
- Donation reminder (Ted, 2026-10-10, the only nag allowed; logic in `Sources/KodiakCore/Donation.swift`, tested): `launchCount`
  goes up each time the main window opens (not `--screenshots` or `--self-test`). The startup pop-up (`SheetKind.donation`,
  `DonationView`) shows while `showDonationPrompt` is on, never for Supporters, never with What's New, and after the
  Beta notice closes when that shows (never stacked). Its Don't Show This Again switch and Settings > General > Ask Me to Donate at Startup stay greyed
  until 10 starts. A banner (`DonationBanner`) shows on every 10th start. Supporters see neither, and a code turns the reminder
  off. Reset All Settings starts `launchCount` again (Ted, 0.8.2); Supporters stay off. Keep the wording identical to Windows and Linux. Getting Started's features list (`features` in HomeAbout.swift) stays A to Z and current.

## Feedback and Support
- Contact & Feedback: four accent buttons (Send Feedback, Report a Bug, Request a Feature, Security) open the website's
  contact form in the browser (`Feedback.link`): https://www.kodiakcleaner.app/contact/?topic=<General Feedback | Bug Report |
  Feature Request | Security>&version=<"0.8.1 Dev (Mac)">, percent-encoded. Copy Details copies the Details, which never
  contain files, folders, user names or findings.
- Never put Ted's email address anywhere (app, docs, tests, packaging, store text). All contact goes through the website's
  contact form (Ted, 2026-10-10). Security reports use the Security topic, never public issues.
- Supporter codes: the same ECDSA P-256 codes as Windows and Linux, verified offline with CryptoKit. Put Ted's
  `supporter-public.pem` in `Resources/keys/` once he has made it. Never handle his private key.

## Versions and Channels
- `version.json` is the single source; build.sh writes it into Info.plist. Dev 0.8.x, Beta 0.9.x, Stable 1.x. Never change
  channel unless Ted says so. `AppInfo.versionName` ("0.8.1 Dev") wherever a version shows or is sent.
- `./build.sh package --release` refuses the test key and a version with no CHANGELOG entry, and turns off the Developer page.

## Commit Routine
After each completed, tested change:
1. Push to `macos-dev` and check the workflow run: tests, self-test and screenshots (look at them) must pass.
2. Bump the patch number in `version.json` (+0.0.1) unless Ted gave a channel or minor instruction.
3. Add the entry to `CHANGELOG.md` under `## [version_name] - YYYY-MM-DD`, grouped by area.
4. Update README.md and this file if behavior or rules changed.
5. Commit as `[version_name]: short summary` with a body listing changes by area.
6. Every release ships `KodiakCleaner-<Branch>-Mac.dmg` and `KodiakCleaner-<Branch>-Mac.zip` (fixed names; the version lives
   in the release tag), each with the `Docs/` folder (seven plain-text files from `tools/make_docs.py`). The Windows project
   folder's `Application\Mac` holds both.
Never publish a release unless Ted asks.

## Signing
Dev builds are ad-hoc signed (Apple silicon needs a signature to run), not notarized, so Gatekeeper asks the person to use
Open Anyway in Privacy & Security the first time. A Developer ID certificate and notarization (Ted's Apple Developer account,
US$99 a year) remove that; only Ted can set that up.
