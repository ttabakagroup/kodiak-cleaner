# Kodiak Cleaner for Linux Changelog

## [0.8.19 Dev] - 2026-10-24

### Drive Wiper
- New Entire Drive mode beside Free Space Only, like the Windows edition: tick whole drives to erase before you give them away. Each drive shows its type (Hard Drive, SSD, NVMe SSD or Flash Drive), size, mount points, the method it will get and its status. Drives that hold Linux (/, /boot, /boot/efi, /home, /usr, /var), swap, your home folder or Kodiak Cleaner, or that are part of LVM, RAID or an open encrypted drive, can't be ticked, and say why.
- Wipe Type: Quick removes the partition tables and boot sectors only; Thorough writes zeros over hard drives and flash drives; Secure uses your choice of 1 Pass (Zeros), DoD 3 Passes, DoD 7 Passes or 30 Passes, and Verify the Last Pass reads every byte back. SSD and NVMe drives get their own firmware erase on Thorough and Secure (nvme-cli's crypto or block erase; a secure discard on other SSDs); a drive that offers none is overwritten with random data instead, and Kodiak Cleaner says so.
- After Wiping: Leave It Uninitialized Like a New Drive, or one partition (GPT or MBR) formatted exFAT, FAT32, NTFS or ext4 (only the ones whose tools are installed) with the name you choose. Ticking a drive picks the recommended format for its type, shown above the choices with why, and marked Recommended: flash drives MBR with FAT32 (32 GB or smaller) or exFAT, other USB drives GPT with exFAT, SSDs and internal hard drives GPT with ext4. Change it and Use Recommended puts it back. The choices stay visible but grayed out while the drive is to be left uninitialized. MBR stops at 2 TB and FAT32 at 32 GB.
- Three checks before anything is erased: a summary, then each drive's name typed in, then ERASE. One administrator password for the whole batch, and every drive is checked again as administrator right before it's erased (still the same drive, still allowed).
- Progress shows the pass, the percentage, time elapsed and time left. Stop works, and a stopped drive still has its partition data removed. If setting the drive up afterwards fails, the reason is shown, and a drive that won't take GPT is set up as MBR. Each erase is noted in the activity log.
- Free Space Only's Cancel is now Stop, like the Windows edition.

## [0.8.18 Dev] - 2026-10-24

### Accessibility
- Screen readers now name buttons that show an icon with their label, the Select All box, each row's tick box ("Select <name>"), and the search boxes and drop-downs on the Cleaner, Browser Extensions, Programs, Triggers and Scheduled Cleaning pages.

## [0.8.17 Dev] - 2026-10-24

### Sidebar
- Reset Tools Order also puts Favorites back to Cleaner, Triggers, Programs and Scheduled Cleaning (after asking first); any other tools you pinned go back to Tools. It's now a small centered button with a sort icon.

### Duplicate Finder
- Check All is now called Keep Original.
- The likely original (the copy Keep Original keeps) is listed first in each set.

## [0.8.16 Dev] - 2026-10-24

### All Lists
- Drag to select: hold the mouse button anywhere on a list and drag to highlight every row you pass. An outline in your accent color follows the mouse the whole time, and dragging past the top or bottom keeps scrolling. Ctrl-click and Shift-click highlight several rows too.
- Drag over tick boxes: press a tick box and drag across the others, and every box you pass gets the same tick. Space ticks or unticks every highlighted row at once.
- Right-click menus and buttons act on every highlighted row when nothing is ticked (for example Large Files' Move to Trash, Triggers' Turn Off, and Restore in Triggers History).
- Every list says what it shows while it's empty.
- Ticking is instant on long lists: Select All, Clear, Check All and Keep Newest or Oldest no longer freeze Duplicate Finder, Large Files or Empty Folders with thousands of rows (Select All on 15,000 rows went from about two minutes to a tenth of a second). The ticked count and size update as a running total.

### Everywhere
- Ctrl+F jumps to the search box on any page that has one.
- Draggable dividers: where a page has two areas side by side or one above the other, the grip between them can be dragged to give either more room. It's between the Cleaner's items and results, Disk Analyzer's folders and largest files, the Triggers and Programs lists and their details, Duplicate Finder's options and results, Hash Checker's hashes and files, and the Settings list and its pages. The grip turns your accent color under the mouse, Kodiak Cleaner remembers where you left it, a double-click puts it back, and Reset All Settings puts them all back.
- Hover details now use your color scheme, so their text is always readable.

### Sidebar
- The Cleaning heading is now Favorites, which stays put while the tools scroll. Pin any page to Favorites by right-clicking it or dragging it there, and drag or right-click to reorder or unpin. Reset Tools Order under the tools puts everything back A to Z.
- Settings > General > Sidebar: Favorites Order (Default Order or A to Z) and Restore Default Order (Cleaner, Triggers, Programs and Scheduled Cleaning back at the top).

### Settings
- Sort By at the top of the Settings list: Default (General first) or A to Z.
- Appearance: Why These Colors, Ted's reasons for Honey, Classic, Glacier and Lagoon, under the color schemes. The scheme tiles now wrap in a narrow window instead of pushing the page sideways.

### Tools
- New Hash Checker: add or drop files (or folders) to see their MD5, SHA-1, SHA-256, SHA-384 and SHA-512, worked out in one read. Paste hashes from a download page or load a checksum file (.sha256, .md5, SHA256SUMS and others): every file shows Verified, Doesn't Match or Not in the List, and a loaded list adds the files it names. Find a file by name or any part of a hash, copy any hash, open a file's details (double-click, or straight away when you drop one file), Create Hash File saves name.sha256 beside a file, and Save Checksum File writes a list sha256sum can check.

### Duplicate Finder
- New Check All button: ticks every copy except one in each set, keeping the copy without "Copy" or (1) in its name, outside Downloads, and the oldest. Nothing is removed until you click Remove Selected.
- Remove To: send removed copies to the Trash or move them to a folder you choose. Each move gets its own dated folder, sorted by the files' old folders, with Duplicate Report.csv listing the copy that stayed and where each duplicate was.
- Restore puts any moved duplicate back where it was.
- Clearer layout: Summary, Actions and Results each have their own place.

## [0.8.15 Dev] - 2026-10-23

### Interface
- The Cleaner's and Drive Wiper's progress bars now show how long the work has run (1:05 elapsed) and, once there's enough progress to tell, about how long is left (about 3 min left, less than a minute left), like the Windows edition.

## [0.8.14 Dev] - 2026-10-23

### Supporter
- Remove Code now asks first ("Remove Supporter Code?"), like the Windows edition. You can enter the code again at any time.

## [0.8.13 Dev] - 2026-10-23

### Supporter
- The Support on Ko-fi buttons now open the Kodiak Cleaner supporter item in Ted's Ko-fi Shop, so your supporter code comes for Kodiak Cleaner.

## [0.8.12 Dev] - 2026-10-23

### Supporter
- The Support card and the Terms of Use now say that one supporter code covers up to 10 of your computers.

## [0.8.11 Dev] - 2026-10-23

### Supporter
- The Support Kodiak Cleaner card now says that a one-time $10 or more unlocks the Supporter color schemes.

## [0.8.10 Dev] - 2026-10-23

### Supporter
- Supporter codes are now checked with Ted's own key, so real codes from Ko-fi work.

## [0.8.9 Dev] - 2026-10-22

### Appearance
- New Lagoon color scheme for Supporters: deep blue and clear water, in light and dark, next to Honey, Classic and Glacier in Settings > Appearance.

## [0.8.8 Dev] - 2026-10-11

### Donation Reminder
- The reminder now also shows after What's New closes, instead of being skipped on the first start after an update. Each start still counts once, starting at 1 on the very first start (new tests check this).

## [0.8.7 Dev] - 2026-10-10

### Settings
- Every Settings page now lists its cards A to Z, and the settings in each card A to Z. Reset stays the last card on General. Each switch keeps its own label and does what it did before.
- General: Settings Backup, Startup, Your Files, then Reset. Startup starts with Ask Me to Donate at Startup; Your Files lists Activity Log, Backups and History, Keep an Activity Log, then Settings.
- Cleaning: Administrator Items now comes before Before Cleaning, and Clean When You Sign In moved up to second.
- Triggers: Changes and Backups now comes before What to Show.
- Appearance: Accent Color, Color Scheme, Light or Dark, then Motion.
- Supporter: Support Kodiak Cleaner now comes before Supporter Code.

### Triggers
- Hover over an entry to see everything about it: its name, type, status, who it runs for, description, the full command, the program it starts, the service unit, the file it lives in, its notes, a warning for entries that are part of your system, and whether Kodiak Cleaner can change it. Details that are empty are left out.

### Programs
- Hover over a program to see everything about it: name, description, version, publisher (Snaps), where it came from (system package and package manager, Flatpak and its remote, or Snap), when it was installed or last updated, size, where it lives, its menu entry, package name, app ID, the command that removes it, and any note. Details Kodiak Cleaner doesn't know are left out.

## [0.8.6 Dev] - 2026-10-10

### Donations
- Reset All Settings now starts the donation reminder's count again, so the reminder is back on and can be turned off after 10 more starts. Supporters keep their code and still never see it.
- On a start with the Beta Software notice, the donation pop-up now waits until the notice is closed instead of being skipped.

## [0.8.5 Dev] - 2026-10-10

### Donations
- A Help Keep Kodiak Cleaner Free pop-up at startup asks you to consider a donation on Ko-fi. Donate on Ko-fi opens Ko-fi in your browser; Not Now, the window's close button or Esc closes it. It never opens on top of What's New or the Beta Software notice.
- Don't Show This Again at Startup in the pop-up, and Ask Me to Donate at Startup in Settings > General, turn the reminder off. Both stay greyed out until Kodiak Cleaner has started 10 times, and say how many starts are left.
- On every 10th start a slim banner above the page asks for a donation, with Donate on Ko-fi and Dismiss.
- Supporters see neither: the reminder turns off as soon as a supporter code is entered.
- Only starts that open the window count; Clean Now and scheduled cleanings don't. Reset All Settings keeps the count (changed in 0.8.6).

## [0.8.4 Dev] - 2026-10-10

### Contact & Feedback
- Contact & Feedback now opens the contact form on kodiakcleaner.app in your browser instead of an email. Send Feedback, Report a Bug, Request a Feature and Security each pick the matching topic and fill in your Kodiak Cleaner version; nothing is sent until you press Send there. Copy Details copies the details for a bug report to paste into the form.

### Documents
- The Privacy Policy, Terms of Use, ReadMe and Security notes point to the contact form instead of an email address.

## [0.8.3 Dev] - 2026-10-09

### Cleaner
- Old Kernels now orders kernel versions by number, so backported kernels such as 6.12.12+bpo are no longer mistaken for older than 6.12.9+bpo (the newest kernel could have been removed), and a mix of kernel names no longer stops the scan.
- Old Log Files removes only rotated log files again: it no longer deletes empty log folders that programs such as nginx need, or links inside /var/log. Administrator cleanups now remove each file through the folder it was found in, so a folder swapped for a link mid-clean can't redirect a delete.
- A home folder reached through a link (for example /home on another drive) is now cleaned; before, nothing in it was found.
- A link where a Cleaner item points (for example ~/.cache/pip pointing at another folder) now stays in place, like the folders themselves.
- Files are checked again just before they go: one used again since the scan (with Keep Recent Temporary Files on), or no longer a plain file, stays.
- Temporary Files is skipped when Kodiak Cleaner runs as administrator, since every file in /tmp would count as its own, including those of running services.

### Triggers
- Removing a cron job no longer rewrites your crontab when it couldn't be read, or when the job is already gone; you see why instead.
- Service names starting with "-" are refused, and systemctl is told where its options end.

### Scheduled Cleaning
- Clean When You Sign In now works when Kodiak Cleaner's path holds a dollar sign, a double quote or a backtick (the autostart entry is quoted as the desktop file standard asks).

### Settings
- A damaged settings file that isn't UTF-8 no longer stops Kodiak Cleaner from starting; it starts with the default settings.

## [0.8.2 Dev] - 2026-10-06

### Programs
- New Programs page: a searchable list of the apps installed on this computer (system packages, Flatpak, Snap and your own menu entries) with icons, versions and sizes. Tick several and Uninstall removes them, asking for the administrator password only once. Parts of the system and desktop, and anything else that would be removed along with them, are protected.
- After an uninstall, Kodiak Cleaner offers the program's leftover folders in your home folder, matched only by their exact name. Nothing is ticked for you, and ticked folders go to the Trash.
- Programs History records everything uninstalled and can reinstall it, or restore one of your own menu entries. Repair reinstalls a program's system package to put back missing or damaged files.

### Scheduled Cleaning
- New Scheduled Cleaning page: named cleanings that run on chosen weekdays or a day of the month through systemd user timers, even when Kodiak Cleaner is closed. A cleaning missed while the computer was off runs at the next chance, and the timers remove themselves if Kodiak Cleaner is uninstalled. Administrator and Advanced Cleaning items never run on a schedule.
- Clean When You Sign In (Settings > Cleaning) cleans your ticked Cleaner items a minute after you sign in.
- A Clean Now action on Kodiak Cleaner's launcher (right-click it in your app menu or dock) cleans your ticked items without opening a window.

### Tools
- A new Tools section in the sidebar, A to Z.
- Browser Extensions: lists the extensions in Brave, Chrome, Chromium, Edge, Firefox, Floorp, Helium, LibreWolf, Mullvad Browser, Opera and Vivaldi (including Snap and Flatpak copies), with icons, versions and on or off state, so you can spot ones you don't recognize. Read-only.
- Disk Analyzer: see which folders and files take the most space, look inside any folder, and move big files to the Trash.
- Drive Wiper: overwrites the free space on your home drive or another drive you can write to (1, 3 or 7 passes), with a progress bar and Cancel, and warns that TRIM is the better tool on SSDs and btrfs or ZFS.
- Duplicate Finder and Large Files: find identical copies or very big files and move the ones you pick to the Trash. One copy of each file always stays.
- Empty Folders: find folders left behind with nothing in them and move them to the Trash. Your own folders, such as Documents, and hidden folders are never offered.
- System Information: distribution, kernel, desktop, processor, memory, graphics, drives and up time on one page, with Copy to Clipboard.

### Backups and Logs
- New activity log: one plain-text file per month in ~/.local/share/kodiak-cleaner/Logs noting cleanings, trigger and program changes, backups and errors. It stays on this computer, keeps the last 12 months, and can be turned off under Settings > General > Keep an Activity Log.
- Settings > General has Back Up Settings and Restore Settings, and shows where your backups and the activity log are, with Open Folder.

## [0.8.1 Dev] - 2026-10-05

### Appearance
- Honey, Classic and Glacier now use the exact colors from Ted's updated build standard, and every color scheme passes the WCAG 2.2 AA contrast check in light and dark, with its own accent and with every accent color. The keyboard focus ring has its own color in each scheme.
- Classic is now a Supporter theme, like Glacier. Honey stays free for everyone, and a copy without a supporter code that had Classic chosen shows Honey.
- Color Scheme tiles read "Warm cream and gold", "Crisp white and blue" and "Calm blue-grey", and locked tiles show a "Supporter Theme" chip.
- Accent colors are Blue, Teal, Green, Purple, Pink, Orange and Slate (Graphite is now Slate), after the color scheme's own accent. They stay free for everyone. Each one is shifted lighter or darker per scheme and mode so text and buttons stay readable, and the swatch shows the shade you'll get.
- The color scheme's own accent is always the first swatch. Classic's own accent is blue, so Classic has no second Blue swatch; its first swatch is named "Theme Accent (Blue)".
- The Accent Color card now sits right under Color Scheme. Swatches are round with a thin border so they show on any card, and the chosen one has a ring and a checkmark.
- Text boxes use the scheme's control border color, so their edges are easier to see.

### Packages
- The downloads are now named KodiakCleaner-Dev-AppImage.zip (with KodiakCleaner-Dev-x86_64.AppImage inside) and KodiakCleaner-Dev-Deb.zip (with KodiakCleaner-Dev-amd64.deb inside), the same names every release, so a download link always gets the newest build.
- Both ZIPs have a Docs folder with Changelog.txt, Download.url, Homepage.url, License.txt, Privacy.txt, ReadMe.txt and TermsOfUse.txt, all plain text. The .deb installs the same folder in /usr/lib/kodiak-cleaner/Docs, and the AppImage carries it too. They replace CHANGELOG.md, README.md, LICENSE, PRIVACY.md, HOMEPAGE.txt, DOWNLOAD.txt and INSTALL.txt.
- ReadMe.txt explains what Kodiak Cleaner needs, installing, updating without losing settings or history, uninstalling and privacy in plain words.
- Download.url downloads the newest Dev build straight from GitHub.

## [0.8.0 Dev] - 2026-10-05

### Added
- **Cleaner:** system junk (thumbnails, Trash, your own temporary files, recent files list, shell history), journal logs, crash reports and core dumps; APT, DNF, Pacman and Zypper package caches; unused Flatpak runtimes and app caches; old Snap revisions and the Snap download cache; browser cache, cookies, history and sessions for Brave, Chromium, Firefox, Floorp, Google Chrome, Helium, LibreWolf, Microsoft Edge, Mullvad Browser, Opera, Thunderbird (cache) and Vivaldi; Discord, Go, npm, pip, Slack, Spotify, Visual Studio Code and Yarn caches.
- **Advanced Cleaning:** old kernels, unused packages, old log files and the entire cache folder, hidden behind a warning, never ticked for you, each with a caution.
- **Administrator items:** one password prompt per clean, through your desktop's own polkit prompt and a helper that only runs a fixed list of jobs.
- **Triggers:** one list of autostart entries, systemd services, timers, sockets and path units (yours and the system's), cron jobs, shell and login startup files, and device rules that run programs. Notes flag missing programs, temporary or hidden folders, recent changes, code downloaded and run, and jobs that run at every start.
- **Triggers changes:** turn entries on or off, remove your own, add an autostart entry, and restore anything from Triggers History. Every change is backed up first.
- **Look and feel:** Honey (default), Classic and Glacier color schemes, light or dark or match device, accent colors and an Animations switch.
- **Settings, About, Contact & Feedback, Changelog, Privacy Policy and Terms of Use** pages, a Beta Software notice and What's New after updates.
- **Supporter:** offline supporter codes (the same codes as the Windows edition) unlock Glacier and the badge.
- **Packages:** an AppImage and a .deb, each delivered inside a ZIP.
