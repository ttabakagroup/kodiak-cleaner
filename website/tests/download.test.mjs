// functions/download/[file].js: serves files from the R2 bucket, with resume support.
import { test } from "node:test";
import assert from "node:assert/strict";
import { onRequestGet, onRequestHead } from "../functions/download/[file].js";

const data = new TextEncoder().encode("0123456789");
const ETAG = '"abc"';
// A stand-in for the R2 binding: get() takes a Range header or an explicit { offset, length }, and an
// If-None-Match / If-Match precondition (as R2 does), but like R2 it knows nothing about If-Range.
function makeBucket({ files = ["KodiakCleaner-Portable.zip", "KodiakCleanerMac.dmg"], etag = () => ETAG, content = () => data } = {}) {
  const calls = [];
  const meta = (key) => ({ key, size: content().length, etag: etag().slice(1, -1), httpEtag: etag(), uploaded: new Date("2026-10-07T12:00:00Z"), writeHttpMetadata() {} });
  return {
    calls,
    async head(key) { calls.push(["head", key]); return files.includes(key) ? meta(key) : null; },
    async get(key, opts = {}) {
      calls.push(["get", key, opts]);
      if (!files.includes(key)) return null;
      const m = meta(key);
      const cond = opts.onlyIf instanceof Headers ? opts.onlyIf : new Headers();
      if ((cond.has("if-none-match") && cond.get("if-none-match") === m.httpEtag) || (cond.has("if-match") && cond.get("if-match") !== m.httpEtag)) return m;
      let r;
      if (opts.range instanceof Headers) {
        const x = /bytes=(\d+)-(\d+)/.exec(opts.range.get("range") || "");
        if (x) r = { offset: +x[1], length: +x[2] - +x[1] + 1 };
      } else if (opts.range) r = opts.range;
      const bytes = content();
      return { ...m, range: r, body: new Blob([r ? bytes.slice(r.offset, r.offset + r.length) : bytes]).stream() };
    },
  };
}
const bucket = makeBucket();
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

test("serves the Mac disk image with its own type", async () => {
  const res = await get("KodiakCleanerMac.dmg");
  assert.equal(res.status, 200);
  assert.equal(res.headers.get("content-type"), "application/x-apple-diskimage");
});

test("downloads are revalidated every time, so a replaced installer matches /updates/dev.json", async () => {
  const res = await get("KodiakCleaner-Portable.zip");
  assert.equal(res.headers.get("cache-control"), "no-cache");
});

test("an unsatisfiable range gets 416, not the whole file labelled as a part", async () => {
  const res = await get("KodiakCleaner-Portable.zip", { range: "bytes=20-30" });
  assert.equal(res.status, 416);
  assert.equal(res.headers.get("content-range"), "bytes */10");
});

test("open-ended and suffix ranges are answered with the right bytes and Content-Range", async () => {
  for (const [range, cr, body] of [["bytes=7-", "bytes 7-9/10", "789"], ["bytes=-3", "bytes 7-9/10", "789"], ["bytes=8-100", "bytes 8-9/10", "89"], ["bytes=-30", "bytes 0-9/10", "0123456789"]]) {
    const res = await get("KodiakCleaner-Portable.zip", { range });
    assert.equal(res.status, 206, range);
    assert.equal(res.headers.get("content-range"), cr, range);
    assert.equal(res.headers.get("content-length"), String(body.length), range);
    assert.equal(await res.text(), body, range);
  }
});

test("malformed or multi-part ranges are ignored and the whole file is sent with 200", async () => {
  for (const range of ["bytes=abc", "bytes=0-1,4-5", "items=0-1", "bytes=5-2"]) {
    const res = await get("KodiakCleaner-Portable.zip", { range });
    assert.equal(res.status, 200, range);
    assert.equal(res.headers.get("content-range"), null, range);
    assert.equal(await res.text(), "0123456789", range);
  }
});

test("a resume against a replaced file (If-Range mismatch) gets the new file whole, never a spliced part", async () => {
  const stale = await get("KodiakCleaner-Portable.zip", { range: "bytes=5-", "if-range": '"old"' });
  assert.equal(stale.status, 200);
  assert.equal(await stale.text(), "0123456789");
  const current = await get("KodiakCleaner-Portable.zip", { range: "bytes=5-", "if-range": ETAG });
  assert.equal(current.status, 206);
  assert.equal(await current.text(), "56789");
});

test("a file replaced between the size lookup and the read is sent whole", async () => {
  let n = 0;
  const swapping = makeBucket({ etag: () => (n++ === 0 ? '"v1"' : '"v2"') });
  const res = await get("KodiakCleaner-Portable.zip", { range: "bytes=2-4" }, { DOWNLOADS: swapping });
  assert.equal(res.status, 200);
  assert.equal(await res.text(), "0123456789");
});

test("conditional requests: 304 when current, 412 when If-Match fails", async () => {
  assert.equal((await get("KodiakCleaner-Portable.zip", { "if-none-match": ETAG })).status, 304);
  assert.equal((await get("KodiakCleaner-Portable.zip", { "if-match": '"nope"' })).status, 412);
  assert.equal((await get("KodiakCleaner-Portable.zip", { "if-match": ETAG })).status, 200);
});

test("HEAD answers from metadata without reading the file", async () => {
  const b = makeBucket();
  const head = (file, headers = {}) => onRequestHead({ request: new Request(`https://www.kodiakcleaner.app/download/${file}`, { method: "HEAD", headers }), env: { DOWNLOADS: b }, params: { file } });
  const res = await head("KodiakCleaner-Portable.zip");
  assert.equal(res.status, 200);
  assert.equal(res.headers.get("content-length"), "10");
  assert.equal(res.headers.get("content-type"), "application/zip");
  assert.equal(res.headers.get("etag"), ETAG);
  assert.equal((await head("KodiakCleaner-Portable.zip", { "if-none-match": ETAG })).status, 304);
  assert.equal((await head("Nope.zip")).status, 404);
  assert.equal((await head("../x.zip")).status, 404);
  assert.ok(b.calls.every(([op]) => op === "head"), JSON.stringify(b.calls.map(([op]) => op)));
});
