import { icons } from "../icons.mjs";
import { shot, betaNotice, pageHead } from "../layout.mjs";

const az = (list) => [...list].sort((a, b) => a.localeCompare(b, "en", { sensitivity: "base" }));
const ul = (items) => `<ul>${items.map((i) => `<li>${i}</li>`).join("")}</ul>`;

export default function features(ctx) {
  const windowsItems = az(["Delivery Optimization Cache", "DirectX Shader Cache", "Memory Dumps", "Recent Documents List", "Recycle Bin", "Temporary Files (User)", "Temporary Files (Windows)", "Thumbnail Cache", "Windows Error Reports", "Windows Update Download Cache"]);
  const browsers = az(["Ablaze Floorp", "Brave", "Google Chrome", "Helium", "Microsoft Edge", "Mozilla Firefox", "Mullvad Browser", "Opera", "Opera GX", "Vivaldi", "Waterfox"]);
  const advancedItems = az(["Active Setup Temp Folders", "Downloaded Program Files", "Graphics Driver Shader Caches", "Live Kernel Reports", "Offline Web Pages", "Prefetch Data", "Print Queue", "Remote Desktop Cache", "Temporary Files (Store Apps)", "Temporary Files (Windows Services)", "Windows Icon Cache", "Windows Internet Cache", "Windows Internet History", "Windows Log Files", "Windows Sidebar Cache"]);
  const windowsTasks = az(["BranchCache", "Clipboard", "Compress System Installation", "DNS Cache", "Run (in Start Menu)", "System Hibernation File", "Windows Event Viewer Logs", "Windows Shadow Copies", "Windows Update Cleanup"]);
  const registryChecks = az(["ActiveX and Class Issues", "Application Paths", "Applications", "Fonts", "Help Files", "Installer", "Missing Shared DLLs", "MUI Cache", "Obsolete Software", "Run At Startup", "Sound Events", "Start Menu Ordering", "Type Libraries", "Unused File Extensions", "Windows Services"]);
  const chips = (list) => `<div class="chips">${list.map((x) => `<span class="chip">${x}</span>`).join("")}</div>`;

  // Cleaning pages, A to Z.
  const cleaning = [
    { id: "advanced-cleaning", icon: "sparkle", title: "Advanced Cleaning", img: ["advanced-cleaning.png", "Advanced Cleaning items with cautions"],
      text: `<p>Deeper items for when you need more room, shown only after a warning and never ticked for you. Items that can cause harm carry a caution, ask before they can be ticked, are skipped by Select All and never run on a schedule.</p>
      <p><strong>Items:</strong></p>${chips(advancedItems)}
      <p class="mt"><strong>Windows tasks</strong>, each using Windows' own tool:</p>${chips(windowsTasks)}
      <p class="mt muted">After an analysis Kodiak Cleaner quietly measures these items, and only suggests them when they'd free at least 1 GB, or 200 MB on a nearly full drive.</p>` },
    { id: "cleaning", icon: "broom", title: "Cleaner", img: ["cleaner.png", "Cleaner page after Analyze"],
      text: `<p>The heart of Kodiak Cleaner: tick what you want, click Analyze to see what each item would free, then Run Cleaner.</p>
      <p><strong>Windows:</strong></p>${chips(windowsItems)}
      <p class="mt"><strong>Browsers</strong> (cache, history and cookies, each shown with its own icon):</p>${chips(browsers)}
      ${ul(["Search box for any item, browser or description.", "Sort By: Default, A to Z, Last Cleaned, Most Space Freed or Largest Found.", "A Last Cleaned column, and Reset to Default for the safe basics.", "Browsers are skipped while they're open, and files in use are skipped."])}` },
    { id: "programs", icon: "package", title: "Programs", img: ["programs.png", "Programs page with installed programs"],
      text: ul(["Searchable list of installed programs, each with its own icon.", "Uninstall several at once. Each one gets a registry backup, its own uninstaller, then a list of leftovers to remove.", "Waits until an uninstaller has completely finished, including ones that hand over to a helper.", "Repair, Rename and Delete Entry for broken or confusing listings (backed up first).", "History records everything removed or changed, and Restore Selected puts it back where it can."]) },
    { id: "registry", icon: "code", title: "Registry", img: ["registry.png", "Registry scan results"],
      text: `<p>Finds entries that point at files or programs that no longer exist. Fifteen checks, all ticked to start:</p>${chips(registryChecks)}${ul(["A Type column shows which check found each entry.", "Fix everything, a selection, or one entry with a right-click.", "Every fix is saved to a .reg backup first. Double-click a backup to undo.", "Only reports an entry when the file or folder it points to is definitely gone from a local drive. Files in the Windows folder, drivers and core Windows services are never reported."])}` },
    { id: "scheduled-cleaning", icon: "calendar", title: "Scheduled Cleaning", img: ["schedule.png", "New Schedule window", "New Schedule"],
      text: ul(["Named cleanings, such as your browsers every week and Windows once a month.", "Pick weekdays or a day of the month, a time, and the items.", "Windows Task Scheduler starts Kodiak Cleaner, which cleans without a window and closes. Nothing runs in the background.", "A cleaning missed while the PC was off runs at the next chance.", "Items with a caution can't be scheduled."]) },
    { id: "startup", icon: "power", title: "Startup", img: ["startup.png", "Startup programs list"],
      text: ul(["Programs that start when you sign in, per user and for all users, each with its icon.", "Turn them off or on one at a time or in a batch, just like Task Manager.", "Add a program or shortcut to your Startup folder.", "Removed entries are backed up first, and History can restore them, on or off as they were."]) },
  ];

  // Tools, A to Z.
  const tools = [
    ["puzzle", "Browser Extensions", "Lists extensions in Ablaze Floorp, Brave, Google Chrome, Helium, Microsoft Edge, Mozilla Firefox, Mullvad Browser and Vivaldi, with icons, versions and on or off state. Read-only, so you can spot ones you don't recognize."],
    ["pie", "Disk Analyzer", "Shows how much space each folder takes and its share of the total, plus the 100 largest files. Drill into any folder and send a file to the Recycle Bin."],
    ["eraser", "Drive Wiper", "Overwrites free space (1, 3 or 7 passes), or securely erases a whole non-system drive with Windows' own format /P. Refuses the Windows drive and Kodiak Cleaner's own drive."],
    ["copy", "Duplicate Finder", "Finds files with identical contents (size, quick hash, then full SHA-256). Removed copies go to the Recycle Bin, and the last copy is never removed."],
    ["folder", "Empty Folders", "Finds folders with nothing inside and moves the ones you pick to the Recycle Bin. Windows, program and AppData folders are never searched."],
    ["file", "Large Files", "Lists the biggest files in a folder (50 MB and up, your choice). Windows, program folders and hidden or system files are skipped."],
    ["tasks", "Scheduled Tasks", "Lists the programs Windows runs on a schedule and turns them off or back on. Windows' own tasks are hidden unless you ask, and tasks are never deleted."],
    ["info", "System Information", "Windows edition and build, processor, memory, drives and up time, with Copy to Clipboard. Read-only."],
    ["restore", "System Restore", "Lists, creates and removes restore points, and opens System Restore to roll back. The newest point can't be removed."],
  ];

  // Settings areas, A to Z.
  const settings = [
    ["Advanced", "Always start as administrator, and skip the User Account Control prompt (installed copies only)."],
    ["Application", "Shows where your backups and logs are kept, with Open Folder, and the Keep an Activity Log switch."],
    ["Appearance", "Honey (free), Classic or Glacier (supporter themes) color scheme, an accent color for everyone (the scheme's own, your Windows accent, Blue, Teal, Green, Purple, Pink, Orange or Slate), Match Device, Light or Dark, and an Animations switch. Every combination meets WCAG 2.2 AA contrast."],
    ["Cleaning", "Clean when Windows starts, ask before cleaning, and keep recent temp and Recycle Bin files from the last 24 hours."],
    ["Contact & Feedback", "Send Feedback, Report a Bug, Request a Feature and Security, each opening a short form that becomes an email in your own email app."],
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
    "Uninstalling leaves nothing behind, and asks before deleting your backups and logs.",
  ];

  const body = `
${pageHead("Features", "Everything Kodiak Cleaner can do, from everyday cleaning to nine built-in tools. Every feature is free.")}
<nav class="jump" aria-label="On this page"><div class="wrap chips">
  <a class="chip-link" href="#cleaning-pages">Cleaning</a>
  <a class="chip-link" href="#tools">Tools</a>
  <a class="chip-link" href="#settings">Settings</a>
  <a class="chip-link" href="#safety">Safety</a>
  <a class="chip-link" href="#privacy">Privacy</a>
  <a class="chip-link" href="/linux/">Linux Edition</a>
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
    <div class="section-head"><span class="eyebrow">Tools</span><h2>Nine Built-In Tools</h2><p class="lead">Find what's taking space, spot what doesn't belong and look after Windows. Anything removed goes to the Recycle Bin first.</p></div>
    <div class="grid cols-3 tool-grid">
      ${tools.map(([ic, t, d]) => `<div class="card hover reveal"><h3><span class="icon">${icons[ic]}</span>${t}</h3><p>${d}</p></div>`).join("\n      ")}
    </div>
    <div class="mt-xl reveal">${shot(ctx, "disk-analyzer.png", "Disk Analyzer showing folder sizes and the largest files")}</div>
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
      <div class="card"><span class="icon">${icons.download}</span><h3>Updates Only When Asked</h3><p>Kodiak Cleaner only goes online to check GitHub for updates, when you click Check Now or turn on automatic updates.</p></div>
      <div class="card"><span class="icon">${icons.mail}</span><h3>Feedback Through Your Email</h3><p>In-app feedback opens your own email app. Nothing is sent until you press Send.</p></div>
    </div>
    <div class="card mt-xl"><span class="icon">${icons.file}</span><h3>Docs Folder Included</h3><p>The installer, the portable ZIP and the Linux downloads each come with a Docs folder: Changelog, Download, Homepage, License, Privacy, ReadMe and Terms of Use, all in plain text.</p></div>
    <div class="mt-xl">${betaNotice()}</div>
    <p class="center mt-xl"><a class="btn primary lg" href="/download/">${icons.download}Download Kodiak Cleaner</a></p>
  </div>
</section>`;

  return {
    id: "features",
    path: "/features/",
    out: "features/index.html",
    title: "Features",
    description: "Every Kodiak Cleaner feature: Cleaner, Advanced Cleaning, Registry with fifteen checks, Startup, Programs, Scheduled Cleaning, nine built-in tools, settings, safety and privacy, plus the Linux edition.",
    body,
  };
}
