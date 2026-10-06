import { pageHead } from "../layout.mjs";
import { esc, inline } from "../markdown.mjs";

export default function changelog(ctx) {
  const { releases } = ctx;
  const nav = releases.map((r) => `<li><a href="#v${r.version}">${esc(r.name)}</a></li>`).join("");
  const entries = releases.map((r) => {
    const d = ctx.dl(r);
    const links = d.published ? `<span class="links"><a href="${d.releasePage}" rel="noopener">Release Page</a><a href="/download/">Download</a></span>` : "";
    const secs = r.sections.map((s) => `${s.title ? `<h3>${esc(s.title)}</h3>` : ""}<ul>${s.items.map((i) => `<li>${inline(i)}</li>`).join("")}</ul>`).join("");
    return `<article class="card release" id="v${r.version}"><header><h2>${esc(r.name)}</h2><time datetime="${r.date}">${ctx.longDate(r.date)}</time>${links}</header>${secs}</article>`;
  }).join("\n");

  const body = `
${pageHead("Changelog", "Every change to Kodiak Cleaner, newest first. The same notes appear in What's New after an update.")}
<section class="tight">
  <div class="wrap log-layout">
    <nav class="log-nav" aria-label="Versions"><h2>Versions</h2><ol>${nav}</ol></nav>
    <div>${entries}</div>
  </div>
</section>`;

  return {
    id: "changelog",
    path: "/changelog/",
    out: "changelog/index.html",
    title: "Changelog",
    description: "The full Kodiak Cleaner changelog: every version and every change, newest first.",
    body,
  };
}
