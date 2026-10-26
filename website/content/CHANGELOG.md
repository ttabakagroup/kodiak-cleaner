# Changelog

Every change to Kodiak Cleaner, newest first. After an update, Kodiak Cleaner's What's New window shows
the changes since your previous version, and About > See All Changes shows the full list. The changelog
starts fresh with the first Beta; About > All Changes Since the Start also shows every earlier Dev build.

## [Unreleased]

## [0.9.6 Beta] - 2026-10-26

### Settings and Appearance
- Each Color Scheme in Settings > Appearance now shows a strip of its three main colors (primary, secondary and background), so you can see what it looks like before you pick it. The strips follow Light or Dark mode. The schemes now sit two to a line.
- Why These Colors is now a numbered list (1. Honey, 2. Classic, 3. Glacier, 4. Lagoon), and Classic is described as my thank-you to supporters.

## [0.9.5 Beta] - 2026-10-26

### Microsoft Store
- The 0.9.3 Beta try at putting Open Kodiak Cleaner in Windows 11's short Recycle Bin menu is removed from the Store package, since Windows never loads it there. The Recycle Bin setting now says to use Shift + right-click (or Show More Options) to find it.

## [0.9.4 Beta] - 2026-10-26

### Microsoft Store
- Open Kodiak Cleaner is back under Show More Options on the Recycle Bin in the Store version. Windows 11 doesn't allow it in the short right-click menu there, and the 0.9.3 Beta try hid it from both menus.

## [0.9.3 Beta] - 2026-10-26

### Microsoft Store
- Kodiak Cleaner can now be built as a Microsoft Store (MSIX) package. Nothing changes for the MSI and portable versions.
- The Store version keeps its settings in its own app folder (Windows removes it on uninstall), and its backups, logs and reports in Documents\Kodiak Cleaner like the MSI.
- The Store installs its updates, so the Store version never checks the website; Check for Updates opens the Store's Downloads page.
- Skip UAC, administrator scheduled cleanings and the Driver Cleaner's Safe Mode restart are left out of the Store version (Windows doesn't start Store apps in Safe Mode).
- Settings > Application > Before You Uninstall has a Remove Windows Changes button in the Store version, since a Store uninstall can't turn off Clean When Windows Starts, the Recycle Bin menu or scheduled cleanings by itself.
- System Information and bug reports show "Microsoft Store" as the install type.
- In the Store version, "Open Kodiak Cleaner" is offered in Windows 11's short right-click menu for the Recycle Bin (an early try: if Windows doesn't show it there, it stays under Show More Options as before).

## [0.9.2 Beta] - 2026-10-26

### Terms of Use
- Supporting on Ko-fi can be one time or monthly, with a link to the new Refund Policy at kodiakcleaner.app/refunds/.
- The supporter extras now list the Lagoon theme too, and the update-check sentence matches the Privacy Policy (Check for Updates, Check Now, or the schedule you pick).
- A new Governing Law section (Illinois, USA). Effective October 26, 2026.

## [0.9.1 Beta] - 2026-10-25

### Settings and Appearance
- New Settings > Tools page lists every tool A to Z with a switch. Turn off the tools you don't use to hide them from Favorites and Tools in the sidebar and from the jump list; turn one back on and it returns to the same place. Show Every Tool brings them all back, and so does Reset to Default.
- Right-click any tool in the sidebar and pick Hide from Sidebar to hide it straight away.
- A hidden tool still opens from a link or the command line (--page=), with a note saying where to turn it back on. Hiding a tool never stops the command line, scheduled cleanings or Clean When Windows Starts.
- If every tool is hidden, the sidebar says so and Choose Tools opens Settings > Tools.

### Command Line
- New Settings > Help > Command Line page explains every switch, the exit codes, using it in scripts and as administrator, and the page names for --page=, with a Copy button for each example.
- KodiakCleaner.exe /help now also lists /autoclean, /schedule:, --page= and every page name, and shows in a window that scrolls when there's no console.
- The README's Command Line section covers every switch with examples, the exit codes, administrator rights and every page name, and the FAQ has new answers on the command line and on hiding tools.

### Fixes
- Mass Rename: the outline around an example or a part at the left edge of the rules is no longer cut off. The same spacing is fixed in Duplicate Finder's options and the other scrolling areas laid out the same way.

## [0.9.0 Beta] - 2026-10-25

