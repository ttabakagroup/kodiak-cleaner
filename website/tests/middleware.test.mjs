// Other addresses and preview links go to www.kodiakcleaner.app; www pages are served normally.
import { test } from "node:test";
import assert from "node:assert/strict";
import { onRequest } from "../functions/_middleware.js";

const run = (url) => onRequest({ request: new Request(url), next: async () => new Response("page") });
const goes = async (from, to) => {
  const res = await run(from);
  assert.equal(res.status, 301, from);
  assert.equal(res.headers.get("location"), to);
};

test("bare domain and pages.dev go to www with the same path and query", async () => {
  await goes("https://kodiakcleaner.app/features/?x=1", "https://www.kodiakcleaner.app/features/?x=1");
  await goes("https://kodiakcleaner.pages.dev/changelog/", "https://www.kodiakcleaner.app/changelog/");
});

test("preview links go to www, and /download or /downloads land on the download page", async () => {
  await goes("https://3c0fc5da.kodiakcleaner.pages.dev/download", "https://www.kodiakcleaner.app/download/");
  await goes("https://3c0fc5da.kodiakcleaner.pages.dev/downloads", "https://www.kodiakcleaner.app/download/");
  await goes("https://www.kodiakcleaner.app/downloads", "https://www.kodiakcleaner.app/download/");
});

test("www pages are served normally", async () => {
  for (const u of ["https://www.kodiakcleaner.app/", "https://www.kodiakcleaner.app/download/", "https://www.kodiakcleaner.app/api/contact"]) {
    assert.equal(await (await run(u)).text(), "page", u);
  }
});

test("the update file and the installer are served on www without a redirect (the app doesn't follow redirects)", async () => {
  for (const url of ["https://www.kodiakcleaner.app/updates/dev.json", "https://www.kodiakcleaner.app/download/KodiakCleaner-Installer.msi"]) {
    const res = await run(url);
    assert.equal(res.status, 200, url);
  }
});
