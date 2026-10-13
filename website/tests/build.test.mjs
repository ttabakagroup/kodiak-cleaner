// build.mjs: the build copes with the files Windows and macOS leave in image folders.
import { test } from "node:test";
import assert from "node:assert/strict";
import { cpSync, mkdtempSync, writeFileSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";

const here = join(dirname(fileURLToPath(import.meta.url)), "..");

test("a Thumbs.db or .DS_Store beside the screenshots doesn't break the build", () => {
  const dir = mkdtempSync(join(tmpdir(), "kc-build-"));
  try {
    for (const p of ["build.mjs", "site.config.json", "package.json", "src", "content"]) cpSync(join(here, p), join(dir, p), { recursive: true });
    writeFileSync(join(dir, "src", "assets", "images", "screens", "Thumbs.db"), "x");
    writeFileSync(join(dir, "src", "assets", "images", "screens", ".DS_Store"), "");
    const run = spawnSync(process.execPath, ["build.mjs"], { cwd: dir, encoding: "utf8" });
    assert.equal(run.status, 0, run.stderr);
    // Screenshot sizes still come from the PNG headers.
    const png = readFileSync(join(dir, "src", "assets", "images", "screens", "disk-analyzer.png"));
    const html = readFileSync(join(dir, "public", "features", "index.html"), "utf8");
    assert.ok(html.includes(`src="/images/screens/disk-analyzer.png" alt="Disk Analyzer map of folders and files colored by kind, with the Contents list beside it" width="${png.readUInt32BE(16)}" height="${png.readUInt32BE(20)}"`));
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});
