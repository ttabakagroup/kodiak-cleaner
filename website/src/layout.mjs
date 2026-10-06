import { icons } from "./icons.mjs";
import { esc } from "./markdown.mjs";

// Main menu. Home leads; the rest follow the order people usually look for them.
export const NAV = [
  { id: "home", href: "/", label: "Home" },
  { id: "features", href: "/features/", label: "Features" },
  { id: "linux", href: "/linux/", label: "Linux" },
  { id: "download", href: "/download/", label: "Download" },
  { id: "changelog", href: "/changelog/", label: "Changelog" },
  { id: "archive", href: "/archive/", label: "Archive" },
  { id: "contact", href: "/contact/", label: "Contact Us" },
];

export const wordmark = `<span class="wordmark"><span class="w1">Kodiak</span> <span class="w2">Cleaner</span></span>`;

export function shot(ctx, file, alt, { eager = false, bar = "Kodiak Cleaner" } = {}) {
  const size = ctx.imageSizes[file] || [1440, 900];
  return `<div class="frame"><div class="bar"><i></i><i></i><i></i><span>${esc(bar)}</span></div>` +
    `<button class="zoom" type="button" aria-label="View larger: ${esc(alt)}"><img src="/images/screens/${file}" alt="${esc(alt)}" width="${size[0]}" height="${size[1]}" ${eager ? 'fetchpriority="high"' : 'loading="lazy"'} decoding="async"></button></div>`;
}

export function betaNotice() {
  return `<div class="notice" role="note">${icons.alert}<p><strong>Beta software: use at your own risk.</strong> Kodiak Cleaner is still in development, and more testing is needed before every feature can be confirmed to work as intended. It deletes things by design, so check what's selected before you clean and keep your own backups of anything important.</p></div>`;
}

export function pageHead(title, lead) {
  return `<header class="page-head"><div class="wrap"><img class="mark" src="/images/logo.svg" alt="" width="128" height="128"><div><h1>${title}</h1><p class="lead">${lead}</p></div></div></header>`;
}

