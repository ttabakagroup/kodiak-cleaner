import { icons } from "../icons.mjs";
import { shot, pageHead } from "../layout.mjs";
import { esc, inline } from "../markdown.mjs";

const BAR = "Kodiak Cleaner for Linux";
const chips = (list) => `<div class="chips">${list.map((x) => `<span class="chip">${x}</span>`).join("")}</div>`;

// Linux download cards; also used on the Download page.
export function linuxDownloads(ctx) {
  const { cfg } = ctx;
  const lx = cfg.linux;
  return `<div class="grid cols-2">
      <div class="card dl-card reveal">
        <span class="chip badge">Debian and Ubuntu</span>
        <span class="icon">${icons.package}</span>
        <h3>DEB Package</h3>
        <p class="muted">For Debian 12, Ubuntu 22.04, Linux Mint 21 and newer.</p>
        <ul>
          <li>Adds Kodiak Cleaner to your app menu.</li>
          <li>Installs everything it needs through apt.</li>
          <li>Update by installing the newer .deb.</li>
        </ul>
        <a class="btn primary lg" href="/download/${lx.deb}">${icons.download}<span>Download DEB<small>ZIP with the .deb inside${lx.debSize ? " · " + esc(lx.debSize) : ""}</small></span></a>
        <p class="file">${esc(lx.deb)}</p>
      </div>
      <div class="card dl-card reveal">
        <span class="icon">${icons.terminal}</span>
        <h3>AppImage</h3>
        <p class="muted">For any 64-bit distribution with GTK 4 and libadwaita.</p>
        <ul>
          <li>Runs without installing, from any folder.</li>
          <li>Uses your system's own Python 3 and GTK 4.</li>
          <li>Update by replacing the old .AppImage.</li>
        </ul>
        <a class="btn lg" href="/download/${lx.appimage}">${icons.download}<span>Download AppImage<small>ZIP with the AppImage inside${lx.appimageSize ? " · " + esc(lx.appimageSize) : ""}</small></span></a>
        <p class="file">${esc(lx.appimage)}</p>
      </div>
    </div>`;
}

