using System.Xml.Linq;

namespace KodiakCleaner.Core.Tests;

/// <summary>
/// Ted (0.8.99): two areas side by side or one above the other get a draggable divider between them, and where it was dragged
/// to is remembered. These tests keep every page divider on the shared grip style with a key of its own.
/// </summary>
public class PaneSplitterTests
{
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static List<(string File, XElement Element)> Splitters() =>
        Directory.EnumerateFiles(Repo.File("src", "KodiakCleaner.App"), "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .SelectMany(f => XDocument.Load(f).Descendants(Ns + "GridSplitter").Select(e => (Path.GetFileName(f), e)))
            .ToList();

    private static string? Remember(XElement e) =>
        e.Attributes().FirstOrDefault(a => a.Name.LocalName == "PaneSplitter.Remember")?.Value;

    [Fact]
    public void Page_dividers_use_the_grip_style_and_remember_their_place_under_their_own_key()
    {
        // The main window's sidebar edge has its own style and keeps its width in [General] SidebarWidth.
        var pages = Splitters().Where(s => s.File != "MainWindow.xaml").ToList();
        Assert.Contains(pages, s => s.File == "StartupView.xaml");
        // Drive Wiper (Ted, 0.8.115): between the progress card and the settings, one above the other like Startup's.
        Assert.Contains(pages, s => s.File == "WiperView.xaml" && Remember(s.Element) == "wiper" && (string?)s.Element.Attribute("Style") == "{StaticResource PaneRows}");
        foreach (var (file, e) in pages)
        {
            Assert.True((string?)e.Attribute("Style") is "{StaticResource PaneRows}" or "{StaticResource PaneColumns}", $"{file}: use PaneRows or PaneColumns");
            Assert.False(string.IsNullOrWhiteSpace(Remember(e)), $"{file}: give the divider views:PaneSplitter.Remember");
        }
        var keys = pages.Select(s => Remember(s.Element)!).ToList();
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(keys, k => Assert.True(k.IndexOfAny(new[] { ';', '=', '|' }) < 0, $"{k}: no ; = or | in a key"));
    }
}
