import { icons } from "../icons.mjs";
import { pageHead } from "../layout.mjs";
import { esc } from "../markdown.mjs";

// Frequently Asked Questions (Ted, 2026-10-25). Answers come from the app's faq.md, README.md, CommandLine.cs and
// CHANGELOG.md and from this site's own pages, never from guesses. Groups and the questions in each are A to Z,
// like the app's faq.md. Never show an email address (use Contact Us), and never say donate or tip.
const az = (list) => [...list].sort((a, b) => a[0].localeCompare(b[0], "en", { sensitivity: "base" }));
const slug = (s) => s.toLowerCase().replace(/&[a-z]+;/g, "").replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");

export function faqGroups(ctx) {
  const { cfg, macLatest, linuxLatest } = ctx;
  return az([
    ["About Kodiak Cleaner", az([
      ["What Is Kodiak Cleaner?", `<p>A small, honest cleaner for Windows 10 and 11 (64-bit), with editions for Mac and Linux. It cleans junk files, browser data and program caches, fixes registry entries that point at things that are gone, manages startup programs and uninstalls programs with their leftovers. It also has built-in tools such as Disk Analyzer, Duplicate Finder, Hash Checker and Integrity Check. There are no ads, no bundled offers, no telemetry and no background services. <a href="/features/">See every feature</a>.</p>`],
      ["Is Kodiak Cleaner Free?", `<p>Yes, with every feature, for everyone. There is no paid or Premium version, no trial and no upsell. It's also open source under the MIT License, so anyone may use it, read the code, change it and share it. <a href="/open-source/">More about open source</a>.</p>`],
      ["What Does Beta Mean?", `<p>Kodiak Cleaner for Windows is in the Beta channel (versions 0.9), the last step before the first full release, 1.0. Every tool is there and more people are testing it, so it may still have bugs. It deletes things by design: check what's selected before you clean, keep your own backups of anything important, and please report anything that doesn't work under Settings &gt; Contact &amp; Feedback.</p><p>The changelog starts fresh with 0.9.0 Beta. About &gt; All Changes Since the Start also lists the earlier Dev builds.</p>`],
      ["Will My Dev Build Update to the Beta?", `<p>Yes. The Dev builds (0.8.x) ended with 0.8.121 Dev, and a Dev install that checks for updates is now offered the Beta. You can also run the Beta installer over it. Every channel installs to <code>Program Files\\Kodiak Cleaner</code>, and updating keeps your settings, backups and supporter code.</p>`],
    ])],
    ["Downloads and Updates", az([
      ["Installer or Portable: Which Should I Pick?", `<p><strong>The installer</strong> (an MSI) suits most people. It adds Start menu shortcuts, installs updates for you and can skip the administrator prompt. <strong>The portable ZIP</strong> runs without installing, from any folder or a USB drive: extract it and run <code>KodiakCleaner.exe</code>. It keeps its settings, backups and logs beside the program instead of in Documents\\Kodiak Cleaner.</p><p>Both are a single self-contained program, so there's nothing else to install. <a href="/download/">Go to the Download page</a>.</p>`],
      ["How Do Updates Work?", `<p>Click Check for Updates on the About page, or Check Now under Settings &gt; Updates. Kodiak Cleaner asks its website whether a newer version is out. If there is one, it downloads the installer, checks it against the SHA-256 checksum the website lists, and installs it. Your settings, backups and supporter code are kept, and What's New shows what changed. Check for Updates Automatically is off until you turn it on.</p><p><strong>Portable:</strong> replace <code>KodiakCleaner.exe</code> and keep <code>KodiakCleaner.ini</code> and the Backups, Logs and Reports folders next to it.</p>`],
      ["Why Does Windows SmartScreen Warn Me?", `<p>Kodiak Cleaner is a free project, and its installer isn't code-signed yet. Until it is, Windows may show <strong>Windows protected your PC</strong> the first time you open it. That's expected: click <strong>More info</strong>, then <strong>Run anyway</strong>.</p><p>To check your copy is the real one, run <code>Get-FileHash</code> on it in PowerShell and compare the result with the SHA-256 on the <a href="/download/#smartscreen">Download page</a>. If it doesn't match, or Windows shows a different warning, please don't run it, and <a href="/contact/">contact us</a>.</p>`],
      ["How Do I Check That a Download Isn't Damaged?", `<p>Open Hash Checker under Tools and drop the file on the page. Paste the hash from the download page into Expected Hashes, or drop the <code>.sha256</code>, <code>.md5</code> or <code>SHA256SUMS</code> file that came with it. The file shows Verified when it matches, or Doesn't Match when it's damaged, changed or a different version. Hash Checker only reads your files and never sends them anywhere.</p>`],
    ])],
    ["Help and Accessibility", az([
      ["How Do I Get Help or Report a Bug?", `<p>Use the <a href="/contact/">Contact Us</a> form and pick a topic: Bug Report, Feature Request, General Feedback, Accessibility or Security. In the app, Settings &gt; Contact &amp; Feedback opens the same form in your browser, and nothing is sent until you press Send. Ted reads every message.</p>`],
      ["How Do I Report a Security Problem?", `<p>Please report it privately through <a href="/contact/?topic=Security">Contact Us with the Security topic</a>, so it reaches Ted first.</p>`],
      ["Is Kodiak Cleaner Accessible?", `<p>We're working toward WCAG 2.1 at level AA and the Americans with Disabilities Act for the app and this website. The app is meant to work with the keyboard, gives buttons and icons text labels for screen readers, and every color scheme and accent meets WCAG 2.2 AA contrast. Under Settings &gt; Appearance, the Text Size slider makes text, icons and buttons 80% to 200% of the normal size, and Animations can be turned off. This is an ongoing effort, so if something is hard to use with your setup, <a href="/contact/?topic=Accessibility">tell us</a>. <a href="/accessibility/">Read the accessibility statement</a>.</p>`],
    ])],
    ["Mac and Linux", az([
      ["Is There a Mac Edition?", `<p>Yes. Kodiak Cleaner for Mac (version ${esc(macLatest.name)}) is for macOS 26 or newer, on Apple silicon and Intel Macs. It has the Cleaner and Triggers, which shows everything that starts by itself. It isn't signed with an Apple Developer ID yet, so the first time, open it with Open Anyway in System Settings &gt; Privacy &amp; Security. <a href="/mac/">See the steps and learn more</a>.</p>`],
      ["Is There a Linux Edition?", `<p>Yes. Kodiak Cleaner for Linux (version ${esc(linuxLatest.name)}) is for 64-bit Linux with GTK 4 and libadwaita, as a DEB package for Debian and Ubuntu or an AppImage for any distribution. It has the Cleaner, Triggers and several of the Windows tools. <a href="/linux/">Learn more about the Linux edition</a>.</p>`],
      ["Do the Mac and Linux Editions Have Every Tool?", `<p>Not yet. Windows is the most complete edition. The Mac and Linux editions are still Dev builds, with the same color schemes and the same privacy. The <a href="/features/list/">Full Feature List</a> shows which edition has each feature, and supporter codes work in all three.</p>`],
    ])],
    ["Privacy and Safety", az([
      ["Does Kodiak Cleaner Send Anything Online?", `<p>No. There's no telemetry, analytics, crash reports, accounts or ads, and nothing is sent automatically, ever. Kodiak Cleaner for Windows only goes online to check for updates, when you click Check for Updates or turn on automatic checks. Buttons such as Support on Ko-fi, Get the Latest Driver and Contact &amp; Feedback open your browser only when you click them. Integrity Check's Windows image repair uses Windows' own DISM tool, which may download repair files from Windows Update. The Mac edition has no network code at all, and the Linux edition never goes online by itself. <a href="/privacy/">Read the Privacy Policy</a>.</p>`],
      ["Does It Run in the Background?", `<p>No. There's no service and nothing running unless you open it. Scheduled cleanings are started by Windows Task Scheduler and close when they're done.</p>`],
      ["Is Kodiak Cleaner Safe to Use?", `<p>It only changes what you pick. Registry fixes, startup entries, scheduled tasks, program changes and drivers are backed up before every change, and files that tools find, such as duplicates, large files, empty folders and uninstall leftovers, go to the Recycle Bin. Browsers are skipped while they're open, files in use are skipped, and links and junctions are never followed. Advanced Cleaning shows a warning every time it opens, and Drive Wiper asks three times before it erases a drive.</p><p>Files the Cleaner deletes, wiped free space and erased drives can't be brought back, so look over each list before you click. <a href="/features/#safety">More about built-in safety</a>.</p>`],
      ["What Are Caution Items?", `<p>Some Advanced Cleaning items can cause harm, such as clearing saved browser passwords. They carry a caution, ask before they can be ticked, are never ticked for you, are skipped by Select All, and never run on a schedule or from the command line.</p>`],
      ["How Do I Undo a Change?", `<ul><li><strong>Registry:</strong> every fix is saved as a .reg file in Backups\\Registry first. Double-click it to put the keys back.</li><li><strong>Startup and Programs:</strong> History restores removed entries, renames and removed leftovers. Uninstalls are only recorded, so install the program again to get it back.</li><li><strong>Files:</strong> files removed by tools go to the Recycle Bin, so restore them from there. Mass Rename has Undo Last Rename.</li><li><strong>Scheduled tasks:</strong> a deleted task is backed up first, and Restore puts it back.</li><li><strong>Whole PC:</strong> System Restore creates restore points, and Driver Cleaner makes one before it removes drivers.</li></ul>`],
      ["Where Are My Backups, Logs and Reports?", `<p>An installed copy keeps them in the Kodiak Cleaner folder in your Documents, and a portable copy keeps them beside <code>KodiakCleaner.exe</code>. Settings &gt; Application has an Open Folder button. Settings are a plain <code>KodiakCleaner.ini</code> file, and nothing is kept in the cloud. Uninstalling asks whether to delete your backups, logs and reports.</p>`],
    ])],
    ["Supporter Codes", az([
      ["How Do Supporter Codes Work?", `<p>Every feature is free. If you support Kodiak Cleaner on Ko-fi with $10 or more, you get a supporter code by email as a thank-you. Enter it under Settings &gt; Supporter and click Apply Code. The code is checked on your computer only and is never sent anywhere, and it works in the Windows, Mac and Linux editions. <a href="/donate/">Support on Ko-fi</a>.</p>`],
      ["How Many Computers Does One Code Cover?", `<p>One supporter code covers up to 10 of your computers. Need more? Use the <a href="/contact/">Contact Us</a> form.</p>`],
      ["What Does a Supporter Code Unlock?", `<p>Only small thank-you extras: the Classic, Glacier and Lagoon color schemes, a Supporter badge (which you can hide), and the Support link goes away. Supporting never unlocks cleaning features or tools, and not supporting never takes anything away. <a href="/features/list/">See what's free and what's Supporter</a>.</p>`],
    ])],
    ["Using Kodiak Cleaner", az([
      ["Can I Hide Tools I Don't Use?", `<p>Yes, from 0.9.1 Beta. Under Settings &gt; Tools, each tool can be turned off to hide it from the sidebar. You can also pin the tools you use most to Favorites at the top of the sidebar: right-click a page and pick Pin to Favorites, or drag it up.</p>`],
      ["Can I Run Kodiak Cleaner From the Command Line?", `<p>Yes, on Windows. Scripts and system administrators can clean, scan or fix the registry, and uninstall programs with no window, for example <code>KodiakCleaner.exe /clean</code> or <code>KodiakCleaner.exe /registry /scan-only</code>. Run <code>KodiakCleaner.exe /help</code> to see every switch. <a href="/features/#command-line">See the switches, examples and exit codes</a>.</p>`],
      ["How Do I Make the Text Bigger?", `<p>Under Settings &gt; Appearance, move the Text Size slider. It makes text, icons and buttons bigger or smaller in every Kodiak Cleaner window, from 80% to 200% of the normal size, on top of the display scaling set in Windows. Click Default Size to go back to 100%.</p>`],
      ["How Do I Move Kodiak Cleaner to a New PC?", `<p>Under Settings &gt; Application, click Back Up Settings. It saves a dated copy of your settings and your Programs and Startup histories in Backups\\Settings. Install Kodiak Cleaner on the new PC, close it, then copy those files over <code>KodiakCleaner.ini</code> and the two <code>history.json</code> files. Your supporter code is part of the settings, so it moves with them.</p>`],
    ])],
  ]).map(([g, list]) => [g, list, slug(g)]);
}

