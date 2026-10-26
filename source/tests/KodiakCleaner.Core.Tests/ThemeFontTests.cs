using System.Text.RegularExpressions;
using Xunit;

namespace KodiakCleaner.Core.Tests;

/// <summary>Every color scheme uses the same fonts (Ted, 0.9.7), so switching schemes or light and dark never changes sizes.</summary>
public class ThemeFontTests
{
    [Fact]
    public void EverySchemeUsesFredoka()
    {
        foreach (var file in Directory.EnumerateFiles(Repo.File("src", "KodiakCleaner.App", "Themes"), "*.xaml"))
        {
            if (Path.GetFileName(file) == "Controls.xaml") continue;
            var xaml = File.ReadAllText(file);
            Assert.Equal("pack://application:,,,/Assets/Fonts/#Fredoka", Regex.Match(xaml, @"<FontFamily x:Key=""FontDisplay"">([^<]*)<").Groups[1].Value);
            Assert.Equal("pack://application:,,,/Assets/Fonts/#Fredoka", Regex.Match(xaml, @"<FontFamily x:Key=""FontBody"">([^<]*)<").Groups[1].Value);
        }
        foreach (var file in new[] { "Fredoka-Regular.ttf", "Fredoka-SemiBold.ttf", "Fredoka-OFL.txt" })
        {
            Assert.True(File.Exists(Repo.File("src", "KodiakCleaner.App", "Assets", "Fonts", file)), file);
            Assert.Contains(@"Assets\Fonts\" + file, Repo.Read("src", "KodiakCleaner.App", "KodiakCleaner.App.csproj"));
        }
    }

    [Fact]
    public void FredokaIsHintedSoRoundLettersSitOnTheLine()
    {
        // Ted 2026-10-26: unhinted Fredoka left a dot under C and e (the round overshoot showing one pixel row low in
        // Display text mode). The faces are run through ttfautohint, which adds the fpgm and prep tables.
        foreach (var file in new[] { "Fredoka-Regular.ttf", "Fredoka-SemiBold.ttf" })
        {
            var bytes = File.ReadAllBytes(Repo.File("src", "KodiakCleaner.App", "Assets", "Fonts", file));
            int tables = bytes[4] << 8 | bytes[5];
            var tags = Enumerable.Range(0, tables).Select(i => System.Text.Encoding.ASCII.GetString(bytes, 12 + 16 * i, 4)).ToList();
            Assert.Contains("fpgm", tags);
            Assert.Contains("prep", tags);
        }
    }
}
