import { icons } from "../icons.mjs";
import { pageHead } from "../layout.mjs";
import { esc } from "../markdown.mjs";

// Every feature, marked Free or Supporter, with the editions that have it.
// Supporter means a supporter code unlocks it (see the app's Settings > Supporter page). Everything else is free.
// Groups and the features inside them are A to Z. W = Windows, M = Mac, L = Linux.
const S = "Supporter";
const GROUPS = [
  ["Appearance", [
    ["Accent Colors", "The color scheme's own accent, Blue, Teal, Green, Purple, Pink, Orange or Slate, plus your system's accent where the system has one.", "WML"],
    ["Animations Switch", "Turn the app's animations off.", "WML"],
    ["Classic Color Scheme", "Crisp white and blue, in light and dark.", "WML", S],
    ["Glacier Color Scheme", "Calm blue-grey, in light and dark.", "WML", S],
    ["Honey Color Scheme", "Warm cream and gold, in light and dark. The default.", "WML"],
    ["Light, Dark or Match Device", "Pick a mode, or follow your system.", "WML"],
  ]],
  ["Cleaning", [
    ["Advanced Cleaning", "Deeper items behind a warning, never ticked for you, with a caution on anything risky.", "WML"],
    ["Analyze Before Cleaning", "See what each item would free before anything is removed.", "WML"],
    ["Browser Cleaning", "Cache, cookies, history and sessions for the browsers found on your computer. Bookmarks always stay.", "WML"],
    ["Clean at Startup or Sign-In", "Clean your ticked items when Windows starts or when you sign in.", "WL"],
    ["Keep Recent Temporary Files", "Leave temporary files from the last 24 hours alone.", "WL"],
    ["Program Caches", "Caches from programs such as Discord, Slack, Spotify, Visual Studio Code and package managers.", "WML"],
    ["Scheduled Cleaning", "Named cleanings on chosen weekdays or a day of the month, even when Kodiak Cleaner is closed.", "WL"],
    ["Sort By", "Default, A to Z, Largest Found, Last Cleaned and Most Space Freed, with a Last Cleaned column.", "WML"],
    ["System Junk", "Temporary files, logs, crash reports, thumbnails, the Recycle Bin or Trash, and more for each system.", "WML"],
    ["Windows Tasks", "Windows Update Cleanup, DNS Cache, Shadow Copies and other jobs, each using Windows' own tool.", "W"],
  ]],
  ["Programs", [
    ["Leftover Cleanup", "After an uninstall, offers the folders and entries the program left behind.", "WL"],
    ["Programs History", "Records every uninstall and change, and can restore it.", "WL"],
    ["Rename and Delete Entry", "Fix a program's name, or remove an entry whose program is already gone.", "W"],
    ["Repair", "Repair or reinstall a program to put back missing or damaged files.", "WL"],
    ["Uninstall Several at Once", "Tick or Ctrl-click several programs and uninstall them together.", "WL"],
  ]],
  ["Registry", [
    ["Registry Backup", "Back Up Now saves every registry area Kodiak Cleaner can change into one .reg file.", "W"],
    ["Registry Cleaner", "Fifteen checks for entries that point at files or programs that are gone, with a .reg backup before every fix.", "W"],
  ]],
  ["Settings and Safety", [
    ["Activity Log", "One plain-text file per month noting cleanings, changes and errors. Stays on your computer.", "WL"],
    ["Always Start as Administrator", "Start with administrator rights without the User Account Control prompt (installed copies).", "W"],
    ["Back Up and Restore Settings", "Save your settings to a file and restore them later.", "WL"],
    ["Close to Tray", "Minimize or close Kodiak Cleaner to the system tray.", "W"],
    ["Contact and Feedback", "Send Feedback, Report a Bug, Request a Feature and Security forms.", "WML"],
    ["Recycle Bin Menu and Jump List", "Clean from the Recycle Bin's right-click menu or the taskbar.", "W"],
    ["Settings Search", "Type to find any setting.", "W"],
    ["Updates", "Check for new versions as often as you like, and optionally download and install them when you close Kodiak Cleaner. Checked against SHA-256.", "W"],
    ["What's New", "Shows what changed after each update.", "WML"],
  ]],
  ["Startup and Triggers", [
    ["Scheduled Tasks", "See the programs Windows runs on a schedule, and turn them off or on.", "W"],
    ["Startup", "Programs that start when you sign in. Turn them off or on, add your own, and restore removed ones from History.", "W"],
    ["Triggers", "Everything that starts by itself (autostart, services, timers, launch agents, cron jobs, startup scripts), with notes on entries worth a look and a backup before every change.", "ML"],
  ]],
  ["Supporter Extras", [
    ["Support Link Hidden", "The Support Kodiak Cleaner link in Settings goes away.", "W", S],
    ["Supporter Badge", "A small Supporter badge under the Kodiak Cleaner name. You can hide it.", "WML", S],
  ]],
  ["Tools", [
    ["Boot Timer", "Restart and see how long shutting down, starting Windows and the desktop settling took, and which startup programs, drivers and services slowed it.", "W"],
    ["Browser Extension Controls", "Turn extensions on or off, or remove them, in Chrome, Edge, Brave and Firefox-based browsers.", "W"],
    ["Browser Extensions", "Lists the extensions in your browsers so you can spot ones you don't recognize.", "WL"],
    ["Defrag and TRIM", "Defragments hard drives and TRIMs SSD and NVMe drives with Windows' own optimizer, picking the right one for each drive.", "W"],
    ["Disk Analyzer", "See which folders and files take the most space.", "WL"],
    ["Disk Analyzer Map", "A map of every folder and file sized by the space it takes and colored by kind, for all drives, chosen drives or a folder.", "W"],
    ["Drive Wiper", "Overwrite free space so deleted files can't be recovered.", "WL"],
    ["Driver Cleaner", "Removes an old AMD, Intel or NVIDIA graphics driver or a Realtek audio driver completely before a fresh install, with backups and a restore point first.", "W"],
    ["Duplicate Finder", "Find files with identical contents. One copy always stays.", "WL"],
    ["Duplicate Finder In-Depth", "Find files that are alike or have similar names, search whole drives, list names that fit a pattern, and confirm sets by SHA-256.", "W"],
    ["Empty Folders", "Find folders with nothing in them.", "WL"],
    ["Erase Entire Drive", "Wipe whole drives the right way for each type (Quick, Thorough or Secure, up to 30 passes), then leave them like new or format them, after three checks.", "W"],
    ["Integrity Check", "Check and repair Windows system files and your drives with Windows' own tools, plus drive health, drive error history and a memory test.", "W"],
    ["Large Files", "List the biggest files in a folder.", "WL"],
    ["Mass Rename", "Rename many files at once by a pattern, see every new name first, and undo the last rename.", "W"],
    ["Point-in-Time Restore", "Turn on and set up Windows' frequent snapshots, see them, take one now and restart to restore (Windows 11 24H2 and later).", "W"],
    ["System Information", "Your computer's system, processor, memory, drives and up time, with Copy to Clipboard.", "WL"],
    ["System Information Advanced View and Reports", "Serial numbers, memory modules, firmware, TPM, network details and temperatures, and Export Report as Text, HTML or CSV.", "W"],
    ["System Restore", "List, create and remove restore points.", "W"],
  ]],
];

