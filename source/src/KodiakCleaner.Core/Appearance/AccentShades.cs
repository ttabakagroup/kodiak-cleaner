namespace KodiakCleaner.Core.Appearance;

/// <summary>The theme colors an accent is fitted against, as #RRGGBB.</summary>
public sealed record AccentGround(string Bg, string Surface, string SurfaceAlt, bool Dark);

/// <summary>The shades one accent gives in one theme and mode (#RRGGBB; Tint is #AARRGGBB).</summary>
public sealed record AccentShadeSet(string Accent, string AccentText, string Primary, string OnPrimary, string Tint);

/// <summary>
/// Ted's accessible accent shades: a C# port of accentTokens() in the standard's accent.js (tools/design/accent.js).
/// Keeps the hue and saturation and moves only the lightness until every use meets WCAG 2.2 AA: fills, switches and
/// the focus ring 3:1 against the page, cards and inset areas; links and accent text 4.5:1 (also on the selected-item
/// tint); white button labels 4.5:1 on the primary fill. A color that already passes is kept exactly.
/// </summary>
public static class AccentShades
{
    /// <summary>The built-in swatches after the theme's own accent and Windows, in the standard's order.</summary>
    public static readonly IReadOnlyList<(string Name, string Hex)> Swatches = new[]
    {
        ("Blue", "#2563EB"), ("Teal", "#0D9488"), ("Green", "#16A34A"), ("Purple", "#7C3AED"),
        ("Pink", "#DB2777"), ("Orange", "#EA580C"), ("Slate", "#525F73"),
    };

    /// <summary>
    /// The swatch that is a color scheme's own accent (Classic's is Blue, #2563EB), or null. That scheme's first swatch already is
    /// this color, so the scheme hides the duplicate and treats a saved choice of it as its own accent (Ted, 0.8.32).
    /// </summary>
    public static string? SchemeSwatch(string scheme) => scheme == "Classic" ? "Blue" : null;

    public static AccentShadeSet For(string baseHex, AccentGround theme)
    {
        var grounds = new[] { theme.Bg, theme.Surface, theme.SurfaceAlt };
        bool darker = !theme.Dark;
        var accent = Fit(baseHex, grounds, 3, darker);
        double alpha = theme.Dark ? 0.24 : 0.18;
        var tintOnSurface = Mix(accent, alpha, theme.Surface);
        var accentText = Fit(baseHex, grounds.Append(tintOnSurface).ToArray(), 4.5, darker);
        const string onPrimary = "#FFFFFF";
        var primary = Fit(baseHex, new[] { onPrimary }, 4.5, true);
        var tint = $"#{(int)Math.Round(alpha * 255):X2}{accent[1..]}";
        return new AccentShadeSet(accent, accentText, primary, onPrimary, tint);
    }

    public static double Contrast(string a, string b)
    {
        double x = Luminance(ToRgb(a)), y = Luminance(ToRgb(b));
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }

    private static string Fit(string hex, string[] against, double need, bool darker)
    {
        hex = hex.ToUpperInvariant();
        if (against.All(bg => Contrast(hex, bg) >= need)) return hex;
        var (h, s, l) = ToHsl(hex);
        for (int i = 0; i < 200; i++)
        {
            var c = FromHsl(h, s, l);
            if (against.All(bg => Contrast(c, bg) >= need)) return c;
            l = Math.Min(1, Math.Max(0, l + (darker ? -0.005 : 0.005)));
        }
        return darker ? "#000000" : "#FFFFFF";
    }

    private static double[] ToRgb(string hex)
    {
        var h = hex.TrimStart('#');
        if (h.Length == 8) h = h[2..]; // #AARRGGBB: the color part
        return new[] { Convert.ToInt32(h[..2], 16), Convert.ToInt32(h[2..4], 16), Convert.ToInt32(h[4..6], 16) }.Select(v => (double)v).ToArray();
    }

    // JavaScript's Math.round rounds halves up; .NET's default would round them to even.
    private static string ToHex(double[] c) => "#" + string.Concat(c.Select(v => ((int)Math.Floor(v + 0.5)).ToString("X2")));

    private static double Lin(double v) { v /= 255; return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4); }
    private static double Luminance(double[] c) => 0.2126 * Lin(c[0]) + 0.7152 * Lin(c[1]) + 0.0722 * Lin(c[2]);

    private static string Mix(string fg, double alpha, string bg)
    {
        var f = ToRgb(fg); var b = ToRgb(bg);
        return ToHex(f.Select((v, i) => v * alpha + b[i] * (1 - alpha)).ToArray());
    }

    private static (double H, double S, double L) ToHsl(string hex)
    {
        var c = ToRgb(hex).Select(v => v / 255).ToArray();
        double r = c[0], g = c[1], b = c[2], max = c.Max(), min = c.Min(), l = (max + min) / 2;
        if (max == min) return (0, 0, l);
        double d = max - min, s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        double h = max == r ? (g - b) / d + (g < b ? 6 : 0) : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
        return (h / 6, s, l);
    }

    private static string FromHsl(double h, double s, double l)
    {
        if (s == 0) return ToHex(new[] { l * 255, l * 255, l * 255 });
        double q = l < 0.5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
        double F(double t)
        {
            t = ((t % 1) + 1) % 1; // JS: (t + 1) % 1 for t in (-1, 2)
            return t < 1.0 / 6 ? p + (q - p) * 6 * t : t < 0.5 ? q : t < 2.0 / 3 ? p + (q - p) * (2.0 / 3 - t) * 6 : p;
        }
        return ToHex(new[] { F(h + 1.0 / 3) * 255, F(h) * 255, F(h - 1.0 / 3) * 255 });
    }
}
