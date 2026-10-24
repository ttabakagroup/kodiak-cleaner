// Builds the Kodiak Cleaner website into public/.
//   node build.mjs          build the site
//   node build.mjs --sync   first copy CHANGELOG.md and the other documents from ../source (or ../ccleaner-alt) (Windows)
//                           ../kodiak-linux (Linux, into content/linux/) and ../kodiak-mac (Mac, into content/mac/)
import { readFileSync, writeFileSync, mkdirSync, rmSync, cpSync, existsSync, readdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { parseChangelog } from "./src/markdown.mjs";
import { layout } from "./src/layout.mjs";
import home from "./src/pages/home.mjs";
import features from "./src/pages/features.mjs";
import download from "./src/pages/download.mjs";
import archive from "./src/pages/archive.mjs";
import changelog from "./src/pages/changelog.mjs";
import contact from "./src/pages/contact.mjs";
import linux from "./src/pages/linux.mjs";
import mac from "./src/pages/mac.mjs";
import donate from "./src/pages/donate.mjs";
import featureList from "./src/pages/featurelist.mjs";
import openSource from "./src/pages/opensource.mjs";
import { story, privacy, terms, notFound } from "./src/pages/docs.mjs";
import accessibility from "./src/pages/accessibility.mjs";

const here = dirname(fileURLToPath(import.meta.url));
const at = (...p) => join(here, ...p);
const read = (...p) => readFileSync(at(...p), "utf8");

// Keep the documents in step with the app when its folder sits beside this one.
const DOCS = ["CHANGELOG.md", "PRIVACY.md", "terms-of-use.md", "why-it-was-made.md", "LICENSE", "version.json"];
const LINUX_DOCS = ["CHANGELOG.md", "version.json"]; // also what's copied from the Mac app
if (process.argv.includes("--sync")) {
  const app = [at("..", "source"), at("..", "ccleaner-alt")].find((p) => existsSync(join(p, "version.json")));
  if (!app) throw new Error("--sync needs the app folder at ../source (or ../ccleaner-alt)");
  for (const f of DOCS) cpSync(join(app, f), at("content", f));
  console.log(`Synced ${DOCS.length} documents from ${app}`);
  const lin = at("..", "kodiak-linux");
  if (!existsSync(lin)) throw new Error("--sync needs the Linux app folder at ../kodiak-linux");
  mkdirSync(at("content", "linux"), { recursive: true });
  for (const f of LINUX_DOCS) cpSync(join(lin, f), at("content", "linux", f));
  console.log(`Synced ${LINUX_DOCS.length} documents from ${lin}`);
  const macApp = at("..", "kodiak-mac");
  if (!existsSync(macApp)) throw new Error("--sync needs the Mac app folder at ../kodiak-mac");
  mkdirSync(at("content", "mac"), { recursive: true });
  for (const f of LINUX_DOCS) cpSync(join(macApp, f), at("content", "mac", f));
  console.log(`Synced ${LINUX_DOCS.length} documents from ${macApp}`);
}

const cfg = JSON.parse(read("site.config.json"));
const releases = parseChangelog(read("content", "CHANGELOG.md"));
if (!releases.length) throw new Error("No versions found in content/CHANGELOG.md");
const latest = releases[0];
const linuxReleases = parseChangelog(read("content", "linux", "CHANGELOG.md"));
if (!linuxReleases.length) throw new Error("No versions found in content/linux/CHANGELOG.md");
const macReleases = parseChangelog(read("content", "mac", "CHANGELOG.md"));
if (!macReleases.length) throw new Error("No versions found in content/mac/CHANGELOG.md");

const MONTHS = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
const parts = (iso) => iso.split("-").map(Number);

// PNG width and height straight from the file header, for width/height attributes.
// Only real PNGs are read, so a stray Thumbs.db or .DS_Store in the folder doesn't stop the build.
const PNG_SIGNATURE = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
const imageSizes = {};
for (const f of readdirSync(at("src", "assets", "images", "screens"))) {
  if (!/\.png$/i.test(f)) continue;
  const b = readFileSync(at("src", "assets", "images", "screens", f));
  if (b.length < 24 || !b.subarray(0, 8).equals(PNG_SIGNATURE)) throw new Error(`src/assets/images/screens/${f} isn't a PNG file`);
  imageSizes[f] = [b.readUInt32BE(16), b.readUInt32BE(20)];
}

// Launch video overlay on the homepage, shown when site.config.json "launchVideo" names a file in src/assets/video/.
const lv = cfg.launchVideo;
const launchVideo = lv && existsSync(at("src", "assets", "video", lv.file)) ? { ...lv, posterExists: !!lv.poster && existsSync(at("src", "assets", "video", lv.poster)) } : null;
if (lv && !launchVideo) console.warn(`Note: launch video src/assets/video/${lv.file} isn't there yet, so the homepage has no video overlay.`);
if (launchVideo) {
  const size = readFileSync(at("src", "assets", "video", lv.file)).length;
  if (size > 25 * 1024 * 1024) throw new Error(`src/assets/video/${lv.file} is over Cloudflare Pages' 25 MiB file limit; serve it from R2 instead`);
}

const ctx = {
  cfg,
  launchVideo,
  releases,
  latest,
  linuxReleases,
  linuxLatest: linuxReleases[0],
  macReleases,
  macLatest: macReleases[0],
  imageSizes,
  buildId: Date.now().toString(36),
  content: {
    story: read("content", "why-it-was-made.md"),
    privacy: read("content", "PRIVACY.md"),
    terms: read("content", "terms-of-use.md"),
    license: read("content", "LICENSE"),
  },
  longDate: (iso) => { if (!iso) return ""; const [y, m, d] = parts(iso); return `${MONTHS[m - 1]} ${d}, ${y}`; },
  shortDate: (iso) => { if (!iso) return ""; const [y, m, d] = parts(iso); return `${MONTHS[m - 1].slice(0, 3)} ${d}, ${y}`; },
  // Download links for a version. Release assets follow build.ps1 and the GitHub workflow, tag v0.8.35-dev.
  // Files: KodiakCleaner-Dev-Setup.msi (0.8.37 on) or KodiakCleaner-Dev-Setup.zip (0.8.31 to 0.8.36), and
  // KodiakCleaner-Dev-Portable.zip since 0.8.31,
  // KodiakCleaner-0.8.27-x64-msi-dev.zip and KodiakCleaner-0.8.27-x64-portable-dev.zip before that.
  dl(r) {
    const suffix = r.channel === "stable" ? "" : `-${r.channel}`;
    const tag = `v${r.version}${suffix}`;
    const base = `${cfg.repo}/releases/download/${tag}/`;
    const fixedNames = r.version.split(".").map(Number).reduce((a, n) => a * 1000 + n, 0) >= 8031;
    const Channel = r.channel[0].toUpperCase() + r.channel.slice(1);
    const bareMsi = r.version.split(".").map(Number).reduce((a, n) => a * 1000 + n, 0) >= 8037;
    const installerFile = bareMsi ? `KodiakCleaner-${Channel}-Setup.msi` : fixedNames ? `KodiakCleaner-${Channel}-Setup.zip` : `KodiakCleaner-${r.version}-x64-msi${suffix}.zip`;
    const portableFile = fixedNames ? `KodiakCleaner-${Channel}-Portable.zip` : `KodiakCleaner-${r.version}-x64-portable${suffix}.zip`;
    const info = cfg.releases[r.version];
    // While "hostedDownloads" is in site.config.json, the newest version downloads from this
    // website (/download/<file>, served from Cloudflare R2) instead of GitHub.
    const hosted = r === releases[0] && cfg.hostedDownloads;
    if (hosted) {
      return {
        tag, installerFile: hosted.installer, portableFile: hosted.portable,
        installer: `/download/${hosted.installer}`,
        portable: `/download/${hosted.portable}`,
        installerIsMsi: hosted.installer.endsWith(".msi"),
        releasePage: `${cfg.repo}/releases/tag/${tag}`,
        published: true,
        installerSize: hosted.installerSize || info?.installerSize, portableSize: hosted.portableSize || info?.portableSize,
      };
    }
    return {
      tag, installerFile, portableFile,
      installer: base + installerFile,
      portable: base + portableFile,
      releasePage: `${cfg.repo}/releases/tag/${tag}`,
      published: Boolean(info),
      installerSize: info?.installerSize, portableSize: info?.portableSize,
    };
  },
};

if (!cfg.releases[latest.version]) {
  console.warn(`Warning: ${latest.name} is the newest version in the changelog but isn't listed under "releases" in site.config.json, so its downloads are marked Not Published.`);
}

const out = at("public");
rmSync(out, { recursive: true, force: true });
mkdirSync(out, { recursive: true });

// Static files
cpSync(at("src", "assets", "css"), join(out, "css"), { recursive: true });
cpSync(at("src", "assets", "js"), join(out, "js"), { recursive: true });
cpSync(at("src", "assets", "fonts"), join(out, "fonts"), { recursive: true });
cpSync(at("src", "assets", "images"), join(out, "images"), { recursive: true });
if (existsSync(at("src", "assets", "video"))) cpSync(at("src", "assets", "video"), join(out, "video"), { recursive: true });
cpSync(at("src", "assets", "favicon.ico"), join(out, "favicon.ico"));
cpSync(at("src", "static"), out, { recursive: true });

// Links to other websites open in a new tab (Ted, 2026-10-10). Applied to every finished page,
// so links written later, including ones in the synced Markdown documents, get it too.
const OWN_HOST = /^https?:\/\/(www\.)?kodiakcleaner\.app(\/|$)/i;
function externalLinks(html) {
  return html.replace(/<a\b([^>]*?)\bhref="(https?:\/\/[^"]+)"([^>]*)>/gi, (tag, before, href, after) => {
    if (OWN_HOST.test(href)) return tag;
    const attrs = (before + after).replace(/\s+target="[^"]*"/gi, "").replace(/\s+rel="[^"]*"/gi, "");
    return `<a${attrs.replace(/\s+$/, "")} href="${href}" target="_blank" rel="noopener noreferrer">`;
  });
}

// Pages
const pages = [home, features, featureList, openSource, linux, mac, download, archive, changelog, contact, donate, story, privacy, terms, accessibility, notFound].map((p) => p(ctx));
for (const page of pages) {
  const file = join(out, page.out);
  mkdirSync(dirname(file), { recursive: true });
  writeFileSync(file, externalLinks(layout(page, ctx)));
}

// Sitemap and robots
const site = cfg.siteUrl.replace(/\/$/, "");
const urls = pages.filter((p) => p.id !== "404").map((p) => `  <url><loc>${site}${p.path}</loc><lastmod>${latest.date}</lastmod></url>`).join("\n");
writeFileSync(join(out, "sitemap.xml"), `<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n${urls}\n</urlset>\n`);
writeFileSync(join(out, "robots.txt"), `User-agent: *\nAllow: /\nSitemap: ${site}/sitemap.xml\n`);

console.log(`Built ${pages.length} pages for Kodiak Cleaner ${latest.name} (${releases.length} versions) Kodiak Cleaner for Linux ${linuxReleases[0].name} and Kodiak Cleaner for Mac ${macReleases[0].name} into public/`);
