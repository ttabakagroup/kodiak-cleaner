import { icons } from "../icons.mjs";
import { shot, pageHead } from "../layout.mjs";
import { esc, inline } from "../markdown.mjs";

const BAR = "Kodiak Cleaner for Mac";
const chips = (list) => `<div class="chips">${list.map((x) => `<span class="chip">${x}</span>`).join("")}</div>`;

// Mac download cards; also used on the Download page.
export function macDownloads(ctx) {
  const m = ctx.cfg.mac;
  return `<div class="grid cols-2">
      <div class="card dl-card reveal">
        <span class="chip badge">Recommended</span>
        <span class="icon">${icons.drive}</span>
        <h3>Disk Image</h3>
        <p class="muted">The usual way to install a Mac app.</p>
        <ul>
          <li>Open it and drag Kodiak Cleaner to Applications.</li>
          <li>Includes the Docs folder.</li>
          <li>Update by dragging the newer copy over the old one.</li>
        </ul>
        <a class="btn primary lg" href="/download/${m.dmg}">${icons.download}<span>Download DMG<small>Disk image${m.dmgSize ? " · " + esc(m.dmgSize) : ""}</small></span></a>
        <p class="file">${esc(m.dmg)}</p>
      </div>
      <div class="card dl-card reveal">
        <span class="icon">${icons.package}</span>
        <h3>ZIP</h3>
        <p class="muted">The same app, packed in a ZIP.</p>
        <ul>
          <li>Unzip it and move Kodiak Cleaner to Applications.</li>
          <li>Includes the Docs folder.</li>
          <li>Handy where disk images are blocked.</li>
        </ul>
        <a class="btn lg" href="/download/${m.zip}">${icons.download}<span>Download ZIP<small>ZIP with the app inside${m.zipSize ? " · " + esc(m.zipSize) : ""}</small></span></a>
        <p class="file">${esc(m.zip)}</p>
      </div>
    </div>`;
}

