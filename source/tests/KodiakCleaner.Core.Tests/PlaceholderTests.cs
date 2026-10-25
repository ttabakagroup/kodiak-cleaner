using System.Xml.Linq;

namespace KodiakCleaner.Core.Tests;

/// <summary>
/// Ted (2026-10-23): a table or list with no entries shows placeholder text saying what will show there, so an empty card never
/// looks barren or broken. Views/Placeholder.cs draws it; these tests make every table and list in the app set
/// <c>Placeholder.Text</c>, unless it is listed below with why it never sits empty on screen.
/// </summary>
public class PlaceholderTests
{
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>Lists that are never empty while shown (fixed choices, or hidden or explained when empty), keyed "File: source", with the reason.</summary>
    private static readonly Dictionary<string, string> NeverEmpty = new()
    {
        ["AboutPage.xaml: {Binding Credits}"] = "Fixed list of credits.",
        ["AboutPage.xaml: {Binding ShownReleases}"] = "The changelog, filled when See All Changes opens it.",
        ["BootTimerView.xaml: {Binding History}"] = "Its Recent Restarts card is hidden until there is a timed restart.",
        ["CleanerView.xaml: {Binding Sorts}"] = "Fixed Sort By choices.",
        ["DiskAnalyzerView.xaml: {Binding Trail}"] = "The folder path above the map; always holds at least the drive.",
        ["DocumentView.xaml: {Binding Blocks}"] = "A help document's paragraphs.",
        ["DriverCleanerView.xaml: {Binding DriverLinks}"] = "Fixed links to the driver makers.",
        ["DriverCleanerView.xaml: {Binding Adapters}"] = "Says \"No graphics driver from AMD, Intel or NVIDIA is installed\" when empty.",
        ["DriverCleanerView.xaml: {Binding Vendors}"] = "Fixed driver maker choices.",
        ["HashCheckerView.xaml: {Binding ShownKinds}"] = "Fixed hash type choices.",
        ["HashCheckerView.xaml: {Binding Facts}"] = "Details of one file, always filled.",
        ["HashCheckerView.xaml: {Binding Lines}"] = "Details of one file, always filled.",
        ["IntegrityCheckView.xaml: {Binding Events}"] = "Hidden while empty; the card's heading says what was found.",
        ["IntegrityCheckView.xaml: {Binding Disks}"] = "Hidden while empty; the card's heading says what was found.",
        ["LargeFilesView.xaml: {Binding MinSizes}"] = "Fixed size choices.",
        ["MassRenameView.xaml: {Binding Examples}"] = "Fixed examples.",
        ["RegistryView.xaml: {Binding Categories}"] = "Fixed Look For choices.",
        ["SettingsView.xaml: {Binding FavoriteOrders}"] = "Fixed choices.",
        ["SettingsView.xaml: {Binding Accents}"] = "Fixed accent colors.",
        ["SettingsView.xaml: {Binding Palettes}"] = "Fixed color schemes.",
        ["SettingsView.xaml: {Binding Themes}"] = "Fixed Light, Dark and Match Device choices.",
        ["SettingsView.xaml: {Binding Source={StaticResource GroupedSections}}"] = "Fixed Settings sections.",
        ["WiperView.xaml: {Binding HealthResults}"] = "Drive check verdicts in the progress card, hidden while empty.",
        ["WiperView.xaml: {Binding Modes}"] = "Fixed wipe types.",
        ["WiperView.xaml: {Binding LevelChoices}"] = "Fixed choices.",
        ["WiperView.xaml: {Binding MethodChoices}"] = "Fixed choices.",
        ["WiperView.xaml: {Binding StyleChoices}"] = "Fixed choices.",
        ["WiperView.xaml: {Binding FileSystemChoices}"] = "Fixed choices.",
        ["Controls.xaml: {Binding Sections}"] = "One changelog version's sections, always filled.",
        ["Controls.xaml: {Binding Items}"] = "One changelog section's lines, always filled.",
        ["DiskAnalyzerPickWindow.xaml: Places"] = "Your own folders (Documents, Pictures and so on), always there.",
        ["DuplicateRestoreWindow.xaml: Grid"] = "Has its own empty message (Empty).",
        ["MainWindow.xaml: {Binding FooterPages}"] = "The sidebar's pages.",
        ["MainWindow.xaml: {Binding FavoritePages}"] = "The sidebar's favorites; the group is hidden while there are none.",
        ["MainWindow.xaml: {Binding ToolPages}"] = "The sidebar's tools.",
        ["NewStartupWindow.xaml: List"] = "Only opens when there are new startup programs to show.",
        ["ProgramHistoryWindow.xaml: Grid"] = "Has its own empty message (Empty).",
        ["ScheduleEditWindow.xaml: Groups"] = "The Cleaner's items, always there.",
        ["ScheduleEditWindow.xaml: {Binding Items}"] = "The items of one Cleaner group, always there.",
        ["ScheduleWindow.xaml: Grid"] = "Has its own empty message (Empty).",
        ["ScheduledTaskRestoreWindow.xaml: Grid"] = "Has its own empty message (Empty).",
        ["WelcomeWindow.xaml: FeatureList"] = "Fixed list of features.",
        ["WhatsNewWindow.xaml: ReleaseList"] = "Only opens when there are changes to show.",
    };

    private static IEnumerable<(string Key, XElement Element)> Lists() =>
        Directory.EnumerateFiles(Repo.File("src", "KodiakCleaner.App"), "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .SelectMany(f => XDocument.Load(f).Descendants().Where(e => e.Name == Ns + "DataGrid" || e.Name == Ns + "ItemsControl")
                .Select(e => ($"{Path.GetFileName(f)}: {(string?)e.Attribute("ItemsSource") ?? (string?)e.Attribute(X + "Name") ?? "(unnamed)"}", e)));

    [Fact]
    public void Every_table_and_list_says_what_shows_there_while_empty()
    {
        var problems = Lists()
            .Where(l => !l.Element.Attributes().Any(a => a.Name.LocalName == "Placeholder.Text" && a.Value.Trim().Length > 0) && !NeverEmpty.ContainsKey(l.Key))
            .Select(l => $"{l.Key}: give it views:Placeholder.Text (what shows here and how to fill it), or add it to NeverEmpty with a reason.")
            .ToList();
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Never_empty_list_has_no_stale_entries()
    {
        var keys = Lists().Select(l => l.Key).ToHashSet();
        Assert.DoesNotContain(NeverEmpty.Keys, k => !keys.Contains(k));
    }

    [Fact]
    public void Placeholder_text_is_a_full_sentence()
    {
        foreach (var (key, list) in Lists())
            if (list.Attributes().FirstOrDefault(a => a.Name.LocalName == "Placeholder.Text") is { } text)
                Assert.True(text.Value.EndsWith('.') && char.IsUpper(text.Value[0]), $"{key}: \"{text.Value}\" should be a sentence.");
    }
}
