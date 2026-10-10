# Kodiak Cleaner for Mac

Not a Crap Cleaner. A Cleaner App, Built Around What Matters.

Kodiak Cleaner for Mac is the Mac edition of Kodiak Cleaner by Ted Tabaka: a small, honest cleaner for caches, logs, temporary files and browser data, plus Triggers, one list of everything that starts by itself on your Mac. No telemetry, no ads, no upsells, no background services. Free and open source under the MIT License.

**Status:** 0.8.2 Dev, beta software. Use at your own risk and keep your own backups.

## Contents

- [What It Does](#what-it-does)
- [Download and Install](#download-and-install)
- [Privacy and Safety](#privacy-and-safety)
- [Look and Feel](#look-and-feel)
- [Supporter](#supporter)
- [Building](#building)
- [Files](#files)

## What It Does

### Cleaner
- **macOS:** crash reports, logs, temporary files, Quick Look thumbnails, saved window state, shell history and the Trash.
- **Browsers:** cache, cookies, history and sessions for Arc, Brave, Chromium, Firefox, Floorp, Google Chrome, Helium, LibreWolf, Microsoft Edge, Opera, Safari, Vivaldi and Zen, and Thunderbird's cache. Bookmarks always stay.
- **Apps and developer tools:** Mail downloads, CocoaPods, Discord, Go, Homebrew, npm, pip, Slack, Spotify, Visual Studio Code, Xcode and Yarn.
- **Advanced Cleaning:** the entire cache folder, Xcode archives and device support files, behind a warning and never ticked for you.
- Only apps found on the Mac are listed, and apps that are open are skipped. Analyze shows what is there before Clean removes anything.
- Sort By: Default (macOS first, apps A to Z, Advanced Cleaning last, ticked items first), A to Z, Largest Found, Last Cleaned, Most Space Freed.

### Triggers
- Launch agents (yours and everyone's), launch daemons, cron jobs and shell startup files, with Apple's own items available for reference.
- Notes flag missing programs, programs in temporary, Downloads or hidden folders, code that is downloaded and run, recent changes and items that restart themselves. Needs a Look shows just those.
- Turn launch agents, launch daemons and cron jobs off or on, and remove your own. Every change is backed up first and can be restored from Triggers History.

## Download and Install

The Dev build comes as **KodiakCleaner-Dev-Mac.dmg** (disk image) and **KodiakCleaner-Dev-Mac.zip**, each with a Docs folder. Kodiak Cleaner needs macOS 26 Tahoe or macOS 27 Golden Gate.

The Dev build isn't signed with an Apple Developer ID or notarized yet, so the first time it opens macOS says it couldn't verify it. Open System Settings, Privacy & Security, click **Open Anyway** next to the message about Kodiak Cleaner and enter your password. Docs/ReadMe.txt walks through it.

## Privacy and Safety

- Nothing leaves the Mac. Kodiak Cleaner has no network code; links open in your own browser.
- Contact & Feedback opens the contact form on the website, https://www.kodiakcleaner.app/contact/, with the topic and version filled in.
- Files are only removed inside your home folder and your own temporary folder, and links are removed, never followed.
- Cleaning never needs your administrator password. Launch daemon changes ask for it through macOS's own prompt, once per batch.
- Safari, Mail and the Trash need Full Disk Access, which you grant (and can take back) in System Settings.
- Settings and Triggers History live in ~/Library/Application Support/Kodiak Cleaner.

## Look and Feel

Honey (default, free), Classic and Glacier (Supporter themes), each in light and dark or matching the Mac. Accent colors for everyone: the scheme's own, macOS (follows the Mac's accent color), Blue, Teal, Green, Purple, Pink, Orange and Slate. Every combination meets WCAG 2.2 AA contrast, which the tests check.

## Supporter

Supporting on [Ko-fi](https://ko-fi.com/ttabaka) is optional. A supporter code (the same one works in the Windows and Linux editions) unlocks Classic, Glacier and a badge, checked offline on the Mac.

Kodiak Cleaner asks for a donation in a short pop-up at startup, and shows a small banner on every 10th start. The pop-up can be turned off (in the pop-up itself or in Settings > General) once Kodiak Cleaner has started 10 times. Supporters see neither.

## Building

Needs a Mac with Xcode 26 or newer. The GitHub workflow (`.github/workflows/macos.yml`) does all of this on a GitHub Mac.

```sh
./build.sh test          # source checks and unit tests
./build.sh package       # dist/Kodiak Cleaner.app, KodiakCleaner-Dev-Mac.zip, KodiakCleaner-Dev-Mac.dmg, dist/Docs
./build.sh self-test     # checks the built app on this Mac without changing anything
./build.sh screenshots   # every page in every scheme, light and dark, in dist/screenshots
```

## Files

| Path | What it is |
|---|---|
| `Package.swift` | Swift package: KodiakCore (logic), KodiakCleaner (the app), KodiakCoreTests |
| `Sources/KodiakCore/` | Cleaner catalog and cleaning, Triggers, themes and accents, settings, supporter codes, changelog, feedback |
| `Sources/KodiakCleaner/` | The SwiftUI app: pages, components, theme, dialogs, self-test and screenshots |
| `Tests/KodiakCoreTests/` | Unit tests (cleaning, Triggers, contrast, settings, supporter codes) |
| `tests/check_source.py` | Title Case, colors only in the theme files, no network code, docs present |
| `Resources/` | Bundled into the app: logo, Fredoka font, supporter keys |
| `packaging/` | Info.plist, app icon and the ReadMe for the Docs folder |
| `tools/make_docs.py` | Writes the seven plain-text Docs files |
| `build.sh` | Test, build, package, self-test and screenshots |
| `CHANGELOG.md`, `PRIVACY.md`, `terms-of-use.md`, `LICENSE`, `SECURITY.md` | Shown in the app and copied into Docs |
| `CLAUDE.md` | Project rules for future work |
| `version.json` | Version and channel |

Copyright © 2026 Ted Tabaka. Free and open source under the MIT License.
