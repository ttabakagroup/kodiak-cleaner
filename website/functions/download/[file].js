// Serves the download files at https://www.kodiakcleaner.app/download/<file>.
// The files are too big for Cloudflare Pages (25 MiB per file), so they live in a Cloudflare R2
// bucket bound to this project as DOWNLOADS. Upload them in the dashboard under R2 > the bucket.

const TYPES = { dmg: "application/x-apple-diskimage", msi: "application/x-msi", zip: "application/zip" };
const NAME_RE = /^[A-Za-z0-9][A-Za-z0-9._-]*\.(dmg|msi|zip)$/;

export async function onRequestGet({ request, env, params }) {
  const name = String(params.file || "");
  if (!NAME_RE.test(name)) return notFound();
  if (!env.DOWNLOADS) return notFound();

  // Range requests (resumed downloads) are worked out here rather than handed to R2 as raw headers:
  // R2 ignores If-Range, and answers a range it can't satisfy (or several ranges) with the whole file,
  // which a resuming client would splice onto what it already has.
  if (request.headers.has("range")) {
    const meta = await env.DOWNLOADS.head(name);
    if (!meta) return notFound();
    const cond = conditionalStatus(request, meta);
    if (cond) return new Response(null, { status: cond, headers: fileHeaders(name, meta) });
    const r = rangeFor(request, meta);
    if (r === "unsatisfiable") {
      const headers = fileHeaders(name, meta);
      headers.set("content-range", `bytes */${meta.size}`);
      return new Response(null, { status: 416, headers });
    }
    if (r) {
      const obj = await env.DOWNLOADS.get(name, { range: { offset: r.offset, length: r.length } });
      if (!obj) return notFound();
      if (obj.etag === meta.etag && "body" in obj) {
        const headers = fileHeaders(name, obj);
        headers.set("content-range", `bytes ${r.offset}-${r.offset + r.length - 1}/${obj.size}`);
        headers.set("content-length", String(r.length));
        return new Response(obj.body, { status: 206, headers });
      }
      // The file was replaced between the two lookups: send the new file whole instead of a mixed range.
      if (obj.body) await obj.body.cancel();
    }
  }

  const obj = await env.DOWNLOADS.get(name, { onlyIf: request.headers });
  if (!obj) return notFound();
  const headers = fileHeaders(name, obj);
  if (!("body" in obj)) return new Response(null, { status: conditionalStatus(request, obj) || 304, headers });
  headers.set("content-length", String(obj.size));
  return new Response(obj.body, { headers });
}

// HEAD answers from the object's metadata, without reading the file from R2.
export async function onRequestHead({ request, env, params }) {
  const name = String(params.file || "");
  if (!NAME_RE.test(name)) return notFound();
  if (!env.DOWNLOADS) return notFound();
  const meta = await env.DOWNLOADS.head(name);
  if (!meta) return notFound();
  const headers = fileHeaders(name, meta);
  const cond = conditionalStatus(request, meta);
  if (cond) return new Response(null, { status: cond, headers });
  headers.set("content-length", String(meta.size));
  return new Response(null, { headers });
}

function fileHeaders(name, obj) {
  const headers = new Headers();
  obj.writeHttpMetadata(headers);
  headers.set("content-type", TYPES[name.split(".").pop()]);
  headers.set("content-disposition", `attachment; filename="${name}"`);
  headers.set("etag", obj.httpEtag);
  headers.set("accept-ranges", "bytes");
  // no-cache: browsers and the app always check the ETag, so a file replaced in R2 is picked up straight away
  // (the app compares the installer with the SHA-256 in /updates/dev.json).
  headers.set("cache-control", "no-cache");
  return headers;
}

const etags = (v) => v.split(",").map((t) => t.trim().replace(/^W\//, "")).filter(Boolean);
const date = (v) => { const t = Date.parse(v); return Number.isNaN(t) ? null : t; };
const seconds = (d) => Math.floor(new Date(d).getTime() / 1000) * 1000;

// RFC 9110 section 13.2.2: 412 when If-Match / If-Unmodified-Since fail, 304 when the
// client's copy is current (If-None-Match / If-Modified-Since), otherwise null.
export function conditionalStatus(request, obj) {
  const h = request.headers;
  const tag = obj.httpEtag;
  const ifMatch = h.get("if-match");
  if (ifMatch !== null) {
    const list = ifMatch.split(",").map((t) => t.trim()); // strong comparison: W/ tags never match
    if (!list.includes("*") && !list.includes(tag)) return 412;
  } else if (h.has("if-unmodified-since")) {
    const d = date(h.get("if-unmodified-since"));
    if (d !== null && obj.uploaded && seconds(obj.uploaded) > d) return 412;
  }
  const ifNone = h.get("if-none-match");
  if (ifNone !== null) {
    const list = etags(ifNone);
    if (list.includes("*") || list.includes(tag.replace(/^W\//, ""))) return 304;
  } else if (h.has("if-modified-since")) {
    const d = date(h.get("if-modified-since"));
    if (d !== null && obj.uploaded && seconds(obj.uploaded) <= d) return 304;
  }
  return null;
}

// One "bytes=" range against a file of obj.size bytes (RFC 9110 section 14).
// Returns { offset, length }, "unsatisfiable", or null to ignore the Range header and send the whole file
// (no Range, a malformed or multi-part one, or an If-Range that doesn't match the current file).
export function rangeFor(request, obj) {
  const h = request.headers;
  const value = h.get("range");
  if (!value) return null;
  const ifRange = h.get("if-range");
  if (ifRange !== null && ifRange.trim() !== obj.httpEtag) return null;
  const m = /^bytes=(\d*)-(\d*)$/.exec(value.trim());
  if (!m || (m[1] === "" && m[2] === "")) return null;
  const size = obj.size;
  if (m[1] === "") {
    const n = Number(m[2]);
    if (n === 0 || size === 0) return "unsatisfiable";
    const length = Math.min(n, size);
    return { offset: size - length, length };
  }
  const start = Number(m[1]);
  const end = m[2] === "" ? size - 1 : Number(m[2]);
  if (end < start) return null;
  if (start >= size) return "unsatisfiable";
  return { offset: start, length: Math.min(end, size - 1) - start + 1 };
}

function notFound() {
  return new Response("This download isn't available right now. Please try again later, or get Kodiak Cleaner from https://github.com/ttabakagroup/kodiakcleaner/releases", {
    status: 404,
    headers: { "content-type": "text/plain; charset=utf-8", "cache-control": "no-store" },
  });
}
