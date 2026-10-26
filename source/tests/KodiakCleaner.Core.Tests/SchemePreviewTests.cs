using System.Text.RegularExpressions;
using Xunit;

namespace KodiakCleaner.Core.Tests;

/// <summary>
/// Each Color Scheme tile shows a strip of the scheme's primary, secondary and tertiary colors, a third each (Ted, 0.9.5).
/// The colors come from the scheme's own theme dictionary, so every key must exist there and the three must look different.
/// </summary>
public class SchemePreviewTests
{
    private static readonly string[] Palettes = { "Honey", "Classic", "Glacier", "Lagoon" };

    private static string[] Keys(string palette)
    {
        var code = Repo.Read("src", "KodiakCleaner.App", "ThemeManager.cs");
        var body = Regex.Match(code, @"PreviewKeys\(Palette palette\) => palette switch\s*\{(.*?)\};", RegexOptions.Singleline).Groups[1].Value;
        var line = Regex.Match(body, $@"Palette\.{palette} => new\[\] \{{([^}}]*)\}}");
        if (!line.Success) line = Regex.Match(body, @"_ => new\[\] \{([^}]*)\}");
        return Regex.Matches(line.Groups[1].Value, @"""(\w+)""").Select(m => m.Groups[1].Value).ToArray();
    }

    private static Dictionary<string, string> Colors(string file) =>
        Regex.Matches(Repo.Read("src", "KodiakCleaner.App", "Themes", file + ".xaml"), @"<SolidColorBrush x:Key=""(\w+)"" Color=""(#\w+)""")
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.ToUpperInvariant());

    [Fact]
    public void EveryTileShowsThreeDifferentColorsFromItsOwnTheme()
    {
        foreach (var palette in Palettes)
        {
            var keys = Keys(palette);
            Assert.Equal(3, keys.Length);
            foreach (var mode in new[] { "Light", "Dark" })
            {
                var colors = Colors(palette + mode);
                foreach (var key in keys) Assert.True(colors.ContainsKey(key), $"{palette}{mode} has no {key}");
                Assert.Equal(3, keys.Select(k => colors[k]).Distinct().Count());
            }
        }
    }

    [Fact]
    public void HoneyShowsItsBrownGoldAndCream()
    {
        var light = Colors("HoneyLight");
        Assert.Equal(new[] { "#6B4A34", "#D9A441", "#F5EFE6" }, Keys("Honey").Select(k => light[k]));
    }

    [Fact]
    public void TheTileHasTheStrip()
    {
        var xaml = Repo.Read("src", "KodiakCleaner.App", "Views", "SettingsView.xaml");
        Assert.Contains("{Binding Fill}", xaml);
        Assert.Contains("p.Refresh()", Repo.Read("src", "KodiakCleaner.App", "ViewModels", "SettingsViewModel.cs"));
    }
}
