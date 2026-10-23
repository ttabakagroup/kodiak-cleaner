# Kodiak Cleaner Privacy Policy

**Effective date:** October 23, 2026

Kodiak Cleaner is a free Windows cleaner made by Ted Tabaka, an independent developer. This policy explains exactly what Kodiak Cleaner does with information. In short: everything stays on your computer.

## What Kodiak Cleaner Collects

Nothing. Kodiak Cleaner has no accounts, no analytics, no telemetry, no crash reporting and no ads. It never sends information about you, your computer or what you clean to anyone.

## What Stays on Your Computer

- **Settings** are saved in KodiakCleaner.ini, next to KodiakCleaner.exe.
- **Backups** are saved in the Kodiak Cleaner folder in your Documents (the portable version keeps them beside KodiakCleaner.exe), in Backups, with a folder for each kind: Drivers (copies of the driver packages Driver Cleaner removes, so they can be installed again), Event Logs (copies of Windows logs saved before they are cleared), Mass Rename (the old and new names of your last rename, so Undo Last Rename can put them back), Programs History (what the Programs page removed), Registry (a .reg file before every registry change), Scheduled Tasks (a copy of each scheduled task, as Task Scheduler would export it, saved before Kodiak Cleaner deletes or replaces it), Settings (backups you make under Settings) and Startup History (the startup entries Kodiak Cleaner has seen, with copies of startup shortcuts so removed ones can be put back). They are only read on your PC.
- **The activity log**, in the Logs folder beside Backups, notes what Kodiak Cleaner did (cleanings, registry fixes, backups, startup and program changes, errors) with counts, sizes and item names, never the contents of your files. One file is kept per month, for a year. You can turn it off under Settings, Application, Keep an Activity Log. It is never sent anywhere.
- **Your supporter code**, if you enter one, is saved in KodiakCleaner.ini and checked on your computer only.
- **Tools** such as Disk Analyzer, Large Files, Empty Folders, Browser Extensions, Scheduled Tasks and System Information read your computer to show you what they find. Results are shown on screen only; they aren't saved or sent anywhere. System Information's Copy to Clipboard copies its list, which includes your computer's name, only when you click it, and only to your own clipboard. Its Export Report saves a report (which includes serial numbers and network addresses) only when you click it, as a file in the Reports folder on your PC; it is never sent anywhere. Integrity Check saves Windows' own messages from each check and repair as a text file in the Reports folder too, and nowhere else. Boot Timer saves the times from your last timed restart, and how much processor and disk time each startup program used, in the Reports folder, and nowhere else.

Kodiak Cleaner stores nothing in AppData, unless Windows Security blocks the Documents folder, in which case backups, logs and reports go to AppData\Local\Kodiak Cleaner instead. A few optional features you turn on add small entries to Windows, and turning them off again removes them:

- **Clean When Windows Starts** adds a startup entry for your Windows account.
- **Jump List Tasks** are kept by Windows itself.
- **Open Kodiak Cleaner From the Recycle Bin Menu** adds a menu item to the Recycle Bin.
- **Scheduled cleanings** you set up under Cleaner, Schedule are added to Windows Task Scheduler.
- **Skip the User Account Control Prompt** adds a scheduled task that starts Kodiak Cleaner with administrator rights.

Uninstalling Kodiak Cleaner removes the program, its settings file and everything the optional features above added to Windows, and asks whether to delete your backups, logs and reports too (keep them if you might still want to undo a registry fix). The portable version leaves no trace outside its own folder apart from the optional features above; turn those off, then delete the folder.

## When Kodiak Cleaner Goes Online

Only to check for updates:

- When you click Check for Updates on the About page or Check Now under Settings, Updates, or
- On the schedule you pick under Settings, Updates (each time Kodiak Cleaner starts, or every hour up to once a month), if you turn on Keep Kodiak Cleaner Updated Automatically (off by default). With it on, a new version is downloaded in the background and installed when you close Kodiak Cleaner, which then opens again.

The check reads one small update file from Kodiak Cleaner's own website (www.kodiakcleaner.app/updates), which names the newest version and its installer's SHA-256 checksum. When an update is downloaded, it comes from the same website; Kodiak Cleaner saves the installer in your Windows temporary folder and checks it against that checksum, and an installer that doesn't match is deleted, not run. Nothing about you or your PC is sent with either request. Like any website, the site's host, Cloudflare, can see your IP address and that the request came from Kodiak Cleaner; Cloudflare's own privacy policy covers what it does with that. The portable version only checks; it never downloads or installs anything.

Buttons such as Support on Ko-fi, Star on GitHub, Website and Project Page open the website in your browser. Those websites have their own privacy policies.

Integrity Check's Repair for the Windows Component Store asks Windows to repair itself, and Windows may download clean copies of its own files from Windows Update to do it, as it does for its own updates. Kodiak Cleaner sends nothing.

## Feedback

Send Feedback, Report a Bug, Request a Feature and Security open the Contact Us form on www.kodiakcleaner.app in your browser, with the topic picked and your Kodiak Cleaner version in the link. Nothing is sent until you fill in the form and press Send Message there; the website's own privacy policy covers what happens to a message. Copy Details copies a few facts (Kodiak Cleaner version, Windows version, plan, install type and theme) you can paste into a bug report. They never include file names, folders, user names or anything you cleaned.

## Children

Kodiak Cleaner doesn't collect personal information from anyone, including children.

## Changes

If Kodiak Cleaner ever changes what it does with information, this policy will be updated before that version is released, and the effective date above will change.

## Contact

Use the Contact Us form at https://www.kodiakcleaner.app/contact/
