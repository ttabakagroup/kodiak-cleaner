# Kodiak Cleaner

Not a Crap Cleaner. A Cleaner App, Built Around What Matters.

Kodiak Cleaner is a small, honest cleaner for Windows by Ted Tabaka. It has no telemetry, ads, upsells, bundled offers or background services. Every cleaning tool is free, and the app is open source under the MIT License.

Beta software: use at your own risk. This is Kodiak Cleaner's Beta, the last step before the first full release. It deletes things by design, so check what's selected before you clean and keep your own backups of anything important.

## What You Need

- Windows 10 or Windows 11, 64-bit (x64)
- About 100 MB of free disk space
- Administrator rights only for system-wide items (Kodiak Cleaner asks when it needs them)

## Install

- Installer: double-click KodiakCleaner-Beta-Setup.msi and follow the steps. Kodiak Cleaner installs to Program Files\Kodiak Cleaner, with these documents in its Docs folder.
- Portable: extract the Portable ZIP to any folder, even a USB drive, and run KodiakCleaner.exe. It keeps its settings, backups, logs and reports in that folder and leaves no trace outside it.

The installer isn't code-signed yet, so Windows SmartScreen may warn you the first time. Click More info, then Run anyway.

## Update Without Losing Anything

Your settings are in KodiakCleaner.ini next to KodiakCleaner.exe. Backups, the activity log and exported reports are in Documents\Kodiak Cleaner (Backups, Logs and Reports folders); the portable version keeps those folders beside KodiakCleaner.exe. Settings > Application shows exactly where. To be safe, use Settings > Application > Back Up Settings before any update.

- Installed: click Check for Updates on the About page (or Check Now under Settings > Updates), or run a newer installer over the old one. KodiakCleaner.ini, Backups, Logs and Reports are kept.
- Portable: replace KodiakCleaner.exe and the Docs folder only. Keep KodiakCleaner.ini and the Backups, Logs and Reports folders.

After an update, What's New shows everything that changed since your previous version. Changelog.txt has every change since the first Beta, 0.9.0; About > All Changes Since the Start in Kodiak Cleaner also shows the earlier Dev builds.

## Uninstall

Uninstall Kodiak Cleaner from Windows Settings > Apps. It removes the program, its settings, its scheduled tasks and its other Windows entries, then asks whether to delete Documents\Kodiak Cleaner (your backups, logs and reports) too. Keep them if you might still want to undo a registry fix. For the portable version, turn off Clean When Windows Starts, the Recycle Bin menu and any schedules, then delete its folder.

## Privacy

Everything stays on your PC, and Kodiak Cleaner sends nothing on its own. The only time it goes online is the update check, which is off until you turn it on or click Check for Updates. Privacy.txt has the details.

## These Documents

- Changelog.txt: every change since the first Beta, newest first
- Download.url: where to get the newest installer
- Homepage.url: the Kodiak Cleaner website
- License.txt: the MIT License
- Privacy.txt: the privacy policy
- ReadMe.txt: this file
- TermsOfUse.txt: the Terms of Use

## Help and Feedback

- Feedback, bug reports and feature requests: Settings > Contact & Feedback, or the Contact Us form at https://www.kodiakcleaner.app/contact/
- Website: https://www.kodiakcleaner.app/
- Source code: https://www.kodiakcleaner.app/open-source/
- Say thanks: https://ko-fi.com/s/aabd61b988

Copyright © 2026 Ted Tabaka. Free and open source under the MIT License.
