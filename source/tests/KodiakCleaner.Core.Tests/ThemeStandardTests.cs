using System.Text.RegularExpressions;
using KodiakCleaner.Core.Appearance;
using Xunit;

namespace KodiakCleaner.Core.Tests;

/// <summary>Ted's standard (2026-10-05): the six themes match tools/design/theme.css, and accents get accessible shades.</summary>
public class ThemeStandardTests
{
    private static readonly Dictionary<string, string> Files = new()
    {
        ["honey-light"] = "HoneyLight", ["honey-dark"] = "HoneyDark", ["classic-light"] = "ClassicLight",
        ["classic-dark"] = "ClassicDark", ["glacier-light"] = "GlacierLight", ["glacier-dark"] = "GlacierDark",
    };

    private static Dictionary<string, string> Xaml(string theme) =>
        Regex.Matches(Repo.Read("src", "KodiakCleaner.App", "Themes", Files[theme] + ".xaml"), @"<SolidColorBrush x:Key=""(\w+)"" Color=""(#\w+)""")
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.ToUpperInvariant());

    /// <summary>theme.css blocks, in the order the standard writes them, each on top of Honey light.</summary>
    private static Dictionary<string, string> Css(string theme)
    {
        var css = Repo.Read("tools", "design", "theme.css");
        Dictionary<string, string> Block(string selector)
        {
            var m = Regex.Match(css, selector + @"\s*\{([^}]*)\}");
            return Regex.Matches(m.Groups[1].Value, @"--([\w-]+):\s*([^;]+);").ToDictionary(x => x.Groups[1].Value, x => x.Groups[2].Value.Trim());
        }
        var t = Block(Regex.Escape("/* Honey light (default) */") + @"\s*:root");
        void Over(string sel) { foreach (var (k, v) in Block(Regex.Escape(sel))) t[k] = v; }
        if (theme == "honey-dark") Over(":root[data-theme=\"dark\"]");
        if (theme.StartsWith("classic")) Over(":root[data-palette=\"classic\"]");
        if (theme == "classic-dark") Over(":root[data-palette=\"classic\"][data-theme=\"dark\"]");
        if (theme.StartsWith("glacier")) Over(":root[data-palette=\"glacier\"]");
        if (theme == "glacier-dark") Over(":root[data-palette=\"glacier\"][data-theme=\"dark\"]");
        for (int i = 0; i < 3; i++)
            foreach (var (k, v) in t.ToList())
                if (Regex.Match(v, @"^var\(--([\w-]+)\)$") is { Success: true } r) t[k] = t[r.Groups[1].Value];
        return t;
    }

    private static string Argb(string css)
    {
        if (css.StartsWith('#')) return css.ToUpperInvariant();
        var m = Regex.Match(css.Replace(" ", ""), @"rgba\(([\d.]+),([\d.]+),([\d.]+),([\d.]+)\)");
        double V(int i) => double.Parse(m.Groups[i].Value, System.Globalization.CultureInfo.InvariantCulture);
        return $"#{(int)Math.Round(V(4) * 255, MidpointRounding.AwayFromZero):X2}{(int)V(1):X2}{(int)V(2):X2}{(int)V(3):X2}";
    }

    private static string Key(string token) => string.Concat(token.Split('-').Select(p => char.ToUpperInvariant(p[0]) + p[1..]));

    [Fact]
    public void Each_schemes_own_accent_comes_first_and_is_not_repeated()
    {
        // Ted (0.8.32): the scheme's own accent is the first swatch. A built-in swatch that is the same color (Classic's Blue) is
        // the scheme's own one, hidden in that scheme, and no other swatch duplicates a scheme accent.
        foreach (var scheme in new[] { "Honey", "Classic", "Glacier" })
        {
            var own = Css(scheme.ToLowerInvariant() + "-light")["accent"].ToUpperInvariant();
            var same = AccentShades.Swatches.Where(s => s.Hex == own).Select(s => s.Name).SingleOrDefault();
            Assert.Equal(AccentShades.SchemeSwatch(scheme), same);
        }
    }

    [Theory]
    [InlineData("honey-light")] [InlineData("honey-dark")] [InlineData("classic-light")]
    [InlineData("classic-dark")] [InlineData("glacier-light")] [InlineData("glacier-dark")]
    public void Every_theme_color_matches_the_standard(string theme)
    {
        var xaml = Xaml(theme);
        foreach (var (token, value) in Css(theme))
        {
            if (token is "font" or "font-display" or "radius" or "radius-card" or "focus-glow") continue;
            Assert.True(xaml.TryGetValue(Key(token), out var mine), $"{theme}: {Key(token)} is missing");
            Assert.True(Argb(value) == mine, $"{theme}: {Key(token)} is {mine}, the standard says {Argb(value)}");
        }
    }

    [Theory]
    [InlineData("#2563EB", "honey-light", "#2563EB", "#1558E9", "#2563EB")]
    [InlineData("#2563EB", "honey-dark", "#2765EB", "#749CF2", "#2563EB")]
    [InlineData("#2563EB", "classic-light", "#2563EB", "#1558E9", "#2563EB")]
    [InlineData("#2563EB", "classic-dark", "#3A72ED", "#7EA3F3", "#2563EB")]
    [InlineData("#2563EB", "glacier-light", "#2563EB", "#1554DF", "#2563EB")]
    [InlineData("#2563EB", "glacier-dark", "#336DEC", "#82A6F4", "#2563EB")]
    [InlineData("#0D9488", "honey-light", "#0D9488", "#0A736A", "#0C8479")]
    [InlineData("#0D9488", "honey-dark", "#0D9488", "#10B7A8", "#0C8479")]
    [InlineData("#0D9488", "classic-light", "#0D9488", "#0A736A", "#0C8479")]
    [InlineData("#0D9488", "classic-dark", "#0D9488", "#10BAAA", "#0C8479")]
    [InlineData("#0D9488", "glacier-light", "#0D9488", "#0A6E66", "#0C8479")]
    [InlineData("#0D9488", "glacier-dark", "#0D9488", "#11C1B1", "#0C8479")]
    [InlineData("#16A34A", "honey-light", "#159F48", "#107837", "#12883E")]
    [InlineData("#16A34A", "honey-dark", "#16A34A", "#1AC057", "#12883E")]
    [InlineData("#16A34A", "classic-light", "#16A149", "#107837", "#12883E")]
    [InlineData("#16A34A", "classic-dark", "#16A34A", "#1AC258", "#12883E")]
    [InlineData("#16A34A", "glacier-light", "#159C47", "#0F7234", "#12883E")]
    [InlineData("#16A34A", "glacier-dark", "#16A34A", "#1BC75A", "#12883E")]
    [InlineData("#7C3AED", "honey-light", "#7C3AED", "#7935ED", "#7C3AED")]
    [InlineData("#7C3AED", "honey-dark", "#8548EE", "#B189F4", "#7C3AED")]
    [InlineData("#7C3AED", "classic-light", "#7C3AED", "#7935ED", "#7C3AED")]
    [InlineData("#7C3AED", "classic-dark", "#8F56F0", "#B590F5", "#7C3AED")]
    [InlineData("#7C3AED", "glacier-light", "#7C3AED", "#742EEC", "#7C3AED")]
    [InlineData("#7C3AED", "glacier-dark", "#8C51EF", "#B995F5", "#7C3AED")]
    [InlineData("#DB2777", "honey-light", "#DB2777", "#BC1F65", "#DB2777")]
    [InlineData("#DB2777", "honey-dark", "#DB2777", "#E97AAB", "#DB2777")]
    [InlineData("#DB2777", "classic-light", "#DB2777", "#BC1F65", "#DB2777")]
    [InlineData("#DB2777", "classic-dark", "#DC2E7B", "#E97AAB", "#DB2777")]
    [InlineData("#DB2777", "glacier-light", "#DB2777", "#B61E62", "#DB2777")]
    [InlineData("#DB2777", "glacier-dark", "#DB2777", "#EA7EAE", "#DB2777")]
    [InlineData("#EA580C", "honey-light", "#EA580C", "#B24309", "#CD4D0B")]
    [InlineData("#EA580C", "honey-dark", "#EA580C", "#F6874D", "#CD4D0B")]
    [InlineData("#EA580C", "classic-light", "#EA580C", "#B24309", "#CD4D0B")]
    [InlineData("#EA580C", "classic-dark", "#EA580C", "#F6874D", "#CD4D0B")]
    [InlineData("#EA580C", "glacier-light", "#EA580C", "#AB4009", "#CD4D0B")]
    [InlineData("#EA580C", "glacier-dark", "#EA580C", "#F68D56", "#CD4D0B")]
    [InlineData("#525F73", "honey-light", "#525F73", "#525F73", "#525F73")]
    [InlineData("#525F73", "honey-dark", "#617088", "#94A0B3", "#525F73")]
    [InlineData("#525F73", "classic-light", "#525F73", "#525F73", "#525F73")]
    [InlineData("#525F73", "classic-dark", "#687992", "#9AA6B7", "#525F73")]
    [InlineData("#525F73", "glacier-light", "#525F73", "#525F73", "#525F73")]
    [InlineData("#525F73", "glacier-dark", "#66768F", "#9FAABA", "#525F73")]
    public void Accent_shades_match_the_standard(string baseHex, string theme, string accent, string accentText, string primary)
    {
        var x = Xaml(theme);
        var set = AccentShades.For(baseHex, new AccentGround(x["Bg"], x["Surface"], x["SurfaceAlt"], theme.EndsWith("dark")));
        Assert.Equal(accent, set.Accent);
        Assert.Equal(accentText, set.AccentText);
        Assert.Equal(primary, set.Primary);
        Assert.Equal("#FFFFFF", set.OnPrimary);
    }

    [Theory]
    [InlineData("#0078D4")] [InlineData("#D0000C")] [InlineData("#FFB900")] [InlineData("#FFC0CB")]
    [InlineData("#E3008C")] [InlineData("#00B7C3")] [InlineData("#111111")] [InlineData("#FFFFFF")] [InlineData("#767676")]
    public void Any_windows_accent_becomes_readable_in_every_theme(string windows)
    {
        foreach (var theme in Files.Keys)
        {
            var x = Xaml(theme);
            var set = AccentShades.For(windows, new AccentGround(x["Bg"], x["Surface"], x["SurfaceAlt"], theme.EndsWith("dark")));
            foreach (var ground in new[] { x["Bg"], x["Surface"], x["SurfaceAlt"] })
            {
                Assert.True(AccentShades.Contrast(set.Accent, ground) >= 3, $"{theme} {windows}: accent");
                Assert.True(AccentShades.Contrast(set.AccentText, ground) >= 4.5, $"{theme} {windows}: accent text");
            }
            Assert.True(AccentShades.Contrast(set.OnPrimary, set.Primary) >= 4.5, $"{theme} {windows}: button label");
        }
    }

    [Theory]
    [InlineData("#D9A441", "#FFFFFF")] // Honey light: white labels
    [InlineData("#8FB6C7", "#1E2528")] // Glacier dark: dark labels
    [InlineData("#525F73", "#FFFFFF")] // Slate, a muted accent
    public void Disk_analyzer_chart_shades_follow_the_accent_and_keep_labels_readable(string accent, string label)
    {
        var turns = new[] { -48.0, -24, 0, 24, 48, 80 };
        var shades = turns.Select(t => AccentShades.ChartShade(accent, t, 1, label)).ToList();
        Assert.All(shades, c => Assert.True(AccentShades.Contrast(c, label) >= 4.5, $"{c} on {label}"));
        Assert.Equal(shades.Count, shades.Distinct().Count()); // every kind gets its own color
    }
}
