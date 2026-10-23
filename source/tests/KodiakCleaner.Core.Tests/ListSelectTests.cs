using System.Xml.Linq;

namespace KodiakCleaner.Core.Tests;

/// <summary>
/// Ted (0.8.90): every list with several entries takes Ctrl-click, Shift-click and dragging the mouse to highlight a broad
/// selection, and a press-and-drag over tick boxes ticks them all. Views/ListSelect.cs does it for every table and list through
/// the shared styles, so these tests catch a new list that opts out by accident.
/// </summary>
public class ListSelectTests
{
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    /// <summary>Tables where one row is used at a time on purpose, with the reason.</summary>
    private static readonly Dictionary<string, string> SingleRowTables = new()
    {
        ["ScheduleWindow.xaml"] = "Edit, Run Now and Delete work on one cleaning schedule at a time.",
    };

    /// <summary>Tables whose right-click menu acts on the clicked row only, with the reason.</summary>
    private static readonly Dictionary<string, string> OneRowMenus = new();

    private static IEnumerable<(string File, XElement Element)> Elements(string localName) =>
        Directory.EnumerateFiles(Repo.File("src", "KodiakCleaner.App"), "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .SelectMany(f => XDocument.Load(f).Descendants(Ns + localName).Select(e => (Path.GetFileName(f), e)));

    [Fact]
    public void Shared_styles_switch_drag_select_on_for_every_table_and_list()
    {
        var xaml = XDocument.Load(Repo.File("src", "KodiakCleaner.App", "Themes", "Controls.xaml"));
        string? Setter(string type, string property) => xaml.Root!.Elements(Ns + "Style")
            .Where(s => (string?)s.Attribute("TargetType") == type && s.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")) is null)
            .SelectMany(s => s.Elements(Ns + "Setter")).FirstOrDefault(s => (string?)s.Attribute("Property") == property)?.Attribute("Value")?.Value;

        Assert.Equal("Extended", Setter("DataGrid", "SelectionMode"));
        // Whole rows highlight, never a single cell (Ted, 0.8.92): under the mouse and when selected.
        Assert.Equal("FullRow", Setter("DataGrid", "SelectionUnit"));
        var row = xaml.Root!.Elements(Ns + "Style").Single(s => (string?)s.Attribute("TargetType") == "DataGridRow" &&
                                                                 s.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")) is null);
        var triggers = row.Descendants(Ns + "Trigger").Select(t => (string?)t.Attribute("Property")).ToList();
        Assert.Contains("IsMouseOver", triggers);
        Assert.Contains("IsSelected", triggers);
        Assert.Equal("True", Setter("DataGrid", "views:ListSelect.IsEnabled"));
        Assert.Equal("True", Setter("ItemsControl", "views:ListSelect.IsEnabled"));
    }

    [Fact]
    public void Every_table_allows_several_rows_unless_listed()
    {
        var problems = new List<string>();
        foreach (var (file, grid) in Elements("DataGrid"))
        {
            var mode = (string?)grid.Attribute("SelectionMode");
            if (mode is not null && mode != "Extended" && !SingleRowTables.ContainsKey(file))
                problems.Add($"{file}: a table set to SelectionMode=\"{mode}\" can't drag-select. Remove it or add the file to SingleRowTables with a reason.");
            if (grid.Attributes().Any(a => a.Name.LocalName == "ListSelect.IsEnabled"))
                problems.Add($"{file}: a table turns ListSelect off.");
            if ((string?)grid.Attribute("SelectionUnit") is { } unit && unit != "FullRow")
                problems.Add($"{file}: a table set to SelectionUnit=\"{unit}\" highlights cells, not whole rows.");
            if (grid.Attribute("Style") is { } style)
                problems.Add($"{file}: a table with its own Style ({style.Value}) loses the shared one; use BasedOn and keep ListSelect.");
        }
        foreach (var (file, list) in Elements("ItemsControl"))
            if (list.Attribute("Style") is not null || list.Attributes().Any(a => a.Name.LocalName == "ListSelect.IsEnabled"))
                problems.Add($"{file}: a list with its own Style or ListSelect setting loses drag-to-tick.");
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Right_click_menus_see_every_highlighted_row()
    {
        // A table with a right-click menu passes its highlighted rows to the page (SelectionChanged), so the menu acts on all of them.
        var problems = new List<string>();
        foreach (var (file, grid) in Elements("DataGrid"))
        {
            var hasMenu = grid.Attribute("ContextMenu") is not null || grid.Element(Ns + "DataGrid.ContextMenu") is not null;
            if (hasMenu && grid.Attribute("SelectionChanged") is null && !OneRowMenus.ContainsKey(file) && !SingleRowTables.ContainsKey(file))
                problems.Add($"{file}: its right-click menu only sees one row. Pass grid.SelectedItems to the page in SelectionChanged.");
            if (hasMenu && grid.Attribute("PreviewMouseRightButtonDown") is null)
                problems.Add($"{file}: right-click should select the row under the mouse first (GridRightClick).");
        }
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }
}
