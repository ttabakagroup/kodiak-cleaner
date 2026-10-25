using System.Text.RegularExpressions;
using System.Xml.Linq;
using KodiakCleaner.Core.Util;
using Xunit;

namespace KodiakCleaner.Core.Tests;

/// <summary>Settings › Tools (Ted, 0.9.1 Beta): any tool can be hidden from the sidebar and comes back where it was.</summary>
public sealed class HiddenToolsTests
{
    private static readonly string[] Known = { "cleaner", "registry", "startup", "programs", "hash", "disk", "wiper", "rename" };

    [Fact]
    public void Saved_hidden_tools_drop_unknown_and_repeated_keys()
    {
        Assert.Equal(new[] { "hash", "wiper" }, SidebarFavorites.NormalizeHidden(new[] { " Hash", "gone", "wiper", "HASH", "" }, Known).OrderBy(k => k));
        Assert.Empty(SidebarFavorites.NormalizeHidden(null, Known));
    }

    [Fact]
    public void Hidden_tools_leave_the_sidebar_but_keep_their_place()
    {
        var titles = new Dictionary<string, string>
        {
            ["cleaner"] = "Cleaner", ["registry"] = "Registry", ["hash"] = "Hash Checker", ["disk"] = "Disk Analyzer",
            ["wiper"] = "Drive Wiper", ["rename"] = "Mass Rename",
        };
        var favorites = new List<string> { "cleaner", "registry" };
        var hidden = new HashSet<string> { "registry", "wiper" };
        var tools = SidebarFavorites.ArrangeTools(titles.Keys, favorites, null, k => titles[k]);
        Assert.Equal(new[] { "disk", "wiper", "hash", "rename" }, tools);
        Assert.Equal(new[] { "disk", "hash", "rename" }, tools.Where(k => !hidden.Contains(k)));
        Assert.Equal(new[] { "cleaner" }, favorites.Where(k => !hidden.Contains(k)));

        // Mass Rename is moved to the top of the tools shown: Drive Wiper stays right after Disk Analyzer.
        var shown = tools.Where(k => !hidden.Contains(k)).ToList();
        Assert.True(SidebarFavorites.Move(shown, "rename", 0));
        var saved = SidebarFavorites.MergeHidden(shown, tools, hidden);
        Assert.Equal(new[] { "rename", "disk", "wiper", "hash" }, saved);
        // Turned back on, it shows there again.
        Assert.Equal(saved, SidebarFavorites.ArrangeTools(titles.Keys, favorites, saved, k => titles[k]));
    }

    [Fact]
    public void A_hidden_favorite_comes_back_in_Favorites_in_its_place()
    {
        var favorites = new List<string> { "cleaner", "registry", "startup", "programs" };
        var hidden = new HashSet<string> { "registry" };
        var shown = favorites.Where(k => !hidden.Contains(k)).ToList();
        Assert.True(SidebarFavorites.Pin(shown, "hash", 1)); // dropped between Cleaner and Startup as shown
        Assert.Equal(new[] { "cleaner", "registry", "hash", "startup", "programs" }, SidebarFavorites.MergeHidden(shown, favorites, hidden));
        // A hidden first entry stays first; hidden entries that weren't in the list aren't added.
        Assert.Equal(new[] { "cleaner", "startup" }, SidebarFavorites.MergeHidden(new[] { "startup" }, new[] { "cleaner", "startup" }, new HashSet<string> { "cleaner", "wiper" }));
        Assert.Empty(SidebarFavorites.MergeHidden(Array.Empty<string>(), Array.Empty<string>(), hidden));
    }

    [Fact]
    public void Hidden_tools_are_saved_reset_and_followed_everywhere()
    {
        var settings = Repo.Read("src", "KodiakCleaner.App", "Settings.cs");
        Assert.Contains("ini.Get(\"Layout\", \"HiddenTools\")", settings);
        Assert.Contains("ini.Set(\"Layout\", \"HiddenTools\"", settings);
        Assert.Contains("public List<string> HiddenTools { get; set; } = new();", settings);
        // Reset to Default starts from a new Settings and doesn't carry the hidden tools over.
        var reset = Regex.Match(settings, @"public static void Reset\(\)(.*?)Current\.Save\(\);", RegexOptions.Singleline).Groups[1].Value;
        Assert.DoesNotContain("HiddenTools", reset);

        var main = Repo.Read("src", "KodiakCleaner.App", "ViewModels", "MainViewModel.cs");
        Assert.Contains("SettingsPage.SetTools(Pinnable.Select(", main);   // Settings › Tools lists every pinnable page
        Assert.Contains("Where(k => !hidden.Contains(k))", main);          // left out of Favorites and Tools
        Assert.Contains("if (IsHidden(page)) ShowHiddenNotice(page);", main); // opening one anyway says where it went
        Assert.Contains("Navigate(\"tools\")", main);                         // every tool hidden: Home opens Settings › Tools
        Assert.Contains("Settings.Current.HiddenTools.Contains(key", Repo.Read("src", "KodiakCleaner.App", "Integration.cs")); // jump list
        Assert.Contains("ShowNoToolsHint", Repo.Read("src", "KodiakCleaner.App", "MainWindow.xaml"));
        Assert.Contains("\"Hide from Sidebar\"", Repo.Read("src", "KodiakCleaner.App", "MainWindow.xaml.cs"));
        var view = Repo.Read("src", "KodiakCleaner.App", "Views", "SettingsView.xaml");
        Assert.Contains("<DataTemplate x:Key=\"toolsPage\">", view);
        Assert.Contains("IsChecked=\"{Binding IsShown}\"", view);
        Assert.Contains("ShowAllToolsCommand", view);
    }

