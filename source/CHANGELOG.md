# Changelog

Every change to Kodiak Cleaner, newest first. After an update, Kodiak Cleaner's What's New window shows
the changes since your previous version, and About > See all changes shows the full list.

## [Unreleased]

## [0.8.67 Dev] - 2026-10-12

### Duplicate Finder
- In-Depth can now match files by similar names alone, even when their contents differ, with a Name Similarity setting.
- Leave copy marks, version words, dates and times, numbers or your own words out when names are compared, and compare names across file types if you like.
- Name Patterns: type a pattern such as "Invoice #.pdf" to list every matching file together. # stands for a number, ? for one character and a star for any text.
- The "MB" label next to the size setting is no longer cut off.

## [0.8.66 Dev] - 2026-10-12

### Boot Timer (New Tool)
- Restart the PC from Kodiak Cleaner and see how long shutting down, Windows starting and the desktop settling took, plus which startup programs, drivers and services slowed it, slowest first, with Show in Startup and Turn Off at Startup.

### Cleaner
- New Windows items: Start Menu Shortcuts and Desktop Shortcuts (broken ones only), Windows Widgets Cache, Windows Notifications and Game Explorer Cache.
- Every Chromium browser (Chrome, Edge, Brave, Vivaldi, Helium) gets Metrics Temp Files, Session and Bookmarks Backup; every Firefox-based browser gets Session, Bookmarks Backup, Crash Reports, Site Preferences and Saved Form Information. Saved passwords and form data are Advanced Cleaning items with a warning.
- New program groups, shown only when installed: AMD Software, AnyDesk, Discord, FileZilla, Microsoft OneDrive, Node.js, NVIDIA, Steam, Telegram Desktop, VLC Media Player, WhatsApp and Zoom.
- New Advanced Cleaning items: Windows Event Trace Logs, Driver Installation Log Files, IIS Log Files, Windows Internet Cookies, Taskbar Jump Lists, your Downloads folder by kind (Programs, Compressed Files, Pictures, Audio and Video, Documents, Other Files), Other Explorer Lists, Recently Typed URLs, User Assist History, Menu Order Cache, Window Size and Location Cache, Registry Editor Last Key, 7-Zip History, Environment Path (missing folders only) and Wipe Free Space. Registry items are backed up first, and the risky ones ask before they are ticked.

### Defrag and TRIM (New Tool)
- Lists your drives with their type, file system, size and use. Hard drives get Defragment, SSD and NVMe drives get TRIM, and flash drives are left alone. Everything that needs it is ticked by default, so one click optimizes every drive the right way.
- Analyze shows how fragmented hard drives are. Optimize Selected runs one drive at a time with progress, and Stop ends it. Uses Windows' own optimizer and needs administrator rights.

### Disk Analyzer
- New Choose What to Analyze window: pick All Drives (every local drive at once), Drives (tick one or more, network drives too) or Folder. Each drive shows its name, file system, a fill bar and how much is free; Folder has a Browse button and quick picks for Desktop, Documents, Downloads, Music, Pictures, Videos and your user folder.
- A map of what's using the space: every folder and file in the open folder is a box sized by the space it takes and colored by the kind of file it holds most (Archives, Documents, Music, Pictures, Programs, Videos, Other). Big folders show the folders inside them too, up to three levels. Hover a box for its size, share, file count and path; click a folder to look inside.
- A bar and legend show which kinds of files fill the open folder; hover one to highlight it in the map. A drive also shows how full it is.
- Go back up with the Up button, the path above the map (click any folder in it), Backspace or the mouse's back button. Right-click any box or row to Copy Path, Look Inside, Move to Recycle Bin (files) or Show in File Explorer.
- Contents and Largest Files lists beside the map; Largest Files lists the biggest files anywhere inside the open folder.
- Scanning is much faster: drives, and the folders at the top of each one, are read side by side, with a live count of files and the folder being read, and Cancel. Scan Again repeats the last scan.

### Drive Wiper
- Erase Entire Drive works on whole physical disks, and several at once.
- Each drive's type is detected (Hard Drive, SSD, NVMe SSD, Flash Drive) and the right method is used: hard drives and flash drives are overwritten with 1 Pass (Zeros), DoD 3 Passes (DoD 5220.22-M: zeros, ones, random) or DoD 7 Passes (DoD 5220.22-M ECE); SSDs and NVMe drives get their own firmware erase, with a random overwrite when the drive doesn't offer one.
- Verify the Last Pass (on by default) reads every byte back afterwards, and the drive then gets one empty partition named Wiped.
- Disks holding Windows, its start-up partition, the page file, Kodiak Cleaner, your user folder or programs can't be ticked. You confirm with a warning listing every drive and method, then by typing ERASE.
- Free space wiping uses the same DoD patterns; SSDs get a note that it mostly adds wear.

### Driver Cleaner (New Tool)
- Removes an old AMD, Intel or NVIDIA graphics driver, or a Realtek audio driver, completely before a fresh install: its driver store packages, leftover folders and registry keys. The parts that belong to the driver are ticked; other parts from the same maker are left for you to decide.
- Cleaning makes a restore point and backs up each driver package first, removes packages with Windows' own tools, backs up registry keys before deleting them and moves folders to the Recycle Bin.
- Restart in Safe Mode opens Kodiak Cleaner on the Driver Cleaner there for the cleanest removal. A switch stops Windows Update from putting an old driver back.
- When it's done: Later, Shut Down (to swap the card) or Restart, and Get the Latest Driver opens the maker's own download page.

### Duplicate Finder
- Search whole drives: one, several or All Drives, with free space shown. Windows, program folders and every account's AppData are always skipped.
- In-Depth mode finds files that are alike (edited copies, re-saved or resized pictures, older drafts) and ranks them by how alike they are, with the reason shown.
- Verify Hashes reads the listed files again and confirms each set by SHA-256; a copy that changed since the search is marked and unticked.
- Similar files are never ticked for you, and removing one asks first.

### System Restore
- Two tabs: System Restore and Point-in-Time Restore (Windows 11 24H2 and later with the June 2026 update).
- Point-in-Time Restore: turn it on or off, choose how often Windows takes a snapshot (4 to 24 hours), how long it keeps them (4 to 72 hours) and the most space they may use (2 to 50 GB); see the snapshots and when each expires; Snapshot Now; and Restart to Recovery to restore. Settings are backed up before each change.
- Create Restore Point now always makes a new point, even when Windows already made one that day, so you can make one right before a big install and keep it much longer than 72 hours.
- The Windows Shadow Copies cleaning item now says it also removes Point-in-Time Restore snapshots.

## [0.8.65 Dev] - 2026-10-12

