import { icons } from "../icons.mjs";
import { pageHead } from "../layout.mjs";
import { esc } from "../markdown.mjs";

// What open source means for Kodiak Cleaner, with the full MIT License text (content/LICENSE, synced from the app).
export default function openSource(ctx) {
  const { cfg } = ctx;
  // A to Z.
  const freedoms = [
    ["code", "Read and Change the Code", "Every line of Kodiak Cleaner for Windows, Mac and Linux is published under the MIT License. Anyone may read it, learn from it, change it and build their own copy."],
    ["gift", "Free for Everyone", "Use it at home or at work, on as many computers as you like. No account, no trial and no paid version. Supporter codes only unlock two extra color schemes and a badge."],
    ["shield", "Nothing Hidden", "No telemetry, ads or tracking code, in the app or on this website. What the code does is what the app does."],
    ["globe", "Share It", "Pass Kodiak Cleaner on, put it on a USB drive for a friend, or include it in your own project. Just keep the copyright and license notice with it."],
  ];
  // Each edition's own toolkit, Windows first (the most complete edition), then A to Z.
  const builds = [
    ["windows", "Windows", "C# with .NET 8 and WPF. The MSI installer is made with the WiX Toolset, and build.ps1 builds the installer and the portable ZIP."],
    ["laptop", "Mac", "A native SwiftUI app for macOS 26 or newer, for Apple silicon and Intel Macs."],
    ["terminal", "Linux", "Python with GTK 4, packaged as a DEB and an AppImage."],
  ];
  const body = `
${pageHead("Open Source", "Kodiak Cleaner is free and open source software under the MIT License. Every feature is free, and anyone may read, change and share the code.")}
<section class="tight">
  <div class="wrap narrow">
    <div class="section-head center reveal"><span class="os-badge">${icons.code}Open Source · MIT License · Copyright © ${cfg.copyrightYear} Ted Tabaka</span><h2>What Open Source Means Here</h2></div>
    <div class="grid cols-2">
      ${freedoms.map(([ic, t, d]) => `<div class="card reveal"><span class="icon">${icons[ic]}</span><h3>${t}</h3><p>${d}</p></div>`).join("\n      ")}
    </div>
  </div>
</section>

<section class="alt">
  <div class="wrap">
    <div class="section-head center reveal"><span class="eyebrow">The Code</span><h2>One Repository for Every Edition</h2><p class="lead">The Windows, Mac and Linux apps, the installer and this website all live together in one GitHub repository.</p></div>
    <div class="grid cols-3">
      ${builds.map(([ic, t, d]) => `<div class="card reveal"><span class="icon">${icons[ic]}</span><h3>${t}</h3><p>${d}</p></div>`).join("\n      ")}
    </div>
    <div class="btn-row centered mt-xl">
      <a class="btn primary lg" href="${cfg.repo}" rel="noopener">${icons.github}View the Code on GitHub</a>
      <a class="btn lg" href="/changelog/">${icons.list}See Every Change</a>
    </div>
  </div>
</section>

<section>
  <div class="wrap narrow">
    <div class="section-head center reveal"><span class="eyebrow">Help Improve It</span><h2>Ideas, Bugs and Fixes Welcome</h2><p class="lead">Found a bug, want a feature or spotted something in the code? Send it through Contact Us. Security problems have their own topic, so they reach Ted first.</p></div>
    <div class="btn-row centered">
      <a class="btn primary" href="/contact/?topic=Bug%20Report">${icons.mail}Report a Bug</a>
      <a class="btn" href="/contact/?topic=Feature%20Request">${icons.sparkle}Request a Feature</a>
      <a class="btn" href="/contact/?topic=Security">${icons.shield}Report a Security Problem</a>
    </div>
  </div>
</section>

<section class="alt" id="license">
  <div class="wrap narrow">
    <div class="section-head center reveal"><span class="eyebrow">License</span><h2>The MIT License</h2><p class="lead">The same license text that comes with every download, in the Docs folder.</p></div>
    <div class="card pad license-text">${ctx.content.license.trim().split(/\n\s*\n/).map((para) => `<p>${esc(para.replace(/\s*\n\s*/g, " "))}</p>`).join("")}</div>
    <p class="muted mt center">Kodiak Cleaner also uses the Fredoka font (SIL Open Font License 1.1) and .NET and WPF (MIT License). The About page in the app lists every credit.</p>
  </div>
</section>`;
  return {
    id: "open-source",
    path: "/open-source/",
    out: "open-source/index.html",
    title: "Open Source",
    description: "Kodiak Cleaner is free and open source under the MIT License: read, change and share the code for the Windows, Mac and Linux editions, with the full license text.",
    body,
  };
}
