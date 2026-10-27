<div align="center">

<img src="../website/github/images/kodiak-cleaner-logo-512.png" alt="Kodiak Cleaner logo" width="128">

# Kodiak Cleaner

**Not a Crap Cleaner. A Cleaner App, Built Around What Matters.**

A small, honest cleaner for Windows. No telemetry, no ads, no upsells, no bundled offers and no background services.

[![Latest release](https://img.shields.io/github/v/release/ttabakagroup/kodiak-cleaner?include_prereleases&label=release&color=c08a2e)](https://github.com/ttabakagroup/kodiak-cleaner/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-6b4a2f)](LICENSE)
[![Platform: Windows 10 and 11](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011%20(x64)-2f6f9f)](#system-requirements)
[![Built with .NET 8](https://img.shields.io/badge/.NET-8-512bd4)](https://dotnet.microsoft.com/)
[![No telemetry](https://img.shields.io/badge/telemetry-none-5b8a3c)](#privacy)

[**Website**](https://www.kodiakcleaner.app/) ·
[**Download**](https://www.kodiakcleaner.app/download/) ·
[Features](#features) ·
[How to Use](#how-to-use) ·
[Screenshots](#screenshots) ·
[Privacy](#privacy) ·
[FAQ](#faq) ·
[Feedback](#feedback-and-support)

<a href="https://www.kodiakcleaner.app/video/kodiak-cleaner-video.mp4"><img src="../website/github/images/video.jpg" alt="Play the short Kodiak Cleaner video: Analyze, Review, Clean" width="720"></a>

**[▶ Watch the short video](https://www.kodiakcleaner.app/video/kodiak-cleaner-video.mp4)**

<img src="../website/github/images/cleaner.png" alt="Kodiak Cleaner's Cleaner page after Analyze, showing 6.8 GB that can be freed" width="900">

</div>

> [!WARNING]
> **Beta software: use at your own risk.** Kodiak Cleaner is in Beta, the last step before the first full release, and more testing is needed before every feature can be confirmed to work as intended. It deletes things by design, so check what's selected before you clean and keep your own backups of anything important.

## Contents

- [Why Kodiak Cleaner](#why-kodiak-cleaner)
- [Highlights](#highlights)
- [Download](#download)
- [Install, Update and Uninstall](#install-update-and-uninstall)
- [How to Use](#how-to-use)
- [Command Line](#command-line)
- [Features](#features)
- [Screenshots](#screenshots)
- [Privacy](#privacy)
- [Built-In Safety](#built-in-safety)
- [FAQ](#faq)
- [Feedback and Support](#feedback-and-support)
- [Building From Source](#building-from-source)
- [License](#license)

## Why Kodiak Cleaner

For more than two decades, Ted Tabaka used the same cleaning app. Then an upgrade made routine tasks harder and familiar
features harder to find, which inspired Kodiak Cleaner: an open source app focused on simplicity, privacy and practical functionality.

The idea is simple: open Kodiak Cleaner, choose what you want to clean, get it done, and get on with your day.
It is private first, local first, and built with security as a top priority. Read the full story in
[Why It Was Made](why-it-was-made.md).

## Highlights

- **Backups before changes.** Every registry fix, startup removal and program entry change is backed up first, and most removals go to the Recycle Bin.
- **Fast and familiar.** Analyze, review, clean. No waiting for a busy interface or hunting for moved buttons.
- **Free and open source.** Every cleaning tool is free, under the [MIT License](LICENSE).
- **Installed or portable.** Use the MSI installer or run the portable version from any folder, even a USB drive.
- **Local first.** Settings live in a plain `KodiakCleaner.ini` file next to the program; backups, the activity log and exported reports go in `Documents\Kodiak Cleaner`. Nothing goes in AppData.
- **No ads, no tracking.** No telemetry, analytics, crash reports or upsells. The only time it goes online is to check for updates, and only when you ask.
- **Your look.** Four color schemes (Honey free, Classic, Glacier and Lagoon for supporters), seven accent colors or your Windows accent (free for everyone, adjusted so text stays readable), light or dark mode.

## Download

Get the newest build from the **[download page](https://www.kodiakcleaner.app/download/)** on the website. Each release has two downloads:

| Download | File | Best for |
|----------|------|----------|
| **Installer** | `KodiakCleaner-<Channel>-Setup.msi`, for example `KodiakCleaner-Beta-Setup.msi` | Most people. Adds Start menu shortcuts, installs updates for you, and can skip the administrator prompt. |
| **Portable** | `KodiakCleaner-<Channel>-Portable.zip`, for example `KodiakCleaner-Beta-Portable.zip` | Running without installing, from any folder or a USB drive. Leaves no trace outside its own folder. |

Both are a single self-contained program, so there's nothing else to install. The file names stay the same every release (the version is in the release name), and the Portable ZIP has a `Docs` folder with the changelog, read me, license, privacy policy, Terms of Use and links to the website and the newest download, as plain text. The installer puts the same folder in `Program Files\Kodiak Cleaner\Docs`.

### System Requirements

- Windows 10 or Windows 11, 64-bit (x64)
- About 100 MB of free disk space
- Administrator rights only for system-wide items (Kodiak Cleaner asks when it needs them)

### Release Channels

| Channel | Versions | Shown as | Notes |
|---------|----------|----------|-------|
| Dev | 0.8.x | 0.8.121 Dev | Early builds. Ended with 0.8.121 Dev; Dev installs update to the Beta. |
| Beta | 0.9.x | 0.9.0 Beta | Feature-complete builds for wider testing, marked as pre-releases. Current channel. |
| Stable | 1.x | 1.0.0 | Full releases. |

Every channel installs to `Program Files\Kodiak Cleaner`, and installing one replaces another, keeping your settings, backups and Supporter code. In-app updates stay on their own channel.

## Install, Update and Uninstall

### Install

1. Download `KodiakCleaner-Beta-Setup.msi` from the [download page](https://www.kodiakcleaner.app/download/).
2. Double-click the `.msi` and follow the steps.

The installer isn't code-signed yet, so Windows SmartScreen may warn you the first time. Click **More info**, then **Run anyway**.

**Portable:** extract the **Portable** ZIP to any folder and run `KodiakCleaner.exe`.

### Update

- **Installed:** click **Check for Updates** on the **About** page or **Check Now** under **Settings > Updates**, or turn on Check for Updates Automatically there (it tells you when a new version is out) and, if you like, Download and Install Updates Automatically. You can also run a newer MSI over the old one. Your settings and backups are kept either way.
- **Portable:** replace `KodiakCleaner.exe` and the `Docs` folder with the new ones, and keep `KodiakCleaner.ini` and the `Backups` and `Logs` folders next to it.

To be extra safe, use **Settings > Application > Back Up Settings** before updating.

After an update, What's New shows everything that changed since your previous version. [CHANGELOG.md](CHANGELOG.md) starts fresh with the first Beta, 0.9.0; the Dev builds before it (0.8.0 to 0.8.121) are in [docs/history/dev-changelog.md](docs/history/dev-changelog.md), and **About > All Changes Since the Start** shows both.

### Uninstall

Uninstall Kodiak Cleaner from **Windows Settings > Apps**. It removes the program, its settings, its scheduled tasks and its other Windows entries, then asks whether to delete your backups, logs and reports in `Documents\Kodiak Cleaner` too. Keep them if you might still want to undo a registry fix. A silent `msiexec /x ... /qn` keeps them unless you add `DELETEUSERDATA=1`.

For the portable version, turn off **Clean When Windows Starts**, the Recycle Bin menu and any schedules, then delete its folder.

## How to Use

The first time Kodiak Cleaner starts, **Getting Started** walks you through the basics.

<p align="center"><img src="../website/github/images/getting-started.png" alt="Getting Started window" width="520"></p>

### Clean Your PC

1. Open **Cleaner** in the sidebar.
2. Tick what you want to clean. **Reset to Default** brings back the safe basics.
3. Click **Analyze** to see how much space each item would free. Nothing is deleted yet.
4. Click **Run Cleaner** to clean.

Tips:
- Use the search box to find any item, and **Sort By** to order the list (Default, A to Z, Last Cleaned, Most Space Freed or Largest Found).
- Browsers are skipped while they're open, so close them first.
- Some items need administrator rights. Pages that need them, or run faster with them, say what for at the top with a **Restart as Administrator** button, and a step that can't run offers the restart right away (Kodiak Cleaner opens again on the same page). Or turn on **Always Start as Administrator** under **Settings > Advanced**.

### Fix Registry Problems

1. Open **Registry** and click **Scan for Issues**.
2. Review the list, untick anything you want to keep, then click **Fix Selected**.

Every fix is saved to a `.reg` backup first. To undo a fix, open `Backups\Registry` (**Settings > Registry > Open Folder**) and double-click the backup.

### Speed Up Start-Up

1. Open **Startup** to see the programs that start when you sign in.
2. Turn a program off with its switch, or tick several (or Ctrl-click or Shift-click to highlight them) and click **Disable Selected**.

Turning a program off works exactly like Task Manager's Disable and can be undone at any time. **History** lists every entry Kodiak Cleaner has seen, so removed ones can be restored.

When a program adds itself to startup, Kodiak Cleaner tells you the next time it starts (or within 10 minutes while it's open or in the tray). The pop-up lists the new entries so you can **Keep**, **Disable** or **Delete** each one. Turn it off in **Settings > General > Tell Me About New Startup Programs**.

### Uninstall Programs

1. Open **Programs** and tick one or more programs, or Ctrl-click or Shift-click to highlight several and right-click them.
2. Click **Uninstall**. Kodiak Cleaner backs up the program's registry entries, runs its own uninstaller, then offers to remove the folders, shortcuts and registry keys it left behind.

Kodiak Cleaner never uninstalls itself or deletes its own entry. Remove it from **Control Panel > Programs and Features** (or **Settings > Apps**), as described under [Uninstall](#uninstall).

The **Product Code** column shows each Windows Installer program's GUID. Right-click a program and choose **Copy Product Code**, or **Copy Uninstall Command** for a ready-to-run `msiexec /x {GUID} /qn`.

### Schedule Cleanings

1. On the **Cleaner** page, click **Schedule**, then **New Schedule**.
2. Give it a name, pick the days and time, tick the items, and click **Save Schedule**.

Windows Task Scheduler starts Kodiak Cleaner at that time; it cleans without opening a window and then closes, so nothing runs in the background.

### Command Line

Kodiak Cleaner also runs from Command Prompt, PowerShell or a script, with no window, so it can be part of a batch file, a sign-in script or a remote management tool. The same help is built in: **Settings > Help > Command Line** lists every switch with a **Copy** button for each example (it copies the command with Kodiak Cleaner's full path), and `KodiakCleaner.exe /help` prints it.

Run the commands from Kodiak Cleaner's folder (`C:\Program Files\Kodiak Cleaner` for the installed copy, or wherever you put the portable one), or give the full path:

```bat
cd "C:\Program Files\Kodiak Cleaner"
KodiakCleaner.exe /list-items
```

#### Switches

| Command | What it does |
|---|---|
| `KodiakCleaner.exe /clean [/items:ID,ID]` | Cleans the items ticked on the Cleaner page with no window, or just the items listed in `/items:` (IDs from `/list-items`). Items with a caution never run from the command line, the same as scheduled cleanings. |
| `KodiakCleaner.exe /help` | Shows these switches. Started without a console (from Run or a shortcut), they show in a window instead. |
| `KodiakCleaner.exe /list-items` | Lists every Cleaner item ID, marking the ones that are ticked. |
| `KodiakCleaner.exe /list-programs` | Lists installed programs with their version and product code, for `/uninstall:`. |
| `KodiakCleaner.exe /registry [/scan-only] [/categories:"Fonts,Help Files"]` | Scans the registry (every check, or only the ones listed by name, as on the Registry page) and fixes what it finds. Every fix is backed up to a `.reg` file first. `/scan-only` lists the issues and changes nothing. |
| `KodiakCleaner.exe /uninstall:"Program Name" [/quiet]`<br>`KodiakCleaner.exe /uninstall:{PRODUCT-CODE-GUID} [/quiet]` | Backs up the program's registry entries, then runs its uninstaller and waits for it. A program's name must match exactly; when two have the same name, use the product code. `/quiet` asks it not to show anything (`msiexec /x {GUID} /qn`, or the program's own quiet uninstall). Leftovers aren't removed from the command line: use **Programs > Uninstall** for that. Kodiak Cleaner never uninstalls itself; remove it from Control Panel. |

Kodiak Cleaner sets these up in Windows by itself (they work typed by hand too):

| Command | What it does |
|---|---|
| `KodiakCleaner.exe /autoclean` | Cleans the ticked items with no window and no output: what **Clean When Windows Starts** and the jump list's **Run Cleaner** run. |
| `KodiakCleaner.exe /schedule:ID` | Runs one scheduled cleaning, as Windows Task Scheduler does at the time you picked. Make schedules on the Cleaner page with **Schedule**. |

Switches start with `/` or `-`, and take their value after `:` or `=` (`/items:ID` or `-items=ID`).

#### Examples

```bat
:: Clean what's ticked on the Cleaner page
KodiakCleaner.exe /clean

:: Clean only your temporary files and the Recycle Bin
KodiakCleaner.exe /clean /items:win.usertemp,win.recyclebin

:: List registry issues without changing anything
KodiakCleaner.exe /registry /scan-only

:: Fix only font and help file entries (each fix is backed up first)
KodiakCleaner.exe /registry /categories:"Fonts,Help Files"

:: Find a program's exact name or product code, then uninstall it quietly
KodiakCleaner.exe /list-programs
KodiakCleaner.exe /uninstall:"Program Name" /quiet

:: Open Kodiak Cleaner on Mass Rename
KodiakCleaner.exe --page=rename
```

#### Exit Codes

| Code | Meaning |
|---|---|
| `0` | Done. |
| `1` | Done, with some failures (an item that couldn't be fully cleaned, a fix that didn't work). |
| `2` | A wrong switch, nothing found by that name, or Kodiak Cleaner itself. |
| `3` | The uninstall didn't finish: the program is still installed. |

Kodiak Cleaner is a window program, so Command Prompt doesn't wait for it to finish. To wait and read the exit code:

```bat
start /wait KodiakCleaner.exe /clean
echo %ERRORLEVEL%
```

```powershell
$run = Start-Process KodiakCleaner.exe -ArgumentList '/clean' -Wait -PassThru
$run.ExitCode
```

#### Administrator Rights

Run Command Prompt or PowerShell as administrator to clean, fix or uninstall anything shared by all users (Windows temporary files, all-users startup entries, programs installed for everyone). Nothing asks for the rights from the command line; without them, those items are left as they are. Everything a command does is also written to the activity log in `Logs` (`Documents\Kodiak Cleaner\Logs` for an installed copy).

#### Open a Page

`KodiakCleaner.exe --page=KEY` opens Kodiak Cleaner on a page, or brings the open window there. This is what the jump list, Restart as Administrator and the Safe Mode and Boot Timer restarts use. A tool hidden in **Settings > Tools** still opens, and hiding a tool never stops the command line, scheduled cleanings or Clean When Windows Starts.

| Key | Page | Key | Page |
|---|---|---|---|
| `about` | About | `largefiles` | Large Files |
| `boottimer` | Boot Timer | `rename` | Mass Rename |
| `extensions` | Browser Extensions | `programs` | Programs |
| `cleaner` | Cleaner | `registry` | Registry |
| `optimize` | Defrag and TRIM | `tasks` | Scheduled Tasks |
| `disk` | Disk Analyzer | `settings` | Settings |
| `wiper` | Drive Wiper | `startup` | Startup |
| `drivers` | Driver Cleaner | `sysinfo` | System Information |
| `duplicates` | Duplicate Finder | `restore` | System Restore |
| `emptyfolders` | Empty Folders | | |
| `hash` | Hash Checker | | |
| `integrity` | Integrity Check | | |

Settings pages open the same way:

| Key | Page | Key | Page |
|---|---|---|---|
| `advanced` | Settings > Advanced | `backups` | Settings > Registry |
| `appearance` | Settings > Appearance | `supporter` | Settings > Supporter |
| `application` | Settings > Application | `terms` | Settings > Terms of Use |
| `cleaning` | Settings > Cleaning | `tools` | Settings > Tools |
| `commandline` | Settings > Command Line | `updates` | Settings > Updates |
| `feedback` | Settings > Contact & Feedback | `story` | Settings > Why It Was Made |
| `faq` | Settings > FAQ | | |
| `general` | Settings > General | | |
| `privacy` | Settings > Privacy Policy | | |

The installer and Kodiak Cleaner itself also use a few switches you don't need to type (`--uninstall-user`, `--uninstall-system`, `--elevated`, `/elevated`, `--restarted` and `--updated`).

## Features

### Cleaning

| Page | What it does |
|------|--------------|
| **Advanced Cleaning** | Deeper items, shown only after a warning: more temp folders and caches, old Windows logs, Prefetch, the print queue, and Windows tasks run with Windows' own tools (DNS cache, clipboard, Run history, Windows Update cleanup, event logs, older shadow copies, hibernation file, CompactOS). Never ticked by default. Risky items carry a caution, ask before they're ticked and never run on a schedule. |
| **Cleaner** | Windows temp files, Recycle Bin, thumbnail cache, error reports, crash dumps, DirectX shader cache, Windows Update and Delivery Optimization caches, and the recent documents list. Browser cache, history and cookies for Ablaze Floorp, Brave, Google Chrome, Helium, Microsoft Edge, Mozilla Firefox, Mullvad Browser, Opera, Opera GX, Vivaldi and Waterfox, each shown with the browser's own icon. Search, Sort By, a Last Cleaned column, and Reset to Default. |
| **Programs** | Searchable list of installed programs with their icons. Uninstall several at once (one at a time, with leftover removal), Repair, Rename and Delete Entry. A Product Code column with Copy Product Code and Copy Uninstall Command for administrators. History records everything removed or changed and can restore it. |
| **Registry** | Finds entries that point at files or programs that no longer exist, across 15 checks: ActiveX and class issues, applications, application paths, fonts, help files, installer, missing shared DLLs, MUI cache, obsolete software, run at startup, sound events, Start menu ordering, type libraries, unused file extensions and Windows services. Fix all, a selection, or Ctrl-click or Shift-click to highlight several and right-click them. Every fix is backed up first. |
| **Scheduled Cleaning** | Named cleanings that run on chosen weekdays or a day of the month, through Windows Task Scheduler. A cleaning missed while the PC was off runs at the next chance. |
| **Startup** | Programs that start when you sign in (per user and all users), each with its icon. Turn them off or on one at a time or in a batch, add a program to your Startup folder, remove entries (backed up first), and restore removed ones from History. A pop-up tells you when a program adds itself to startup. |

<table>
  <tr>
    <td><img src="../website/github/images/registry.png" alt="Registry scan results"></td>
    <td><img src="../website/github/images/startup.png" alt="Startup programs list"></td>
  </tr>
  <tr>
    <td align="center"><b>Registry</b></td>
    <td align="center"><b>Startup</b></td>
  </tr>
  <tr>
    <td><img src="../website/github/images/programs.png" alt="Installed programs list"></td>
    <td><img src="../website/github/images/advanced-cleaning.png" alt="Advanced Cleaning items with cautions"></td>
  </tr>
  <tr>
    <td align="center"><b>Programs</b></td>
    <td align="center"><b>Advanced Cleaning</b></td>
  </tr>
</table>

### Tools

| Tool | What it does |
|------|--------------|
| **Boot Timer** | Restarts the PC and times it: shutting down, Windows starting, and the desktop settling after sign-in. Then lists what slowed the start, slowest first (startup programs with their processor time and disk use, plus drivers, services and devices Windows itself found slow when run as administrator), with Show in Startup and Turn Off at Startup. Keeps the last 10 timings as a chart. |
| **Browser Extensions** | Lists the extensions in Ablaze Floorp, Brave, Google Chrome, Helium, Microsoft Edge, Mozilla Firefox, Mullvad Browser and Vivaldi, with their icons, versions and on or off state. Right-click to turn one off or on, or remove it, or use its On switch. Chrome, Edge and Brave are changed through their own extension policies (needs administrator); Firefox-based add-ons can be removed while the browser is closed. |
| **Defrag and TRIM** | Lists your drives with their type (Hard Drive, SSD, NVMe SSD, Flash Drive) and gives each the right step with Windows' own optimizer: hard drives are defragmented (Analyze shows how fragmented they are first), SSDs are trimmed, flash drives are left alone. Tick one drive or all of them. Needs administrator rights. |
| **Disk Analyzer** | Pick one drive, several, all of them or a folder, then explore a map of what's using the space, colored by kind of file. Click any folder to look inside, see which kinds of files fill it, list the largest files, and send a file to the Recycle Bin. Run as administrator, whole NTFS drives are read from the drive's file table in seconds. |
| **Drive Wiper** | Overwrites free space, or wipes whole drives (tick one or several). Wipe Type: Quick (the default) removes only the partition data, Thorough writes zeros over every sector, Secure uses DoD 5220.22-M (3 passes), DoD 5220.22-M ECE (7 passes) or 30 passes (the DoD pattern ten times; not a published standard); with Verify the Last Pass on, the last pass is read back and checked. SSDs and NVMe drives run their own Built-In Secure Erase (crypto or block erase, NIST SP 800-88 Purge) on Thorough and Secure: it wipes the data, not the drive's firmware, and the drive keeps working (a random overwrite when the drive doesn't offer one). Every wipe removes all partitions and the MBR or GPT; the drive is then left uninitialized like a new one, or set up as GPT or MBR with NTFS, exFAT or FAT32, named after the drive type (Flash Drive, SSD, Hard Drive, USB Drive) or with no name at all (Name the Drive is unticked by default; names keep to A to Z, numbers, spaces, hyphens and underscores). Three checks before anything starts: a summary, each drive's disk number, then the word ERASE. Refuses any disk that holds Windows, its start-up partition, the page file, Kodiak Cleaner, your user folder or programs, and refuses every disk when Windows doesn't say which disk one of those is on. A wipe that stops partway (a drive unplugged or a write error) says the drive was partly overwritten and needs wiping again. Check the Drive Afterwards (off by default) and Check Drive (read-only) check a drive's health: its own report through Windows, the disk errors Windows logged for it and a read of every sector for unreadable or slow areas (the wipe's read-back counts when Verify the Last Pass ran), with a Healthy, Warning or Failing verdict; Open Reports opens the saved reports. Entire Drive is laid out in four steps (Select the Drive, Wipe and Format, After the Wipe, Actions: the Wipe, Check Drive and Open Reports buttons), and a divider between the progress section and the settings can be dragged; once a wipe or check is done, the X in the progress section's corner hides it and the divider. Step 2 says which Wipe Type to choose for a drive you're giving away or selling (Secure or at least Thorough for hard drives, Thorough or Secure for SSDs, Secure for flash drives and memory cards), with Use Recommended for Giving Away. Click anywhere on a drive's row to tick it; ticking two or more shows a warning. The info button on each row opens Drive Details: model, serial number, type, connection, size, firmware, partition style and partitions, the drive's own health report (SMART values, temperature, wear, error counts) and the disk errors Windows logged, with Copy. |
| **Driver Cleaner** | Removes an old AMD, Intel or NVIDIA graphics driver (or a Realtek audio driver) completely before a fresh install: its driver store packages (with Windows' own `pnputil`), leftover folders (to the Recycle Bin) and registry keys (backed up first). Makes a restore point and backs up each package first, can restart into Safe Mode and open itself there, can stop Windows Update from putting a driver back, checks for BitLocker (shows or saves the recovery key, suspends protection for the Safe Mode restarts, opens Windows' BitLocker page), then offers Restart or Shut Down (to swap the card) and links to the maker's own driver page. |
| **Duplicate Finder** | Finds files with identical contents (size, quick hash, then full SHA-256) in folders or on whole drives (one, several or all; Windows, program folders and AppData are skipped). Run as administrator, a whole NTFS drive is listed straight from its file table, which is much faster than opening every folder, and only files of the same size are ever hashed. In-Depth also finds files that are alike (edited copies, re-saved pictures, older drafts), ranked by how alike they are. Name Matching can also list files by similar names alone, leave out copy marks, version words, dates, numbers or your own words, and gather files by name patterns you type (for example `Invoice #.pdf`). Every similar set is hash checked as part of the search: files with the same SHA-256 as another file in their set are marked Identical and can be ticked. Verify Hashes reads the files again and confirms each set by SHA-256. Only files with a matching SHA-256 can be ticked, and Keep Original ticks every copy but the likely original, which is listed first in each set. Results are a table with one row per copy (name, folder, size, modified, notes) under a heading for each set, the sets that free the most space first. Removed copies go to the Recycle Bin or to a folder you choose, sorted by their old folders with a report of where each came from and which copy stayed, and Restore puts any of them back. The last copy is never removed. |
| **Empty Folders** | Finds folders with nothing inside on whole drives (one, several or all) or in folders you pick, and moves the ones you choose to the Recycle Bin. Windows, program, AppData and hidden folders are never searched, and every account's personal folders are never offered. |
| **Hash Checker** | Works out the MD5, SHA-1, SHA-256, SHA-384 and SHA-512 of any files you add or drop on the page (folders too), in one read per file, and checks them against hashes you paste from a download page or load from a checksum file (.sha256, .md5, SHA256SUMS and the formats sha256sum, macOS, certutil and PowerShell write). Each file shows Verified, Doesn't Match or Not in the List; a loaded list adds the files it names from its folder. Dropping or adding a file opens everything about it (path, size, dates and all five hashes), also when Kodiak Cleaner runs as administrator. Ctrl+F finds a file by name or any part of a hash, the right-click menu copies any hash of the highlighted files, Create Hash File saves name.sha256 (or the shown kind) beside each file so it can be checked later or by whoever receives it, and Save Checksum File writes one list sha256sum can check. Files are only read, never changed or sent anywhere. |
| **Integrity Check** | Checks and repairs Windows and your drives with Windows' own tools, one card per check (A to Z): Drive Error History (drive and file system errors Windows logged in the last 30 days, explained in plain words), Drive File System (checks ticked drives while you work; Repair fixes them, the Windows drive at the next restart, with an optional Look for Bad Sectors), Drive Health (each drive's own report: failing or not, temperature, wear, uncorrected errors), Memory Test (opens Windows' Memory Diagnostic and shows its last result), Windows Component Store (check, and repair from Windows Update) and Windows System Files (check, and repair damaged or changed Windows files; if that fails it offers to repair the Component Store and try again). Check Everything runs every check without changing anything. Repairs ask first and can't be stopped halfway. Windows' messages show under Details and are saved in Reports\Integrity Check; Open Reports (at the top and beside the details) opens that folder. Most checks need administrator rights. |
| **Large Files** | Lists the biggest files in a folder (50 MB and up, your choice) and moves the ones you pick to the Recycle Bin. Windows, program folders, and hidden or system files are skipped. |
| **Mass Rename** | Renames a set of files in a folder (and its subfolders, if you like) by a pattern you set: the old name, a counting number, the folder name, date created, modified or taken, parts of the old name, plus Find and Replace (with regular expressions), letter case, a new or tidied extension and tidy spaces. Every new name is shown next to the old one before anything changes, names Windows wouldn't accept or that two files would share are flagged and skipped, and Undo Last Rename puts the names back. Windows, program folders and AppData are never offered. |
| **Scheduled Tasks** | Lists the programs Windows runs on a schedule, each with the program it starts and its icon (click to see it in File Explorer), and turns them off or back on. Delete backs each task up first, Back Up saves tasks on request, and Restore puts deleted tasks back. Windows' own tasks are hidden unless you show them. |
| **System Restore** | Lists, creates and removes restore points, and opens System Restore to roll back. The newest point can't be removed. Create Restore Point works even when Windows already made one that day, so you can make one right before a big install. The Point-in-Time Restore tab (Windows 11 24H2/25H2, June 2026 update or later) turns that feature on or off, sets how often Windows takes snapshots, how long it keeps them (72 hours at most) and how much space they may use, lists the snapshots, takes one now with Windows' own snapshot task, and restarts into Windows Recovery, where the restore itself happens. |

While a tool works you can open any other page: a small card in the bottom right corner shows what it's doing, how far along it is, how long it has run and about how long is left (click it to go back). Closing Kodiak Cleaner while a task is running asks first, with Keep Open as the default.

<p align="center"><img src="../website/github/images/disk-analyzer.png" alt="Disk Analyzer showing a map of folder sizes colored by kind of file" width="760"></p>

### System Information

System Information sits at the bottom of the sidebar, between Settings and About. It is read-only, and nothing it reads leaves your PC. It reads in the background with a progress bar and a Cancel button, so you can use other pages while it works.

- **Summary**: Windows edition, version and build, up time, processor, memory, drives, and how Kodiak Cleaner is running.
- **Advanced**: drive models, serial numbers, health and connection; memory type, speed, slots and each module's maker, part and serial number; processor cores, threads, speeds and L1/L2/L3 cache; PC model and serial number, motherboard, BIOS, UEFI, Secure Boot and TPM; graphics memory, driver and resolution; each network adapter's MAC, IPv4 and IPv6 addresses, gateway, DNS and DHCP; audio devices; a few Windows settings (antivirus, firewall, power plan, page file, Remote Desktop, UAC and more); and temperatures where Windows shares them.
- **Export Report** saves both views as a Text document, a themed HTML page or a CSV spreadsheet (pick one from the dropdown; the choice is remembered) in the `Reports` folder beside `Backups` and `Logs`. Reports include serial numbers and network addresses, so share them only with people you trust.

Left out on purpose: the Windows product key, nearby Wi-Fi networks, and per-core and SMART tables. Memory timings such as CAS latency can't be read without installing a driver, so they aren't shown.

<p align="center"><img src="../website/github/images/system-information.png" alt="System Information with Summary and Advanced views and Export Report" width="760"></p>

### Settings and Personalization

Every setting is a switch and is saved straight away. Pages are A to Z under each heading in the Settings sidebar, and Settings opens on General. Type in the search box above the pages to find a setting by name.

| Area | What's there |
|------|--------------|
| **Advanced** | Always start as administrator, and skip the User Account Control prompt (installed copies only). |
| **Appearance** | Honey, Classic, Glacier or Lagoon color scheme, an optional accent color, Match Device, Light or Dark, an Animations switch, and a Text Size slider (80% to 200%, on top of Windows' display scaling). |
| **Cleaning** | Clean when Windows starts, ask before cleaning, and keep recent temp and Recycle Bin files from the last 24 hours. |
| **Command Line** | Every command-line switch, exit code and `--page=` key, with a Copy button for each example (see [Command Line](#command-line)). |
| **Contact & Feedback** | Send Feedback, Report a Bug, Request a Feature and Security, each opening the Contact Us form on the website in your browser with the topic picked. |
| **FAQ** | Plain answers about going online, moving to a new PC, undoing a change, Supporter codes, updates, safety, Reset to Default and where backups are. Works offline. |
| **General** | What's New after updates, the Beta notice, minimize or close to the system tray, the sidebar width and paperclip lock, the Recycle Bin menu and Jump List tasks. |
| **Protect** | Back Up Now saves the registry areas Kodiak Cleaner can change into one `.reg` file (the very large ones are backed up with each fix instead), with an optional prompt before each registry fix. Back up, open or reset the settings file. |
| **Tools** | Every tool A to Z with a switch. Turn one off to hide it from Favorites and Tools in the sidebar; turn it back on and it returns to the same place. **Show Every Tool** brings them all back, and so does Reset to Default. A hidden tool leaves the jump list too, but still opens with `--page=`. |
| **Updates** | Check Now (also on the About page as Check for Updates). Check for Updates Automatically (off by default) looks for a new version as often as you pick (On Startup, Every Hour, Every 4 Hours, Daily, Every 3 Days, Weekly, Twice a Month or Monthly) and tells you when one is out. Download and Install Updates Automatically (off by default) also downloads it and installs it when you close Kodiak Cleaner. |

The sidebar can be widened, locked with the paperclip, or collapsed to icons only. Right-click a tool and pick **Hide from Sidebar**, or use **Settings > Tools**, to hide the tools you don't use.

<p align="center"><img src="../website/github/images/appearance.png" alt="Appearance settings with Honey, Classic, Glacier and Lagoon color schemes" width="760"></p>

## Screenshots

<table>
  <tr>
    <td><img src="../website/github/images/cleaner.png" alt="Cleaner in Honey light"></td>
    <td><img src="../website/github/images/cleaner-dark.png" alt="Cleaner in Honey dark"></td>
  </tr>
  <tr>
    <td align="center"><b>Honey</b> (light)</td>
    <td align="center"><b>Honey</b> (dark)</td>
  </tr>
  <tr>
    <td><img src="../website/github/images/programs-classic.png" alt="Programs in Classic"></td>
    <td><img src="../website/github/images/cleaner-glacier.png" alt="Cleaner in Glacier"></td>
  </tr>
  <tr>
    <td align="center"><b>Classic</b> (Supporter theme)</td>
    <td align="center"><b>Glacier</b> (Supporter theme)</td>
  </tr>
  <tr>
    <td><img src="../website/github/images/schedule.png" alt="New Schedule window"></td>
    <td><img src="../website/github/images/advanced-cleaning-warning.png" alt="Advanced Cleaning warning"></td>
  </tr>
  <tr>
    <td align="center"><b>Scheduled Cleaning</b></td>
    <td align="center"><b>Advanced Cleaning warning</b></td>
  </tr>
  <tr>
    <td><img src="../website/github/images/appearance-dark.png" alt="Appearance settings in dark mode"></td>
    <td><img src="../website/github/images/about.png" alt="About page"></td>
  </tr>
  <tr>
    <td align="center"><b>Appearance</b> (dark)</td>
    <td align="center"><b>About</b></td>
  </tr>
</table>

## Privacy

Everything stays on your PC.

- **Feedback** opens the Contact Us form on the website in your browser. Nothing is sent until you press Send Message there.
- **No remote resources.** The Fredoka font (SIL Open Font License) is built in, and icons come from Windows' own icon font.
- **No telemetry**, analytics, crash reports, accounts or ads. Nothing is sent automatically, ever.
- **Settings, backups and logs** are plain files: `KodiakCleaner.ini` next to `KodiakCleaner.exe`, and the `Backups`, `Logs` and `Reports` folders in `Documents\Kodiak Cleaner` (beside the exe for the portable version). The activity log stays on your PC and can be turned off.
- **Updates are the only network use.** Kodiak Cleaner reads one small update file from its website, www.kodiakcleaner.app, only when you click **Check for Updates** on the About page or **Check Now** in Settings, or on the schedule you pick if you turn on Check for Updates Automatically (off by default; on startup, every hour up to monthly). The installer comes from the same website and is checked against the SHA-256 checksum in that file before it's installed.

Read the full [Privacy Policy](PRIVACY.md) and [Terms of Use](terms-of-use.md).

## Built-In Safety

- Browsers are skipped while they're open, and files in use are skipped.
- Cleaning folders are sanity-checked before anything is deleted.
- Kodiak Cleaner runs as a normal user and asks for administrator rights only for system-wide items.
- The registry scanner never reports files in the Windows folder, drivers, or services that start with Windows' core.
- Program changes, registry fixes and startup removals are backed up first, and can be restored from History or by double-clicking the `.reg` backup.
- Risky Advanced Cleaning items carry a caution, ask before they're ticked, and never run on a schedule.
- Symbolic links and junctions are never followed.
- The registry scanner only reports a file as missing when it's on a present, fixed local drive.

## FAQ

**Can Kodiak Cleaner break my PC?**
It's designed to be careful (see [Built-In Safety](#built-in-safety)), but it's beta software that deletes things by design. Use the default items if you're unsure, read each description before ticking, and keep your own backups.

**Does it run in the background?**
No. There's no service and nothing running unless you open it. Scheduled cleanings are started by Windows Task Scheduler and close when they're done.

**Is it really free?**
Yes, with every feature. If it helps you, an optional one-time thank-you of $10 or more on [Ko-fi](https://ko-fi.com/s/aabd61b988) gets a supporter code that unlocks the Classic, Glacier and Lagoon color schemes and a Supporter badge. When it starts, Kodiak Cleaner shows a short, optional reminder that you can support it on Ko-fi, and a small banner on every 10th start. The pop-up can be turned off after 10 starts (in the pop-up or in **Settings > General**), and supporters never see either.

**Why does Windows show a SmartScreen warning?**
The installer isn't code-signed yet. Click **More info**, then **Run anyway**. You can check the source in this repository and build it yourself.

**Where are my settings?**
In `KodiakCleaner.ini` next to `KodiakCleaner.exe`. Backups, the activity log and reports are in `Documents\Kodiak Cleaner` (the portable version keeps them beside the exe). **Settings > Application** shows both places.

**Can I hide tools I don't use?**
Yes. Right-click a tool in the sidebar and pick **Hide from Sidebar**, or turn it off under **Settings > Tools**. It only leaves the sidebar: turn it back on and it returns to the same place, and the command line and scheduled cleanings keep working.

**Can I run it from a script?**
Yes. See [Command Line](#command-line) for every switch, the exit codes and the `--page=` keys, or **Settings > Help > Command Line** in Kodiak Cleaner.

**Do I need administrator rights for backups?**
No. Backups and logs go in your Documents folder, which you can always write to. Only some cleaning and registry areas need administrator rights.

## Feedback and Support

- **Feedback, bug reports and feature requests:** use **Settings > Contact & Feedback** in Kodiak Cleaner, or the [Contact Us form](https://www.kodiakcleaner.app/contact/) on the website.
- **Website:** [www.kodiakcleaner.app](https://www.kodiakcleaner.app/)
- **Say thanks:** [Support Kodiak Cleaner on Ko-fi](https://ko-fi.com/s/aabd61b988).
- **Security problems:** please report them privately. See [SECURITY.md](SECURITY.md).

Ideas, guidance and contributions are welcome. Kodiak Cleaner is open source so it can grow with the people who use it.

## Building From Source

You need Windows with the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). WiX v5 comes from NuGet.

```powershell
.\build.ps1            # the Setup MSI and Portable ZIP in dist\, named after the channel in version.json
.\build.ps1 -Release   # release build (tagged CI builds); Beta and Stable builds always need the supporter key and a CHANGELOG entry
dotnet test tests/KodiakCleaner.Core.Tests   # tests (run on any OS)
python3 tools/design/check_contrast.py tools/design/theme.css   # WCAG 2.2 AA check for every theme and accent
```

GitHub Actions builds every push. For a tag that matches `version.json`, it makes a release build and attaches the Setup MSI and the Portable ZIP to a GitHub release. Beta builds also replace the files in a fixed `latest-beta` release. While the repository's `version.json` says `"repositoryPublic": false`, `Docs\Download.url`, Project Page and Star on GitHub point at the website instead.

On Linux, publish the exe and run `bash installer/package-linux.sh publish dist`: it builds the same MSI under Wine and names both files after the channel.

### Project Layout

```
dist-files/             ReadMe.md, the source of the packages' Docs\ReadMe.txt
docs/history/           the Dev builds' changelog (0.8.0 to 0.8.121), shown in About
installer/              WiX v5 MSI project (build-msi-wine.sh and package-linux.sh build on Linux)
keys/                   supporter public keys and withdrawn code list
src/KodiakCleaner.App/  WPF app: views, view models and the Honey, Classic, Glacier and Lagoon themes
src/KodiakCleaner.Core/ cleaning, registry, startup, tools, updates and supporter logic (no UI)
store-assets/           privacy-policy.html (ready to host) and store screenshots
tests/                  unit tests and project checks
tools/container/        sets up a Linux container to build, test and screenshot under Wine (never shipped)
tools/design/           the build standard's theme.css, accent.js and check_contrast.py (never shipped)
tools/MakeDocs/         writes the seven Docs files for the packages (never shipped)
tools/SupporterCodes/   supporter code tool (never shipped)
build.ps1               builds the exe, the MSI and the portable ZIP
CHANGELOG.md            version history since the first Beta
CLAUDE.md               project rules for development sessions
Directory.Build.props   reads version.json for every project
KodiakCleaner.sln       solution file
LICENSE                 MIT License
PRIVACY.md              privacy policy
README.md               this page
SECURITY.md             how to report a security problem privately
faq.md                  FAQ (built into Settings > FAQ)
terms-of-use.md         Terms of Use
version.json            version, release channel and repositoryPublic (single source)
why-it-was-made.md      the story behind Kodiak Cleaner
```

## License

Kodiak Cleaner is free and open source under the [MIT License](LICENSE).
Copyright © 2026 Ted Tabaka.

<p align="center"><sub>Made by Ted Tabaka, independent developer.</sub></p>