### Browser Extensions
- Right-click an extension to turn it on or off, or remove it, and each row has an On switch. Ctrl-click and Shift-click pick several at once. Hover over a row to see everything about it.
- Chrome, Edge and Brave are changed through their own extension policies, the supported way to manage a browser, so Kodiak Cleaner never edits the browser's protected settings. While a policy is in use the browser says it is managed by your organization; Kodiak Cleaner removes the policy again once nothing needs it. Changing policies needs Restart as Administrator.
- Firefox, Ablaze Floorp and Mullvad Browser add-ons can be removed while that browser is closed. Those browsers only let you turn an add-on off on their own Add-ons page.

### Duplicate Finder
- The buttons at the bottom move onto a second line when the window is narrow, instead of sitting on top of each other.
- Each file shows its name with its folder underneath, so long names no longer run into the date and Show.

### Empty Folders
- Search whole drives, one or several, or tick All Drives, as well as folders. Each drive shows its free space and a bar. Windows' own folders at the top of a drive, hidden folders, and Kodiak Cleaner's own folders are always skipped, and every account's Desktop, Documents and other Windows folders are never removed.

### Registry
- Entries that Windows itself protects are no longer listed. Not even an administrator may remove them, and they belong to Windows components, which Windows repairs with its updates. If one is ever met, the message now says Windows protects it instead of "Attempted to perform an unauthorized operation".
- 32-bit entries that name Program Files when their file is really in Program Files (x86) are no longer reported as missing.

### Scheduled Tasks
- A new Program column shows the program each task runs, with its icon. Click its name, or right-click and choose Open File Location, to see the file in File Explorer. Hover over a task to see its command, publisher, author and description.

## [0.8.64 Dev] - 2026-10-11

### Donation Reminder
- The donation reminder now counts each start exactly once, starting at 1 on the very first start. Copies Kodiak Cleaner reopens by itself (after an update installs, or Restart as Administrator) no longer count as extra starts, which made the countdown jump by two or more.
- The reminder now shows from the first start, right after Getting Started or What's New closes, instead of being skipped on those starts, so the countdown is visible from start 1.

## [0.8.63 Dev] - 2026-10-10

### Settings
- Every Settings card is now in A to Z order, and so is every setting inside a card, so each switch is easy to find. Reset to Default stays the last card in Advanced.

### Registry, Startup and Programs
- Hover over an entry to see everything about it. Registry shows the type, problem, data and full registry key. Startup shows the command, the program it starts, its publisher and exactly where the entry lives. Programs shows the publisher, version, install date, size, install folder, uninstall command, Product Code and registry key.

## [0.8.62 Dev] - 2026-10-10

### Performance
- Kodiak Cleaner no longer keeps working while it sits idle. A hidden busy bar on the Cleaner page kept its sliding animation running from startup, so the window redrew 60 times a second for as long as the app was open, even minimized or in the tray. Busy bars now slide only while they are busy and on screen; under Wine idle CPU use fell from about 4 seconds a minute to almost nothing.

## [0.8.61 Dev] - 2026-10-10

### Cleaner
- Advanced Cleaning has the Windows Disk Cleanup items it was missing: Microsoft Defender Antivirus, Diagnostic Data Viewer Database Files, Feedback Hub Archive Log Files, Language Resource Files, Old Chkdsk Files, RetailDemo Offline Content, Temporary Setup Files, Temporary Windows Installation Files and User File History. Old Chkdsk Files is cleaned by Kodiak Cleaner; the others run Windows' own Disk Cleanup with only that item ticked, because only Windows knows which of those files are safe to remove.
- Language Resource Files, Temporary Windows Installation Files and User File History ask before they are ticked, like the other risky items.
- Windows Log Files now also covers Disk Cleanup's Setup Log Files and Windows Upgrade Log Files; Windows Internet Cache says it is Disk Cleanup's Temporary Internet Files.

## [0.8.60 Dev] - 2026-10-10

### Settings
- Reset to Default now starts the donation reminder's count again: the reminder is back on and can be turned off after 10 more starts. Supporters keep their code and still never see it.

## [0.8.59 Dev] - 2026-10-10

### Why It Was Made
- The story now ends with Ted's sign-off.

### Settings
- Reset to Default is now the last card in Settings > Advanced, so it stays when the Developer page is removed for the public beta. It replaces the smaller Reset Settings row under Application, asks Yes/No first and keeps the same things (start count, supporter code, scheduled cleanings, history, backups, reports and logs).

## [0.8.58 Dev] - 2026-10-10

### Why It Was Made
- More space between paragraphs, so the story is easier to read.

## [0.8.57 Dev] - 2026-10-10

### Why It Was Made
- A new story from Ted, in his own words, under Settings > Help > Why It Was Made.

## [0.8.56 Dev] - 2026-10-10

### Contact & Feedback
- Send Feedback, Report a Bug, Request a Feature and Security now open the Contact Us form on kodiakcleaner.app in your browser, with the topic already picked. The in-app email form is gone, and Kodiak Cleaner no longer shows an email address anywhere: the privacy policy, terms, security notes, Read Me and the installer's support contact all point to the form. Copy Details still copies your version and Windows details to paste into a bug report.

## [0.8.55 Dev] - 2026-10-10

### Donations
- A pop-up at startup asks you to consider a donation on Ko-fi to keep Kodiak Cleaner free and open source. Close it with Not Now or the X. Its Don't Show This Again switch, and Settings > General > Ask Me to Donate at Startup, can be turned off once Kodiak Cleaner has been started 10 times; until then they're greyed out and say how many starts are left. The pop-up never appears on top of Getting Started or What's New.
- On every 10th start, a small banner at the top of the window says Kodiak Cleaner is made for free and a donation keeps the work going, with Donate on Ko-fi and Dismiss.
- Supporters see neither: entering a supporter code turns the pop-up off and hides the banner.

### Developer
- A Reset to Default card under Status puts every setting back to its default after a Yes/No check. It keeps the start count, your supporter code, scheduled cleanings, history, backups, reports and logs.

## [0.8.54 Dev] - 2026-10-10

### Programs
- Kodiak Cleaner no longer uninstalls itself or deletes its own entry. Picking it in Programs (the Uninstall button, right-click, a batch or Delete Entry) or with the command line's /uninstall shows a message asking you to uninstall it from Control Panel, with a button that opens Programs and Features. In a batch, the other programs still go ahead.

## [0.8.53 Dev] - 2026-10-10

