// Checks the built FAQ page (public/faq/) and the Command Line section on the Features page.
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync, readdirSync, statSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..", "public");
const walk = (d) => readdirSync(d).flatMap((f) => (statSync(join(d, f)).isDirectory() ? walk(join(d, f)) : [join(d, f)]));
const pages = walk(root).filter((f) => f.endsWith(".html")).map((f) => ({ file: f.slice(root.length), html: readFileSync(f, "utf8") }));
const page = (file) => pages.find((p) => p.file === file)?.html;
const main = (html) => html.split("<main")[1].split("</main>")[0];
const text = (s) => s.replace(/<[^>]+>/g, " ").replace(/&amp;/g, "&").replace(/&gt;/g, ">").replace(/&quot;/g, '"').replace(/&#39;/g, "'").replace(/\s+/g, " ").trim();
const sorted = (list) => [...list].sort((a, b) => a.localeCompare(b, "en", { sensitivity: "base" }));
const faq = page("/faq/index.html");

test("the FAQ page is built, in the sitemap and linked from every page's footer", () => {
  assert.ok(faq, "missing /faq/");
  assert.match(faq, /<h1>Frequently Asked Questions<\/h1>/);
  assert.match(faq, /<link rel="canonical" href="https:\/\/www\.kodiakcleaner\.app\/faq\/">/);
  assert.ok(readFileSync(join(root, "sitemap.xml"), "utf8").includes("https://www.kodiakcleaner.app/faq/"), "FAQ is in the sitemap");
  for (const p of pages) assert.match(p.html, /<footer[\s\S]*<a href="\/faq\/">FAQ<\/a>/, `${p.file} footer links the FAQ`);
  assert.match(page("/contact/index.html"), /href="\/faq\/"/, "Contact Us points to the FAQ");
});

test("FAQ groups and the questions in each are A to Z, every question is answered, and the jump links match", () => {
  const cards = [...main(faq).matchAll(/<div class="card pad[^"]*" id="([^"]+)">\s*<h2>([^<]+)<\/h2>([\s\S]*?)(?=<div class="card pad|<\/div><\/section>)/g)]
    .filter((m) => m[1] !== "more-questions");
  const groups = cards.map((m) => text(m[2]));
  assert.ok(groups.length >= 5, "a few groups");
  assert.deepEqual(groups, sorted(groups), "groups A to Z");
  for (const [, id, g, inner] of cards) {
    assert.match(faq, new RegExp(`<a class="chip-link" href="#${id}">`), `jump link for ${g}`);
    const items = [...inner.matchAll(/<div class="faq-item" id="[^"]+"><h3>([^<]+)<\/h3>([\s\S]*?)<\/div>/g)];
    const qs = items.map((m) => text(m[1]));
    assert.ok(qs.length >= 2, `${g} has questions`);
    assert.deepEqual(qs, sorted(qs), `${g} questions A to Z`);
    for (const [, q, a] of items) {
      assert.match(q, /\?$/, `"${q}" is a question`);
      assert.ok(text(a).length >= 60, `"${q}" has a real answer`);
    }
  }
  const ids = [...faq.matchAll(/\bid="([^"]+)"/g)].map((m) => m[1]);
  assert.equal(new Set(ids).size, ids.length, "ids are unique");
});

test("the FAQ covers the topics Ted asked for, with the facts from the app", () => {
  const t = text(main(faq));
  for (const s of [
    "Is Kodiak Cleaner Free?", "no paid or Premium version", "MIT License",
    "What Does Beta Mean?", "0.8.121 Dev", "offered the Beta",
    "backed up before every change", "Recycle Bin", "caution",
    "no telemetry", "Privacy Policy",
    "SmartScreen", "More info", "Run anyway", "Get-FileHash",
    "Installer or Portable", "KodiakCleaner.exe",
    "How Do Updates Work?", "SHA-256", "off until you turn it on",
    "supporter code", "up to 10 of your computers", "Classic, Glacier and Lagoon color schemes", "Supporter badge", "Support link goes away", "Every feature is free",
    "Kodiak Cleaner for Mac", "Kodiak Cleaner for Linux",
    "Command Line", "/help",
    "Can I Hide Tools I Don't Use?", "from 0.9.1 Beta", "Settings > Tools", "hide it from the sidebar",
    "WCAG 2.1", "Text Size",
    "Contact Us",
  ]) assert.ok(t.includes(s), `FAQ is missing: ${s}`);
  for (const href of ["/download/", "/donate/", "/linux/", "/mac/", "/privacy/", "/accessibility/", "/open-source/", "/features/#command-line", "/contact/", "/contact/?topic=Security"]) {
    assert.ok(faq.includes(`href="${href}"`), `FAQ links ${href}`);
  }
});

test("the FAQ never shows an email address, never says donate, donation or tip, and keeps the slogan", () => {
  for (const html of [faq, page("/features/index.html")]) {
    const t = text(main(html));
    assert.deepEqual(t.match(/[\w.+-]+@[\w-]+\.[\w.]{2,}/g) || [], [], "no email address");
    assert.ok(!/mailto:/i.test(html), "no mailto link");
    assert.deepEqual(t.match(/\b(donate|donations?|donating|tips?|tipping)\b/gi) || [], [], "Support on Ko-fi, never donate or tip");
  }
  assert.ok(faq.includes("Not a Crap Cleaner. A Cleaner App, Built Around What Matters."), "slogan unchanged in the footer");
  assert.ok(!/premium/i.test(text(main(faq)).replace(/no paid or Premium version/, "")), "no Premium tier");
});

test("the Features page has a Command Line section with every switch, example and exit code from CommandLine.cs", () => {
  const f = page("/features/index.html");
  const sec = f.match(/<section[^>]*id="command-line"[\s\S]*?<\/section>/);
  assert.ok(sec, "Features page has #command-line");
  assert.match(f, /<a class="chip-link" href="#command-line">Command Line<\/a>/);
  const t = text(sec[0]);
  for (const s of [
    "KodiakCleaner.exe /clean [/items:ID,ID]", "KodiakCleaner.exe /list-items",
    'KodiakCleaner.exe /registry [/scan-only] [/categories:"Fonts,Help Files"]',
    'KodiakCleaner.exe /uninstall:"Program Name" [/quiet]', "KodiakCleaner.exe /uninstall:{PRODUCT-CODE-GUID} [/quiet]",
    "KodiakCleaner.exe /list-programs", "KodiakCleaner.exe /help", "msiexec /x {GUID} /qn",
    "start /wait", "%ERRORLEVEL%", "Start-Process KodiakCleaner.exe -ArgumentList '/clean' -Wait -PassThru", "ExitCode",
    "Items with a caution never run", "Run it as administrator", "activity log",
  ]) assert.ok(t.includes(s), `Command Line section is missing: ${s}`);
  const codes = [...sec[0].matchAll(/<li><code>(\d)<\/code> ([^<]+)<\/li>/g)].map((m) => [m[1], m[2]]);
  assert.deepEqual(codes.map((c) => c[0]), ["0", "1", "2", "3"]);
  assert.match(codes[1][1], /some failures/);
  assert.match(codes[2][1], /Wrong switch, nothing found/);
  assert.match(codes[3][1], /uninstall didn't finish/);
  assert.match(text(main(page("/features/list/index.html"))), /Command Line Clean, scan or fix the registry/, "Full Feature List has Command Line");
});