export default function linux(ctx) {
  const { linuxReleases, linuxLatest } = ctx;

  // Cleaner groups in the order Kodiak Cleaner for Linux shows them: System first, Advanced Cleaning last.
  const groups = [
    ["System", "Thumbnails, Trash, your own temporary files in /tmp and /var/tmp, the recent files list and shell history. With administrator rights: journal logs older than two weeks, crash reports and core dumps."],
    ["Package Managers", "APT, DNF, Pacman (keeps the two newest of each) and Zypper downloads, unused Flatpak runtimes and app caches, old Snap revisions and the Snap download cache."],
    ["Browsers", "Cache, cookies, history and sessions, including Snap and Flatpak copies, plus the Thunderbird cache. Bookmarks are never touched."],
    ["Programs", "Discord, Go, npm, pip, Slack, Spotify, Visual Studio Code and Yarn caches."],
    ["Advanced Cleaning", "Old kernels (the running and newest are kept), unused packages, old log files and the whole cache folder. Hidden behind a warning, never ticked for you, each with a caution."],
  ];
  const browsers = ["Brave", "Chromium", "Firefox", "Floorp", "Google Chrome", "Helium", "LibreWolf", "Microsoft Edge", "Mullvad Browser", "Opera", "Vivaldi"];

  // Trigger types, A to Z.
  const triggers = [
    ["Autostart", "~/.config/autostart and /etc/xdg/autostart", "Turn on or off, remove your own, add a new one"],
    ["Cron Job", "Your crontab, /etc/crontab, /etc/cron.d, the cron.daily folders and anacrontab", "Turn your own on or off, or remove them"],
    ["Device Rule", "Programs run by rules in /etc/udev/rules.d", "Shown"],
    ["Login Script", "KDE autostart and environment scripts", "Shown, remove your own"],
    ["Service, Timer, Socket, Path Unit", "systemd, for you and for all users", "Turn on or off, remove your own"],
    ["Shell Startup", "~/.bashrc, ~/.profile, ~/.zshrc, fish, X session files, /etc/profile.d and more", "Shown, and Open File opens it in your editor"],
  ];

  // Color schemes in the standard order: Honey (free), then the Supporter themes Classic and Glacier.
  const themes = [
    ["lx-honey-light", "Honey Light", "linux-cleaner.png"],
    ["lx-honey-dark", "Honey Dark", "linux-cleaner-dark.png"],
    ["lx-classic-light", "Classic Light", "linux-cleaner-classic.png"],
    ["lx-classic-dark", "Classic Dark", "linux-cleaner-classic-dark.png"],
    ["lx-glacier-light", "Glacier Light", "linux-cleaner-glacier.png"],
    ["lx-glacier-dark", "Glacier Dark", "linux-cleaner-glacier-dark.png"],
  ];
  const defaultTheme = "lx-honey-light";

  const log = linuxReleases.map((r) => {
    const secs = r.sections.map((s) => `${s.title ? `<h3>${esc(s.title)}</h3>` : ""}<ul>${s.items.map((i) => `<li>${inline(i)}</li>`).join("")}</ul>`).join("");
    return `<article class="card release" id="linux-v${r.version}"><header><h2>${esc(r.name)}</h2><time datetime="${r.date}">${ctx.longDate(r.date)}</time></header>${secs}</article>`;
  }).join("\n");

  const body = `
${pageHead("Kodiak Cleaner for Linux", `The Linux edition of Kodiak Cleaner: clean junk files, caches and package downloads, and see everything that starts automatically. Version ${esc(linuxLatest.name)}, free and open source.`)}

<section class="tight"><div class="wrap narrow"><div class="notice" role="note">${icons.alert}<p><strong>Beta software: use at your own risk.</strong> Kodiak Cleaner for Linux is new and still in development. Analyze before you clean, read each caution, and keep your own backups of anything important.</p></div></div></section>

<section class="tight">
  <div class="wrap">
    ${linuxDownloads(ctx)}
    <p class="center muted mt">Each ZIP also has a Docs folder with the changelog, license, privacy policy, terms of use and a plain-language ReadMe. Looking for Windows? <a href="/download/">Download Kodiak Cleaner for Windows</a>.</p>
  </div>
</section>

<section class="alt">
  <div class="wrap">
    <div class="showcase reveal">
      <div class="text">
        <span class="eyebrow">Cleaner</span>
        <h2>Junk, Caches and Package Downloads</h2>
        <p>Tick what to clean, click Analyze to see what's there, then Run Cleaner. Sort By offers Default, A to Z, Largest Found, Last Cleaned and Most Space Freed, and only programs found on your computer are listed.</p>
        <p><strong>Browsers:</strong></p>${chips(browsers)}
      </div>
      ${shot(ctx, "linux-cleaner.png", "Kodiak Cleaner for Linux, Cleaner page after Analyze", { bar: BAR })}
    </div>
    <div class="table-wrap mt-xl reveal"><table><thead><tr><th scope="col">Group</th><th scope="col">What It Cleans</th></tr></thead><tbody>
      ${groups.map(([g, d]) => `<tr><td><strong>${g}</strong></td><td>${d}</td></tr>`).join("")}
    </tbody></table></div>
  </div>
</section>

<section>
  <div class="wrap">
    <div class="showcase flip reveal">
      <div class="text">
        <span class="eyebrow">Triggers</span>
        <h2>Everything That Starts Automatically</h2>
        <p>One list of autostart entries, systemd services and timers, cron jobs, shell startup files and device rules, for you and for every user.</p>
        <ul>
          <li>Notes flag entries worth a look: a missing program, a program in a temporary or hidden folder, a recent change, or a command that downloads and runs code.</li>
          <li>Needs a Look shows only the entries with notes.</li>
          <li>Triggers History keeps a copy before every change, and Restore puts it back.</li>
        </ul>
      </div>
      ${shot(ctx, "linux-triggers-needs-a-look.png", "Triggers page showing only the entries that need a look", { bar: BAR })}
    </div>
    <div class="table-wrap mt-xl reveal"><table><thead><tr><th scope="col">Type</th><th scope="col">Where It Comes From</th><th scope="col">What You Can Do</th></tr></thead><tbody>
      ${triggers.map(([t, w, d]) => `<tr><td><strong>${t}</strong></td><td>${w}</td><td>${d}</td></tr>`).join("")}
    </tbody></table></div>
  </div>
</section>

<section class="alt">
  <div class="wrap center">
    <div class="section-head reveal">
      <span class="eyebrow">Make It Yours</span>
      <h2>Honey, Classic and Glacier, Light or Dark</h2>
      <p class="lead">The same color schemes and accent colors as the Windows edition, with light or dark mode that can follow your desktop.</p>
    </div>
    <div class="tabs" role="tablist" aria-label="Color schemes for Linux">
      ${themes.map(([id, label]) => `<button type="button" role="tab" id="tab-${id}" aria-controls="panel-${id}" aria-selected="${id === defaultTheme}" tabindex="${id === defaultTheme ? 0 : -1}">${label}</button>`).join("")}
    </div>
    ${themes.map(([id, label, file]) => `<div class="tab-panel" role="tabpanel" id="panel-${id}" aria-labelledby="tab-${id}"${id === defaultTheme ? "" : " hidden"}>${shot(ctx, file, `Kodiak Cleaner for Linux in ${label}`, { bar: BAR })}</div>`).join("\n    ")}
    <p class="muted mt">Honey is free for everyone. Classic and Glacier are a thank-you for supporters, and a supporter code from the Windows edition works here too.</p>
  </div>
</section>

<section>
  <div class="wrap">
    <div class="grid cols-3">
      <div class="card reveal"><span class="icon">${icons.shield}</span><h3>Safety</h3><p>Files are only removed inside your home folder, plus your own files in /tmp and /var/tmp. Links are removed, never followed. Anything outside your home folder goes through a small administrator helper that runs a fixed list of jobs and asks for your password once per clean.</p></div>
      <div class="card reveal"><span class="icon">${icons.lock}</span><h3>Privacy</h3><p>Kodiak Cleaner for Linux never goes online by itself. No telemetry, analytics, crash reports, ads or accounts. Settings live in ~/.config/kodiak-cleaner and Triggers History in ~/.local/share/kodiak-cleaner.</p></div>
      <div class="card reveal"><span class="icon">${icons.laptop}</span><h3>What It Needs</h3><p>64-bit Linux with Python 3.10 or newer, GTK 4 and libadwaita (GNOME desktops already have them). Elsewhere, install <code>python3-gi gir1.2-gtk-4.0 gir1.2-adw-1</code> (Debian, Ubuntu, Mint), <code>python3-gobject gtk4 libadwaita</code> (Fedora) or <code>python-gobject gtk4 libadwaita</code> (Arch).</p></div>
    </div>
    <div class="grid cols-2 mt-xl">
      <div class="card"><h3>Install</h3><ol>
        <li><strong>DEB:</strong> unzip, then double-click the <code>.deb</code>, or run <code>sudo apt install ./KodiakCleaner-Dev-amd64.deb</code>.</li>
        <li><strong>AppImage:</strong> unzip, allow the <code>.AppImage</code> to run as a program, then double-click it.</li>
      </ol></div>
      <div class="card"><h3>Update and Uninstall</h3><p>Updating keeps your settings and history: install the newer <code>.deb</code>, or replace the old <code>.AppImage</code>. To uninstall, remove the package with your software manager (or <code>sudo apt remove kodiak-cleaner</code>), or delete the AppImage.</p></div>
    </div>
  </div>
</section>

<section class="alt" id="changelog">
  <div class="wrap narrow">
    <div class="section-head center"><span class="eyebrow">Changelog</span><h2>What's New for Linux</h2></div>
    ${log}
  </div>
</section>`;

  return {
    id: "linux",
    path: "/linux/",
    out: "linux/index.html",
    title: "Kodiak Cleaner for Linux",
    description: "Kodiak Cleaner for Linux: a free, open source cleaner for junk files, caches and package downloads, with Triggers to see everything that starts automatically. DEB and AppImage downloads.",
    body,
  };
}
