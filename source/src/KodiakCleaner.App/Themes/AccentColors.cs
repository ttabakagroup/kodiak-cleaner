using System.Windows;
using System.Windows.Media;
using KodiakCleaner.Core.Appearance;
using Microsoft.Win32;

namespace KodiakCleaner.App;

/// <summary>
/// Accent colors (Ted's standard, free for everyone): the scheme's own accent, Windows, Blue, Teal, Green, Purple, Pink,
/// Orange and Slate. An accent recolors buttons, switches, links, highlights and the focus ring; everything else stays with
/// the Honey, Classic or Glacier scheme. Shades come from <see cref="AccentShades"/>, so every theme and mode passes WCAG 2.2 AA.
/// </summary>
public static class AccentColors
{
    /// <summary>Accent names in the order shown, after the scheme's own accent (""). Windows only when Windows reports one.</summary>
    public static IReadOnlyList<string> Names =>
        (WindowsAccentHex() is null ? Array.Empty<string>() : new[] { "Windows" }).Concat(AccentShades.Swatches.Select(s => s.Name)).ToList();

    /// <summary>Saved names from before 0.8.31, when Slate was called Graphite.</summary>
    public static string Migrate(string name) => name == "Graphite" ? "Slate" : name;

    public static bool IsKnown(string name) => Names.Contains(name);

    /// <summary>The swatches a scheme shows after its own accent: its own color isn't repeated (Classic has no second Blue).</summary>
    public static IReadOnlyList<string> NamesFor(Palette palette) =>
        Names.Where(n => n != AccentShades.SchemeSwatch(palette.ToString())).ToList();

    /// <summary>The accent that applies in a scheme: a saved choice of the scheme's own color means the scheme's own accent ("").</summary>
    public static string Effective(string name, Palette palette) =>
        name == AccentShades.SchemeSwatch(palette.ToString()) || !IsKnown(name) ? "" : name;

    /// <summary>The swatch as it looks in the current theme and mode (the adjusted shade, so what people pick is what they get).</summary>
    public static SolidColorBrush? BrushOf(string name, ResourceDictionary theme, bool dark) =>
        ShadesFor(name, theme, dark) is { } set ? Freeze(Parse(set.Accent)) : null;

    /// <summary>The swatch's button-fill shade in the current theme and mode, with the label color that reads on it (charts).</summary>
    public static (SolidColorBrush Fill, SolidColorBrush Label)? FillOf(string name, ResourceDictionary theme, bool dark) =>
        ShadesFor(name, theme, dark) is { } set ? (Freeze(Parse(set.Primary)), Freeze(Parse(set.OnPrimary))) : null;

    /// <summary>Overrides the accent keys in a freshly loaded theme dictionary. Unknown names (or Windows with no accent) keep the scheme's own.</summary>
    public static void ApplyTo(ResourceDictionary theme, string name, bool dark)
    {
        if (ShadesFor(name, theme, dark) is not { } set) return;
        var oldTint = Hex(theme["AccentTint"]);
        var tint = Freeze(Parse(set.Tint));
        theme["Accent"] = theme["ToggleOn"] = theme["FocusRing"] = Freeze(Parse(set.Accent));
        theme["AccentText"] = theme["Link"] = Freeze(Parse(set.AccentText));
        theme["Primary"] = Freeze(Parse(set.Primary));
        theme["OnPrimary"] = Freeze(Parse(set.OnPrimary));
        theme["AccentTint"] = tint;
        // Like the standard's theme.css, the soft focus glow and (in Honey and Glacier) row hover are the accent tint.
        if (Hex(theme["FocusGlow"]) == oldTint) theme["FocusGlow"] = tint;
        if (Hex(theme["RowHover"]) == oldTint) theme["RowHover"] = tint;
    }

    private static AccentShadeSet? ShadesFor(string name, ResourceDictionary theme, bool dark)
    {
        var baseHex = name == "Windows" ? WindowsAccentHex() : AccentShades.Swatches.FirstOrDefault(s => s.Name == name).Hex;
        if (baseHex is null) return null;
        return AccentShades.For(baseHex, new AccentGround(Hex(theme["Bg"]), Hex(theme["Surface"]), Hex(theme["SurfaceAlt"]), dark));
    }

    /// <summary>The Windows accent as #RRGGBB, or null when Windows doesn't report one (the swatch is hidden then). Local only.</summary>
    private static string? WindowsAccentHex()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key?.GetValue("AccentColor") is int abgr)
                return $"#{abgr & 0xFF:X2}{(abgr >> 8) & 0xFF:X2}{(abgr >> 16) & 0xFF:X2}";
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
        return null;
    }

    private static string Hex(object? brush) => brush is SolidColorBrush b
        ? (b.Color.A == 0xFF ? $"#{b.Color.R:X2}{b.Color.G:X2}{b.Color.B:X2}" : $"#{b.Color.A:X2}{b.Color.R:X2}{b.Color.G:X2}{b.Color.B:X2}")
        : "";

    private static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static SolidColorBrush Freeze(Color c)
    {
        var brush = new SolidColorBrush(c);
        brush.Freeze();
        return brush;
    }
}
