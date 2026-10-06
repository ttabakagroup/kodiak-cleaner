// A small Markdown renderer for the project's own documents (headings, paragraphs,
// bullet lists, bold, code and links). It escapes everything else.

export const esc = (s) => String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");

export function inline(text) {
  let s = esc(text);
  s = s.replace(/`([^`]+)`/g, "<code>$1</code>");
  s = s.replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>");
  s = s.replace(/(^|[\s(])_([^_]+)_(?=[\s).,]|$)/g, "$1<em>$2</em>");
  s = s.replace(/\[([^\]]+)\]\(([^)\s]+)\)/g, (m, label, href) => {
    const ext = /^https?:/.test(href);
    return `<a href="${href}"${ext ? ' rel="noopener"' : ""}>${label}</a>`;
  });
  return s;
}

// Renders Markdown blocks. headingShift moves "##" down a level when needed.
export function render(md, { headingShift = 0, skipTitle = false } = {}) {
  const lines = md.replace(/\r\n?/g, "\n").split("\n");
  const out = [];
  let para = [];
  let list = null;
  const flushPara = () => { if (para.length) { out.push(`<p>${inline(para.join(" "))}</p>`); para = []; } };
  const flushList = () => { if (list) { out.push(`<ul>${list.map((i) => `<li>${inline(i)}</li>`).join("")}</ul>`); list = null; } };
  for (const raw of lines) {
    const line = raw.trimEnd();
    const h = /^(#{1,6})\s+(.*)$/.exec(line);
    if (h) {
      flushPara(); flushList();
      if (h[1].length === 1 && skipTitle) continue;
      const level = Math.min(6, h[1].length + headingShift);
      out.push(`<h${level}>${inline(h[2])}</h${level}>`);
      continue;
    }
    const li = /^\s*[-*]\s+(.*)$/.exec(line);
    if (li) { flushPara(); (list ||= []).push(li[1]); continue; }
    if (!line.trim()) { flushPara(); flushList(); continue; }
    if (list && /^\s{2,}\S/.test(raw)) { list[list.length - 1] += " " + line.trim(); continue; }
    flushList();
    para.push(line.trim());
  }
  flushPara(); flushList();
  return out.join("\n");
}

// Splits CHANGELOG.md into releases: [{ name, version, channel, date, sections: [{ title, items }] }]
export function parseChangelog(md) {
  const releases = [];
  let cur = null, sec = null;
  for (const raw of md.replace(/\r\n?/g, "\n").split("\n")) {
    const line = raw.trimEnd();
    const r = /^## \[(.+?)\](?:\s*-\s*(\d{4}-\d{2}-\d{2}))?/.exec(line);
    if (r) {
      cur = null; sec = null;
      if (r[1] === "Unreleased") continue;
      const m = /^(\d+\.\d+\.\d+)(?:\s+(Dev|Beta))?$/.exec(r[1]);
      if (!m) continue;
      cur = { name: r[1], version: m[1], channel: (m[2] || "Stable").toLowerCase(), date: r[2] || "", sections: [] };
      releases.push(cur);
      continue;
    }
    if (!cur) continue;
    const s = /^### (.+)$/.exec(line);
    if (s) { sec = { title: s[1], items: [] }; cur.sections.push(sec); continue; }
    const li = /^- (.*)$/.exec(line);
    if (li) {
      if (!sec) { sec = { title: "", items: [] }; cur.sections.push(sec); }
      sec.items.push(li[1]);
      continue;
    }
    if (sec && sec.items.length && /^\s+\S/.test(raw)) sec.items[sec.items.length - 1] += " " + line.trim();
  }
  return releases;
}
