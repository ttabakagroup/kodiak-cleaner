// Serves the download files at https://www.kodiakcleaner.app/download/<file>.
// The files are too big for Cloudflare Pages (25 MiB per file), so they live in a Cloudflare R2
// bucket bound to this project as DOWNLOADS. Upload them in the dashboard under R2 > the bucket.

const TYPES = { dmg: "application/x-apple-diskimage", msi: "application/x-msi", zip: "application/zip" };

export async function onRequestGet({ request, env, params }) {
  const name = String(params.file || "");
  if (!/^[A-Za-z0-9][A-Za-z0-9._-]*\.(dmg|msi|zip)$/.test(name)) return notFound();
  if (!env.DOWNLOADS) return notFound();

  const obj = await env.DOWNLOADS.get(name, { range: request.headers, onlyIf: request.headers });
  if (!obj) return notFound();

  const headers = new Headers();
  obj.writeHttpMetadata(headers);
  headers.set("content-type", TYPES[name.split(".").pop()]);
  headers.set("content-disposition", `attachment; filename="${name}"`);
  headers.set("etag", obj.httpEtag);
  headers.set("accept-ranges", "bytes");
  headers.set("cache-control", "public, max-age=3600");

  if (!("body" in obj)) return new Response(null, { status: 304, headers }); // matched If-None-Match
  if (obj.range && request.headers.has("range")) {
    const { offset = 0, length = obj.size - offset } = obj.range;
    headers.set("content-range", `bytes ${offset}-${offset + length - 1}/${obj.size}`);
    headers.set("content-length", String(length));
    return new Response(obj.body, { status: 206, headers });
  }
  headers.set("content-length", String(obj.size));
  return new Response(obj.body, { headers });
}

export const onRequestHead = onRequestGet;

function notFound() {
  return new Response("This download isn't available right now. Please try again later, or get Kodiak Cleaner from https://github.com/ttabakagroup/kodiakcleaner/releases", {
    status: 404,
    headers: { "content-type": "text/plain; charset=utf-8", "cache-control": "no-store" },
  });
}
