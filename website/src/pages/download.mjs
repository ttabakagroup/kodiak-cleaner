import { icons } from "../icons.mjs";
import { betaNotice, pageHead } from "../layout.mjs";
import { esc } from "../markdown.mjs";
import { linuxDownloads } from "./linux.mjs";
import { macDownloads } from "./mac.mjs";

export default function download(ctx) {
  const { cfg, latest, linuxLatest, macLatest } = ctx;
  const d = ctx.dl(latest);

  const body = `
${pageHead("Download", "Kodiak Cleaner for Windows, Mac and Linux. Free for everyone, with every feature.")}
<nav class="jump" aria-label="On this page"><div class="wrap chips">
  <a class="chip-link" href="#windows">Windows</a>
  <a class="chip-link" href="#mac">Mac</a>
  <a class="chip-link" href="#linux">Linux</a>
</div></nav>

<section class="tight"><div class="wrap narrow">${betaNotice()}</div></section>

<section class="tight" id="windows">
  <div class="wrap">
    <div class="section-head"><span class="eyebrow">Windows 10 and 11</span><h2>Kodiak Cleaner for Windows</h2><p class="lead">Version ${esc(latest.name)}, released ${ctx.longDate(latest.date)}.</p></div>
    <div class="grid cols-2">
      <div class="card dl-card reveal">
        <span class="chip badge">Recommended</span>
        <span class="icon">${icons.package}</span>
        <h3>Installer</h3>
        <p class="muted">${d.installerIsMsi ? "The MSI installer" : "The MSI installer, packed in a ZIP"}. Best for most people.</p>
        <ul>
          <li>Adds Start menu shortcuts.</li>
          <li>Installs updates for you when you ask.</li>
          <li>Can skip the administrator prompt.</li>
        </ul>
        <a class="btn primary lg" href="${d.installer}">${icons.download}<span>Download Installer<small>${d.installerIsMsi ? "MSI" : "ZIP with the MSI inside"}${d.installerSize ? " · " + esc(d.installerSize) : ""}</small></span></a>
        <p class="file">${esc(d.installerFile)}</p>
      </div>
      <div class="card dl-card reveal">
        <span class="icon">${icons.drive}</span>
        <h3>Portable</h3>
        <p class="muted">Runs without installing, from any folder or a USB drive.</p>
        <ul>
          <li>Leaves no trace outside its own folder.</li>
          <li>Settings stay in KodiakCleaner.ini beside it.</li>
          <li>Includes a Docs folder with the changelog, license and privacy policy.</li>
        </ul>
        <a class="btn lg" href="${d.portable}">${icons.download}<span>Download Portable<small>ZIP${d.portableSize ? " · " + esc(d.portableSize) : ""}</small></span></a>
        <p class="file">${esc(d.portableFile)}</p>
      </div>
    </div>
    <p class="center muted mt">Both are a single self-contained program, so there's nothing else to install. Looking for an older version? See the <a href="/archive/">Archive</a> or <a href="${cfg.repo}/releases" rel="noopener">all releases on GitHub</a>.</p>
  </div>
</section>


<section class="alt">
  <div class="wrap">
    <div class="grid cols-3">
      <div class="card reveal"><span class="icon">${icons.laptop}</span><h3>Windows Requirements</h3><ul class="ticks">
        <li>Windows 10 or Windows 11, 64-bit (x64)</li>
        <li>About 100 MB of free disk space</li>
        <li>Administrator rights only for system-wide items</li>
      </ul></div>
      <div class="card reveal"><span class="icon">${icons.shield}</span><h3>Windows SmartScreen</h3><p>The installer isn't code-signed yet, so Windows may warn you the first time. Click <strong>More info</strong>, then <strong>Run anyway</strong>.</p><p class="muted">You can read the source on GitHub and build it yourself.</p></div>
      <div class="card reveal"><span class="icon">${icons.check}</span><h3>Check Your Download</h3><p>GitHub lists a SHA-256 checksum beside each file on the release page. To compare, run <code>Get-FileHash</code> on the file in PowerShell, or <code>sha256sum</code> on Linux.</p></div>
    </div>
  </div>
</section>

<section>
  <div class="wrap narrow">
    <div class="section-head center"><h2>Install, Update and Uninstall on Windows</h2></div>
    <div class="grid cols-2">
      <div class="card"><h3>Install</h3><ol>
        ${d.installerIsMsi ? `<li>Download the <strong>Installer</strong> above.</li>
        <li>Double-click the <code>.msi</code> and follow the steps.</li>` : `<li>Download the <strong>Installer</strong> ZIP above.</li>
        <li>Open the ZIP and extract the <code>.msi</code> file.</li>
        <li>Double-click the <code>.msi</code> and follow the steps.</li>`}
      </ol><p class="muted"><strong>Portable:</strong> extract the Portable ZIP to any folder and run <code>KodiakCleaner.exe</code>.</p></div>
      <div class="card"><h3>Update</h3><ul>
        <li><strong>Installed:</strong> click Check Now under Settings &gt; Updates, or turn on automatic updates there. You can also run a newer installer over the old one.</li>
        <li><strong>Portable:</strong> replace <code>KodiakCleaner.exe</code> and keep <code>KodiakCleaner.ini</code> and the <code>Backups</code> and <code>Logs</code> folders.</li>
      </ul><p class="muted">Your settings and backups are kept either way, and What's New shows what changed.</p></div>
      <div class="card"><h3>Uninstall</h3><p>Uninstall Kodiak Cleaner from Windows Settings &gt; Apps. It removes its scheduled cleanings, start-up entry, Recycle Bin menu item and settings, then asks whether to delete your backups and logs too. Keep them if you might still want to undo a registry fix. For the portable version, just delete its folder.</p></div>
      <div class="card"><h3>Release Channels</h3><p>Kodiak Cleaner is in the <strong>Dev</strong> channel (0.8.x). <strong>Beta</strong> (0.9.x) comes next, then <strong>Stable</strong> (1.x). Every channel installs to <code>Program Files\\Kodiak Cleaner</code>, and updates stay on their own channel.</p></div>
    </div>
    <div class="btn-row centered mt-xl">
      <a class="btn" href="/changelog/">${icons.file}What's New in ${esc(latest.name)}</a>
      <a class="btn" href="${d.releasePage}" rel="noopener">${icons.github}Release Page on GitHub</a>
      <a class="btn" href="${cfg.repo}" rel="noopener">${icons.code}Source Code</a>
    </div>
  </div>
</section>

<section id="mac">
  <div class="wrap">
    <div class="section-head"><span class="eyebrow">macOS 26 or Newer</span><h2>Kodiak Cleaner for Mac</h2><p class="lead">Version ${esc(macLatest.name)}, released ${ctx.longDate(macLatest.date)}, for Apple silicon and Intel Macs. This Dev build isn't signed yet, so the first time you open it, use Open Anyway in Privacy &amp; Security. <a href="/mac/">See the steps and learn more →</a></p></div>
    ${macDownloads(ctx)}
  </div>
</section>

<section class="alt" id="linux">
  <div class="wrap">
    <div class="section-head"><span class="eyebrow">64-Bit Linux</span><h2>Kodiak Cleaner for Linux</h2><p class="lead">Version ${esc(linuxLatest.name)}, released ${ctx.longDate(linuxLatest.date)}. Cleaner and Triggers, in the same color schemes. <a href="/linux/">Learn more about the Linux edition →</a></p></div>
    ${linuxDownloads(ctx)}
  </div>
</section>`;

  return {
    id: "download",
    path: "/download/",
    out: "download/index.html",
    title: "Download",
    description: `Download Kodiak Cleaner for Windows 10 and 11 (MSI installer or portable ZIP), for Mac (DMG or ZIP) or for Linux (DEB or AppImage). Free and open source.`,
    body,
  };
}