export const featureGroups = GROUPS;

export default function featureList() {
  // On phones the table becomes one card per feature, so each mark carries its edition name.
  const yes = (on, os) => on
    ? `<td class="os yes">${icons.check}<span class="os-label">${os}</span><span class="sr-only">Yes</span></td>`
    : `<td class="os no"><span aria-hidden="true">–</span><span class="os-label">${os}</span><span class="sr-only">Not in the ${os} edition yet</span></td>`;
  const plan = (p) => p === S ? `<span class="plan supporter">${icons.heart}Supporter</span>` : `<span class="plan free">Free</span>`;
  const all = GROUPS.flatMap(([, list]) => list);
  const supporterCount = all.filter((f) => f[3] === S).length;
  const tables = GROUPS.map(([g, list]) => {
    const id = g.toLowerCase().replace(/[^a-z]+/g, "-");
    return `<h2 id="${id}" class="mt-xl">${g}</h2>
    <div class="table-wrap reveal" tabindex="0" role="region" aria-labelledby="${id}"><table class="feature-list">
      <colgroup><col><col class="plan-col"><col class="os-col"><col class="os-col"><col class="os-col"></colgroup>
      <thead><tr><th scope="col">Feature</th><th scope="col">Plan</th><th scope="col" class="os">Windows</th><th scope="col" class="os">Mac</th><th scope="col" class="os">Linux</th></tr></thead>
      <tbody>${list.map(([name, desc, where, p]) => `<tr><th scope="row"><strong>${esc(name)}</strong><span class="desc">${esc(desc)}</span></th><td>${plan(p)}</td>${yes(where.includes("W"), "Windows")}${yes(where.includes("M"), "Mac")}${yes(where.includes("L"), "Linux")}</tr>`).join("")}</tbody>
    </table></div>`;
  }).join("\n    ");

  const body = `
${pageHead("Full Feature List", `Every feature in Kodiak Cleaner, and which editions have it. ${all.length - supporterCount} of ${all.length} are free for everyone; the ${supporterCount} marked Supporter are small thank-you extras.`)}
<nav class="jump" aria-label="On this page"><div class="wrap chips">
  ${GROUPS.map(([g]) => `<a class="chip-link" href="#${g.toLowerCase().replace(/[^a-z]+/g, "-")}">${g}</a>`).join("\n  ")}
</div></nav>
<section class="tight">
  <div class="wrap">
    <div class="card plan-note"><span class="icon">${icons.heart}</span><p><strong>There is no paid or Premium version.</strong> Every cleaning feature and tool is free. A supporter code, sent by email as a thank-you for supporting Kodiak Cleaner on Ko-fi, unlocks only the extras marked Supporter. <a href="/donate/">Become a supporter</a>.</p></div>
    ${tables}
    <p class="muted mt">A dash means the feature isn't in that edition yet. Windows is the most complete edition, and the Mac edition has Cleaner and Triggers so far.</p>
  </div>
</section>`;
  return {
    id: "feature-list",
    path: "/features/list/",
    out: "features/list/index.html",
    title: "Full Feature List",
    description: "Every Kodiak Cleaner feature for Windows, Mac and Linux, each marked Free or Supporter. There is no paid version: supporter codes only unlock color schemes and a badge.",
    body,
  };
}
