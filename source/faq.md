# Frequently Asked Questions

## Does Kodiak Cleaner Send Anything Online?

No. Kodiak Cleaner sends no usage data, shows no ads and has no tracking. It only goes online to check for updates, and only when you click Check for Updates or turn on Check for Updates Automatically under Settings, Updates. Buttons such as Support on Ko-fi, Get Latest Driver and the Contact & Feedback buttons open your browser only when you click them. Integrity Check's Windows image repair is Windows' own DISM tool, which may download repair files from Windows Update.

## How Do I Check That a Download Isn't Damaged?

Open Hash Checker under Tools and drop the file on the page (or click Add Files). Copy the hash the download page lists into Expected Hashes, or drop the .sha256, .md5 or SHA256SUMS file that came with the download. Each file then shows Verified when it matches, or Doesn't Match when it differs from the one listed, which means it is damaged, changed or a different version. Press Ctrl+F to find a file by its name or any part of a hash. To let someone check a file you send or export, click it and choose Create Hash File: a small .sha256 file is saved beside it, and dropping that file here later shows Verified if the file hasn't changed. Hash Checker only reads your files and never sends them anywhere.

## How Do I Move Kodiak Cleaner to a New PC?

Under Settings, Application, click Back Up Settings. It saves a dated copy of your settings and your Programs and Startup histories in Backups\Settings. Install Kodiak Cleaner on the new PC, close it, then copy those files over KodiakCleaner.ini, Backups\Programs History\history.json and Backups\Startup History\history.json. A Supporter code is part of the settings, so it moves with them.

## How Do I Pin a Tool to Favorites?

Right-click any page in the sidebar and pick Pin to Favorites, or drag it up into Favorites. Favorites stay at the top of the sidebar while the Tools list below them scrolls. Right-click a favorite to unpin it or to move it up or down, or drag it to a new place (drag it down into Tools to unpin it). Under Settings, General, Favorites Order shows them in your own order or A to Z, and Restore Default Order puts Cleaner, Registry, Startup and Programs back at the top.

## How Do I Select Many Items at Once?

Every list works like File Explorer. Hold the mouse button anywhere on the list's card and drag: an outline follows the mouse, and every entry it covers is highlighted (dragging past the top or bottom keeps scrolling). Ctrl-click adds or removes one entry, and Shift-click highlights a whole range. Right-click acts on every highlighted entry, and Space ticks or unticks them all. In lists of tick boxes, such as Empty Folders, the outline ticks every entry it covers instead, and clicking anywhere on an entry ticks it. Start the drag on a ticked box to untick them instead.

## How Do I Undo a Change?

- Programs: Programs, History restores deleted entries, renames and removed leftovers. Uninstalls are only recorded; install the program again to get it back.
- Recycle Bin: files removed by Duplicate Finder, Large Files, Empty Folders and uninstall leftovers go to the Recycle Bin, so restore them from there.
- Registry: every fix is saved as a .reg file in Backups\Registry first. Double-click the file to put the keys back.
- Renamed files: Mass Rename, Undo Last Rename puts the old names back.
- Scheduled tasks: a task you delete is backed up first; Scheduled Tasks, Restore puts it back. A task that signs in with a saved password may not back up or restore, because Windows never gives out the password; Task Scheduler's Import Task can put it back if you type the password again.
- Startup: Startup, History restores entries you removed.
- Whole PC: System Restore creates restore points, and Driver Cleaner makes one before it removes drivers (except in Safe Mode, where Windows can't).

Files the Cleaner deletes, wiped free space and erased drives can't be brought back.

## How Do Supporter Codes Work?

Kodiak Cleaner is free, with every tool included. If you support it on Ko-fi with $10 or more, you receive a Supporter code by email. One supporter code covers up to 10 of your computers. Need more? Use the contact form at kodiakcleaner.app/contact/. Enter it under Settings, Supporter and click Apply Code. The code is checked on this PC only and is never sent anywhere. It unlocks the Classic, Glacier and Lagoon themes and a Supporter badge you can hide, and the Support link and startup reminders go away.

## How Do Updates Work?

Click Check for Updates on About, or under Settings, Updates. Kodiak Cleaner asks its website whether a newer version is out, and if there is one it downloads the installer, checks that the file is exactly the one the website lists, and installs it. Your settings, backups and Supporter code are kept. Check for Updates Automatically is off until you turn it on.

## Is Kodiak Cleaner Safe to Use?

Kodiak Cleaner only changes what you pick. Registry fixes, startup entries, scheduled tasks and drivers are backed up before they are changed or removed, and found files go to the Recycle Bin where possible. Advanced Cleaning shows a warning every time it opens, and Drive Wiper asks three times before it erases a drive. Kodiak Cleaner is still being tested, so look over each list before you click, and keep your own backups of anything important.

## What Does Reset to Default Keep?

Reset to Default, under Settings, Advanced, puts every setting, your sidebar Favorites and your Cleaner selections back to how they were at install. It keeps your Supporter code, your cleaning history, your scheduled cleanings and every backup, log and report.

## Where Are My Backups, Logs and Reports?

An installed copy keeps them in the Kodiak Cleaner folder in your Documents; a portable copy keeps them beside KodiakCleaner.exe. Settings, Application, Backups, Logs and Reports has an Open Folder button. Uninstalling Kodiak Cleaner asks whether to delete them.
