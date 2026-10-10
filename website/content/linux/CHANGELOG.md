# Kodiak Cleaner for Linux Changelog

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
