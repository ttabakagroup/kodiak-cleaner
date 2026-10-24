using System.Text.RegularExpressions;

namespace KodiakCleaner.Core.Tests;

/// <summary>
/// Ted (2026-10-24): ticking must never freeze a tool. A table's Select All box that re-reads every row on each tick
/// (<c>Rows.All(r =&gt; r.IsChecked)</c>) makes Select All on a long list re-read the list once per row, which froze Duplicate
/// Finder, Mass Rename and others. Every Select All box reads a running total from <c>ViewModels/CheckTally.cs</c> instead, and this
/// test holds every new tool to it. Only short lists that can't grow long may count directly, listed below with why.
/// </summary>
public class TickTotalsTests
{
    /// <summary>Select All boxes over short lists, keyed "Class.Property", with the reason they may count directly.</summary>
    private static readonly Dictionary<string, string> ShortLists = new()
    {
        ["CleanerViewModel.AllChecked"] = "The Cleaner's fixed items (about a hundred), and its Select All already ticks in bulk.",
        ["DriverCleanerViewModel.AllChecked"] = "One driver maker's packages, folders and keys: a few dozen at most.",
        ["IntegrityCheckViewModel.AllDrivesChecked"] = "The computer's drives.",
        ["OptimizeViewModel.AllChecked"] = "The computer's drives.",
        ["RegistryViewModel.AllCategoriesChecked"] = "The fixed Look For choices.",
        ["RestoreViewModel.AllChecked"] = "System Restore points: Windows keeps a few dozen at most.",
        ["ScheduleGroup.AllChecked"] = "One Cleaner group's items in a schedule: a few dozen at most.",
        ["StartupViewModel.AllChecked"] = "Startup entries: a few dozen.",
        ["WiperViewModel.AllChecked"] = "The computer's disks.",
    };

    private static readonly Regex SelectAll = new(@"public bool\? (All\w*Checked)\s*\{\s*get(?<get>.*?)\bset\b", RegexOptions.Singleline);

    private static IEnumerable<(string Key, string Getter)> SelectAllBoxes() =>
        Directory.EnumerateFiles(Repo.File("src", "KodiakCleaner.App"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .SelectMany(f =>
            {
                var text = File.ReadAllText(f);
                return SelectAll.Matches(text).Select(m =>
                {
                    var owner = Regex.Matches(text[..m.Index], @"class (\w+)").LastOrDefault()?.Groups[1].Value ?? Path.GetFileNameWithoutExtension(f);
                    return ($"{owner}.{m.Groups[1].Value}", m.Groups["get"].Value);
                });
            });

    [Fact]
    public void Select_All_boxes_read_a_running_total()
    {
        var counting = SelectAllBoxes().Where(b => !b.Getter.Contains("Tally.State") && !ShortLists.ContainsKey(b.Key)).Select(b => b.Key).ToList();
        Assert.True(counting.Count == 0,
            "These Select All boxes re-read every row on each tick, which freezes a long list. Use a CheckTally (ViewModels/CheckTally.cs) " +
            "and tick in bulk with Tally.Bulk, or list a short list in TickTotalsTests.ShortLists with why: " + string.Join(", ", counting));
    }

    [Fact]
    public void Short_list_entries_are_still_there()
    {
        var found = SelectAllBoxes().Select(b => b.Key).ToHashSet();
        Assert.DoesNotContain(ShortLists.Keys, k => !found.Contains(k));
    }

    [Fact]
    public void Scan_finds_the_select_all_boxes()
    {
        var boxes = SelectAllBoxes().ToList();
        Assert.Contains(boxes, b => b.Key == "MassRenameViewModel.AllChecked" && b.Getter.Contains("Tally.State"));
        Assert.True(boxes.Count >= 10);
    }
}
