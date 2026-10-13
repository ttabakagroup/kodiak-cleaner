import { icons } from "../icons.mjs";
import { shot, betaNotice, pageHead } from "../layout.mjs";

const az = (list) => [...list].sort((a, b) => a.localeCompare(b, "en", { sensitivity: "base" }));
const ul = (items) => `<ul>${items.map((i) => `<li>${i}</li>`).join("")}</ul>`;

export default function features(ctx) {
  const windowsItems = az(["Delivery Optimization Cache", "Desktop Shortcuts (Broken Ones)", "DirectX Shader Cache", "Game Explorer Cache", "Memory Dumps", "Recent Documents List", "Recycle Bin", "Start Menu Shortcuts (Broken Ones)", "Temporary Files (User)", "Temporary Files (Windows)", "Thumbnail Cache", "Windows Error Reports", "Windows Notifications", "Windows Update Download Cache", "Windows Widgets Cache"]);
  const programGroups = az(["AMD Software", "AnyDesk", "Discord", "FileZilla", "Microsoft OneDrive", "Node.js", "NVIDIA", "Steam", "Telegram Desktop", "VLC Media Player", "WhatsApp", "Zoom"]);
  const browsers = az(["Ablaze Floorp", "Brave", "Google Chrome", "Helium", "Microsoft Edge", "Mozilla Firefox", "Mullvad Browser", "Opera", "Opera GX", "Vivaldi", "Waterfox"]);
  const advancedItems = az(["7-Zip History", "Active Setup Temp Folders", "Downloaded Program Files", "Downloads Folder (by Kind)", "Driver Installation Log Files", "Environment Path (Missing Folders Only)", "Graphics Driver Shader Caches", "IIS Log Files", "Live Kernel Reports", "Menu Order Cache", "Offline Web Pages", "Old Chkdsk Files", "Other Explorer Lists", "Prefetch Data", "Print Queue", "Recently Typed URLs", "Registry Editor Last Key", "Remote Desktop Cache", "Saved Browser Passwords and Form Data", "Taskbar Jump Lists", "Temporary Files (Store Apps)", "Temporary Files (Windows Services)", "User Assist History", "Window Size and Location Cache", "Windows Event Trace Logs", "Windows Icon Cache", "Windows Internet Cache", "Windows Internet Cookies", "Windows Internet History", "Windows Log Files", "Windows Sidebar Cache", "Wipe Free Space"]);
  const windowsTasks = az(["BranchCache", "Clipboard", "Compress System Installation", "DNS Cache", "Run (in Start Menu)", "System Hibernation File", "Windows Event Viewer Logs", "Windows Shadow Copies", "Windows Update Cleanup"]);
  const registryChecks = az(["ActiveX and Class Issues", "Application Paths", "Applications", "Fonts", "Help Files", "Installer", "Missing Shared DLLs", "MUI Cache", "Obsolete Software", "Run At Startup", "Sound Events", "Start Menu Ordering", "Type Libraries", "Unused File Extensions", "Windows Services"]);
  const chips = (list) => `<div class="chips">${list.map((x) => `<span class="chip">${x}</span>`).join("")}</div>`;

  // Cleaning pages, A to Z.
  const cleaning = [
    { id: "advanced-cleaning", icon: "sparkle", title: "Advanced Cleaning", img: ["advanced-cleaning.png", "Advanced Cleaning items with cautions"],
      text: `<p>Deeper items for when you need more room, shown only after a warning and never ticked for you. Items that can cause harm carry a caution, ask before they can be ticked, are skipped by Select All and never run on a schedule.</p>
      <p><strong>Items:</strong></p>${chips(advancedItems)}
      <p class="mt"><strong>Windows tasks</strong>, each using Windows' own tool:</p>${chips(windowsTasks)}
      <p class="mt">Registry items are backed up first, and the risky ones ask before they are ticked.</p>
      <p class="mt muted">After an analysis Kodiak Cleaner quietly measures these items, and only suggests them when they'd free at least 1 GB, or 200 MB on a nearly full drive.</p>` },
    { id: "cleaning", icon: "broom", title: "Cleaner", img: ["cleaner.png", "Cleaner page after Analyze"],
      text: `<p>The heart of Kodiak Cleaner: tick what you want, click Analyze to see what each item would free, then Run Cleaner.</p>
      <p><strong>Windows:</strong></p>${chips(windowsItems)}
      <p class="mt"><strong>Browsers</strong> (cache, history, cookies and sessions, each shown with its own icon; Chromium browsers also get Metrics Temp Files and Bookmarks Backup, and Firefox-based browsers Crash Reports, Site Preferences and Saved Form Information):</p>${chips(browsers)}
      <p class="mt"><strong>Programs</strong>, shown only when installed:</p>${chips(programGroups)}
      ${ul(["Search box for any item, browser or description.", "Sort By: Default, A to Z, Last Cleaned, Most Space Freed or Largest Found.", "A Last Cleaned column, and Reset to Default for the safe basics.", "Browsers are skipped while they're open, and files in use are skipped."])}` },
    { id: "programs", icon: "package", title: "Programs", img: ["programs.png", "Programs page with installed programs"],
      text: ul(["Searchable list of installed programs, each with its own icon.", "Uninstall several at once. Each one gets a registry backup, its own uninstaller, then a list of leftovers to remove.", "Waits until an uninstaller has completely finished, including ones that hand over to a helper.", "Repair, Rename and Delete Entry for broken or confusing listings (backed up first).", "History records everything removed or changed, and Restore Selected puts it back where it can."]) },
    { id: "registry", icon: "code", title: "Registry", img: ["registry.png", "Registry scan results"],
      text: `<p>Finds entries that point at files or programs that no longer exist. Fifteen checks, all ticked to start:</p>${chips(registryChecks)}${ul(["A Type column shows which check found each entry.", "Fix everything, a selection, or one entry with a right-click.", "Every fix is saved to a .reg backup first. Double-click a backup to undo.", "Only reports an entry when the file or folder it points to is definitely gone from a local drive. Files in the Windows folder, drivers and core Windows services are never reported.", "Entries that Windows itself protects are left alone, and 32-bit programs in Program Files (x86) are never reported as missing."])}` },
    { id: "scheduled-cleaning", icon: "calendar", title: "Scheduled Cleaning", img: ["schedule.png", "New Schedule window", "New Schedule"],
      text: ul(["Named cleanings, such as your browsers every week and Windows once a month.", "Pick weekdays or a day of the month, a time, and the items.", "Windows Task Scheduler starts Kodiak Cleaner, which cleans without a window and closes. Nothing runs in the background.", "A cleaning missed while the PC was off runs at the next chance.", "Items with a caution can't be scheduled."]) },
    { id: "startup", icon: "power", title: "Startup", img: ["startup.png", "Startup programs list"],
      text: ul(["Programs that start when you sign in, per user and for all users, each with its icon.", "Turn them off or on one at a time or in a batch, just like Task Manager.", "Add a program or shortcut to your Startup folder.", "Removed entries are backed up first, and History can restore them, on or off as they were."]) },
  ];

  // Tools, A to Z. "isNew" marks the ones added or rebuilt in 0.8.65 to 0.8.70.
  const tools = [
    { id: "boot-timer", icon: "clock", title: "Boot Timer", isNew: true, img: ["boot-timer.png", "Boot Timer showing how long a restart took and what slowed it"],
      text: ul(["Restart from Kodiak Cleaner and see how long shutting down, Windows starting and the desktop settling took.", "A timeline and your recent restarts side by side, so you can tell whether a change helped.", "What Slowed the Start lists the startup programs, drivers and services that held things up, slowest first.", "Show in Startup and Turn Off at Startup, right from the list."]) },
    { id: "browser-extensions", icon: "puzzle", title: "Browser Extensions", isNew: true, img: ["browser-extensions.png", "Browser Extensions listing extensions in Brave, Google Chrome and Microsoft Edge with on and off switches"],
      text: ul(["Every extension in Ablaze Floorp, Brave, Google Chrome, Helium, Microsoft Edge, Mozilla Firefox, Mullvad Browser and Vivaldi, with its icon, version and state.", "Turn an extension on or off with its switch, or right-click to remove it. Ctrl-click and Shift-click pick several.", "Chrome, Edge and Brave are changed through their own extension policies, the supported way to manage a browser, and the policy is removed again once nothing needs it.", "Firefox-based add-ons can be removed while that browser is closed."]) },
    { id: "defrag-and-trim", icon: "zap", title: "Defrag and TRIM", isNew: true, img: ["defrag-and-trim.png", "Defrag and TRIM listing each drive with its type and the right action"],
      text: ul(["Lists your drives with their type, file system, size and use.", "Hard drives get Defragment, SSD and NVMe drives get TRIM, and flash drives are left alone.", "Everything that needs it is ticked, so one click optimizes every drive the right way.", "Analyze shows how fragmented hard drives are. Uses Windows' own optimizer."]) },
    { id: "disk-analyzer", icon: "pie", title: "Disk Analyzer", isNew: true, img: ["disk-analyzer.png", "Disk Analyzer map of folders and files colored by kind, with the Contents list beside it"],
      text: ul(["Analyze All Drives, the drives you tick (network drives too) or one folder.", "A map of every folder and file, sized by the space it takes and colored by kind of file in shades of your own accent color.", "Hover a box for its size and path, click a folder to look inside, and go back with Backspace or the mouse's back button.", "Contents and Largest Files lists, and a right-click menu to Copy Path, Move to Recycle Bin or Show in File Explorer.", "Fast scan: run as administrator and whole NTFS drives are read straight from the drive's own file table, many times faster on a full drive."]) },
    { id: "drive-wiper", icon: "eraser", title: "Drive Wiper", isNew: true, img: ["drive-wiper.png", "Drive Wiper with several drives ticked, the Secure wipe type and format options"],
      text: ul(["Overwrite free space so deleted files can't be recovered, or erase whole drives you're giving away.", "Each drive's type is detected and wiped the right way: 1 Pass, DoD 3 or DoD 7 passes, or 30 passes for hard drives and flash drives, and the drive's own firmware erase for SSD and NVMe drives.", "Wipe Type: Quick, Thorough or Secure, with a read-back check of the last pass.", "Leave it uninitialized like a new drive, or format it as GPT or MBR with NTFS, exFAT or FAT32.", "Three checks before anything starts, and the drives holding Windows, Kodiak Cleaner, your user folder or programs can't be ticked."]) },
    { id: "driver-cleaner", icon: "chip", title: "Driver Cleaner", isNew: true, img: ["driver-cleaner.png", "Driver Cleaner listing NVIDIA driver packages, folders and registry keys to remove"],
      text: ul(["Removes an old AMD, Intel or NVIDIA graphics driver, or a Realtek audio driver, completely before a fresh install.", "A restore point and a backup of each driver package come first. Registry keys are backed up and folders go to the Recycle Bin.", "Restart in Safe Mode for the cleanest removal, and a switch stops Windows Update putting the old driver back.", "Get the Latest Driver opens the maker's own download page."]) },
    { id: "duplicate-finder", icon: "copy", title: "Duplicate Finder", isNew: true, img: ["duplicate-finder.png", "Duplicate Finder In-Depth mode listing files with similar names"],
      text: ul(["Finds identical files in folders or whole drives (size, quick hash, then full SHA-256). The last copy is never removed.", "In-Depth mode finds files that are alike, such as edited copies, re-saved pictures and older drafts, ranked by how alike they are.", "Match by similar names, leaving out copy marks, version words, dates or your own words, or type a name pattern such as \"Invoice #.pdf\".", "Verify Hashes confirms each set again before you remove anything."]) },
    { id: "empty-folders", icon: "folder", title: "Empty Folders", isNew: true, img: ["empty-folders.png", "Empty Folders after searching drive C, with the empty folders it found"],
      text: ul(["Search whole drives, one or several, or tick All Drives, as well as folders.", "Each drive shows its free space and a bar.", "Windows, program and AppData folders are never searched, and your Desktop, Documents and other Windows folders are never removed.", "Removed folders go to the Recycle Bin."]) },
    { id: "integrity-check", icon: "shield", title: "Integrity Check", isNew: true, img: ["integrity-check.png", "Integrity Check results for drive health, memory test and the Windows Component Store"],
      text: ul(["Check and repair Windows and your drives with Windows' own tools. Check Everything runs every check without changing anything.", "Windows System Files, the Windows Component Store and Drive File System, each with Repair where Windows can fix it.", "Drive Health and Drive Error History in plain words, and a Memory Test that runs at the next restart.", "Windows' own messages are kept under Details and in Reports."]) },
    { id: "large-files", icon: "file", title: "Large Files", img: ["large-files.png", "Large Files listing the biggest files in a folder"],
      text: ul(["Lists the biggest files in a folder (50 MB and up, your choice).", "Windows, program folders and hidden or system files are skipped.", "Removed files go to the Recycle Bin."]) },
    { id: "mass-rename", icon: "sliders", title: "Mass Rename", isNew: true, img: ["mass-rename.png", "Mass Rename preview showing each old name beside its new name"],
      text: ul(["Rename many files at once by a pattern: the old name, a counting number, the folder name, and the date created, modified or taken.", "Find and Replace (with regular expressions), letter case, extension and tidy spaces.", "Every new name is shown beside the old one first, and names that would clash are flagged and kept.", "Undo Last Rename puts the names back, even after Kodiak Cleaner is closed and opened again."]) },
    { id: "scheduled-tasks", icon: "tasks", title: "Scheduled Tasks", isNew: true,
      text: ul(["Lists the programs Windows runs on a schedule and turns them off or back on. Tasks are never deleted.", "A Program column shows what each task runs, with its icon. Click it to see the file in File Explorer.", "Hover over a task to see its command, publisher, author and description.", "Windows' own tasks are hidden unless you ask."]) },
    { id: "system-restore", icon: "restore", title: "System Restore", isNew: true, img: ["system-restore.png", "System Restore on the Point-in-Time Restore tab with its snapshot settings"],
      text: ul(["Lists, creates and removes restore points, and opens System Restore to roll back. Create Restore Point always makes a new point, even when Windows made one that day.", "Point-in-Time Restore (Windows 11 24H2 and later): turn it on, choose how often Windows takes a snapshot and how long it keeps them, Snapshot Now, and Restart to Recovery to restore.", "Settings are backed up before each change."]) },
  ];
  const newBadge = `<span class="new-badge">New</span>`;

  // Settings areas, A to Z.
  const settings = [
    ["Advanced", "Always start as administrator, and skip the User Account Control prompt (installed copies only)."],
    ["Application", "Shows where your backups, logs and reports are kept, with Open Folder, and the Keep an Activity Log switch."],
    ["Appearance", "Honey (free), Classic or Glacier (supporter themes) color scheme, an accent color for everyone (the scheme's own, your Windows accent, Blue, Teal, Green, Purple, Pink, Orange or Slate), Match Device, Light or Dark, and an Animations switch. Every combination meets WCAG 2.2 AA contrast."],
    ["Cleaning", "Clean when Windows starts, ask before cleaning, and keep recent temp and Recycle Bin files from the last 24 hours."],
    ["Contact & Feedback", "Send Feedback, Report a Bug, Request a Feature and Security, each opening the Contact Us form on this website."],
    ["General", "What's New after updates, the beta notice, minimize or close to the system tray, the sidebar width and paperclip lock, the Recycle Bin menu and Jump List tasks."],
    ["Protect", "Back Up Now saves every registry area Kodiak Cleaner can change into one .reg file, including fonts, help files, installer folders, sounds and Start menu ordering. Back up, open or reset the settings file."],
    ["Updates", "Check Now, or keep Kodiak Cleaner updated automatically (off by default). Downloads are checked against their SHA-256 checksum."],
  ];

  const safety = [
    "Browsers are skipped while they're open, and files in use are skipped.",
    "Cleaning folders are sanity-checked before anything is deleted.",
    "Kodiak Cleaner runs as a normal user and asks for administrator rights only for system-wide items.",
    "Program changes, registry fixes and startup removals are backed up first, and can be restored.",
    "Risky Advanced Cleaning items carry a caution, ask before they're ticked, and never run on a schedule.",
    "Symbolic links and junctions are never followed.",
    "Every pop-up puts the safe choice first, so pressing Enter never does the risky thing.",
    "Uninstalling leaves nothing behind, and asks before deleting your backups, logs and reports.",
  ];

  const body = `
${pageHead("Features", `Everything Kodiak Cleaner can do, from everyday cleaning to ${tools.length} built-in tools. Every feature is free, and the code is open source under the MIT License.`)}
<nav class="jump" aria-label="On this page"><div class="wrap chips">
  <a class="chip-link" href="/features/list/">Full Feature List</a>
  <a class="chip-link" href="#cleaning-pages">Cleaning</a>
  <a class="chip-link" href="#tools">Tools</a>
  <a class="chip-link" href="#system-information">System Information</a>
  <a class="chip-link" href="#settings">Settings</a>
  <a class="chip-link" href="#safety">Safety</a>
  <a class="chip-link" href="#privacy">Privacy</a>
  <a class="chip-link" href="/open-source/">Open Source</a>
  <a class="chip-link" href="/linux/">Linux Edition</a>
  <a class="chip-link" href="/mac/">Mac Edition</a>
</div></nav>

<section class="feature-group" id="cleaning-pages">
  <div class="wrap">
    <div class="section-head"><span class="eyebrow">Cleaning</span><h2>Clean, Fix and Tidy Up</h2><p class="lead">Six pages for the jobs a cleaner does most, each with backups or the Recycle Bin standing behind it.</p></div>
    ${cleaning.map((f) => `<article class="feature reveal" id="${f.id}">
      <div><h3><span class="icon">${icons[f.icon]}</span>${f.title}</h3>${f.text}</div>
      ${shot(ctx, f.img[0], f.img[1], { bar: f.img[2] || "Kodiak Cleaner" })}
    </article>`).join("\n    ")}
  </div>
</section>

<section class="alt feature-group" id="tools">
  <div class="wrap">
    <div class="section-head"><span class="eyebrow">Tools</span><h2>${tools.length} Built-In Tools</h2><p class="lead">Find what's taking space, spot what doesn't belong and look after Windows and your drives. Anything removed goes to the Recycle Bin or a backup first. Tools marked New were added or rebuilt in versions 0.8.65 to 0.8.70.</p></div>
    <nav class="chips tool-jump" aria-label="Tools">${tools.map((t) => `<a class="chip-link" href="#${t.id}">${t.title}</a>`).join("")}</nav>
    ${tools.map((t) => `<article class="feature reveal${t.img ? "" : " no-shot"}" id="${t.id}">
      <div><h3><span class="icon">${icons[t.icon]}</span>${t.title}${t.isNew ? newBadge : ""}</h3>${t.text}</div>
      ${t.img ? shot(ctx, t.img[0], t.img[1]) : ""}
    </article>`).join("\n    ")}
  </div>
</section>

<section class="feature-group" id="settings">
  <div class="wrap">
    <div class="section-head"><span class="eyebrow">Settings</span><h2>Settings and Personalization</h2><p class="lead">Every setting is a switch and is saved straight away, in a plain KodiakCleaner.ini file.</p></div>
    <div class="showcase even">
      <div class="table-wrap"><table><thead><tr><th scope="col">Area</th><th scope="col">What's There</th></tr></thead><tbody>
        ${settings.map(([a, d]) => `<tr><td><strong>${a}</strong></td><td>${d}</td></tr>`).join("")}
      </tbody></table></div>
      <div>${shot(ctx, "appearance.png", "Appearance settings with the Honey, Classic and Glacier color schemes")}<p class="muted mt">The sidebar can be widened, locked with the paperclip, or collapsed to icons only. Getting Started walks you through the basics the first time.</p></div>
    </div>
  </div>
</section>

<section class="feature-group" id="system-information">
  <div class="wrap">
    <div class="section-head"><span class="eyebrow">System Information</span><h2>Know Your PC</h2><p class="lead">Its own page at the bottom of the sidebar, between Settings and About. Read-only: nothing is changed.</p></div>
    <div class="grid cols-3">
      <div class="card reveal"><h3><span class="icon">${icons.info}</span>Summary</h3><p>Windows edition and build, processor, memory, drives and up time at a glance, with Copy to Clipboard.</p></div>
      <div class="card reveal"><h3><span class="icon">${icons.list}</span>Advanced</h3><p>Drive models and serial numbers, memory modules and slots, processor cores, threads and cache, motherboard and BIOS, UEFI, Secure Boot and TPM, graphics, network adapters (MAC, IP, gateway, DNS, DHCP), audio devices and temperatures where Windows shares them.</p></div>
      <div class="card reveal"><h3><span class="icon">${icons.file}</span>Export Report</h3><p>Save both views as Text, HTML or CSV in the Reports folder. The HTML report uses your current color scheme, and your choice of format is remembered.</p></div>
    </div>
  </div>
</section>

<section class="alt feature-group" id="safety">
  <div class="wrap">
    <div class="showcase flip">
      <div class="text"><span class="eyebrow">Safety</span><h2>Built-In Safety</h2><ul class="ticks">${safety.map((s) => `<li>${s}</li>`).join("")}</ul></div>
      ${shot(ctx, "advanced-cleaning-warning.png", "The warning shown before Advanced Cleaning opens")}
    </div>
  </div>
</section>

<section class="feature-group" id="privacy">
  <div class="wrap narrow">
    <div class="section-head center"><span class="eyebrow">Privacy</span><h2>Private and Local First</h2></div>
    <div class="grid cols-2">
      <div class="card"><span class="icon">${icons.shield}</span><h3>Nothing Collected</h3><p>No telemetry, analytics, crash reports, accounts or ads. Nothing is sent automatically, ever.</p></div>
      <div class="card"><span class="icon">${icons.lock}</span><h3>Your Files Stay Yours</h3><p>Settings, backups and the optional activity log are plain files on your own PC: in Documents\\Kodiak Cleaner for the installed version, or beside the program for the portable one. Never in the cloud.</p></div>
      <div class="card"><span class="icon">${icons.download}</span><h3>Updates Only When Asked</h3><p>Kodiak Cleaner only goes online to check for updates, when you click Check Now or turn on automatic updates.</p></div>
      <div class="card"><span class="icon">${icons.mail}</span><h3>Feedback When You Choose</h3><p>The feedback buttons in the app open the Contact Us form on this website. Nothing is sent until you press Send.</p></div>
    </div>
    <div class="card mt-xl"><span class="icon">${icons.file}</span><h3>Docs Folder Included</h3><p>The installer, the portable ZIP and the Mac and Linux downloads each come with a Docs folder: Changelog, Download, Homepage, License, Privacy, ReadMe and Terms of Use, all in plain text.</p></div>
    <div class="mt-xl">${shot(ctx, "application.png", "Application settings showing where backups and the activity log are kept")}</div>
    <div class="card mt-xl"><span class="icon">${icons.code}</span><h3>Open Source</h3><p>Kodiak Cleaner is free and open source under the MIT License: anyone may use it, read the code, change it and share it. <a href="/open-source/">More about open source</a>.</p></div>
    <div class="mt-xl">${betaNotice()}</div>
    <p class="center mt-xl"><a class="btn primary lg" href="/download/">${icons.download}Download Kodiak Cleaner</a></p>
  </div>
</section>`;

  return {
    id: "features",
    path: "/features/",
    out: "features/index.html",
    title: "Features",
    description: "Every Kodiak Cleaner feature: Cleaner, Advanced Cleaning, Registry with fifteen checks, Startup, Programs, Scheduled Cleaning, thirteen built-in tools including Boot Timer, Driver Cleaner, Integrity Check and Mass Rename, settings, safety and privacy. Free and open source.",
    body,
  };
}
