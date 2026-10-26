// build.mjs: the build copes with the files Windows and macOS leave in image folders.
import { test } from "node:test";
import assert from "node:assert/strict";
import { cpSync, mkdtempSync, mkdirSync, writeFileSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";
import { createHash } from "node:crypto";

const here = join(dirname(fileURLToPath(import.meta.url)), "..");

test("a Thumbs.db or .DS_Store beside the screenshots doesn't break the build", () => {
  const dir = mkdtempSync(join(tmpdir(), "kc-build-"));
  try {
    for (const p of ["build.mjs", "site.config.json", "package.json", "src", "content"]) cpSync(join(here, p), join(dir, p), { recursive: true });
    writeFileSync(join(dir, "src", "assets", "images", "screens", "Thumbs.db"), "x");
    writeFileSync(join(dir, "src", "assets", "images", "screens", ".DS_Store"), "");
    const run = spawnSync(process.execPath, ["build.mjs"], { cwd: dir, encoding: "utf8" });
    assert.equal(run.status, 0, run.stderr);
    // Screenshot sizes still come from the PNG headers, and the URL carries a content hash so a retaken shot isn't served from cache.
    const png = readFileSync(join(dir, "src", "assets", "images", "screens", "disk-analyzer.png"));
    const v = createHash("sha256").update(png).digest("hex").slice(0, 10);
    const html = readFileSync(join(dir, "public", "features", "index.html"), "utf8");
    assert.ok(html.includes(`src="/images/screens/disk-analyzer.png?v=${v}" alt="Disk Analyzer map of folders and files colored by kind, with the Contents list beside it" width="${png.readUInt32BE(16)}" height="${png.readUInt32BE(20)}"`));
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test("changelog headings are read for Dev and Beta builds", async () => {
  const { parseChangelog } = await import("../src/markdown.mjs");
  const r = parseChangelog("# Changelog\n\n## [Unreleased]\n\n## [0.9.0 Beta] - 2026-10-25\n\n### Beta\n- One\n\n## [0.8.121 Dev] - 2026-10-25\n\n- Two\n\n## [1.0.0] - 2027-01-01\n");
  assert.deepEqual(r.map((x) => [x.name, x.version, x.channel, x.date]), [
    ["0.9.0 Beta", "0.9.0", "beta", "2026-10-25"],
    ["0.8.121 Dev", "0.8.121", "dev", "2026-10-25"],
    ["1.0.0", "1.0.0", "stable", "2027-01-01"],
  ]);
});

test("--sync takes the Windows app folder from KC_WINDOWS_APP and copies the Dev history", () => {
  const top = mkdtempSync(join(tmpdir(), "kc-sync-"));
  try {
    const dir = join(top, "website");
    for (const p of ["build.mjs", "site.config.json", "package.json", "src", "content"]) cpSync(join(here, p), join(dir, p), { recursive: true });
    const app = join(top, "windows-app");
    mkdirSync(join(app, "docs", "history"), { recursive: true });
    for (const f of ["PRIVACY.md", "terms-of-use.md", "why-it-was-made.md", "LICENSE"]) cpSync(join(here, "content", f), join(app, f));
    writeFileSync(join(app, "version.json"), '{ "channel": "beta", "version": "0.9.0", "targetStable": "1.0.0" }\n');
    writeFileSync(join(app, "CHANGELOG.md"), "# Changelog\n\n## [0.9.0 Beta] - 2026-10-25\n\n### Test\n- Synced Beta entry\n");
    writeFileSync(join(app, "docs", "history", "dev-changelog.md"), "# History\n\n## [0.8.121 Dev] - 2026-10-25\n\n### Test\n- Synced Dev entry\n");
    for (const os of ["linux", "mac"]) cpSync(join(here, "content", os), join(top, `kodiak-${os}`), { recursive: true });
    const run = (env) => spawnSync(process.execPath, ["build.mjs", "--sync"], { cwd: dir, encoding: "utf8", env: { ...process.env, ...env } });
    // Without KC_WINDOWS_APP it looks for ../source, which isn't there.
    const missing = run({ KC_WINDOWS_APP: "" });
    assert.notEqual(missing.status, 0);
    assert.match(missing.stderr, /KC_WINDOWS_APP/);
    const ok = run({ KC_WINDOWS_APP: "../windows-app" });
    assert.equal(ok.status, 0, ok.stderr);
    assert.match(readFileSync(join(dir, "content", "changelog-history.md"), "utf8"), /Synced Dev entry/);
    assert.match(readFileSync(join(dir, "public", "changelog", "index.html"), "utf8"), /Synced Beta entry/);
    assert.match(readFileSync(join(dir, "public", "changelog", "history", "index.html"), "utf8"), /Synced Dev entry/);
  } finally {
    rmSync(top, { recursive: true, force: true });
  }
});
