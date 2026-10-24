using System.Xml.Linq;

namespace KodiakCleaner.Core.Tests;

/// <summary>
/// WCAG 2.1 AA checks for the Windows app (audit 2026-10-24). Screen readers need a name for every control (4.1.2): most are filled
/// in at run time by Views/AccessibleNames.cs from a control's label, the text beside it or its tooltip, so an icon-only button
/// needs a tooltip (or its own AutomationProperties.Name) to have anything to be named from. Keyboard focus must show (2.4.7), and a
/// field's edge must stand out from the card (1.4.11, ControlBorder is the 3:1 token).
/// </summary>
public class AccessibilityTests
{
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static IEnumerable<string> XamlFiles() =>
        Directory.EnumerateFiles(Repo.File("src", "KodiakCleaner.App"), "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                        && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar));

    private static bool IsGlyphs(string? s) => !string.IsNullOrWhiteSpace(s) && s.All(c => c is >= '' and <= '' || char.IsWhiteSpace(c));

    [Fact]
    public void ControlsAreNamedForScreenReaders()
    {
        var app = File.ReadAllText(Repo.File("src", "KodiakCleaner.App", "App.xaml.cs"));
        Assert.Contains("AccessibleNames.Register()", app);
    }

    [Fact]
    public void IconOnlyButtonsSayWhatTheyDo()
    {
        var bad = new List<string>();
        foreach (var file in XamlFiles())
        {
            var doc = XDocument.Load(file);
            foreach (var e in doc.Descendants().Where(e => e.Name.Namespace == Ns && e.Name.LocalName is "Button" or "ToggleButton" or "RepeatButton"))
            {
                if (e.Ancestors().Any(a => a.Name.LocalName is "ControlTemplate" or "Style")) continue;
                if (e.Attribute("ToolTip") is not null || e.Attribute("AutomationProperties.Name") is not null
                    || e.Elements().Any(c => c.Name.LocalName is "Button.ToolTip" or "ToggleButton.ToolTip")) continue;
                var content = (string?)e.Attribute("Content");
                var texts = e.Descendants().Where(d => d.Name.LocalName is "TextBlock" or "Run")
                    .Select(d => (string?)d.Attribute("Text") ?? d.Value).ToList();
                var iconOnly = content is not null ? IsGlyphs(content) : texts.Count > 0 && texts.All(IsGlyphs);
                if (iconOnly) bad.Add($"{Path.GetFileName(file)}: {e.Attribute("Command") ?? e.Attribute("Click") ?? e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))}");
            }
        }
        Assert.True(bad.Count == 0, "Icon-only buttons need a ToolTip or AutomationProperties.Name:\n" + string.Join("\n", bad));
    }

    [Fact]
    public void KeyboardFocusAlwaysShows()
    {
        // Each style that turns the standard focus ring off draws its own instead; anything new needs a ring.
        var allowed = new[] { "PaneSplitterBase", "SidebarSplitter", "TextBox", "DataGrid" };
        var doc = XDocument.Load(Repo.File("src", "KodiakCleaner.App", "Themes", "Controls.xaml"));
        var off = doc.Descendants(Ns + "Setter")
            .Where(s => (string?)s.Attribute("Property") == "FocusVisualStyle" && ((string?)s.Attribute("Value"))?.Contains("x:Null") == true)
            .Select(s => s.Parent!)
            .Select(style => (string?)style.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")) ?? (string?)style.Attribute("TargetType"))
            .ToList();
        Assert.All(off, name => Assert.Contains(name, allowed));
        foreach (var file in XamlFiles().Where(f => !f.EndsWith("Controls.xaml")))
            Assert.DoesNotContain("FocusVisualStyle=\"{x:Null}\"", File.ReadAllText(file));
        // Tables show focus on the cell, inside it (a ring outside would hide under the next row).
        var cell = doc.Descendants(Ns + "Style").Single(s => (string?)s.Attribute("TargetType") == "DataGridCell");
        Assert.Contains(cell.Elements(Ns + "Setter"), s => (string?)s.Attribute("Property") == "FocusVisualStyle"
                                                          && (string?)s.Attribute("Value") == "{StaticResource CellFocusVisual}");
    }

    [Fact]
    public void FieldsHaveEdgesYouCanSee()
    {
        var doc = XDocument.Load(Repo.File("src", "KodiakCleaner.App", "Themes", "Controls.xaml"));
        foreach (var type in new[] { "TextBox", "ComboBox" })
        {
            var style = doc.Descendants(Ns + "Style").Single(s => (string?)s.Attribute("TargetType") == type && s.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")) is null);
            var border = style.Elements(Ns + "Setter").Single(s => (string?)s.Attribute("Property") == "BorderBrush");
            Assert.Equal("{DynamicResource ControlBorder}", (string?)border.Attribute("Value"));
        }
    }
}
