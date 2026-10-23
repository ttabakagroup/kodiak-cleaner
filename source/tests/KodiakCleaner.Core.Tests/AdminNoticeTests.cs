using System.Text.RegularExpressions;

namespace KodiakCleaner.Core.Tests;

/// <summary>
/// Ted (2026-10-23): a tool or page that needs administrator rights prompts for the restart. Running as a standard user, the
/// open page's entry in AdminNeeds.cs shows at the top with Restart as Administrator, and a step that can't run offers the
/// restart through Elevation.OfferRestart. These tests catch a new page or message that skips either.
/// </summary>
public class AdminNoticeTests
{
    /// <summary>Pages that check for administrator rights without a notice, with the reason.</summary>
    private static readonly Dictionary<string, string> NoNotice = new()
    {
        ["SettingsViewModel"] = "Only Skip the Windows Permission Prompt needs the rights, and that switch offers the restart itself.",
    };

    private static string App(params string[] path) => File.ReadAllText(Repo.File(["src", "KodiakCleaner.App", .. path]));

    private static List<string> PagesWithNotice() =>
        Regex.Matches(App("AdminNeeds.cs"), @"\[typeof\((\w+)\)\]").Select(m => m.Groups[1].Value).ToList();

    /// <summary>
    /// Every page's view model (a property of MainViewModel) with its own source text. Rows and tabs inside a page (a startup
    /// entry, Point-in-Time Restore) are covered by their page's notice.
    /// </summary>
    private static IEnumerable<(string Name, string Body)> Pages()
    {
        var main = App("ViewModels", "MainViewModel.cs");
        foreach (var file in Directory.EnumerateFiles(Repo.File("src", "KodiakCleaner.App", "ViewModels"), "*.cs"))
        {
            var text = File.ReadAllText(file);
            var starts = Regex.Matches(text, @"^(?:public |internal )?(?:sealed |abstract |static )*(?:partial )?class (\w+)", RegexOptions.Multiline);
            for (int i = 0; i < starts.Count; i++)
            {
                var name = starts[i].Groups[1].Value;
                if (!main.Contains($"public {name} ")) continue;
                var end = i + 1 < starts.Count ? starts[i + 1].Index : text.Length;
                yield return (name, text[starts[i].Index..end]);
            }
        }
    }

    /// <summary>Each whole Dialogs.Error(...) or Dialogs.Info(...) call in the text, brackets inside strings ignored.</summary>
    private static IEnumerable<string> MessageCalls(string text)
    {
        foreach (Match m in Regex.Matches(text, @"Dialogs\.(?:Error|Info)\("))
        {
            int depth = 0, i = m.Index + m.Length - 1;
            bool inString = false;
            for (; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '"' && text[i - 1] != '\\') inString = !inString;
                else if (!inString && c == '(') depth++;
                else if (!inString && c == ')' && --depth == 0) break;
            }
            yield return text[m.Index..Math.Min(i + 1, text.Length)];
        }
    }

    [Fact]
    public void Every_page_that_needs_administrator_rights_has_a_notice()
    {
        var listed = PagesWithNotice();
        var missing = Pages()
            .Where(c => !NoNotice.ContainsKey(c.Name) && !listed.Contains(c.Name))
            .Where(c => c.Body.Contains("IsAdministrator") || c.Body.Contains("administrator rights", StringComparison.OrdinalIgnoreCase) ||
                        c.Body.Contains("Elevation.OfferRestart"))
            .Select(c => c.Name).ToList();
        Assert.True(missing.Count == 0,
            "These pages check for administrator rights but have no entry in AdminNeeds.cs (add one, so the page shows " +
            "Restart as Administrator): " + string.Join(", ", missing));
    }

    [Fact]
    public void Notices_are_for_real_pages_and_listed_A_to_Z()
    {
        var listed = PagesWithNotice();
        Assert.NotEmpty(listed);
        Assert.Equal(listed.Order(StringComparer.Ordinal), listed);
        var main = App("ViewModels", "MainViewModel.cs");
        foreach (var page in listed)
            Assert.True(main.Contains($"public {page} "), $"{page} in AdminNeeds.cs isn't a page in MainViewModel.");
    }

    [Fact]
    public void Administrator_messages_offer_the_restart()
    {
        var problems = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Repo.File("src", "KodiakCleaner.App"), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)) continue;
            var text = File.ReadAllText(file);
            foreach (var call in MessageCalls(text))
                if (Regex.IsMatch(call, @"Restart as Administrator|run(?:s)? as administrator", RegexOptions.IgnoreCase))
                    problems.Add($"{Path.GetFileName(file)}: {call.Split('\n')[0].Trim()}");
        }
        Assert.True(problems.Count == 0,
            "Use Elevation.OfferRestart (it asks to restart as administrator right away), not a plain message:\n" + string.Join("\n", problems));
    }

    [Fact]
    public void Only_the_shared_notice_has_a_restart_button()
    {
        var owners = Directory.EnumerateFiles(Repo.File("src", "KodiakCleaner.App"), "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .Where(f => File.ReadAllText(f).Contains("RestartAsAdminCommand"))
            .Select(Path.GetFileName).ToList();
        Assert.Equal(["MainWindow.xaml"], owners);
        var main = App("MainWindow.xaml");
        Assert.Contains("HasPageAdminNeed", main);
        Assert.DoesNotContain("IsNotAdministrator", main);
    }
}