export default function mac(ctx) {
  const { macReleases, macLatest } = ctx;

  // Cleaner groups in the order Kodiak Cleaner for Mac shows them: macOS first, Advanced Cleaning last.
  const groups = [
    ["macOS", "Crash reports, logs, temporary files, Quick Look thumbnails, saved window state, shell history and the Trash."],
    ["Browsers", "Cache, cookies, history and sessions, plus Thunderbird's cache. Bookmarks always stay."],
    ["Apps and Developer Tools", "Mail downloads, CocoaPods, Discord, Go, Homebrew, npm, pip, Slack, Spotify, Visual Studio Code, Xcode and Yarn."],
    ["Advanced Cleaning", "The entire cache folder, Xcode archives and device support files. Hidden behind a warning and never ticked for you."],
  ];
  const browsers = ["Arc", "Brave", "Chromium", "Firefox", "Floorp", "Google Chrome", "Helium", "LibreWolf", "Microsoft Edge", "Opera", "Safari", "Vivaldi", "Zen"];

  // Trigger types, A to Z.
  const triggers = [
    ["Cron Job", "Your crontab", "Turn off or on, remove your own"],
    ["Launch Agent", "~/Library/LaunchAgents and /Library/LaunchAgents", "Turn off or on, remove your own"],
    ["Launch Daemon", "/Library/LaunchDaemons", "Turn off or on (asks for your administrator password once per batch)"],
    ["Shell Startup", "~/.zshrc, ~/.zprofile, ~/.bash_profile and the other zsh and bash startup files", "Shown"],
  ];

  // Color schemes in the standard order: Honey (free), then the Supporter themes Classic and Glacier.
  const themes = [
    ["mac-honey-light", "Honey Light", "mac-cleaner-honey-light.png"],
    ["mac-honey-dark", "Honey Dark", "mac-cleaner-honey-dark.png"],
    ["mac-classic-light", "Classic Light", "mac-cleaner-classic-light.png"],
    ["mac-classic-dark", "Classic Dark", "mac-cleaner-classic-dark.png"],
    ["mac-glacier-light", "Glacier Light", "mac-cleaner-glacier-light.png"],
    ["mac-glacier-dark", "Glacier Dark", "mac-cleaner-glacier-dark.png"],
  ];
  const defaultTheme = "mac-honey-light";

  const log = macReleases.map((r) => {
    const secs = r.sections.map((s) => `${s.title ? `<h3>${esc(s.title)}</h3>` : ""}<ul>${s.items.map((i) => `<li>${inline(i)}</li>`).join("")}</ul>`).join("");
    return `<article class="card release" id="mac-v${r.version}"><header><h2>${esc(r.name)}</h2><time datetime="${r.date}">${ctx.longDate(r.date)}</time></header>${secs}</article>`;
  }).join("\n");

  const body = `
${pageHead("Kodiak Cleaner for Mac", `The Mac edition of Kodiak Cleaner: clean caches, logs, temporary files and browser data, and see everything that starts by itself. Version ${esc(macLatest.name)}, free and open source.`)}

<section class="tight"><div class="wrap narrow"><div class="notice" role="note">${icons.alert}<p><strong>Beta software: use at your own risk.</strong> Kodiak Cleaner for Mac is new and still in development. Analyze before you clean, read each caution, and keep your own backups of anything important.</p></div></div></section>

<section class="tight">
  <div class="wrap">
    ${macDownloads(ctx)}
    <p class="center muted mt">For macOS 26 Tahoe or newer, on Apple silicon and Intel Macs. Looking for another computer? <a href="/download/">See every download</a>.</p>
  </div>
</section>

<section class="tight">
  <div class="wrap narrow">
    <div class="card">
      <span class="icon">${icons.shield}</span>
      <h3>Opening It the First Time</h3>
      <p>This Dev build isn't signed with an Apple Developer ID or notarized yet, so the first time it opens macOS says it couldn't verify Kodiak Cleaner. To open it:</p>
      <ol>
        <li>Move Kodiak Cleaner to Applications and double-click it. When macOS stops it, click <strong>Done</strong> (or <strong>OK</strong>).</li>
        <li>Open <strong>System Settings</strong>, then <strong>Privacy &amp; Security</strong>, and scroll down to Security.</li>
        <li>Next to the message about Kodiak Cleaner, click <strong>Open Anyway</strong>, then enter your password.</li>
        <li>Click <strong>Open Anyway</strong> once more. From then on Kodiak Cleaner opens normally.</li>
      </ol>
      <p>If Open Anyway doesn't show up, open Terminal and run <code>xattr -dr com.apple.quarantine "/Applications/Kodiak Cleaner.app"</code>.</p>
      <p class="muted">The Docs folder's ReadMe walks through the same steps.</p>
    </div>
  </div>
</section>

<section class="alt">
  <div class="wrap">
    <div class="showcase reveal">
      <div class="text">
        <span class="eyebrow">Cleaner</span>
        <h2>Caches, Logs and Browser Data</h2>
        <p>Tick what to clean, click Analyze to see what's there, then Clean. Only apps found on your Mac are listed, and apps that are open are skipped. Sort By offers Default, A to Z, Largest Found, Last Cleaned and Most Space Freed.</p>
        <p><strong>Browsers:</strong></p>${chips(browsers)}
      </div>
      ${shot(ctx, "mac-cleaner-honey-light.png", "Kodiak Cleaner for Mac, Cleaner page after Analyze", { bar: BAR })}
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
        <h2>Everything That Starts by Itself</h2>
        <p>One list of launch agents (yours and everyone's), launch daemons, cron jobs and shell startup files, with Apple's own items available for reference.</p>
        <ul>
          <li>Notes flag missing programs, programs in temporary, Downloads or hidden folders, code that is downloaded and run, recent changes and items that restart themselves.</li>
          <li>Needs a Look shows only those.</li>
          <li>Every change is backed up first and can be restored from Triggers History.</li>
        </ul>
      </div>
      ${shot(ctx, "mac-triggers-honey-light.png", "Kodiak Cleaner for Mac, Triggers page", { bar: BAR })}
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
      <p class="lead">The same color schemes as the Windows and Linux editions, with accent colors for everyone, including one that follows your Mac's own accent color.</p>
    </div>
    <div class="tabs" role="tablist" aria-label="Color schemes for Mac">
      ${themes.map(([id, label]) => `<button type="button" role="tab" id="tab-${id}" aria-controls="panel-${id}" aria-selected="${id === defaultTheme}" tabindex="${id === defaultTheme ? 0 : -1}">${label}</button>`).join("")}
    </div>
    ${themes.map(([id, label, file]) => `<div class="tab-panel" role="tabpanel" id="panel-${id}" aria-labelledby="tab-${id}"${id === defaultTheme ? "" : " hidden"}>${shot(ctx, file, `Kodiak Cleaner for Mac in ${label}`, { bar: BAR })}</div>`).join("\n    ")}
    <p class="muted mt">Honey is free for everyone. Classic and Glacier are a thank-you for supporters, and a supporter code from the Windows or Linux edition works here too.</p>
  </div>
</section>

<section>
  <div class="wrap">
    <div class="grid cols-3">
      <div class="card reveal"><span class="icon">${icons.shield}</span><h3>Safety</h3><p>Files are only removed inside your home folder and your own temporary folder, and links are removed, never followed. Cleaning never needs your administrator password; only launch daemon changes ask for it, through macOS's own prompt.</p></div>
      <div class="card reveal"><span class="icon">${icons.lock}</span><h3>Privacy</h3><p>Nothing leaves your Mac. Kodiak Cleaner for Mac has no network code at all. Settings and Triggers History live in ~/Library/Application Support/Kodiak Cleaner.</p></div>
      <div class="card reveal"><span class="icon">${icons.check}</span><h3>Full Disk Access</h3><p>Safari, Mail and the Trash need Full Disk Access. Turn on Kodiak Cleaner in System Settings &gt; Privacy &amp; Security &gt; Full Disk Access (you can turn it off again any time), then quit and reopen it. Everything else works without it, and Kodiak Cleaner marks the items that need it.</p></div>
    </div>
  </div>
</section>

<section class="alt" id="changelog">
  <div class="wrap narrow">
    <div class="section-head center"><span class="eyebrow">Changelog</span><h2>What's New for Mac</h2></div>
    ${log}
  </div>
</section>`;

  return {
    id: "mac",
    path: "/mac/",
    out: "mac/index.html",
    title: "Kodiak Cleaner for Mac",
    description: "Kodiak Cleaner for Mac: a free, open source cleaner for caches, logs and browser data on macOS 26 or newer, with Triggers to see everything that starts by itself. DMG and ZIP downloads.",
    body,
  };
}
