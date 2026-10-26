# Microsoft Store Listing

Text to paste into Partner Center > Kodiak Cleaner > Store listings (English, United States). Store rules for cleaners
(policy 10.2.3 and 11.x): no promises of a faster PC, no alarming numbers or warnings, no pressure to clean, and no names
of other products. Keep it that way when this changes.

## Product Name

Kodiak Cleaner

## Description

Beta: Kodiak Cleaner is in Beta. Please report anything that doesn't work through the contact page on kodiakcleaner.app.

Kodiak Cleaner is a small, honest cleaner for Windows. Clear out junk files, fix broken registry entries, choose what
starts with Windows and uninstall programs cleanly. It has no telemetry, no ads, no upsells and no background services.

How it works:
1. Analyze. Tick what you want to clean and click Analyze. Kodiak Cleaner shows how much each item would free. Nothing
   is deleted yet.
2. Review. Check the results and untick anything you'd rather keep.
3. Clean. Click Run Cleaner. Open browsers and files in use are skipped.

Safe by design:
- Every registry fix, startup removal and program entry change is backed up first, and you can restore it from the app.
- Most removals go to the Recycle Bin.
- Deeper cleaning items explain what they do and are never ticked for you.
- Settings and backups are plain files on your own PC. Nothing is kept in the cloud, and no account is needed.

Every feature and tool is free, and Kodiak Cleaner is open source under the MIT License.

## What's New in This Version

The first Microsoft Store release. The Store installs Kodiak Cleaner's updates for you.

## Product Features (one per line, up to 20)

- Cleaner for Windows, browser and app junk, with Analyze before anything is removed
- Registry checks with a backup before every fix
- Startup manager with history, so removed entries can be restored
- Programs: uninstall several at once and clean up leftovers
- Disk Analyzer: a map of what fills your drives
- Duplicate Finder: copies, look-alikes and similar names
- Large Files and Empty Folders finders
- Scheduled cleanings through Windows Task Scheduler
- System Restore points and Point-in-Time Restore snapshots
- Drive Wiper with three checks before anything is erased
- Driver Cleaner for old graphics and audio drivers
- Integrity Check for Windows, drives and memory
- Hash Checker, Mass Rename, Boot Timer, Scheduled Tasks and Browser Extensions
- Honey color scheme with accent colors, light and dark mode, and a Text Size setting
- Keyboard and screen reader support
- No telemetry, ads, upsells or background services

## Search Terms (up to 7)

cleaner, disk cleanup, junk files, privacy, startup manager, duplicate files, uninstaller

## Screenshots (1440 x 900, PNG)

Upload in this order, with these captions. The files are in the project's packaging/store/screenshots folder, taken
from the 0.9.0 Beta screens.

| File | Caption |
|---|---|
| 1-cleaner.png | Analyze first, then choose what to clean. |
| 2-cleaner-dark.png | Light or dark mode, with accent colors. |
| 3-registry.png | Registry checks, with a backup before every fix. |
| 4-startup.png | Choose what starts with Windows, and restore it later if you change your mind. |
| 5-programs.png | Uninstall several programs at once, with leftovers cleaned up after. |
| 6-disk-analyzer.png | See what fills your drives. |
| 7-duplicate-finder.png | Find duplicate files across whole drives. |
| 8-schedule.png | Scheduled cleanings on the days you choose. |

## Other Fields

| Field | Value |
|---|---|
| Category | Utilities & tools |
| Privacy policy URL | https://www.kodiakcleaner.app/privacy/ |
| Website | https://www.kodiakcleaner.app/ |
| Support contact info | https://www.kodiakcleaner.app/contact/ |
| Additional license terms | https://www.kodiakcleaner.app/terms/ |
| Copyright and trademark info | Copyright © 2026 Ted Tabaka. MIT License. |
| Pricing | Free, all markets |
| Store logo (1:1, 300 x 300) | AppTileIcon-300x300.png |

## Restricted Capabilities: Why (Submission > Packages)

- **runFullTrust:** Kodiak Cleaner is a .NET 8 WPF desktop app. It runs as a normal full-trust desktop program.
- **unvirtualizedResources:** Kodiak Cleaner's job is changing the user's real Windows settings, always after a
  confirmation and always with a backup first: removing startup entries under HKCU\...\Run, cleaning registry entries
  under HKCU, adding and removing an optional Recycle Bin menu entry, and cleaning files in AppData. With
  virtualization on, these changes would land in a private copy and nothing would really be cleaned. Every registry
  change is backed up to a .reg file and can be restored from the app.

## Notes for Certification

Kodiak Cleaner has no account or sign-in and sends nothing anywhere. The Store version never checks the website for
updates. Administrator features (Driver Cleaner, Drive Wiper, some cleaning items) only run after the user clicks
Restart as Administrator and approves the Windows prompt. Drive Wiper and Driver Cleaner ask several times before
doing anything; please don't run them on the test device's system disk (Drive Wiper refuses the Windows disk anyway).
Settings > Application > Remove Windows Changes turns off everything the app set up in Windows before an uninstall.
