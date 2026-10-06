// functions/download/[file].js: serves files from the R2 bucket, with resume support.
import { test } from "node:test";
import assert from "node:assert/strict";
import { onRequestGet } from "../functions/download/[file].js";

const data = new TextEncoder().encode("0123456789");
const bucket = {
  async get(key, opts) {
    if (key !== "KodiakCleaner-Portable.zip") return null;
    const range = opts.range.get("range");
    const m = range && /bytes=(\d+)-(\d+)/.exec(range);
    const r = m ? { offset: +m[1], length: +m[2] - +m[1] + 1 } : undefined;
    return { size: data.length, httpEtag: '"abc"', range: r, body: new Blob([m ? data.slice(r.offset, r.offset + r.length) : data]).stream(), writeHttpMetadata() {} };
  },
};
const get = (file, headers = {}, env = { DOWNLOADS: bucket }) => onRequestGet({ request: new Request(`https://www.kodiakcleaner.app/download/${file}`, { headers }), env, params: { file } });

test("serves a file as a download", async () => {
  const res = await get("KodiakCleaner-Portable.zip");
  assert.equal(res.status, 200);
  assert.equal(res.headers.get("content-type"), "application/zip");
  assert.match(res.headers.get("content-disposition"), /attachment; filename="KodiakCleaner-Portable.zip"/);
  assert.equal(await res.text(), "0123456789");
});

test("resumes a partial download", async () => {
  const res = await get("KodiakCleaner-Portable.zip", { range: "bytes=2-4" });
  assert.equal(res.status, 206);
  assert.equal(res.headers.get("content-range"), "bytes 2-4/10");
  assert.equal(await res.text(), "234");
});

test("missing files, odd names and a missing bucket give a plain 404", async () => {
  for (const [file, env] of [["Nope.zip", undefined], ["../secret.zip", undefined], ["notes.txt", undefined], ["KodiakCleaner-Portable.zip", {}]]) {
    assert.equal((await get(file, {}, env)).status, 404, file);
  }
});
