namespace KodiakCleaner.Core.Util;

/// <summary>One command-line switch: how to write it, what it does, and a ready-to-run example.</summary>
public sealed record CliSwitch(string Name, IReadOnlyList<string> Usages, IReadOnlyList<string> Lines, string Example)
{
    /// <summary>The usage lines as one block, for the Settings page.</summary>
    public string UsageText => string.Join("\n", Usages);
    public string LinesText => string.Join(" ", Lines);
    /// <summary>Each usage line in words, so the Settings page wraps a long one only between words, never after a slash.</summary>
    public IReadOnlyList<IReadOnlyList<string>> UsageWords => Usages.Select(CommandLineHelp.Words).ToList();
    public IReadOnlyList<string> ExampleWords => CommandLineHelp.Words(Example);
}

/// <summary>One exit code and what it means.</summary>
public sealed record CliExitCode(int Code, string Meaning);

/// <summary>A page that --page= opens, by its key.</summary>
public sealed record CliPage(string Key, string Title)
{
    public string Argument => "--page=" + Key;
    /// <summary>The name without "Settings > ", for the Settings Pages list (its heading says it).</summary>
    public string ShortTitle => Title.StartsWith("Settings > ", StringComparison.Ordinal) ? Title["Settings > ".Length..] : Title;
}

/// <summary>
/// The one description of Kodiak Cleaner's command line (Ted, 0.9.1 Beta): <c>KodiakCleaner.exe /help</c>, Settings › Help ›
/// Command Line and the README's Command Line section all come from here, and a test checks that the README names every
/// switch, example, exit code and page key, and that the page keys match the app's own.
/// </summary>
public static class CommandLineHelp
{
    /// <summary>The switches that run without a window, A to Z.</summary>
    public static readonly IReadOnlyList<CliSwitch> Switches = new CliSwitch[]
    {
        new("clean", new[] { "KodiakCleaner.exe /clean [/items:ID,ID]" },
            new[]
            {
                "Cleans the items ticked on the Cleaner page (or just the listed items) with no window.",
                "Items with a caution never run from here, the same as scheduled cleanings.",
            },
            "KodiakCleaner.exe /clean /items:win.usertemp,win.recyclebin"),
        new("help", new[] { "KodiakCleaner.exe /help" },
            new[] { "Shows these switches. Started without a console (from Run or a shortcut), they show in a window instead." },
            "KodiakCleaner.exe /help"),
        new("list-items", new[] { "KodiakCleaner.exe /list-items" },
            new[] { "Lists every Cleaner item ID for /items:, marking the ones that are ticked." },
            "KodiakCleaner.exe /list-items"),
        new("list-programs", new[] { "KodiakCleaner.exe /list-programs" },
            new[] { "Lists installed programs with their version and product code, for /uninstall:." },
            "KodiakCleaner.exe /list-programs"),
        new("registry", new[] { "KodiakCleaner.exe /registry [/scan-only] [/categories:\"Fonts,Help Files\"]" },
            new[]
            {
                "Scans the registry (every check, or the ones listed) and fixes what it finds.",
                "Every fix is backed up to a .reg file first. /scan-only lists the issues and changes nothing.",
            },
            "KodiakCleaner.exe /registry /scan-only"),
        new("uninstall", new[] { "KodiakCleaner.exe /uninstall:\"Program Name\" [/quiet]", "KodiakCleaner.exe /uninstall:{PRODUCT-CODE-GUID} [/quiet]" },
            new[]
            {
                "Backs up the program's registry entries, then runs its uninstaller and waits for it.",
                "/quiet asks it not to show anything (msiexec /x {GUID} /qn, or the program's own quiet uninstall).",
                "Leftover files aren't removed from here; use Programs > Uninstall for that.",
                "Kodiak Cleaner never uninstalls itself; remove it from Control Panel.",
            },
            "KodiakCleaner.exe /uninstall:\"Program Name\" /quiet"),
    };

    /// <summary>Switches Kodiak Cleaner sets up in Windows by itself; they work typed by hand too.</summary>
    public static readonly IReadOnlyList<CliSwitch> SetUpByKodiakCleaner = new CliSwitch[]
    {
        new("autoclean", new[] { "KodiakCleaner.exe /autoclean" },
            new[] { "Cleans the ticked items with no window and no output: what Clean When Windows Starts and the jump list's Run Cleaner run." },
            "KodiakCleaner.exe /autoclean"),
        new("schedule", new[] { "KodiakCleaner.exe /schedule:ID" },
            new[] { "Runs one scheduled cleaning, as Windows Task Scheduler does at the time you picked. Make schedules on the Cleaner page with Schedule." },
            "KodiakCleaner.exe /schedule:ID"),
    };

    public static readonly IReadOnlyList<CliExitCode> ExitCodes = new CliExitCode[]
    {
        new(0, "Done."),
        new(1, "Done, with some failures (an item that couldn't be fully cleaned, a fix that didn't work)."),
        new(2, "A wrong switch, nothing found by that name, or Kodiak Cleaner itself."),
        new(3, "The uninstall didn't finish: the program is still installed."),
    };