    /// <summary>
    /// The scrolling area clips what is drawn outside it, focus rings included, so a ScrollViewer's Padding on the left or top cut
    /// off the outline of whatever sat at that edge (Mass Rename's Examples, Ted 0.9.1 Beta). The space goes on the content instead;
    /// right Padding (room for the scroll bar) is fine.
    /// </summary>
    [Fact]
    public void Scroll_areas_never_clip_focus_rings_at_their_edges()
    {
        var problems = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Repo.File("src", "KodiakCleaner.App"), "*.xaml", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
            foreach (var viewer in XDocument.Load(file).Descendants().Where(e => e.Name.LocalName == "ScrollViewer"))
                if ((string?)viewer.Attribute("Padding") is { } padding)
                {
                    var parts = padding.Split(',').Select(p => double.Parse(p, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                    var (left, top) = parts.Length switch { 1 => (parts[0], parts[0]), 2 => (parts[0], parts[1]), _ => (parts[0], parts[1]) };
                    if (left != 0 || top != 0) problems.Add($"{Path.GetFileName(file)}: ScrollViewer Padding=\"{padding}\" clips focus rings; put the space on its content's Margin.");
                }
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }
}

/// <summary>
/// The command line (Ted, 0.9.1 Beta): /help, Settings › Help › Command Line and the README all describe the same switches, exit
/// codes and page keys, from <see cref="CommandLineHelp"/>.
/// </summary>
public sealed class CommandLineHelpTests
{
    private static string Readme => Repo.Read("README.md");

    [Fact]
    public void Lists_are_A_to_Z()
    {
        var names = CommandLineHelp.Switches.Select(s => s.Name).ToList();
        Assert.Equal(names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase), names);
        foreach (var pages in new[] { CommandLineHelp.Pages, CommandLineHelp.SettingsPages })
        {
            var titles = pages.Select(p => p.Title).ToList();
            Assert.Equal(titles.OrderBy(t => t, StringComparer.OrdinalIgnoreCase), titles);
        }
    }

    [Fact]
    public void Every_switch_the_app_handles_is_described_and_nothing_else()
    {
        var cli = Repo.Read("src", "KodiakCleaner.App", "CommandLine.cs");
        var handled = Regex.Match(cli, @"Commands = \{([^}]*)\}").Groups[1].Value
            .Split(',').Select(c => c.Trim().Trim('"')).Where(c => c.Length > 0 && c != "?").OrderBy(c => c).ToList();
        Assert.Equal(handled, CommandLineHelp.Switches.Select(s => s.Name).OrderBy(c => c));
        Assert.Contains("CommandLineHelp.Text(", cli); // /help prints the shared text
        // Exit codes match the ones Cli returns.
        var codes = Regex.Match(cli, @"public const int (.*?);").Groups[1].Value.Split(',')
            .Select(c => int.Parse(c.Split('=')[1].Trim())).ToList();
        Assert.Equal(codes, CommandLineHelp.ExitCodes.Select(c => c.Code));
        // The switches Kodiak Cleaner sets up itself are the ones the app reads.
        Assert.Contains("AutoCleanArg = \"/autoclean\"", Repo.Read("src", "KodiakCleaner.App", "Integration.cs"));
        Assert.Contains("Arg = \"/schedule:\"", Repo.Read("src", "KodiakCleaner.App", "ScheduledCleaning.cs"));
        Assert.Equal(new[] { "autoclean", "schedule" }, CommandLineHelp.SetUpByKodiakCleaner.Select(s => s.Name));
        foreach (var s in CommandLineHelp.Switches.Concat(CommandLineHelp.SetUpByKodiakCleaner))
        {
            Assert.All(s.Usages, u => Assert.StartsWith("KodiakCleaner.exe /" + s.Name, u));
            Assert.StartsWith("KodiakCleaner.exe /" + s.Name, s.Example);
        }
    }

    [Fact]
    public void Page_keys_are_the_ones_the_app_opens()
    {
        var main = Repo.Read("src", "KodiakCleaner.App", "ViewModels", "MainViewModel.cs");
        var pageKeys = Regex.Matches(main, @"\[""(\w+)""\] = ").Select(m => m.Groups[1].Value).OrderBy(k => k);
        Assert.Equal(pageKeys, CommandLineHelp.Pages.Select(p => p.Key).OrderBy(k => k));
        var settings = Repo.Read("src", "KodiakCleaner.App", "ViewModels", "SettingsViewModel.cs");
        var sections = Regex.Matches(settings, @"Add\(""(\w+)"", ""([^""]+)""").Select(m => (Key: m.Groups[1].Value, Title: m.Groups[2].Value)).ToList();
        Assert.Equal(sections.Select(s => s.Key).OrderBy(k => k), CommandLineHelp.SettingsPages.Select(p => p.Key).OrderBy(k => k));
        foreach (var page in CommandLineHelp.SettingsPages)
            Assert.Equal("Settings > " + sections.First(s => s.Key == page.Key).Title, page.Title);
        Assert.Contains(sections, s => s.Key == "commandline" && s.Title == "Command Line");
        Assert.Matches(@"Add\(""commandline"", ""Command Line"", ""[^""]*"", ""Help""", settings);
        Assert.Matches(@"Add\(""tools"", ""Tools"", ""[^""]*"", """"", settings);
    }

    [Fact]
    public void Help_text_has_every_switch_exit_code_and_page()
    {
        var text = CommandLineHelp.Text("Kodiak Cleaner", "0.9.1 Beta", @"C:\Logs");
        Assert.StartsWith("Kodiak Cleaner 0.9.1 Beta command line", text);
        foreach (var s in CommandLineHelp.Switches.Concat(CommandLineHelp.SetUpByKodiakCleaner))
        {
            Assert.All(s.Usages, u => Assert.Contains(u, text));
            Assert.All(s.Lines, l => Assert.Contains(l, text));
        }
        foreach (var c in CommandLineHelp.ExitCodes) Assert.Contains($"{c.Code} ", text);
        Assert.Contains("1 done, with some failures", text);
        Assert.Contains("or Kodiak Cleaner itself", text); // the app's name keeps its capitals
        foreach (var p in CommandLineHelp.Pages.Concat(CommandLineHelp.SettingsPages)) Assert.Contains(p.Key, text);
        Assert.All(CommandLineHelp.Notes, n => Assert.Contains(n, text));
        Assert.Contains(@"C:\Logs", text);
    }

    [Fact]
    public void Readme_documents_every_switch_example_exit_code_and_page_key()
    {
        var readme = Readme;
        var section = readme[readme.IndexOf("### Command Line", StringComparison.Ordinal)..readme.IndexOf("## Features", StringComparison.Ordinal)];
        foreach (var s in CommandLineHelp.Switches.Concat(CommandLineHelp.SetUpByKodiakCleaner))
        {
            Assert.All(s.Usages, u => Assert.Contains($"`{u}`", section));
            Assert.Contains(s.Example, section);
        }
        foreach (var c in CommandLineHelp.ExitCodes) Assert.Contains($"| `{c.Code}` | {c.Meaning} |", section);
        Assert.Contains($"`{CommandLineHelp.PageUsage}`", section);
        Assert.Contains(CommandLineHelp.PageExample, section);
        foreach (var p in CommandLineHelp.Pages.Concat(CommandLineHelp.SettingsPages)) Assert.Contains($"| `{p.Key}` | {p.Title} |", section);
        Assert.Contains("start /wait KodiakCleaner.exe /clean", section);
        Assert.Contains("Start-Process KodiakCleaner.exe -ArgumentList '/clean' -Wait -PassThru", section);
        Assert.Contains("Settings > Help > Command Line", section);
        Assert.Contains("[Command Line](#command-line)", readme);
    }

    [Fact]
    public void Faq_explains_the_command_line_and_hiding_tools()
    {
        var faq = Repo.Read("faq.md");
        Assert.Contains("## Can I Run Kodiak Cleaner From the Command Line?", faq);
        Assert.Contains("## How Do I Hide a Tool I Don't Use?", faq);
        foreach (var s in new[] { "/clean", "/registry", "/uninstall:", "/help", "--page=", "Settings, Command Line", "Settings, Tools" })
            Assert.Contains(s, faq);
    }
}
