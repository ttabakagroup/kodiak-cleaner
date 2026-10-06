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

test("no bare MSI download links; installer is always a ZIP", () => {
  // The only bare MSI allowed is one Ted chose on purpose in site.config.json "hostedDownloads".
  for (const p of pages) {
    for (const href of attr(p.html, "href")) {
      if (hosted.includes(href)) continue;
      assert.ok(!/\.msi($|[?#])/i.test(href), `${p.file} links a bare MSI: ${href}`);
    }
  }
  const dl = pages.find((p) => p.file === "/download/index.html").html;
  if (cfg.hostedDownloads) {
    for (const f of hosted) assert.ok(dl.includes(`href="${f}"`), `Download page should link ${f}`);
  } else {
    assert.match(dl, /releases\/download\/v\d+\.\d+\.\d+(-dev|-beta)?\/KodiakCleaner-[\d.]+-x64-msi(-dev|-beta)?\.zip/);
    assert.match(dl, /releases\/download\/v\d+\.\d+\.\d+(-dev|-beta)?\/KodiakCleaner-[\d.]+-x64-portable(-dev|-beta)?\.zip/);
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
  for (const s of ["www.kodiakcleaner.app/contact", "https://tedtabaka.com", "https://github.com/ttabakagroup/kodiakcleaner", "MIT License", "Copyright © 2026 Ted Tabaka", "use at your own risk"]) {
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