    /// <summary>
    /// What --page= opens, A to Z by name: every tool and the bottom of the sidebar, then the Settings pages. A hidden tool
    /// opens too (Settings › Tools only takes it off the sidebar).
    /// </summary>
    public static readonly IReadOnlyList<CliPage> Pages = new CliPage[]
    {
        new("about", "About"),
        new("boottimer", "Boot Timer"),
        new("extensions", "Browser Extensions"),
        new("cleaner", "Cleaner"),
        new("optimize", "Defrag and TRIM"),
        new("disk", "Disk Analyzer"),
        new("wiper", "Drive Wiper"),
        new("drivers", "Driver Cleaner"),
        new("duplicates", "Duplicate Finder"),
        new("emptyfolders", "Empty Folders"),
        new("hash", "Hash Checker"),
        new("integrity", "Integrity Check"),
        new("largefiles", "Large Files"),
        new("rename", "Mass Rename"),
        new("programs", "Programs"),
        new("registry", "Registry"),
        new("tasks", "Scheduled Tasks"),
        new("settings", "Settings"),
        new("startup", "Startup"),
        new("sysinfo", "System Information"),
        new("restore", "System Restore"),
    };

    /// <summary>Settings pages --page= opens, A to Z by name.</summary>
    public static readonly IReadOnlyList<CliPage> SettingsPages = new CliPage[]
    {
        new("advanced", "Settings > Advanced"),
        new("appearance", "Settings > Appearance"),
        new("application", "Settings > Application"),
        new("cleaning", "Settings > Cleaning"),
        new("commandline", "Settings > Command Line"),
        new("feedback", "Settings > Contact & Feedback"),
        new("faq", "Settings > FAQ"),
        new("general", "Settings > General"),
        new("privacy", "Settings > Privacy Policy"),
        new("backups", "Settings > Registry"),
        new("supporter", "Settings > Supporter"),
        new("terms", "Settings > Terms of Use"),
        new("tools", "Settings > Tools"),
        new("updates", "Settings > Updates"),
        new("story", "Settings > Why It Was Made"),
    };

    public const string PageUsage = "KodiakCleaner.exe --page=KEY";
    public const string PageExample = "KodiakCleaner.exe --page=rename";
    public const string PageSummary =
        "Opens Kodiak Cleaner on a page, or brings the open window there. A tool hidden in Settings > Tools still opens.";

    /// <summary>Notes for scripts: administrator rights, waiting for the exit code, the activity log.</summary>
    public static readonly IReadOnlyList<string> Notes = new[]
    {
        "Run it as administrator to clean, fix or uninstall anything shared by all users. Nothing asks for the rights from here; without them, those items are left as they are.",
        "Kodiak Cleaner is a window program, so Command Prompt doesn't wait for it: start it with start /wait and then read %ERRORLEVEL%, or in PowerShell use Start-Process with -Wait -PassThru and read ExitCode.",
        "Switches start with / or -, and take their value after : or = (/items:ID or -items=ID).",
        "Hiding a tool in Settings > Tools only takes it off the sidebar: the command line, scheduled cleanings and Clean When Windows Starts keep working.",
    };

    /// <summary>A command split at its spaces (a quoted name stays one piece's worth of words, which is fine to wrap between).</summary>
    public static IReadOnlyList<string> Words(string command) => command.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>The text of <c>KodiakCleaner.exe /help</c>.</summary>
    public static string Text(string appName, string versionName, string logsFolder)
    {
        var lines = new List<string> { $"{appName} {versionName} command line", "" };
        foreach (var s in Switches)
        {
            lines.AddRange(s.Usages.Select(u => "  " + u));
            lines.AddRange(s.Lines.Select(l => "      " + l));
        }
        lines.Add("");
        lines.Add("Set up by Kodiak Cleaner itself:");
        foreach (var s in SetUpByKodiakCleaner)
        {
            lines.AddRange(s.Usages.Select(u => "  " + u));
            lines.AddRange(s.Lines.Select(l => "      " + l));
        }
        lines.Add("");
        lines.Add("  " + PageUsage);
        lines.Add("      " + PageSummary);
        lines.Add("      Pages: " + string.Join(", ", Pages.Select(p => p.Key)) + ".");
        lines.Add("      Settings pages: " + string.Join(", ", SettingsPages.Select(p => p.Key)) + ".");
        lines.Add("");
        lines.Add("Exit codes: " + string.Join("; ", ExitCodes.Select(c => $"{c.Code} {char.ToLowerInvariant(c.Meaning[0])}{c.Meaning.TrimEnd('.')[1..]}")) + ".");
        lines.AddRange(Notes);
        lines.Add($"Everything is also written to the activity log ({logsFolder}).");
        return string.Join(Environment.NewLine, lines);
    }
}
