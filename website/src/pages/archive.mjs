import { icons } from "../icons.mjs";
import { pageHead } from "../layout.mjs";
import { esc } from "../markdown.mjs";

export default function archive(ctx) {
  const { cfg, releases, latest } = ctx;
  const channel = (r) => r.channel[0].toUpperCase() + r.channel.slice(1);

  const rows = releases.map((r) => {
    const d = ctx.dl(r);
    const areas = [...new Set(r.sections.map((s) => s.title).filter(Boolean))].sort((a, b) => a.localeCompare(b));
    const files = d.published
      ? `<a href="${d.installer}">${d.installerIsMsi ? "Installer MSI" : "Installer ZIP"}</a><br><a href="${d.portable}">Portable ZIP</a><br><a href="${d.releasePage}" rel="noopener">Release Page</a>`
      : `<span class="muted">Not published</span>`;
    return `<tr id="v${r.version}">
      <td><strong><a href="/changelog/#v${r.version}">${esc(r.name)}</a></strong>${r === latest ? ' <span class="chip">Latest</span>' : ""}</td>
      <td class="nowrap">${ctx.shortDate(r.date)}</td>
      <td>${channel(r)}</td>
      <td>${areas.map((a) => `<span class="chip">${esc(a)}</span>`).join("")}</td>
      <td>${files}</td>
    </tr>`;
  }).join("\n");

  const published = releases.filter((r) => ctx.dl(r).published).length;

  const body = `
${pageHead("Archive", "Every version of Kodiak Cleaner, with release dates, what changed and where to get each one.")}

<section class="tight">
  <div class="wrap">
    <div class="grid cols-4">
      <div class="card"><span class="icon">${icons.sparkle}</span><h3>${esc(latest.name)}</h3><p class="muted">Latest version, released ${ctx.longDate(latest.date)}.</p></div>
      <div class="card"><span class="icon">${icons.archive}</span><h3>${releases.length} Versions</h3><p class="muted">Listed below, newest first. ${published} published as downloads.</p></div>
      <div class="card"><span class="icon">${icons.code}</span><h3>MIT License</h3><p class="muted">Every version is free and open source.</p></div>
      <div class="card"><span class="icon">${icons.laptop}</span><h3>Windows 10 and 11</h3><p class="muted">64-bit (x64) only.</p></div>
    </div>
  </div>
</section>

<section class="tight">
  <div class="wrap">
    <div class="section-head"><h2>All Versions</h2><p class="lead">Kodiak Cleaner for Windows, newest first. Click a version to read its full notes in the changelog. Downloads stay on GitHub for every published release.</p></div>
    <div class="table-wrap"><table>
      <thead><tr><th scope="col">Version</th><th scope="col">Released</th><th scope="col">Channel</th><th scope="col">Areas Changed</th><th scope="col">Downloads</th></tr></thead>
      <tbody>${rows}</tbody>
    </table></div>
  </div>
</section>

<section class="alt">
  <div class="wrap">
    <div class="section-head center"><h2>Good to Know</h2></div>
    <div class="grid cols-3">
      <div class="card"><h3>File Names</h3><p>Since 0.8.31 the downloads keep the same names every release, with the channel in the name and the version in the release tag:</p><p><code>KodiakCleaner-Dev-Setup.msi</code><br><code>KodiakCleaner-Dev-Portable.zip</code></p><p class="muted">From 0.8.31 to 0.8.36 the installer came as KodiakCleaner-Dev-Setup.zip. Earlier releases were named by version, such as <code>KodiakCleaner-0.8.27-x64-msi-dev.zip</code>.</p></div>
      <div class="card"><h3>Release Channels</h3><p><strong>Dev</strong> (0.8.x) is early work, marked as a pre-release. <strong>Beta</strong> (0.9.x) is feature-complete and ready for wider testing. <strong>Stable</strong> (1.x) is the full release.</p><p class="muted">Every channel installs to the same folder, and installing one replaces another.</p></div>
      <div class="card"><h3>Version Numbers</h3><p>Version numbers restarted at 0.8.0 Dev on October 4, 2026, when Kodiak Cleaner was brought up to Ted's app standard. An earlier 1.2.0 preview was never published.</p></div>
      <div class="card"><h3>Older Versions</h3><p>Versions marked "Not published" were development builds made before the GitHub repository opened, so they have no download. Their notes stay in the changelog.</p><p class="muted">Newer versions include every fix from older ones, so the latest is always the one to use.</p></div>
      <div class="card"><h3>Checking a Download</h3><p>Each file on a GitHub release page shows its SHA-256 checksum. Compare it with <code>Get-FileHash</code> in PowerShell. Kodiak Cleaner's own updater does this check for you.</p></div>
      <div class="card"><h3>Kodiak Cleaner for Linux</h3><p>The Linux edition has its own version numbers, starting at 0.8.0 Dev on October 5, 2026. Its changelog is on the <a href="/linux/#changelog">Linux page</a>.</p><p class="muted">Its downloads are KodiakCleaner-Dev-Deb.zip and KodiakCleaner-Dev-AppImage.zip.</p></div>
            <div class="card"><h3>Source Code</h3><p>Every release is tagged in the repository (for example <code>v0.8.27-dev</code>), so the exact code behind any download can be read or built.</p><p><a href="${cfg.repo}/tags" rel="noopener">Browse tags on GitHub →</a></p></div>
    </div>
  </div>
</section>`;

  return {
    id: "archive",
    path: "/archive/",
    out: "archive/index.html",
    title: "Archive",
    description: "Every Kodiak Cleaner version with release dates, channels, what changed and download links for published releases.",
    body,
  };
}