### Command Line
- Kodiak Cleaner can now run from the command line with no window. `/clean` cleans the ticked Cleaner items (or `/items:ID,ID`), `/registry` scans and fixes the registry with a backup first (`/scan-only` changes nothing), and `/uninstall:"Name"` or `/uninstall:{GUID}` runs a program's uninstaller after backing up its registry entries (`/quiet` for a silent uninstall). `/list-items`, `/list-programs` and `/help` list what's there. Output goes to the console, everything is written to the activity log, and the exit code says how it went.

### Programs
- A new Product Code column shows each Windows Installer program's GUID, and the search box finds programs by it. Right-click Copy Product Code copies it, and Copy Uninstall Command copies `msiexec /x {GUID} /qn` for administrators.

### Startup
- When a program adds itself to startup, a pop-up lists it the next time Kodiak Cleaner starts, or within 10 minutes while it's open or in the tray. Choose Keep, Disable or Delete for each one; deleting is backed up and can be restored from History. Turn it off under Settings > General > Tell Me About New Startup Programs.

## [0.8.52 Dev] - 2026-10-10

### Registry
- Ctrl-click picks several issues and Shift-click picks a range, as in Startup and Programs. Right-click Fix acts on every highlighted issue, and Fix Selected uses them when nothing is ticked.

## [0.8.51 Dev] - 2026-10-09

### Updates
- Settings > Updates now has two switches. Check for Updates Automatically looks for a new version as often as you choose and tells you when one is available; it no longer downloads anything by itself. Download and Install Updates Automatically also downloads the update and installs it when you close Kodiak Cleaner, which then opens again. It's greyed out while checking is off. If you had automatic updates on before, both switches start on, so nothing changes for you.

