/* Accent colors for Honey, Classic and Glacier. Free for everyone, never locked.
   An accent recolors buttons, switches, links, highlights and the focus ring.
   Every shade is computed against the current theme so it always meets WCAG 2.2 AA:
   text 4.5:1, controls and focus rings 3:1. Copy this file into the project unchanged.

   In the page:  applyAccent("blue")        a built-in swatch
                 applyAccent("#D0000C")     any color, e.g. the Windows accent
                 applyAccent("theme")       back to the theme's own accent
   Call applyAccent again whenever the palette or light/dark mode changes. */
(function (root) {
  "use strict";

  // Swatch order is fixed: theme's own accent, Windows (only where the platform exposes it), then these.
  const ACCENT_SWATCHES = [
    { id: "blue", name: "Blue", hex: "#2563EB" },
    { id: "teal", name: "Teal", hex: "#0D9488" },
    { id: "green", name: "Green", hex: "#16A34A" },
    { id: "purple", name: "Purple", hex: "#7C3AED" },
    { id: "pink", name: "Pink", hex: "#DB2777" },
    { id: "orange", name: "Orange", hex: "#EA580C" },
    { id: "slate", name: "Slate", hex: "#525F73" },
  ];
  const ACCENT_TOKENS = ["--accent", "--accent-text", "--link", "--accent-tint", "--primary",
    "--on-primary", "--toggle-on", "--focus-ring"];

  const toRgb = (hex) => { const h = hex.replace("#", ""); return [0, 2, 4].map((i) => parseInt(h.slice(i, i + 2), 16)); };
  const toHex = (c) => "#" + c.map((v) => Math.round(v).toString(16).padStart(2, "0")).join("").toUpperCase();
  const lin = (v) => { v /= 255; return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; };
  const lum = (c) => 0.2126 * lin(c[0]) + 0.7152 * lin(c[1]) + 0.0722 * lin(c[2]);
  const contrast = (a, b) => { const x = lum(toRgb(a)), y = lum(toRgb(b)); return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05); };
  const mix = (fg, alpha, bg) => { const f = toRgb(fg), b = toRgb(bg); return toHex(f.map((v, i) => v * alpha + b[i] * (1 - alpha))); };

  function toHsl(hex) {
    const [r, g, b] = toRgb(hex).map((v) => v / 255);
    const max = Math.max(r, g, b), min = Math.min(r, g, b), l = (max + min) / 2;
    if (max === min) return [0, 0, l];
    const d = max - min, s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
    const h = max === r ? (g - b) / d + (g < b ? 6 : 0) : max === g ? (b - r) / d + 2 : (r - g) / d + 4;
    return [h / 6, s, l];
  }
  function fromHsl(h, s, l) {
    if (s === 0) return toHex([l * 255, l * 255, l * 255]);
    const q = l < 0.5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
    const f = (t) => { t = (t + 1) % 1; return t < 1 / 6 ? p + (q - p) * 6 * t : t < 1 / 2 ? q : t < 2 / 3 ? p + (q - p) * (2 / 3 - t) * 6 : p; };
    return toHex([f(h + 1 / 3) * 255, f(h) * 255, f(h - 1 / 3) * 255]);
  }

  // Same hue and saturation; move lightness one way until every background passes.
  function fit(hex, against, need, darker) {
    hex = hex.toUpperCase();
    if (against.every((bg) => contrast(hex, bg) >= need)) return hex;  // already passes: keep it exactly
    const [h, s] = toHsl(hex); let l = toHsl(hex)[2];
    for (let i = 0; i < 200; i++) {
      const c = fromHsl(h, s, l);
      if (against.every((bg) => contrast(c, bg) >= need)) return c;
      l = Math.min(1, Math.max(0, l + (darker ? -0.005 : 0.005)));
    }
    return darker ? "#000000" : "#FFFFFF";
  }

  /* Pure function: works in the browser and in Node (used by scripts/check_contrast.py).
     theme = { bg, surface, surfaceAlt, text, dark } as #RRGGBB from the current palette and mode. */
  function accentTokens(base, theme) {
    const grounds = [theme.bg, theme.surface, theme.surfaceAlt];
    const darker = !theme.dark;
    const accent = fit(base, grounds, 3, darker);              // fills, switches, focus ring
    const alpha = theme.dark ? 0.24 : 0.18;
    const tintOnSurface = mix(accent, alpha, theme.surface);
    const accentText = fit(base, [...grounds, tintOnSurface], 4.5, darker); // links and accent text
    const onPrimary = "#FFFFFF";                                  // same white label in every theme and mode
    const primary = fit(base, [onPrimary], 4.5, true);           // darken the button fill until white text passes
    const [r, g, b] = toRgb(accent);
    return {
      "--accent": accent, "--toggle-on": accent, "--focus-ring": accent,
      "--accent-text": accentText, "--link": accentText,
      "--accent-tint": `rgba(${r},${g},${b},${alpha})`,
      "--primary": primary, "--on-primary": onPrimary,
    };
  }

  function themeFromPage() {
    const cs = getComputedStyle(document.documentElement);
    const read = (n) => {
      const probe = document.createElement("i"); probe.style.color = cs.getPropertyValue(n).trim();
      document.documentElement.appendChild(probe);
      const m = getComputedStyle(probe).color.match(/\d+/g).slice(0, 3).map(Number); probe.remove();
      return toHex(m);
    };
    return { bg: read("--bg"), surface: read("--surface"), surfaceAlt: read("--surface-alt"), text: read("--text"),
      dark: cs.getPropertyValue("color-scheme").trim() === "dark" };
  }

  // The Windows accent, when the platform exposes it. Returns #RRGGBB or null (then hide the Windows swatch).
  function systemAccent() {
    if (!(root.CSS && CSS.supports("color", "AccentColor"))) return null;
    const probe = document.createElement("i"); probe.style.color = "AccentColor";
    document.documentElement.appendChild(probe);
    const m = getComputedStyle(probe).color.match(/\d+/g); probe.remove();
    return m ? toHex(m.slice(0, 3).map(Number)) : null;
  }

  // choice: "theme", "windows", a swatch id, or a #RRGGBB color.
  function applyAccent(choice) {
    const el = document.documentElement;
    ACCENT_TOKENS.forEach((t) => el.style.removeProperty(t));        // start from the theme's own accent
    let base = null;
    if (choice === "windows") base = systemAccent();
    else if (/^#[0-9a-f]{6}$/i.test(choice || "")) base = choice;
    else { const s = ACCENT_SWATCHES.find((x) => x.id === choice); base = s ? s.hex : null; }
    if (!base) { el.removeAttribute("data-accent"); return; }      // "theme", or Windows not available
    const tokens = accentTokens(base, themeFromPage());
    Object.entries(tokens).forEach(([k, v]) => el.style.setProperty(k, v));
    el.setAttribute("data-accent", choice);
  }

  const api = { ACCENT_SWATCHES, ACCENT_TOKENS, accentTokens, applyAccent, systemAccent, contrast };
  if (typeof module !== "undefined" && module.exports) module.exports = api;
  else root.AppAccent = api;
})(typeof window !== "undefined" ? window : globalThis);