export default function faq(ctx) {
  const groups = faqGroups(ctx);
  const body = `
${pageHead("Frequently Asked Questions", "Quick answers about Kodiak Cleaner: what it is, staying safe and private, downloads and updates, supporter codes, the Mac and Linux editions and the command line.")}
<nav class="jump" aria-label="On this page"><div class="wrap chips">
  ${groups.map(([g, , id]) => `<a class="chip-link" href="#${id}">${g}</a>`).join("\n  ")}
</div></nav>
<section class="tight"><div class="wrap narrow prose faq">
  ${groups.map(([g, list, id], i) => `<div class="card pad${i ? " mt-xl" : ""}" id="${id}">
    <h2>${g}</h2>
    ${list.map(([q, a]) => `<div class="faq-item" id="${slug(q)}"><h3>${q}</h3>${a}</div>`).join("\n    ")}
  </div>`).join("\n  ")}
  <div class="card pad mt-xl center" id="more-questions">
    <span class="icon">${icons.mail}</span>
    <h2>Still Have a Question?</h2>
    <p>Send it through the Contact Us form and Ted will answer.</p>
    <div class="btn-row centered"><a class="btn primary" href="/contact/">Contact Us</a><a class="btn" href="/features/">See Every Feature</a></div>
  </div>
</div></section>`;
  return {
    id: "faq",
    path: "/faq/",
    out: "faq/index.html",
    title: "Frequently Asked Questions",
    description: "Answers to common questions about Kodiak Cleaner: whether it's free, the Beta, safety and backups, privacy, SmartScreen, installer or portable, updates, supporter codes, Mac and Linux, and the command line.",
    body,
  };
}
