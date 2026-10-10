import { icons } from "../icons.mjs";
import { shot, betaNotice, wordmark } from "../layout.mjs";
import { esc } from "../markdown.mjs";

export default function home(ctx) {
  const { cfg, latest, linuxLatest, macLatest } = ctx;

  // A to Z.
  const highlights = [
    ["backup", "Backups Before Changes", "Every registry fix, startup removal and program entry change is backed up first, and most removals go to the Recycle Bin."],
    ["zap", "Fast and Familiar", "Opens quickly and works the way you expect: analyze, review, clean. No busy interface, no buttons that moved."],
    ["gift", "Free and Open Source", "Every feature is free, under the MIT License. Read the code, build it yourself, or help improve it."],
    ["drive", "Installed or Portable", "Use the MSI installer, or run the portable version from any folder, even a USB drive."],
    ["lock", "Local First", "Settings and backups are plain files on your own PC. Nothing is kept in the cloud."],
    ["shield", "No Ads, No Tracking", "No telemetry, analytics, crash reports or upsells. It only goes online to check for updates, and only when you ask."],
    ["terminal", "Now on Mac and Linux", "Kodiak Cleaner for Mac and Kodiak Cleaner for Linux clean junk, caches and browser data, and show everything that starts by itself."],
    ["palette", "Your Look", "Honey, Classic and Glacier color schemes, accent colors for everyone, and light or dark mode."],
  ];

  // Screens people ask about most, A to Z.
  const gallery = [
    ["advanced-cleaning.png", "Advanced Cleaning", "Deeper items with clear cautions, never ticked for you."],
    ["appearance.png", "Appearance", "Honey, Classic or Glacier, with accent colors and light or dark mode."],
    ["disk-analyzer.png", "Disk Analyzer", "See which folders take the space, plus the 100 largest files."],
    ["large-files.png", "Large Files", "Find the biggest files in any folder, 50 MB and up."],
    ["programs.png", "Programs", "Uninstall several at once, with leftovers cleaned up after."],
    ["registry.png", "Registry", "Fifteen checks for broken entries, with a backup before every fix."],
    ["schedule.png", "Scheduled Cleaning", "Named cleanings on the days you choose, through Windows Task Scheduler."],
    ["startup.png", "Startup", "Turn start-up programs off or on, and restore removed ones from History."],
  ];

  // Color schemes in the standard order: Honey (free), then the Supporter themes Classic and Glacier.
  const themes = [
    ["honey-light", "Honey Light", "cleaner.png", "Cleaner in the Honey color scheme, light mode"],
    ["honey-dark", "Honey Dark", "cleaner-dark.png", "Cleaner in the Honey color scheme, dark mode"],
    ["classic-light", "Classic Light", "cleaner-classic.png", "Cleaner in the Classic color scheme, light mode, a supporter theme"],
    ["classic-dark", "Classic Dark", "cleaner-classic-dark.png", "Cleaner in the Classic color scheme, dark mode, a supporter theme"],
    ["glacier-light", "Glacier Light", "cleaner-glacier.png", "Cleaner in the Glacier color scheme, light mode, a supporter theme"],
    ["glacier-dark", "Glacier Dark", "cleaner-glacier-dark.png", "Cleaner in the Glacier color scheme, dark mode, a supporter theme"],
  ];
  const defaultTheme = "honey-dark";

  const body = `
<section class="hero">
  <div class="wrap">
    <div class="hero-logo"><span class="halo"></span><img src="/images/logo.svg" alt="Kodiak Cleaner logo: a friendly bear in a computer screen" width="230" height="230" fetchpriority="high"></div>
    <h1>${wordmark}</h1>
    <p class="slogan">Not a Crap Cleaner. A Cleaner App, Built Around What Matters.</p>
    <p class="lead">A small, honest cleaner for Windows, Mac and Linux. Clear out junk files, fix broken registry entries, tame start-up programs and uninstall software cleanly, with no telemetry, no ads, no upsells and no background services.</p>
    <div class="btn-row">
      <a class="btn primary lg" href="/download/#windows">${icons.windows}<span>Download for Windows<small>Version ${esc(latest.name)} · Free</small></span></a>
      <a class="btn lg" href="/download/#mac">${icons.laptop}<span>Download for Mac<small>Version ${esc(macLatest.name)} · Free</small></span></a>
      <a class="btn lg" href="/download/#linux">${icons.terminal}<span>Download for Linux<small>Version ${esc(linuxLatest.name)} · Free</small></span></a>
      <a class="btn lg" href="${cfg.repo}" rel="noopener">${icons.github}<span>View on GitHub<small>Open source, MIT License</small></span></a>
    </div>
    ${ctx.launchVideo ? `<div class="btn-row hero-video"><button class="btn" type="button" data-intro-open>${icons.play}Watch the Video</button></div>` : ""}
    <p class="hero-meta"><span>Windows 10 and 11 (64-bit)</span><span>macOS 26 or newer</span><span>Linux (DEB or AppImage)</span><span>No account needed</span></p>
    <div class="hero-shot">${shot(ctx, "cleaner.png", "Kodiak Cleaner's Cleaner page after Analyze, showing 717.1 MB that can be freed", { eager: true })}</div>
  </div>
</section>

<section class="tight"><div class="wrap narrow">${betaNotice()}</div></section>

<section class="tight">
  <div class="wrap">
    <div class="section-head center reveal">
      <span class="eyebrow">How It Works</span>
      <h2>Open It, Clean It, Get On With Your Day</h2>
      <p class="lead">Kodiak Cleaner shows you what it found before anything is removed, so you always stay in charge.</p>
    </div>
    <div class="grid cols-3 steps">
      <div class="card step reveal"><h3>Analyze</h3><p>Tick what you want to clean and click Analyze. Kodiak Cleaner measures how much each item would free. Nothing is deleted yet.</p></div>
      <div class="card step reveal"><h3>Review</h3><p>Check the results, sort by size or by when each item was last cleaned, and untick anything you'd rather keep.</p></div>
      <div class="card step reveal"><h3>Clean</h3><p>Click Run Cleaner. Browsers that are open and files in use are skipped, so nothing important is pulled out from under you.</p></div>
    </div>
  </div>
</section>

<section class="alt">
  <div class="wrap">
    <div class="section-head center reveal">
      <span class="eyebrow">Why People Choose It</span>
      <h2>Everything You Need, Nothing You Don't</h2>
    </div>
    <div class="grid cols-3">
      ${highlights.map(([ic, t, d]) => `<div class="card hover reveal"><span class="icon">${icons[ic]}</span><h3>${t}</h3><p>${d}</p></div>`).join("\n      ")}
      <a class="card hover reveal link-card" href="/features/list/"><span class="icon">${icons.list}</span><h3>See Every Feature</h3><p class="muted">The full list for Windows, Mac and Linux, each marked Free or Supporter.</p></a>
    </div>
  </div>
</section>

<section>
  <div class="wrap">
    <div class="showcase reveal">
      <div class="text">
        <span class="eyebrow">Cleaner</span>
        <h2>Clean Windows and Every Browser You Use</h2>
        <p>Temporary files, the Recycle Bin, thumbnail and shader caches, error reports and update leftovers, plus cache, history and cookies for eleven browsers, each shown with its own icon.</p>
        <ul>
          <li>Search any item and sort the list five ways.</li>
          <li>Reset to Default brings back the safe basics in one click.</li>
          <li>Schedule named cleanings for the days you choose.</li>
        </ul>
        <a href="/features/#cleaning">More about cleaning →</a>
      </div>
      ${shot(ctx, "cleaner-dark.png", "Cleaner page in Honey dark mode")}
    </div>
    <div class="showcase flip reveal">
      <div class="text">
        <span class="eyebrow">Programs</span>
        <h2>Uninstall Cleanly, Leftovers and All</h2>
        <p>Kodiak Cleaner backs up a program's registry entries, runs its own uninstaller, waits until it has truly finished, then offers to remove the folders, shortcuts and registry keys it left behind.</p>
        <ul>
          <li>Uninstall several programs, one at a time.</li>
          <li>Repair, Rename or Delete Entry for broken listings.</li>
          <li>History records every change and can restore it.</li>
        </ul>
        <a href="/features/#programs">More about Programs →</a>
      </div>
      ${shot(ctx, "programs.png", "Programs page with installed programs and their icons")}
    </div>
    <div class="showcase reveal">
      <div class="text">
        <span class="eyebrow">Tools</span>
        <h2>Eight Handy Tools, Built In</h2>
        <p>Browser Extensions, Disk Analyzer, Drive Wiper, Duplicate Finder, Empty Folders, Large Files, Scheduled Tasks and System Restore. Anything removed goes to the Recycle Bin first. System Information has its own page, with an Advanced view and reports you can export.</p>
        <a href="/features/#tools">See all the tools →</a>
      </div>
      ${shot(ctx, "disk-analyzer.png", "Disk Analyzer showing folder sizes and the largest files")}
    </div>
    <div class="showcase flip reveal">
      <div class="text">
        <span class="eyebrow">Linux</span>
        <h2>Kodiak Cleaner for Linux</h2>
        <p>The Linux edition cleans system junk, browser data, package manager downloads and program caches, and its Triggers page lists everything that starts automatically: autostart entries, systemd services and timers, cron jobs and startup scripts.</p>
        <ul>
          <li>DEB package for Debian, Ubuntu and Mint, or an AppImage for other distributions.</li>
          <li>The same color schemes, and the same supporter code.</li>
          <li>Every change to a trigger is backed up and can be restored.</li>
        </ul>
        <a href="/linux/">More about the Linux edition →</a>
      </div>
      ${shot(ctx, "linux-triggers.png", "Kodiak Cleaner for Linux, Triggers page", { bar: "Kodiak Cleaner for Linux" })}
    </div>
    <div class="showcase reveal">
      <div class="text">
        <span class="eyebrow">Mac</span>
        <h2>Kodiak Cleaner for Mac</h2>
        <p>A native Mac app for macOS 26 or newer. It cleans macOS junk, browser data and developer tool caches, and its Triggers page lists launch agents, launch daemons, cron jobs and shell startup files.</p>
        <ul>
          <li>A disk image or a ZIP, for Apple silicon and Intel Macs.</li>
          <li>No network code at all. Nothing leaves your Mac.</li>
          <li>The same color schemes, and the same supporter code.</li>
        </ul>
        <a href="/mac/">More about the Mac edition →</a>
      </div>
      ${shot(ctx, "mac-cleaner-honey-dark.png", "Kodiak Cleaner for Mac, Cleaner page in Honey dark mode", { bar: "Kodiak Cleaner for Mac" })}
    </div>
  </div>
</section>

<section class="alt">
  <div class="wrap center">
    <div class="section-head reveal">
      <span class="eyebrow">Make It Yours</span>
      <h2>Honey, Classic and Glacier, Light or Dark</h2>
      <p class="lead">Three color schemes, accent colors for everyone, and light or dark mode that can follow Windows. Every combination meets the WCAG 2.2 AA contrast standard.</p>
    </div>
    <div class="tabs" role="tablist" aria-label="Color schemes">
      ${themes.map(([id, label]) => `<button type="button" role="tab" id="tab-${id}" aria-controls="panel-${id}" aria-selected="${id === defaultTheme}" tabindex="${id === defaultTheme ? 0 : -1}">${label}</button>`).join("")}
    </div>
    ${themes.map(([id, label, file, alt]) => `<div class="tab-panel" role="tabpanel" id="panel-${id}" aria-labelledby="tab-${id}"${id === defaultTheme ? "" : " hidden"}>${shot(ctx, file, alt)}</div>`).join("\n    ")}
    <p class="muted mt">Honey is free for everyone. Classic and Glacier are a thank-you for supporters. Every feature is free for everyone.</p>
  </div>
</section>

<section>
  <div class="wrap">
    <div class="section-head center reveal">
      <span class="eyebrow">Take a Look</span>
      <h2>A Closer Look</h2>
      <p class="lead">Click any screenshot to see it full size.</p>
    </div>
    <div class="gallery">
      ${gallery.map(([f, t, d]) => `<figure class="reveal">${shot(ctx, f, t + " in Kodiak Cleaner")}<figcaption>${t}<span>${d}</span></figcaption></figure>`).join("\n      ")}
    </div>
  </div>
</section>

<section class="alt">
  <div class="wrap narrow center reveal">
    <span class="eyebrow">Why It Was Made</span>
    <p class="quote">For more than two decades, I used the same cleaning app. The latest upgrade changed that.</p>
    <p class="lead">Routine tasks took more effort and familiar features got harder to find, so Ted built his own cleaner and made it open source: a simple, private and practical tool that respects your time.</p>
    <div class="signature"><img src="/images/logo.svg" alt="" width="48" height="48"><span>Ted Tabaka, creator of Kodiak Cleaner</span></div>
    <p class="mt"><a class="btn" href="/why-it-was-made/">Read the Full Story</a></p>
  </div>
</section>

<section>
  <div class="wrap">
    <div class="showcase reveal">
      <div class="text">
        <span class="eyebrow">Privacy</span>
        <h2>Everything Stays on Your PC</h2>
        <ul class="ticks">
          <li><strong>No telemetry</strong>, analytics, crash reports, accounts or ads. Nothing is sent automatically, ever.</li>
          <li><strong>Settings and backups</strong> are plain files on your own PC.</li>
          <li><strong>Updates are the only network use</strong>, and only when you click Check Now or turn on automatic updates (off by default).</li>
          <li><strong>Feedback</strong> opens a message in your own email app. Nothing is sent until you press Send.</li>
        </ul>
        <p class="mt"><a href="/privacy/">Read the Privacy Policy →</a></p>
      </div>
      ${shot(ctx, "about.png", "About page with the version, privacy summary and credits")}
    </div>
  </div>
</section>

<section class="cta">
  <div class="wrap">
    <div class="card reveal">
      <img src="/images/logo.svg" alt="" width="110" height="110">
      <h2>Ready for a Cleaner PC?</h2>
      <p class="lead cta-lead">Free, open source and private, for Windows, Mac and Linux.</p>
      <div class="btn-row centered">
        <a class="btn primary lg" href="/download/">${icons.download}Download Kodiak Cleaner</a>
        <a class="btn lg" href="/contact/">${icons.mail}Contact Us</a>
      </div>
    </div>
  </div>
</section>`;

  // Launch video, shown over the page once per visitor (see js/intro.js).
  const lv = ctx.launchVideo;
  const intro = lv ? `
<dialog class="intro" id="intro" aria-labelledby="intro-title" data-intro-id="${esc(lv.id)}">
  <button class="icon-btn close" type="button" data-intro-close aria-label="Close the video">${icons.close}</button>
  <h2 id="intro-title" class="intro-title">${esc(lv.title)}</h2>
  <video src="/video/${esc(lv.file)}"${lv.posterExists ? ` poster="/video/${esc(lv.poster)}"` : ""} muted playsinline preload="none" controls></video>
  <div class="btn-row centered">
    <button class="btn" type="button" data-intro-sound>${icons.volume}Turn Sound On</button>
    <button class="btn primary" type="button" data-intro-close>Continue to the Site</button>
  </div>
</dialog>` : "";

  return {
    id: "home",
    path: "/",
    out: "index.html",
    title: "Home",
    description: "Kodiak Cleaner is a free, open source cleaner for Windows, Mac and Linux with no telemetry, no ads and no upsells. Clean junk files, fix the registry, manage start-up programs and uninstall software cleanly.",
    body: body + intro,
    scripts: lv ? `<script src="/js/intro.js?v=${ctx.buildId}" defer></script>` : "",
  };
}