### System Information
- Opening System Information no longer freezes Kodiak Cleaner. It used to check every drive on the main window's thread, so a sleeping disk or a disconnected network drive could hang the window. Everything is now read in the background, and a drive that doesn't answer within 5 seconds is listed as not answering.
- While it reads, the page shows a progress bar (Advanced shows which part it's on) and a Cancel button. Cancel shows a plain screen with Refresh. You can switch to other pages while it reads and come back when it's done.
- Parts of Advanced that take 5 seconds or more are noted in the activity log, to help find slow ones.

## [0.8.50 Dev] - 2026-10-09

### Pop-Ups
- Yes and No have swapped places in every Yes/No question: No is on the left and Yes is on the right, and Yes is now the default, so pressing Enter answers Yes. Esc or closing the window still answers No. This covers confirmations (Remove, Uninstall, Delete Entry, Clean and the rest), the Advanced Cleaning warning and the cautions on risky items, where Yes stays red, and the Yes, No, Cancel questions.

## [0.8.49 Dev] - 2026-10-09

### Programs
- Ctrl-click picks several programs and Shift-click picks a range, as in File Explorer. Right-click Uninstall and Delete Entry act on every highlighted program (one confirm for all), and the Uninstall button uses them when nothing is ticked. Repair and Rename work on one program at a time.

### Startup
- Ctrl-click picks several entries and Shift-click picks a range. Right-click Enable, Disable and Remove From Startup act on every highlighted entry, and the buttons above the list use them when nothing is ticked.
- Right-clicking a highlighted row keeps the whole selection; right-clicking any other row picks just that one.

## [0.8.48 Dev] - 2026-10-09

### Fixes
- Registry and Startup no longer report a working startup entry as missing when its command is a script that takes a program as its argument (for example start.cmd C:\Apps\app.exe), so Fix can't remove it.
- With Always Start as Administrator (or after Restart as Administrator), opening Kodiak Cleaner again, using the jump list, or a scheduled cleaning now reaches the open window instead of silently doing nothing.

### Tools
- Duplicate Finder no longer lists a file as its own duplicate when a search folder is also reached through a link, and Delete checks that the copy you keep is still there and unchanged before removing the others.
- Empty Folders never searches or lists any user's AppData, including other accounts' profiles under C:\Users.
- Large Files refuses to start inside Windows or a program folder.
- Wipe Free Space stops and removes its filler files when Kodiak Cleaner is closed mid-wipe, instead of leaving the drive full.

## [0.8.47 Dev] - 2026-10-07

### Fixes
- Kodiak Cleaner draws its window before anything else at startup. The Beta Software notice, What's New and the update check now wait until the window has appeared, and the Windows registrations (startup entry, Recycle Bin menu, jump list, scheduled cleanings) are checked once it's idle, so a first start after an update no longer looks frozen.
- After an update installs, the reopened Kodiak Cleaner comes to the front instead of opening behind other windows with its notice hidden.
- Uninstall cleanup never runs while an update is replacing Kodiak Cleaner, even where Windows Installer doesn't say an upgrade is under way, so an update can't ask about backups, logs and reports or remove the startup entry and Recycle Bin menu.
- A start that takes 5 seconds or more is noted in the activity log, to help explain a slow start.

## [0.8.46 Dev] - 2026-10-06

### System Information
- System Information moved out of Tools to the bottom of the sidebar, between Settings and About.
- New Advanced view beside Summary: drive models and serial numbers, memory type, speed, slots and each module's details, processor cores, threads and cache, PC and motherboard serial numbers, BIOS, UEFI, Secure Boot and TPM, graphics memory and driver, each network adapter's MAC, IP addresses, gateway, DNS and DHCP, audio devices, a few Windows settings, and temperatures where Windows shares them.
- New Export Report: pick Text, HTML or CSV from the dropdown and save a report of both views in the new Reports folder. The HTML report uses your current color scheme. Your choice of format is remembered.

### Application
- The Reports folder sits beside Backups and Logs (Documents\Kodiak Cleaner, or beside KodiakCleaner.exe for the portable version) and is shown under Settings > Application. Uninstalling asks whether to delete reports along with backups and logs.

## [0.8.45 Dev] - 2026-10-06

### Settings
- New search box above General in the Settings sidebar. Start typing and the list shows only the pages whose settings match, with the matching setting names under each page. Enter opens the first match and Esc clears the search.
- Check for Updates under Settings > Updates is now a dropdown, so more choices can be added later. On Startup is still selected out of the box.

## [0.8.44 Dev] - 2026-10-06

### Updates
- New Check for Updates choice under Settings > Updates, below Keep Kodiak Cleaner Updated Automatically: On Startup (the default, as in 0.8.43), Every Hour, Every 4 Hours, Daily, Every 3 Days, Weekly, Twice a Month or Monthly. The longer intervals check when Kodiak Cleaner starts or while it's open, once that long has passed since the last check.

## [0.8.43 Dev] - 2026-10-06

### Updates
- With Keep Kodiak Cleaner Updated Automatically on, Kodiak Cleaner now checks for a new version every time it starts, instead of once a day.
- After an update installs, Kodiak Cleaner opens again on its own. If the installation is cancelled or fails, it stays closed.

## [0.8.42 Dev] - 2026-10-06

### About
- The Check for Updates button now sits on the right of its card, the same size as before, with the last check time and the note beside it, so the card is shorter.

## [0.8.41 Dev] - 2026-10-06

### About
- New Check for Updates card right below the version card, with a large Check for Updates button, Install Update next to it when a new version is found, and the last check time. It's the same check as Settings > Updates, which still has Check Now and the Keep Kodiak Cleaner Updated Automatically switch.

## [0.8.40 Dev] - 2026-10-06

### Updates
- Kodiak Cleaner now checks its own website, www.kodiakcleaner.app, for updates instead of GitHub, so Check Now and automatic updates keep working while the GitHub project is private. It reads one small update file naming the newest version and its installer's checksum, downloads the installer from the same website, and installs it only if the checksum matches.
- Updates are only accepted from www.kodiakcleaner.app over a secure connection. If the website answers with anything else, such as a sign-in page, nothing is downloaded and the next start tries again.
- The activity log notes when an update has been downloaded and verified.

### Privacy
- The privacy policy and About page now say that the update check goes to Kodiak Cleaner's website, and About no longer says backups are kept next to KodiakCleaner.exe.

## [0.8.39 Dev] - 2026-10-06

### Settings
- A divider with a little space on each side now sits above Support Kodiak Cleaner at the bottom of the Settings sidebar, setting it and the privacy note apart from the pages above.

## [0.8.38 Dev] - 2026-10-06

### Installer
- The GitHub build makes the MSI again. Windows Installer's own checks flagged the Start menu shortcut as if Kodiak Cleaner were installed for one user only; it is installed for everyone, so those three checks (ICE38, ICE43 and ICE57) are now switched off rather than moving the shortcut's marker, which would have made every future upgrade delete the Start menu shortcut.

## [0.8.37 Dev] - 2026-10-06

### Downloads
- The installer is now just KodiakCleaner-Dev-Setup.msi; the extra Setup ZIP (the same MSI plus the Docs folder) is gone, since the installer puts the Docs folder in place itself. Automatic updates use the MSI directly, and still understand older releases that only had the ZIP.

## [0.8.36 Dev] - 2026-10-06

### Backups and Logs
- An installed Kodiak Cleaner now keeps its backups in Documents\Kodiak Cleaner\Backups instead of beside the program in Program Files, so saving a backup never needs administrator rights or special folder permissions. The portable version still keeps everything beside KodiakCleaner.exe.
- Each kind of backup has its own folder: Event Logs, Programs History, Registry, Settings and Startup History. Backups from earlier versions are moved there automatically the first time 0.8.36 starts.
- New activity log in Documents\Kodiak Cleaner\Logs: one plain-text file per month noting cleanings, registry fixes, backups, startup and program changes, updates and errors. It stays on your PC, keeps the last 12 months, and can be turned off under Settings > Application > Keep an Activity Log.
- Settings > Application shows where your backups and logs are, with an Open Folder button. Settings > Registry shows where registry backups go.

### Uninstall
- Uninstalling Kodiak Cleaner now leaves nothing behind: it removes its scheduled cleanings and Skip UAC task, the Clean When Windows Starts entry, the Recycle Bin menu item and any downloaded update, along with the program and its settings.
- It then asks whether to delete your backups and logs too. Keep them if you might still want to undo a registry fix. A silent uninstall (msiexec /qn) keeps them unless you add DELETEUSERDATA=1.

## [0.8.35 Dev] - 2026-10-05

### Appearance
- The keyboard focus outline on sidebar items (in Settings and the main sidebar) now hugs the highlighted item instead of running wider than it and into the scroll bar.

## [0.8.34 Dev] - 2026-10-05

### Appearance
- The "Supporter Theme" badge on the Classic and Glacier tiles stays on one line, with more room below the description, and shrinks a little instead of wrapping when space is tight.
- The three Color Scheme tiles line up: each name and description sits at the top, so Honey no longer sits lower than the others.
- In a narrow window the tiles stack instead of being cut off.

## [0.8.33 Dev] - 2026-10-05

### Performance
- Kodiak Cleaner opens faster. Startup, Programs, System Information and Drive Wiper now read their lists the first time you open them instead of while Kodiak Cleaner starts, like Browser Extensions, Scheduled Tasks and System Restore already did.
- The Cleaner list only builds the rows you can see, instead of every item in every group, so the window appears sooner and scrolls smoothly.

## [0.8.32 Dev] - 2026-10-05

### Appearance
- In every color scheme, the scheme's own accent is the first swatch. Classic's own accent is blue, so Classic no longer shows a second Blue swatch; its first swatch is named "Theme Accent (Blue)", and Blue chosen in another scheme becomes Classic's own blue there.

## [0.8.31 Dev] - 2026-10-05

### Appearance
- Honey, Classic and Glacier now use the exact colors from Ted's updated build standard, and every theme passes the WCAG 2.2 AA contrast check in light and dark. The keyboard focus ring has its own color in each theme.
- Classic is now a Supporter theme, like Glacier. Honey stays free for everyone, and a copy without a supporter code that had Classic chosen shows Honey.
- Color Scheme tiles read "Warm cream and gold", "Crisp white and blue" and "Calm blue-grey", and locked tiles show a "Supporter Theme" chip.
- Accent colors are Blue, Teal, Green, Purple, Pink, Orange and Slate (Graphite is now Slate), plus the theme's own accent and your Windows accent. They stay free for everyone. Each one is shifted lighter or darker per theme and mode so text and buttons stay readable, and the swatch shows the shade you'll get. The Windows swatch is hidden when Windows doesn't report an accent.
- Accent swatches are round with a thin border so they show on any card, and the chosen one has a thicker ring and a checkmark. Arrow keys move between them.

### Packages
- The downloads are now named KodiakCleaner-Dev-Setup.msi, KodiakCleaner-Dev-Setup.zip (the installer with the Docs folder) and KodiakCleaner-Dev-Portable.zip, the same name every release, so a download link always gets the newest build.
- Both ZIPs have a Docs folder with Changelog.txt, Download.url, Homepage.url, License.txt, Privacy.txt, ReadMe.txt and TermsOfUse.txt, all plain text. The installer puts the same folder in Program Files\Kodiak Cleaner\Docs. They replace CHANGELOG.md, README.md, LICENSE, terms-of-use.md, PRIVACY.md, HOMEPAGE.url and DOWNLOAD.txt.
- ReadMe.txt explains installing, updating without losing settings or backups, uninstalling and privacy in plain words.
- Download.url downloads the newest Dev installer straight from GitHub. Automatic updates find the new Setup ZIP and still understand the old file names.

## [0.8.30 Dev] - 2026-10-05

### Registry
- Look For now has 15 checks, A to Z in three columns, all ticked to start, with Select All. The new checks are ActiveX and Class Issues, Applications, Fonts, Help Files, Installer, Sound Events, Start Menu Ordering, Type Libraries, Unused File Extensions and Windows Services. Startup Entries is renamed Run At Startup, and Obsolete Uninstall Entries is renamed Obsolete Software.
- Each new check only reports an entry when the file or folder it points to is definitely gone from a local drive. Files in the Windows folder, drivers and services that start with Windows' core are never reported. Every fix is still saved to a .reg backup first.
- The results table has a Type column showing which check found each entry.
- Back Up Now also saves the fonts, help files, installer folders, sounds and Start menu ordering areas.

## [0.8.29 Dev] - 2026-10-05

### Interface
- A divider with a little space around it now sits above Settings and About in the sidebar, so the Tools list never seems to run into them, whether the sidebar is collapsed or the window is short.
- The "Running as a standard user" bar matches your color scheme. It uses the scheme's accent tint with an info icon and regular text in Honey, Classic and Glacier, light or dark, instead of a warning color.

## [0.8.28 Dev] - 2026-10-04

### About
- Kodiak Cleaner has its own website, www.kodiakcleaner.app. Learn More has a new Website button next to Project Page, which still opens GitHub.

### Packages
- HOMEPAGE.url and the installer's support and help links in Windows Settings > Apps open www.kodiakcleaner.app. Downloads and updates still come from the GitHub Releases page.

## [0.8.27 Dev] - 2026-10-04

### Settings
- Why It Was Made has a book icon in the Settings sidebar instead of an X.
- Text in Getting Started and the other pop-up windows is drawn as crisply as the main window, so descriptions no longer look lighter in some places.

### Packages
- The portable ZIP includes CHANGELOG.md, README.md, LICENSE, terms-of-use.md, PRIVACY.md, HOMEPAGE.url and DOWNLOAD.txt again, as the README says (Linux builds had left them out).
- DOWNLOAD.txt links to the Releases page, which lists Dev and Beta builds. The "latest release" link skipped them.

### Project
- New README with screenshots, a download guide, how-to steps and an FAQ.
- SECURITY.md points to the Security button under Contact & Feedback.

## [0.8.26 Dev] - 2026-10-04

### Cleaner
- Sort By Default now lists the ticked items first in each group, in A to Z order, followed by the unticked items A to Z. Groups keep their order: Windows first, then each program A to Z, and Advanced Cleaning last.
- The list re-sorts when Kodiak Cleaner opens, after Reset to Default, and whenever you click Default (even when it's already selected), so rows don't jump while you're ticking them.

## [0.8.25 Dev] - 2026-10-04

### Why It Was Made
- The story now opens "For well over two decades, I used the same cleaning app." (Ted's change.)

## [0.8.24 Dev] - 2026-10-04

### Look and Feel
- Title Case checked across Kodiak Cleaner: folder pickers, the Erase Entire Drive prompt, the For column in Programs (All Users) and the Startup folder locations now use it too, and a test now checks labels set in code as well.
- Why It Was Made uses larger text, so the story fills more of the page.

### Getting Started
- The Features list now includes everything added recently (Advanced Cleaning, Scheduled Cleaning, Search and Sort, Startup and Programs History, extension icons) and is in A to Z order.

### Contact & Feedback
- New Security button beside Send Feedback, Report a Bug and Request a Feature. It opens the security report form, still sent privately by email.
- Report a Bug, Request a Feature and Security now use the same accent color as Send Feedback.
- The small "Report a Security Problem Privately" link is gone; the Security button replaces it.

## [0.8.23 Dev] - 2026-10-04

### Cleaner
- Sort By now starts with Default, selected out of the box: Windows first, then each program A to Z, items A to Z in every group, and Advanced Cleaning last.
- A to Z is now plain alphabetical order, so Windows takes its alphabetical place among the programs. Everyone starts on Default after this update.

## [0.8.22 Dev] - 2026-10-04

### Programs
- New History button: a record of everything removed or changed on the Programs page, with the date: uninstalled programs, deleted entries, renames, and the leftover registry keys, files, folders, shortcuts and startup entries removed after an uninstall.
- Restore Selected puts records back: deleted entries, renames and leftover registry keys from their backups, leftover files and folders from the Recycle Bin (while they are still in it), and leftover startup entries from the Startup history. An uninstalled program is kept as a record only, since it can't be put back from here.
- Search, Select All and Forget Selected work as in the Startup history.

### Settings
- Back Up Settings now also saves the Programs history.

## [0.8.21 Dev] - 2026-10-04

### Cleaner
- Sort By above the list: A to Z (the default), Last Cleaned, Most Space Freed and Largest Found. Groups and the items inside them move to match, Advanced Cleaning stays last, and your choice is remembered.
- A new Last Cleaned column shows when each item was last cleaned (Today, Yesterday, 5 days ago or Never). Kodiak Cleaner now remembers when each item was cleaned and how much it freed, in KodiakCleaner.ini.
- New Schedule button: make named cleanings that run on chosen days, for example your browsers every week and Windows once a month. Pick weekly days or a day of the month, a time, and the items (with Basic Items, All Browsers, Same as Cleaner and Clear shortcuts, and a tick on each group heading for the whole group).
- Schedules use Windows Task Scheduler: it starts Kodiak Cleaner at that time, which cleans without a window and then closes, so nothing runs in the background. A schedule missed while the PC was off runs at the next chance. Run Now, Edit and Delete are in the same window.
- Items with a caution can't be scheduled. Items that need administrator rights are skipped unless the installed version's administrator turns on Run as Administrator for that schedule.

### Rules
- Anything added to any list from now on is placed in A to Z order.

## [0.8.20 Dev] - 2026-10-04

### Cleaner
- The list is now in alphabetical order: Windows comes first, then each program A to Z (Ablaze Floorp, Brave, Google Chrome and so on), with Advanced Cleaning last. The items inside every group, Windows and Advanced Cleaning included, are A to Z too.
- New Reset to Default button: after a confirm, it ticks only the basic items Kodiak Cleaner starts with and unticks everything else. It never ticks anything from Advanced Cleaning or anything with a caution.

## [0.8.19 Dev] - 2026-10-04

### Look and Feel
- Every pop-up (confirms, cautions, messages and problems) now uses Kodiak Cleaner's own window instead of the plain Windows box. It follows your color scheme, light or dark mode and accent color, with rounded corners and an icon that shows what kind of message it is.
- Cautions, like the Advanced Cleaning warning and the warnings on risky items such as Shadow Copies, show the risk in a highlighted box. The risky Yes button is red and No is the default, so pressing Enter is always the safe choice.
- Buttons can also be picked with their first letter (Y, N, C, O).

## [0.8.18 Dev] - 2026-10-04

### Cleaner
- New Advanced Cleaning items: Windows Icon Cache, Windows Internet History, Remote Desktop Cache, Windows Sidebar Cache, Downloaded Program Files, Offline Web Pages, Active Setup Temp Folders, Prefetch Data and Print Queue. Windows Log Files now includes Windows upgrade logs too.
- New Windows tasks under Advanced Cleaning, each using Windows' own tool: DNS Cache, Clipboard, Run (in Start Menu) (backed up first), BranchCache, Windows Update Cleanup (DISM), Windows Event Viewer Logs (each log saved to Backups first; the Security log is never cleared), Windows Shadow Copies (the newest is always kept), System Hibernation File (powercfg) and Compress System Installation (compact /compactos).
- Items that can cause harm show a red caution under their description, ask before they can be ticked, are skipped by Select All, and never run in the automatic cleans (Clean When Windows Starts, the jump list and the tray). They run only when you click Run Cleaner.
- New browsers: Opera, Opera GX and Waterfox.
- Left out on purpose: Game Statistics Files (no longer used since Windows 10), User File History (it's File History's own backup data), Windows Defender (its records are part of your protection, and Windows blocks changes to them), Windows Downloads (your own files), Windows Font Cache (locked by a Windows service), Windows Installer Cache (removing it breaks repairs, uninstalls and updates) and Windows MUI Cache (Registry already removes the stale entries).

## [0.8.17 Dev] - 2026-10-04

### Startup
- Add: pick a program or a shortcut and Kodiak Cleaner puts a shortcut to it in your Startup folder. A picked shortcut is moved there (or copied, if you turn Move the Shortcut off). Administrators can add it for everyone on the PC.
- Startup History: Kodiak Cleaner records every startup entry it sees, each time the Startup page is opened or refreshed (it never watches in the background). The History button lists them all with when each was first seen and when it was removed, whether by Kodiak Cleaner or by something else. Tick removed entries and click Restore Selected to put them back, on or off as they were. Forget Selected drops records you don't need.
- Before removing an entry, Kodiak Cleaner backs it up (registry entries to a .reg file, shortcuts to the Recycle Bin and a saved copy) and records it in the history, including entries removed by Uninstall's leftovers step.
- Shortcuts show their program's icon.

### Cleaner
- Opening Advanced Cleaning first shows a warning that cleaning these items can harm your computer or make a problem harder to troubleshoot, and asks you to be mindful. No is the default.

### Browser Extensions
- Each extension shows its own icon beside its name, and each browser shows the installed browser's icon. Both are read from files already on your PC.
- Extensions in Helium, Ablaze Floorp and Mullvad Browser are listed too.
- The search box matches the other pages.

### Settings
- Back Up Settings also saves a dated copy of the Startup History.

## [0.8.16 Dev] - 2026-10-04

### Cleaner
- Each browser shows its own icon beside its name, read from the browser installed on your PC. Windows gets the File Explorer icon. A browser whose program can't be found shows a plain placeholder, and no logos are bundled with Kodiak Cleaner.
- A search box above the list finds items by name, browser or description. Select All ticks only what the search shows.
- New browsers: Ablaze Floorp (cache and cookies), Helium (cache, history and cookies) and Mullvad Browser (cache and cookies, installed or on the desktop). Each is listed only when it's on your PC. Helium uses the same program name as Google Chrome, so it is skipped while either is open.
- Advanced Cleaning: a new button shows deeper temporary-file items: temp folders of Windows services, temp files of Microsoft Store apps, the Windows internet cache, Windows log files more than 7 days old, Live Kernel Reports and graphics driver shader caches. None are ticked until you choose them, and hiding Advanced Cleaning unticks them again.
- After an analysis, Kodiak Cleaner quietly measures Advanced Cleaning's items. It suggests opening it only when they would free at least 1 GB, or at least 200 MB while the Windows drive is nearly full (under 10% or 10 GB free). Otherwise it says nothing.
- Results say "1 file" instead of "1 files".

### Startup
- Every entry shows the icon of the program it starts. Entries whose program is missing show a plain placeholder.
- A search box above the list finds entries by name, command or location. Select All ticks only what the search shows.

## [0.8.15 Dev] - 2026-10-04

### Programs
- Every program shows its own icon next to its name, the same one Windows' Apps list uses where it can find it.
- Tick boxes and Select All: tick several programs and click Uninstall to remove them one at a time. Each one gets the full routine (registry backup, its own uninstaller, then the leftovers list) and finishes before the next starts. At the end you get a summary of each. Select All ticks only the programs your search shows.
- Kodiak Cleaner now waits until an uninstaller has completely finished, including ones that hand over to a helper in the Temp folder or ask questions part way through, before it looks for leftovers.
- Windows Installer programs go straight to their uninstall instead of opening their change screen.
- If an uninstall is cancelled, you can stop the rest of the list or carry on with the next program.

## [0.8.14 Dev] - 2026-10-04

### Programs
- Uninstall now cleans up after itself. Kodiak Cleaner first backs up the program's registry entries to a .reg file and notes what it has installed, then runs the program's own uninstaller. Once the program is really gone, it looks again for leftover folders (Program Files, AppData, ProgramData), Start Menu and desktop shortcuts, registry keys and startup entries, and lists them for you to choose from. Folders and shortcuts go to the Recycle Bin, and registry keys and startup entries are backed up first.
- If an uninstaller hands over to another window and closes, Kodiak Cleaner waits for it, and asks before looking for leftovers. If the uninstall was cancelled, nothing else is touched.
- Leftovers are matched by the program's exact name only. Windows, shared folders such as Common Files, folders that hold another installed program, and names another program uses are never offered.

### Registry
- Backups now keep values of rare and nonstandard types exactly, so restoring a backup brings them back with the same type.

## [0.8.13 Dev] - 2026-10-04

### Cleaner
- Ticking or unticking an item is saved straight away, so Clean When Windows Starts, the jump list and the tray's Run Cleaner Now always clean what you see ticked.
- Empty folders that a program created in the last day are left alone when an item only cleans older files, just like the files themselves.
- Changing an age setting while an analysis runs no longer shows an error.

### Registry
- A folder Windows redirects for 32-bit programs (System32, SysWOW64) is checked in both places, and a file that can't be read is no longer reported as missing.
- Entries whose program path can't be worked out for sure (no quotes and no known file type) are no longer flagged as missing.
- If the backup can't be saved, nothing is changed.
- Backups of values that hold line breaks are written so they still restore.

### Startup and Programs
- Removing a startup entry also backs up whether it was on or off, and startup-folder entries are backed up too.
- Uninstall and Repair start the program's own uninstaller directly instead of going through a command prompt.

### Tools
- Empty Folders never lists or removes anything inside AppData, Windows or program folders, even when you search a folder that contains them.
- Empty Folders never offers your Desktop, Documents, Downloads, Pictures or other Windows folders for removal, even when they're empty. Empty folders inside them are still found.
- Disk Analyzer no longer shows repeated error messages when the folder box is cleared.
- Drive Wiper won't offer the drive that holds your user folder or Program Files, and checks that the drive is still the same one right before erasing it.
- Wipe Free Space clears anything left from an interrupted run first, and stops with a message if it couldn't write anything.
- Right-clicking empty space in a table no longer acts on the row that was selected before.

### Updates
- Automatic updates download the MSI ZIP that each release ships, check it against its published checksum and install it after Kodiak Cleaner has fully closed, so files are replaced right away instead of at the next restart.
- A check that couldn't reach GitHub no longer waits a full day before trying again.

### Start-up and Windows
- Restart as Administrator waits for the old window to close, so it reliably comes back elevated.
- Opening Kodiak Cleaner normally while an administrator copy is running no longer leaves a hidden copy behind.
- A window that was maximized comes back maximized from the system tray.
- Restore All Settings now resets the sidebar, animations and badge straight away.

### Text
- The Privacy Policy now lists every tool, the optional Windows entries a few features add, what uninstalling leaves behind and how update downloads are checked.
- Terms of Use describe what each part of Kodiak Cleaner changes, including scheduled tasks, programs and the Recycle Bin.
- Light wording edits to Why It Was Made, the README and the security policy.

## [0.8.12 Dev] - 2026-10-04

### Sidebar
- When the sidebar is collapsed, its icons are bigger and centered.
- Pointing at any sidebar entry shows its name, collapsed or not.
- Drive Wiper has a new icon, a drive with a bin on it, and Disk Analyzer now uses a pie chart, so the two no longer share an icon.

### Settings
- New Sidebar card under General: Adjustable Sidebar Width (off keeps the default width and hides the paperclip) and Show the Paperclip.

### About
- The About page now fills the window like every other page.

## [0.8.11 Dev] - 2026-10-04

### Sidebar
- Tools now start open. Click the Tools heading to fold them away; Kodiak Cleaner remembers the choice.
- The sidebar's width can be adjusted. Click the paperclip at the bottom right of the sidebar to stand it upright, then drag the sidebar's edge. Click it again to lay it on its side, which resets the default width and locks it there.
- New collapse button below the paperclip: it shrinks the sidebar to just the logo and the icons. Click it again to bring the names back.
- The Kodiak Cleaner name shrinks to fit when the sidebar is made narrower.

### Cleaner
- The checkbox column header now has a Select All box. Every Select All box shows a dash when only some rows are ticked.

### New Tools
- Large Files: lists the biggest files in a folder (50 MB, 100 MB, 500 MB or 1 GB and up). Tick files or right-click one to move it to the Recycle Bin, or open the folder it's in. Windows, program folders, and hidden or system files are skipped.
- Scheduled Tasks: lists the programs Windows runs on a schedule, with switches, batch buttons and a right-click menu to turn them off or on. Windows' own tasks are hidden unless Show Windows Tasks is on. Tasks are never deleted.
- System Information: Windows edition and build, processor, memory, drives and up time, with Copy to Clipboard for support questions. Read-only; nothing leaves your PC.
- Tools stay in alphabetical order, and Getting Started lists the new ones.

## [0.8.10 Dev] - 2026-10-04

### Look and Feel
- New Animations switch under Settings > Appearance > Motion. Off stops every animation, including the logo dance and moving progress bars.
- The Getting Started window shows a bigger logo that does a little dance when it opens, and again on hover or click.
- The Glacier tile's lock label now reads Supporter, so it no longer gets cut off.

### Registry
- The checkbox column header now has a Select All box.
- Right-click an issue and choose Fix This Issue to fix just that one (backed up first, like every fix).

### Help
- Why It Was Made opens with a short title, "Built Around What Matters".

### Developer
- The plan preview offers just Free and Supporter.

### Packages
- File names carry the channel once, at the end: KodiakCleaner-0.8.10-x64-msi-dev.zip and KodiakCleaner-0.8.10-x64-portable-dev.zip.

## [0.8.9 Dev] - 2026-10-04

### Look and Feel
- The bear logo does a little dance when you point at it or click it. It stays still when animation effects are off in Windows.
- The Kodiak Cleaner name in the sidebar is now the largest text in the app, and the sidebar is a little wider so it fits.
- Checkbox and on/off columns have a fixed width with no column divider, and their header boxes line up with the boxes below. Column dividers now show only in the header.

### Startup
- Right-click an entry to Enable, Disable or Remove From Startup.

### Settings
- New Close to System Tray switch: the close button hides Kodiak Cleaner by the clock instead of quitting. Use Exit in the tray icon's menu to quit.
- Settings are grouped into titled cards: General (When Kodiak Cleaner Starts, Window and System Tray, Windows Integration), Cleaning (Automatic Cleaning, Safety, Keep Recent Files), Updates (Check Now, Automatic Updates) and Advanced (Administrator Rights). Clean When Windows Starts moved to Cleaning.

## [0.8.8 Dev] - 2026-10-04

### Settings
- Advanced: new Always Start as Administrator switch. Kodiak Cleaner asks Windows for administrator rights every time it starts; decline the prompt and it starts normally. Skip the User Account Control Prompt (installed copies) does the same without the prompt.

### Installer
- The MSI now ships only inside a ZIP, never as a bare .msi file.

## [0.8.7 Dev] - 2026-10-04

### Look and Feel
- The name is just Kodiak Cleaner everywhere: the title bar, taskbar, Start menu, installed apps list, installer and messages. The version still shows the channel, for example "0.8.7 Dev" under the name in the sidebar, with a Dev chip on About.

## [0.8.6 Dev] - 2026-10-04

### Look and Feel
- The Kodiak Cleaner name and logo at the top of the sidebar are larger, so they stand out next to the page titles.
- Ted's name now reads Ted Tabaka everywhere, including the copyright and license.

### Startup
- The Beta Software notice says to use Kodiak Cleaner at your own risk, and that more testing is needed before every feature can be confirmed to work as intended.

### Settings
- Terms of Use has a new Beta Software: Use at Your Own Risk section.
- Developer: the Preview Plan explains the difference between Off and Free.

### Installer
- Kodiak Cleaner installs to Program Files\Kodiak Cleaner for every channel. Dev, Beta and Stable now replace each other instead of installing side by side. Installing 0.8.6 over 0.8.5 moves it out of the old Kodiak Cleaner Dev folder. Settings start fresh once, and any registry backups stay in the old folder's Backups (delete the old folder when you no longer need them).

## [0.8.5 Dev] - 2026-10-04

### New Logo
- Kodiak Cleaner has its own logo: a computer with bear ears, a bear face on the screen and a sparkle for a clean PC. It's on the app icon, the sidebar, About, Getting Started and the installer.

### Programs
- Right-click a program for Uninstall, Repair, Rename and Delete Entry; the same actions are buttons at the top.
- Repair fixes Windows Installer programs, or opens the program's own repair screen.
- Rename changes the name shown here and in Windows' Apps list. Delete Entry removes a leftover from the list without uninstalling. Both back up the entry to a .reg file first.
- Clicking the search box empties it so you can type a new search straight away.

### Registry
- Scan for Issues, Fix Selected, Back Up Now and Open Backups Folder sit in one row above the results.

### Startup
- Tick entries (or the box in the header for all of them), then Disable Selected or Enable Selected in one go. Remove Selected works on ticked entries too.
- The on/off switch moved to the right end of each row.

### Look and Feel
- Every table has alternating row colors, clearer row lines and a visible column divider that turns the accent color when you can drag it to resize.
- Tools are listed in alphabetical order.
- Cleaner and Registry options use Title Case too.

### Fixes
- Text in search and other boxes no longer starts too far to the right.
- The Rename box now follows the color scheme.

### Installer
- The MSI's welcome page and page headers show the Kodiak Cleaner bear.
- Each release also includes the MSI inside a ZIP, for browsers that block .msi downloads.

## [0.8.4 Dev] - 2026-10-04

### Startup
- A Beta Software notice appears each time Kodiak Cleaner starts, as a reminder that cleaning software in beta may delete information you didn't mean to remove. Turn on Don't Show This Again at Startup before closing it to stop it, or bring it back under Settings > General.

### Look and Feel
- Getting Started has a Features card listing everything Kodiak Cleaner can do.
- Appearance explains why each color scheme was chosen.

### Settings
- Why It Was Made: lightly edited, and it now says Kodiak Cleaner throughout.

### Installer
- The MSI installer is built with every release again, including Dev builds, and two errors that stopped it from building are fixed.

## [0.8.3 Dev] - 2026-10-04

### Look and Feel
- New Classic color scheme, between Honey and Glacier: the familiar greys and blue of the first versions. Free for everyone.
- Title Case on every button, switch and option label.
- The sidebar has a Cleaning heading, and Tools fold away until you click the heading (Kodiak Cleaner remembers your choice).

### Settings
- Contact & Feedback: Send Feedback, Report a Bug and Request a Feature open a form. Send Email opens your email app with the message ready to send.
- Privacy Policy is now a page under Help.
- Backups is split into Registry (with Back Up Now, which saves every area Kodiak Cleaner can change) and Application (back up or reset your settings file).
- The footer shows the copyright and the license on separate lines; Terms of Use stays under Help.

### About
- The Get in Touch card is gone. Getting Started and Project Page moved to a new Learn More card.

### Fixes
- Copying no longer fails with "OpenClipboard Failed" when another program is holding the clipboard.

## [0.8.2 Dev] - 2026-10-04
PCSweep is now **Kodiak Cleaner**: Not a Crap Cleaner. A Cleaner App, Built Around What Matters.

### Name
- New name everywhere: the window, Start menu, installer, files (KodiakCleaner.exe, KodiakCleaner.ini) and the GitHub project (ttabakagroup/kodiakcleaner).
- The slogan appears on About and Getting Started.

### Tools
- Disk Analyzer: see which folders and files take up the most space, drill into folders, and send big files to the Recycle Bin.
- Empty Folders: find folders with nothing in them and move them to the Recycle Bin. Windows, program folders and AppData are never searched.
- Browser Extensions: list the extensions in Chrome, Edge, Brave, Vivaldi and Firefox, with version and on/off state.

### Settings
- Why It Was Made: Ted's story behind Kodiak Cleaner, under Help (also linked from About).

## [0.8.1 Dev] - 2026-10-04

### Settings
- Accent colors are back: Windows (follows your Windows accent color), Blue, Teal, Green, Purple, Pink, Orange and Graphite, on top of the Honey or Glacier color scheme. The first swatch keeps the scheme's own accent.
- About is back in the main sidebar, just under Settings.

## [0.8.0 Dev] - 2026-10-04
First development build of Kodiak Cleaner.

### Cleaning
- Cleaner for Windows temporary files, the Recycle Bin, thumbnail cache, error reports, crash dumps, DirectX shader cache, Windows Update and Delivery Optimization caches and the recent documents list.
- Browser cleaning (cache, history, cookies) for Chrome, Edge, Brave, Vivaldi and Firefox, skipped while the browser is open.
- Analyze first to see sizes, then run the cleaner. Your selections are remembered.
- Registry scanner that backs up every fix to a .reg file before making it.
- Startup manager that turns programs on and off the same way Task Manager does.
- Installed programs list with search and uninstall.

### Tools
- Duplicate Finder: finds files with identical contents. Removed copies go to the Recycle Bin, and the last copy is never removed.
- System Restore: list, create and remove Windows restore points.
- Drive Wiper: overwrite free space, or securely erase a whole non-system drive.

### Settings
- Options & Settings with its own sidebar: General, Cleaning, Updates and Advanced, then Appearance, Supporter, About, Backups, Contact & Feedback and Terms of Use.
- Every setting is a switch, saved straight away to KodiakCleaner.ini next to KodiakCleaner.exe. Nothing is stored in AppData.
- Honey (warm cream and gold) and Glacier (calm blue-grey) color schemes, each in light and dark, following Windows by default.
- About page with the version and channel, your plan, what's new, a privacy summary and credits.
- Contact & Feedback opens a message in your own email app; nothing is sent automatically.
- Supporter page: a one-time thank-you on Ko-fi unlocks the Glacier theme and a Supporter Badge. Codes are checked on your PC only.
- Built-in Terms of Use and Privacy Policy that work offline.
- Getting Started window on first launch, and What's New after each update.

### Install
- MSI installer and portable ZIP. Dev, Beta and Stable builds install side by side.
- Optional update check against GitHub releases (off by default), with each download verified against its published SHA-256.
