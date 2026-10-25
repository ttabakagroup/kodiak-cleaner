import { pageHead } from "../layout.mjs";
import { esc, inline } from "../markdown.mjs";

// One changelog entry per version, with the versions listed beside them.
function entries(ctx, list) {
  const nav = list.map((r) => `<li><a href="#v${r.version}">${esc(r.name)}</a></li>`).join("");
  const items = list.map((r) => {
    const d = ctx.dl(r);
    const links = d.published ? `<span class="links"><a href="${d.releasePage}" rel="noopener">Release Page</a>${r === ctx.latest ? `<a href="/download/">Download</a>` : ""}</span>` : "";
    const secs = r.sections.map((s) => `${s.title ? `<h3>${esc(s.title)}</h3>` : ""}<ul>${s.items.map((i) => `<li>${inline(i)}</li>`).join("")}</ul>`).join("");
    return `<article class="card release" id="v${r.version}"><header><h2>${esc(r.name)}</h2><time datetime="${r.date}">${ctx.longDate(r.date)}</time>${links}</header>${secs}</article>`;
  }).join("\n");
  return { nav, items };
}

// The changelog since the Beta. The Dev history before it is on its own page, linked quietly at the bottom.
export default function changelog(ctx) {
  const { nav, items } = entries(ctx, ctx.releases);
  const more = ctx.history.length ? `\n    <p class="muted center mt-xl"><a href="/changelog/history/">All Changes Since the Start</a></p>` : "";

  const body = `
${pageHead("Changelog", `Every change to Kodiak Cleaner since the ${esc(ctx.channel)}, newest first. The same notes appear in What's New after an update.`)}
<section class="tight">
  <div class="wrap log-layout">
    <nav class="log-nav" aria-label="Versions"><h2>Versions</h2><ol>${nav}</ol></nav>
    <div>${items}${more}</div>
  </div>
</section>`;

  return {
    id: "changelog",
    path: "/changelog/",
    out: "changelog/index.html",
    title: "Changelog",
    description: `The Kodiak Cleaner changelog: every ${ctx.channel} version and every change, newest first.`,
    body,
  };
}

// /changelog/history/: the Dev builds from before the Beta. Not in the menu, the sitemap or search results.
export function changelogHistory(ctx) {
  if (!ctx.history.length) return null;
  const { nav, items } = entries(ctx, ctx.history);
  const oldest = ctx.history[ctx.history.length - 1], newest = ctx.history[0];

  const body = `
${pageHead("All Changes Since the Start", `The Dev builds that came before the ${esc(ctx.channel)}, ${esc(oldest.name)} to ${esc(newest.name)}, newest first. For what's new now, see the <a href="/changelog/">Changelog</a>.`)}
<section class="tight">
  <div class="wrap log-layout">
    <nav class="log-nav" aria-label="Versions"><h2>Versions</h2><ol>${nav}</ol></nav>
    <div>${items}
    <p class="muted center mt-xl"><a href="/changelog/">Back to the Changelog</a></p></div>
  </div>
</section>`;

  return {
    id: "changelog-history",
    path: "/changelog/history/",
    out: "changelog/history/index.html",
    title: "All Changes Since the Start",
    description: `Every change to Kodiak Cleaner for Windows in the Dev builds before the ${ctx.channel}, from ${oldest.name} to ${newest.name}.`,
    noindex: true,
    body,
  };
}
