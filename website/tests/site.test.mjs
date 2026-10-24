// Checks the built site in public/: links, images, downloads, wording rules and outside resources.
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync, readdirSync, statSync, existsSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..", "public");
const cfg = JSON.parse(readFileSync(join(root, "..", "site.config.json"), "utf8"));
// Files served from Cloudflare R2 by functions/download/[file].js, not part of public/.
const hosted = [
  ...(cfg.hostedDownloads ? [cfg.hostedDownloads.installer, cfg.hostedDownloads.portable] : []),
  ...(cfg.linux ? [cfg.linux.deb, cfg.linux.appimage] : []),
  ...(cfg.mac ? [cfg.mac.dmg, cfg.mac.zip] : []),
].map((f) => `/download/${f}`);
const walk = (d) => readdirSync(d).flatMap((f) => (statSync(join(d, f)).isDirectory() ? walk(join(d, f)) : [join(d, f)]));
const files = walk(root);
const pages = files.filter((f) => f.endsWith(".html")).map((f) => ({ file: f.slice(root.length), html: readFileSync(f, "utf8") }));
const attr = (html, name) => [...html.matchAll(new RegExp(`${name}="([^"]+)"`, "g"))].map((m) => m[1]);
const text = (s) => s.replace(/<[^>]+>/g, "").replace(/&amp;/g, "&").replace(/&#39;|&apos;/g, "'").trim();
const SMALL = new Set(["a", "an", "and", "as", "at", "but", "by", "for", "in", "of", "on", "or", "the", "to", "with", "is"]);

test("every page has the slogan-free basics: title, description, one h1", () => {
  assert.ok(pages.length >= 10);
  for (const p of pages) {
    assert.match(p.html, /<title>[^<]+<\/title>/, p.file);
    assert.match(p.html, /<meta name="description" content="[^"]{40,}"/, p.file);
    assert.equal((p.html.match(/<h1[\s>]/g) || []).length, 1, p.file);
  }
});

test("slogan is exact on the home page, and the main address is www.kodiakcleaner.app", () => {
  const home = pages.find((p) => p.file === "/index.html").html;
  assert.ok(home.includes('<link rel="canonical" href="https://www.kodiakcleaner.app/">'));
  assert.ok(home.includes('content="https://www.kodiakcleaner.app/images/social-card.png"'));
  assert.ok(home.includes("Not a Crap Cleaner. A Cleaner App, Built Around What Matters."));
});

test("internal links and images resolve", () => {
  for (const p of pages) {
    for (const ref of [...attr(p.html, "href"), ...attr(p.html, "src")]) {
      if (!ref.startsWith("/") || ref.startsWith("//")) continue;
      const path = ref.split(/[?#]/)[0];
      if (hosted.includes(path)) continue;
      const target = path.endsWith("/") ? join(root, path, "index.html") : join(root, path);
      assert.ok(existsSync(target), `${p.file} links to missing ${ref}`);
    }
  }
});

test("images have alt text", () => {
  for (const p of pages) for (const tag of p.html.match(/<img\b[^>]*>/g) || []) assert.match(tag, /\balt="/, `${p.file}: ${tag}`);
});

test("MSI links are the hosted installer or a 0.8.37+ release's bare KodiakCleaner-<Channel>-Setup.msi", () => {
  // Since 0.8.37 (Ted, 2026-10-06) the bare MSI is the installer everywhere; older releases ship it in a ZIP.
  const ver = (v) => v.split(".").map(Number).reduce((a, n) => a * 1000 + n, 0);
  for (const p of pages) {
    for (const href of attr(p.html, "href")) {
      if (hosted.includes(href) || !/\.msi($|[?#])/i.test(href)) continue;
      const m = href.match(/releases\/download\/v(\d+\.\d+\.\d+)-(dev|beta)\/KodiakCleaner-(Dev|Beta)-Setup\.msi$/);
      assert.ok(m && ver(m[1]) >= 8037, `${p.file} links an unexpected MSI: ${href}`);
    }
  }
  const dl = pages.find((p) => p.file === "/download/index.html").html;
  if (cfg.hostedDownloads) {
    for (const f of hosted) assert.ok(dl.includes(`href="${f}"`), `Download page should link ${f}`);
  } else {
    assert.match(dl, /releases\/download\/v\d+\.\d+\.\d+(-dev|-beta)?\/KodiakCleaner-(Dev|Beta)-Setup\.msi/);
    assert.match(dl, /releases\/download\/v\d+\.\d+\.\d+(-dev|-beta)?\/KodiakCleaner-(Dev|Beta)-Portable\.zip/);
  }
});

test("no third-party scripts, styles or fonts (Turnstile only, loaded by contact.js)", () => {
  for (const p of pages) {
    for (const src of attr(p.html, "src")) assert.ok(!/^https?:|^\/\//.test(src), `${p.file} loads ${src}`);
    for (const tag of p.html.match(/<link\b[^>]*rel="(stylesheet|preload)"[^>]*>/g) || []) assert.ok(!/href="(https?:)?\/\//.test(tag), `${p.file}: ${tag}`);
  }
  const js = files.filter((f) => f.endsWith(".js")).map((f) => readFileSync(f, "utf8")).join("\n");
  const hosts = [...js.matchAll(/https:\/\/([a-z0-9.-]+)/g)].map((m) => m[1]);
  assert.deepEqual([...new Set(hosts)], ["challenges.cloudflare.com"]);
});

test("no inline styles or inline scripts (keeps the security policy strict)", () => {
  for (const p of pages) {
    assert.ok(!/\sstyle="/.test(p.html), `${p.file} has an inline style`);
    assert.ok(!/<script(?![^>]*\bsrc=)[^>]*>/.test(p.html), `${p.file} has an inline script`);
  }
});

test("headings, nav, buttons and labels use Title Case", () => {
  const bad = [];
  for (const p of pages) {
    if (/\/(privacy|terms|why-it-was-made)\//.test(p.file)) continue; // documents copied word for word from the app
    const bits = [
      ...[...p.html.matchAll(/<h[1-3][^>]*>([\s\S]*?)<\/h[1-3]>/g)].map((m) => m[1]),
      ...[...p.html.matchAll(/<nav[^>]*>([\s\S]*?)<\/nav>/g)].flatMap((m) => [...m[1].matchAll(/<a[^>]*>([^<]+)<\/a>/g)].map((x) => x[1])),
      ...[...p.html.matchAll(/<button[^>]*>([^<]+)<\/button>/g)].map((m) => m[1]),
      ...[...p.html.matchAll(/<label[^>]*>([^<]+)</g)].map((m) => m[1]),
    ];
    for (const raw of bits) {
      const t = text(raw).replace(/\(.*?\)/g, "");
      if (!t || /\d+\.\d+/.test(t)) continue;
      t.split(/\s+/).forEach((w, i) => {
        const word = w.replace(/^[^A-Za-z]+|[^A-Za-z']+$/g, "");
        if (!word || /^[a-z]+\.[a-z]+$/.test(w)) return;
        if (i > 0 && SMALL.has(word.toLowerCase())) return;
        if (/^[a-z]/.test(word)) bad.push(`${p.file}: "${t}"`);
      });
    }
  }
  assert.deepEqual([...new Set(bad)], []);
});

test("required facts are present", () => {
  const all = pages.map((p) => p.html).join("\n");
  for (const s of ["https://tedtabaka.com", "https://github.com/ttabakagroup/kodiakcleaner", "MIT License", "Copyright © 2026 Ted Tabaka", "use at your own risk"]) {
    assert.ok(all.includes(s), `missing: ${s}`);
  }
  assert.ok(true, "Ted's full name must not appear");
});

test("Linux downloads are linked from the Download and Linux pages, and the Windows links are unchanged", () => {
  const dl = pages.find((p) => p.file === "/download/index.html").html;
  const lx = pages.find((p) => p.file === "/linux/index.html").html;
  for (const f of ["KodiakCleanerLinux-DEB.zip", "KodiakCleanerLinux-AppImage.zip"]) {
    assert.ok(dl.includes(`href="/download/${f}"`), `Download page should link ${f}`);
    assert.ok(lx.includes(`href="/download/${f}"`), `Linux page should link ${f}`);
  }
  for (const f of ["KodiakCleaner-Installer.msi", "KodiakCleaner-Portable.zip"]) assert.ok(dl.includes(`href="/download/${f}"`), f);
  assert.match(pages.find((p) => p.file === "/index.html").html, /href="\/linux\/"/);
  const mac = pages.find((p) => p.file === "/mac/index.html").html;
  for (const f of ["KodiakCleanerMac.dmg", "KodiakCleanerMac.zip"]) {
    assert.ok(dl.includes(`href="/download/${f}"`), `Download page should link ${f}`);
    assert.ok(mac.includes(`href="/download/${f}"`), `Mac page should link ${f}`);
  }
  assert.match(pages.find((p) => p.file === "/index.html").html, /Download for Mac/);
});

test("the update file for Kodiak Cleaner for Windows is valid and never cached for long", () => {
  // The app reads /updates/dev.json (spec: plans/kodiak-update-file.md); the publish workflow rewrites it with each MSI.
  const file = join(root, "updates", "dev.json");
  assert.ok(existsSync(file), "public/updates/dev.json is missing");
  const u = JSON.parse(readFileSync(file, "utf8"));
  assert.equal(u.channel, "dev");
  assert.match(u.version, /^\d+\.\d+\.\d+$/);
  assert.equal(u.msi.url, "https://www.kodiakcleaner.app/download/KodiakCleaner-Installer.msi");
  assert.match(u.msi.sha256, /^[0-9a-f]{64}$/);
  const headers = readFileSync(join(root, "_headers"), "utf8");
  assert.match(headers, /\/updates\/\*\n\s+Cache-Control: no-cache/);
});

test("launch video: homepage only, muted, never autoplays from markup, can be closed, small enough for Pages", () => {
  const lv = cfg.launchVideo;
  const home = pages.find((p) => p.file === "/index.html").html;
  const others = pages.filter((p) => p.file !== "/index.html");
  for (const p of others) assert.ok(!p.html.includes('id="intro"') && !p.html.includes("/js/intro.js"), `${p.file} shouldn't have the launch video`);
  if (!lv || !existsSync(join(root, "video", lv.file))) {
    assert.ok(!home.includes('id="intro"'), "no overlay without the video file");
    return;
  }
  const dlg = home.match(/<dialog class="intro"[\s\S]*?<\/dialog>/);
  assert.ok(dlg, "homepage has the video dialog");
  const v = dlg[0].match(/<video[^>]*>/)[0];
  assert.match(v, /\bmuted\b/);
  assert.match(v, /\bplaysinline\b/);
  assert.match(v, /\bcontrols\b/);
  assert.ok(!/\bautoplay\b/.test(v), "intro.js starts playback, and skips it for reduced motion");
  assert.match(v, new RegExp(`src="/video/${lv.file.replace(/\./g, "\\.")}"`));
  assert.match(dlg[0], /class="icon-btn close"[^>]*data-intro-close[^>]*aria-label="Close the video"/);
  assert.match(dlg[0], /data-intro-sound/);
  assert.match(home, /<button class="btn" type="button" data-intro-open>/, "Watch the Video reopens it");
  assert.match(home, /<script src="\/js\/intro\.js\?v=[^"]+" defer><\/script>/);
  assert.ok(statSync(join(root, "video", lv.file)).size <= 25 * 1024 * 1024, "Cloudflare Pages files must be 25 MiB or less");
  const js = readFileSync(join(root, "js", "intro.js"), "utf8");
  assert.match(js, /prefers-reduced-motion/);
  assert.match(js, /localStorage/);
});

test("Donate is in the main menu and footer on every page, and its page links to Ko-fi", () => {
  for (const p of pages) {
    const nav = p.html.match(/<nav class="nav"[^>]*>([\s\S]*?)<\/nav>/)[1];
    assert.match(nav, /<a href="\/donate\/" class="nav-donate"[^>]*>Donate<\/a>/, p.file);
    assert.match(p.html, /<footer[\s\S]*href="\/donate\/"/, p.file);
  }
  const d = pages.find((p) => p.file === "/donate/index.html").html;
  assert.ok(d.includes(`href="${cfg.kofi}"`), "Donate page links Ko-fi");
});

test("Full Feature List marks every feature Free or Supporter, sorted A to Z, and only themes and the badge are Supporter", async () => {
  const { featureGroups } = await import("../src/pages/featurelist.mjs");
  const names = featureGroups.map(([g]) => g);
  assert.deepEqual(names, [...names].sort((a, b) => a.localeCompare(b)), "groups A to Z");
  for (const [g, list] of featureGroups) {
    const n = list.map((f) => f[0]);
    assert.deepEqual(n, [...n].sort((a, b) => a.localeCompare(b)), `${g} A to Z`);
    for (const f of list) assert.match(f[2], /^W?M?L?$/, `${f[0]} editions`);
  }
  const supporter = featureGroups.flatMap(([, l]) => l).filter((f) => f[3] === "Supporter").map((f) => f[0]).sort();
  assert.deepEqual(supporter, ["Classic Color Scheme", "Glacier Color Scheme", "Support Link Hidden", "Supporter Badge"]);
  const html = pages.find((p) => p.file === "/features/list/index.html").html;
  const rows = (html.match(/<tr><th scope="row">/g) || []).length;
  assert.equal(rows, featureGroups.flatMap(([, l]) => l).length);
  assert.equal((html.match(/class="plan (free|supporter)"/g) || []).length, rows, "every row has a plan");
  assert.ok(!/premium/i.test(html.replace(/no paid or Premium version/, "")), "no Premium tier");
});

test("download thank-you pop-up is on every page, and Ko-fi links open in a new tab", () => {
  for (const p of pages) {
    const d = p.html.match(/<dialog class="thanks" id="download-thanks"[\s\S]*?<\/dialog>/);
    assert.ok(d, `${p.file} has the thank-you pop-up`);
    assert.match(d[0], /aria-label="Close"/);
    assert.match(d[0], /Maybe Later/);
    for (const a of p.html.match(/<a [^>]*href="https:\/\/ko-fi\.com[^"]*"[^>]*>/g) || []) assert.match(a, /target="_blank" rel="noopener noreferrer"/, `${p.file}: ${a}`);
  }
  const js = readFileSync(join(root, "js", "site.js"), "utf8");
  assert.match(js, /download-thanks/);
  assert.ok(!/preventDefault/.test(js.slice(js.indexOf("download-thanks"))), "the pop-up never blocks the download");
});

test("Ted's email address appears nowhere on the site (people use Contact Us)", () => {
  for (const f of files) {
    if (!/\.(html|js|css|json|txt|xml|md)$/.test(f)) continue;
    assert.ok(!/ttabakagroup@/i.test(readFileSync(f, "utf8")), `${f.slice(root.length)} shows the email address`);
  }
  for (const p of pages) {
    assert.ok(!/href="mailto:/i.test(p.html), `${p.file} has a mailto link`);
    const text = p.html.replace(/<script[\s\S]*?<\/script>/g, "").replace(/<[^>]+>/g, " ");
    assert.deepEqual(text.match(/[\w.+-]+@[\w-]+\.[\w.]{2,}/g) || [], [], `${p.file} shows an email address`);
  }
});

test("links to other websites open in a new tab", () => {
  for (const p of pages) {
    for (const tag of p.html.match(/<a\b[^>]*href="https?:\/\/[^"]+"[^>]*>/gi) || []) {
      if (/href="https?:\/\/(www\.)?kodiakcleaner\.app(\/|")/i.test(tag)) continue;
      assert.match(tag, /target="_blank"/, `${p.file}: ${tag}`);
      assert.match(tag, /rel="noopener noreferrer"/, `${p.file}: ${tag}`);
    }
  }
});

test("the Accessibility page states the WCAG 2.1 and ADA commitment and is linked from every page", () => {
  const a11y = pages.find((p) => p.file === "/accessibility/index.html");
  assert.ok(a11y, "missing /accessibility/");
  for (const s of ["WCAG", "2.1", "Americans with Disabilities Act", "not a claim", 'href="/contact/?topic=Accessibility"']) assert.ok(a11y.html.includes(s), `Accessibility page is missing: ${s}`);
  assert.ok(!/@[a-z0-9-]+\.[a-z]/i.test(text(a11y.html.split("<main")[1].split("</main>")[0])), "no email address on the Accessibility page");
  for (const p of pages) assert.ok(p.html.includes('href="/accessibility/"'), `${p.file} should link the Accessibility page`);
  const contact = pages.find((p) => p.file === "/contact/index.html").html;
  assert.match(contact, /value="Accessibility"/);
  assert.match(contact, /value="General Feedback" checked/);
});
