// Sends every other address to the main one, keeping the path:
//   kodiakcleaner.app/x, kodiakcleaner.pages.dev/x and preview links like 3c0fc5da.kodiakcleaner.pages.dev/x
//   -> https://www.kodiakcleaner.app/x
// Also fixes common guesses at page names (for example /downloads -> /download/).
const MAIN = "www.kodiakcleaner.app";
const ALIASES = { "/downloads": "/download/", "/downloads/": "/download/", "/download": "/download/" };

export function redirectFor(href) {
  const url = new URL(href);
  const host = url.hostname;
  const other = host === "kodiakcleaner.app" || host === "kodiakcleaner.pages.dev" || host.endsWith(".kodiakcleaner.pages.dev");
  const alias = ALIASES[url.pathname];
  if (!other && !alias) return null;
  if (alias) url.pathname = alias;
  url.hostname = MAIN;
  url.protocol = "https:";
  url.port = "";
  return url.toString();
}

export async function onRequest({ request, next }) {
  const target = redirectFor(request.url);
  return target ? Response.redirect(target, 301) : next();
}
