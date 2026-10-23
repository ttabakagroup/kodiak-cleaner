# Kodiak Cleaner for Mac

Not a Crap Cleaner. A Cleaner App, Built Around What Matters.

Kodiak Cleaner for Mac is a small, honest cleaner by Ted Tabaka. It removes caches, temporary files, logs and browser data, and lists everything that starts by itself on your Mac (Triggers) so you can turn it off or remove it. It has no telemetry, ads, upsells or background services, and it is free and open source under the MIT License.

Beta software: use at your own risk. Kodiak Cleaner deletes things by design, so analyze before you clean, read each caution, and keep your own backups of anything important.

## What You Need

- macOS 26 Tahoe or macOS 27 Golden Gate
- A Mac with Apple silicon, or an Intel Mac that runs macOS 26
- Your administrator password only to turn launch daemons off or on (Kodiak Cleaner asks once per batch)

## Install

- Disk image: open KodiakCleaner-Dev-Mac.dmg and drag Kodiak Cleaner to the Applications folder.
- ZIP: open KodiakCleaner-Dev-Mac.zip and drag Kodiak Cleaner from the new folder to the Applications folder.

## The First Time You Open It

This test build isn't signed with an Apple Developer ID yet, so macOS stops it the first time with a message such as "Apple could not verify Kodiak Cleaner is free of malware". To open it anyway:

- Double-click Kodiak Cleaner in Applications, and click Done (or OK) when macOS stops it.
- Open System Settings, then Privacy & Security, and scroll down to Security.
- Next to the message about Kodiak Cleaner, click Open Anyway, then enter your password.
- Click Open Anyway once more. From then on Kodiak Cleaner opens normally.

If Open Anyway doesn't show up, open Terminal and run: xattr -dr com.apple.quarantine "/Applications/Kodiak Cleaner.app"

## Full Disk Access

macOS keeps Safari, Mail and the Trash private. To clean them, open System Settings, then Privacy & Security, then Full Disk Access, and turn on Kodiak Cleaner. Then quit and open Kodiak Cleaner again. Everything else works without it.

## Update Without Losing Anything

Your settings and Triggers History are in ~/Library/Application Support/Kodiak Cleaner. An update never touches them.

- Quit Kodiak Cleaner, then drag the new Kodiak Cleaner to Applications and choose Replace.

After an update, What's New shows everything that changed since your previous version. The full history is in Changelog.txt.

## Uninstall

- Drag Kodiak Cleaner from Applications to the Trash.
- To remove your settings and history too, delete ~/Library/Application Support/Kodiak Cleaner.

## Privacy

Everything stays on your Mac, and Kodiak Cleaner sends nothing on its own. It never goes online by itself. Privacy.txt has the details.

## These Documents

- Changelog.txt: every change, newest first
- Download.url: the newest download on GitHub
- Homepage.url: the Kodiak Cleaner website
- License.txt: the MIT License
- Privacy.txt: the privacy policy
- ReadMe.txt: this file
- TermsOfUse.txt: the Terms of Use

## Help and Feedback

- Feedback, bug reports and feature requests: Settings > Contact & Feedback, or the contact form at https://www.kodiakcleaner.app/contact/
- Website: https://www.kodiakcleaner.app/
- Source code and releases: https://github.com/ttabakagroup/kodiakcleaner
- Say thanks: https://ko-fi.com/s/aabd61b988 (Kodiak Cleaner asks at startup; you can turn that off in Settings > General once it has started 10 times, and Supporters are never asked)

Copyright © 2026 Ted Tabaka. Free and open source under the MIT License.