The first Beta of Kodiak Cleaner: a small, honest cleaner for Windows 10 and 11 with no telemetry, no ads, no bundled offers and no background services. Everything below is in this release.

### Cleaner
- Analyze shows how much space each item would free before anything is deleted, then Run Cleaner cleans what you ticked.
- Cleans Windows temporary files, the Recycle Bin, thumbnail and shader caches, error reports, crash dumps, Windows Update and Delivery Optimization caches, recent document lists and broken shortcuts.
- Cleans the cache, history, cookies and more for Brave, Google Chrome, Microsoft Edge, Mozilla Firefox, Opera, Vivaldi and other popular browsers, each with its own icon. Browsers that are open are skipped.
- Cleans caches, logs and installer leftovers of common programs, but only when they're installed.
- Advanced Cleaning holds deeper items behind a warning, including Windows' own cleanup tasks. Riskier items carry a caution and ask before they're ticked.
- Search, Sort By (Default, A to Z, Last Cleaned, Most Space Freed, Largest Found) and Reset to Default.
- Scheduled Cleaning cleans chosen items on chosen days through Windows Task Scheduler, with nothing running in between.
- Clean When Windows Starts, and a command line for scripts.

### Registry
- Finds entries that point at files or programs that no longer exist, across 15 checks, and only reports what is definitely gone.
- Every fix is saved to a .reg backup first, so any fix can be undone.
- Back Up Now saves the registry areas Kodiak Cleaner can change in one file.

### Startup
- Lists the programs that start when you sign in, for you and for all users, each with its icon.
- Turn programs off or on, add a program, or remove an entry (backed up first). History restores removed entries.
- Tells you when a program adds itself to startup, so you can keep, turn off or remove it.

### Programs
- Lists installed programs with their icons, and uninstalls one or several, one at a time.
- After an uninstall, offers to remove the folders, shortcuts and registry keys the program left behind.
- Repair, Rename and Delete Entry, plus Copy Product Code and Copy Uninstall Command.
- History records every removal and change, and restores what it can.

### Tools
- Boot Timer: restarts and times your PC's startup, then shows what slowed it down.
- Browser Extensions: lists every browser's extensions and turns them off or removes them.
- Defrag and TRIM: defragments hard drives and trims SSDs with Windows' own optimizer, matched to each drive.
- Disk Analyzer: a map of what fills a drive or folder, colored by kind of file.
- Drive Wiper: wipes free space or whole drives with the right method for each drive type, after three checks, and checks a drive's health.
- Driver Cleaner: removes old graphics or audio drivers completely before a fresh install, with backups and a Safe Mode option.
- Duplicate Finder: finds identical files, and files that are alike, on drives or in folders, and keeps the likely original.
- Empty Folders: finds empty folders and moves the ones you pick to the Recycle Bin.
- Hash Checker: works out MD5, SHA-1, SHA-256, SHA-384 and SHA-512 and checks files against a list of hashes.
- Integrity Check: checks and repairs Windows files, drives and memory with Windows' own tools.
- Large Files: finds the files taking the most space.
- Mass Rename: renames many files at once by a pattern, shows every new name first, and can undo it.
- Scheduled Tasks: shows what Windows runs on a schedule, turns tasks off or on, and backs up, deletes and restores them.
- System Information: your PC at a glance, with a detailed view and reports you can save.
- System Restore: creates and removes restore points, and sets up Point-in-Time Restore on Windows 11.

### Settings and Appearance
- Four color schemes: Honey for everyone, plus Classic, Glacier and Lagoon for supporters. Accent colors, light or dark mode, and a Text Size slider from 80% to 200%.
- Favorites at the top of the sidebar: pin any tool, drag to reorder, and widen, lock or collapse the sidebar.
- A small card shows what a tool is doing while you use another page.
- Search the settings, back up and restore them, and Reset to Default.
- Runs as a normal user and asks for administrator rights only when a page needs them.
- Check for Updates downloads the installer from Kodiak Cleaner's website and checks it before installing. Automatic checks are off until you turn them on.
- Installed with the MSI, or portable from any folder.

### Privacy
- Everything stays on your PC: no telemetry, analytics, crash reports, accounts or ads.
- The only time Kodiak Cleaner goes online is to check for updates, and only when you ask or turn on automatic checks.
- Settings, backups, the activity log and reports are plain files on your PC. The activity log can be turned off.
- Contact & Feedback opens the website's contact form in your browser, so nothing is sent unless you send it.