export function layout(page, ctx) {
  const { cfg, latest, linuxLatest } = ctx;
  const url = cfg.siteUrl.replace(/\/$/, "") + page.path;
  const fullTitle = page.id === "home" ? "Kodiak Cleaner: A Free, Private Cleaner for Windows and Linux" : `${page.title} | Kodiak Cleaner`;
  const nav = NAV.map((n) => `<a href="${n.href}"${n.id === page.id ? ' aria-current="page"' : ""}>${n.label}</a>`).join("");
  return `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>${esc(fullTitle)}</title>
<meta name="description" content="${esc(page.description)}">
<link rel="canonical" href="${url}">
<meta name="color-scheme" content="light dark">
<meta name="theme-color" content="#F5EFE6" media="(prefers-color-scheme: light)">
<meta name="theme-color" content="#1E1815" media="(prefers-color-scheme: dark)">
<meta property="og:type" content="website">
<meta property="og:site_name" content="Kodiak Cleaner">
<meta property="og:title" content="${esc(fullTitle)}">
<meta property="og:description" content="${esc(page.description)}">
<meta property="og:url" content="${url}">
<meta property="og:image" content="${cfg.siteUrl.replace(/\/$/, "")}/images/social-card.png">
<meta name="twitter:card" content="summary_large_image">
<link rel="icon" href="/images/logo.svg" type="image/svg+xml">
<link rel="icon" href="/favicon.ico" sizes="any">
<link rel="apple-touch-icon" href="/images/logo-512.png">
<link rel="preload" href="/fonts/Fredoka-SemiBold.ttf" as="font" type="font/ttf" crossorigin>
<link rel="stylesheet" href="/css/theme.css?v=${ctx.buildId}">
<link rel="stylesheet" href="/css/site.css?v=${ctx.buildId}">
<script src="/js/theme-init.js?v=${ctx.buildId}"></script>
</head>
<body>
<a class="skip" href="#main">Skip to Content</a>
<div class="site-header"><div class="wrap">
  <a class="brand" href="/" aria-label="Kodiak Cleaner home"><img src="/images/logo.svg" alt="" width="42" height="42">${wordmark}</a>
  <nav class="nav" id="site-nav" aria-label="Main">${nav}</nav>
  <div class="header-tools">
    <a class="icon-btn" href="${cfg.repo}" rel="noopener" aria-label="Kodiak Cleaner on GitHub" title="GitHub">${icons.github}</a>
    <button class="icon-btn" type="button" data-theme-toggle aria-label="Appearance">${icons.monitor.replace("<svg", '<svg data-mode="system"')}${icons.sun.replace("<svg", '<svg data-mode="light" hidden')}${icons.moon.replace("<svg", '<svg data-mode="dark" hidden')}</button>
    <button class="icon-btn menu-btn" type="button" data-menu aria-controls="site-nav" aria-expanded="false" aria-label="Menu">${icons.menu}</button>
  </div>
</div></div>
<main id="main">
${page.body}
</main>
<footer class="site-footer">
  <div class="wrap">
    <div class="more-apps">${icons.globe.replace("<svg", '<svg width="32" height="32"')}<p><strong>More apps by Ted Tabaka.</strong> Kodiak Cleaner is one of several apps Ted builds. Learn more about him and his other apps at tedtabaka.com.</p><a class="btn" href="${cfg.homepage}" rel="noopener">Visit tedtabaka.com ${icons.external}</a></div>
    <div class="footer-grid">
      <div>
        <div class="footer-brand"><img src="/images/logo.svg" alt="" width="56" height="56">${wordmark}</div>
        <p class="muted">Not a Crap Cleaner. A Cleaner App, Built Around What Matters.</p>
        <p class="muted">Windows: version ${esc(latest.name)}, for Windows 10 and 11 (64-bit)</p>
        <p class="muted">Linux: version ${esc(linuxLatest.name)}, for 64-bit Linux with GTK 4</p>
      </div>
      <div><h2>Kodiak Cleaner</h2><ul>
        <li><a href="/archive/">Archive</a></li>
        <li><a href="/changelog/">Changelog</a></li>
        <li><a href="/download/">Download</a></li>
        <li><a href="/features/">Features</a></li>
        <li><a href="/linux/">Kodiak Cleaner for Linux</a></li>
        <li><a href="/why-it-was-made/">Why It Was Made</a></li>
      </ul></div>
      <div><h2>Help and Contact</h2><ul>
        <li><a href="/contact/">Contact Us</a></li>
        <li><a href="/privacy/">Privacy Policy</a></li>
        <li><a href="/contact/?topic=Security">Report a Security Problem</a></li>
        <li><a href="/terms/">Terms of Use</a></li>
      </ul></div>
      <div><h2>Elsewhere</h2><ul>
        <li><a href="${cfg.repo}" rel="noopener">GitHub</a></li>
        <li><a href="${cfg.repo}/releases" rel="noopener">GitHub Releases</a></li>
        <li><a href="${cfg.kofi}" rel="noopener">Support on Ko-fi</a></li>
        <li><a href="${cfg.homepage}" rel="noopener">tedtabaka.com</a></li>
      </ul></div>
    </div>
    <div class="footer-bottom">
      <span>Free and open source under the <a href="${cfg.repo}/blob/main/LICENSE" rel="noopener">MIT License</a>. Copyright © ${cfg.copyrightYear} Ted Tabaka.</span>
      <span>No ads and no tracking on this site or in the app.</span>
    </div>
  </div>
</footer>
<dialog class="lightbox" id="lightbox" aria-label="Screenshot"><button class="icon-btn close" type="button" aria-label="Close">${icons.close}</button><img src="data:," alt=""><p></p></dialog>
<script src="/js/site.js?v=${ctx.buildId}" defer></script>
${page.scripts || ""}
</body>
</html>
`;
}
