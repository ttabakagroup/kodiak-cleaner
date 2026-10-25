using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using KodiakCleaner.Core.Cleaning;
using KodiakCleaner.Core.Programs;
using KodiakCleaner.Core.RegistryTools;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App;

/// <summary>
/// Command-line switches for scripts and system administrators (Ted, 0.8.53): run the Cleaner, scan or fix the registry,
/// and uninstall a program, with no window. Output goes to the console it was started from, everything is written to the
/// activity log, and the exit code says how it went (see <see cref="Help"/>).
/// </summary>
public static class Cli
{
    public const int Ok = 0, Partly = 1, BadArgs = 2, NotFinished = 3;

    private static readonly string[] Commands = { "help", "?", "clean", "list-items", "registry", "uninstall", "list-programs" };

    /// <summary>True when the arguments ask for a command-line run.</summary>
    public static bool IsCommand(string[] args) => args.Any(a => Commands.Contains(Switch(a).Name));

    /// <summary>"/items:a,b" or "-items=a,b" gives ("items", "a,b").</summary>
    private static (string Name, string Value) Switch(string arg)
    {
        if (arg.Length < 2 || (arg[0] != '/' && arg[0] != '-')) return ("", "");
        var body = arg.TrimStart('/', '-');
        var cut = body.IndexOfAny(new[] { ':', '=' });
        return cut < 0 ? (body.ToLowerInvariant(), "") : (body[..cut].ToLowerInvariant(), body[(cut + 1)..].Trim('"'));
    }

    /// <summary>The /help text. It comes from <see cref="CommandLineHelp"/>, which Settings › Help › Command Line and the README
    /// follow too (a test keeps them in step).</summary>
    public static string Help => CommandLineHelp.Text(AppInfo.Name, AppInfo.VersionName, Settings.Data.Logs);

    public static int Run(string[] args)
    {
        var console = Output.Attach();
        var switches = args.Select(Switch).Where(s => s.Name.Length > 0).ToList();
        string? Value(string name) => switches.FirstOrDefault(s => s.Name == name) is { Name.Length: > 0 } s ? s.Value : null;
        bool Has(string name) => switches.Any(s => s.Name == name);
        var quiet = Has("quiet");
        try
        {
            if (Has("help") || Has("?"))
            {
                if (console) Output.Line(Help);
                else if (!quiet) { ThemeManager.Apply(); Dialogs.Info(Help, $"{AppInfo.Name} Command Line"); }
                return Ok;
            }
            if (Has("list-items")) return ListItems();
            if (Has("list-programs")) return ListPrograms();
            if (Has("clean")) return Clean(Value("items"));
            if (Has("registry")) return Registry(Has("scan-only"), Value("categories"));
            if (Value("uninstall") is { } program) return Uninstall(program, quiet);
            return Fail(BadArgs, "Nothing to do. Run KodiakCleaner.exe /help for the switches.");
        }
        catch (Exception e)
        {
            ActivityLog.Error("Command Line", e);
            return Fail(Partly, "Error: " + e.Message);
        }
        finally { Output.Done(); }
    }

    private static int Fail(int code, string message)
    {
        Output.Line(message);
        ActivityLog.Write("Command Line", message);
        return code;
    }

    private static int ListItems()
    {
        var ticked = Settings.Current.CheckedTargets?.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var t in TargetRun.Catalog().OrderBy(t => t.Group).ThenBy(t => t.Name))
        {
            var on = ticked?.Contains(t.Id) ?? t.EnabledByDefault;
            Output.Line($"{(on ? "[x]" : "[ ]")} {t.Id,-34} {t.Group}: {t.Name}{(t.Warning.Length > 0 ? " (caution; not run from here)" : "")}");
        }
        return Ok;
    }

    private static int ListPrograms()
    {
        foreach (var p in ProgramInventory.List())
            Output.Line($"{p.Name}\t{p.Version}\t{(p.IsProductCode ? p.KeyName.ToUpperInvariant() : "")}");
        return Ok;
    }

    private static int Clean(string? items)
    {
        var catalog = TargetRun.Catalog();
        IEnumerable<CleanTarget> chosen;
        if (items is not null)
        {
            var wanted = items.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (wanted.Count == 0) return Fail(BadArgs, "Name at least one item: /items:ID,ID. Run /list-items for the IDs.");
            var unknown = wanted.Where(w => !catalog.Any(t => t.Id.Equals(w, StringComparison.OrdinalIgnoreCase))).ToList();
            if (unknown.Count > 0) return Fail(BadArgs, $"Unknown item(s): {string.Join(", ", unknown)}. Run /list-items for the IDs.");
            chosen = catalog.Where(t => wanted.Contains(t.Id));
        }
        else
        {
            var ticked = Settings.Current.CheckedTargets?.ToHashSet(StringComparer.OrdinalIgnoreCase);
            chosen = catalog.Where(t => ticked?.Contains(t.Id) ?? t.EnabledByDefault);
        }

        long freed = 0;
        int cleaned = 0, failed = 0;
        foreach (var t in chosen.ToList())
        {
            if (t.Warning.Length > 0) { Output.Line($"Skipped {t.Group}: {t.Name} (has a caution; clean it from the Cleaner page)"); continue; }
            try
            {
                var run = new TargetRun(t);
                var (f, notRemoved, status) = run.Clean(CancellationToken.None);
                Output.Line($"{t.Group}: {t.Name}: {status}{(f > 0 ? $", {ByteSize.Format(f)} freed" : "")}");
                if (!run.Ran) continue;
                Settings.Current.History.Record(t.Id, DateTime.UtcNow, f);
                freed += f;
                cleaned++;
                if (notRemoved > 0) failed++;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                failed++;
                Output.Line($"{t.Group}: {t.Name}: {e.Message}");
            }
        }
        Settings.Current.Save();
        var line = $"Cleaned {TargetRun.Plural(cleaned, "item")} from the command line, {ByteSize.Format(freed)} freed.";
        Output.Line(line);
        ActivityLog.Write("Cleaner", line);
        return failed > 0 ? Partly : Ok;
    }

    private static int Registry(bool scanOnly, string? categories)
    {
        var all = RegistryScanner.Categories;
        HashSet<string> cats;
        if (categories is null) cats = all.ToHashSet();
        else
        {
            var asked = categories.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var unknown = asked.Where(a => !all.Contains(a, StringComparer.OrdinalIgnoreCase)).ToList();
            if (unknown.Count > 0) return Fail(BadArgs, $"Unknown check(s): {string.Join(", ", unknown)}. The checks are: {string.Join(", ", all)}.");
            cats = all.Where(c => asked.Contains(c, StringComparer.OrdinalIgnoreCase)).ToHashSet();
        }

        var found = RegistryScanner.Scan(cats);
        foreach (var i in found) Output.Line($"{i.Category}: {i.Description}  [{i.Location.DisplayKeyPath}]");
        if (found.Count == 0 || scanOnly)
        {
            var summary = $"Registry scan from the command line found {TargetRun.Plural(found.Count, "issue")}{(scanOnly && found.Count > 0 ? "; nothing was changed (/scan-only)" : "")}.";
            Output.Line(summary);
            ActivityLog.Write("Registry", summary);
            return Ok;
        }

        var (backup, fixedCount, errors) = RegistryScanner.Fix(found, backup: true);
        foreach (var e in errors) Output.Line("Couldn't fix: " + e);
        var line = $"Fixed {TargetRun.Plural(fixedCount, "registry issue")} from the command line." + (backup is null ? "" : $" Backup: {backup}");
        Output.Line(line);
        ActivityLog.Write("Registry", line);
        return errors.Count > 0 ? Partly : Ok;
    }

    private static int Uninstall(string which, bool quiet)
    {
        if (which.Length == 0) return Fail(BadArgs, "Name the program: /uninstall:\"Program Name\" or /uninstall:{PRODUCT-CODE}.");
        var installed = ProgramInventory.List();
        var matches = installed.Where(p => p.KeyName.Equals(which, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0) matches = installed.Where(p => p.Name.Equals(which, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0) return Fail(BadArgs, $"No installed program is called '{which}'. Run /list-programs for the names and product codes.");
        if (matches.Count > 1)
            return Fail(BadArgs, $"{matches.Count} programs are called '{which}'. Use the product code instead:\n" +
                                 string.Join("\n", matches.Select(m => $"  {m.Name} {m.Version}  {m.KeyName}")));
        var program = matches[0];
        if (SelfGuard.IsKodiakCleaner(program)) return Fail(BadArgs, SelfGuard.Explanation);
        if (!program.CanUninstall) return Fail(BadArgs, $"'{program.Name}' has no uninstaller.");
        if (quiet && !ProgramInventory.CanUninstallQuietly(program))
            return Fail(BadArgs, $"'{program.Name}' doesn't offer a quiet uninstall. Run it without /quiet.");

        Output.Line($"Backing up the registry entries of '{program.Name}'...");
        var keys = UninstallFlow.Scan(program, UninstallFlow.Places(program, installed)).Where(l => l.Key is not null).Select(l => l.Key!);
        var backup = ProgramInventory.BackupBeforeUninstall(program, keys);

        var started = DateTime.Now;
        using var proc = quiet ? ProgramInventory.StartQuietUninstall(program) : ProgramInventory.StartUninstall(program);
        if (proc is null) return Fail(BadArgs, $"'{program.Name}' has no uninstaller.");
        Output.Line($"Waiting for the '{program.Name}' uninstaller...");
        proc.WaitForExit();
        var exit = TryExitCode(proc);

        // Some uninstallers hand over to a helper and exit at once; wait while one is still running (at most 30 minutes).
        var deadline = DateTime.Now.AddMinutes(30);
        int idle = 0;
        while (ProgramInventory.IsStillInstalled(program) && DateTime.Now < deadline && idle < 5)
        {
            idle = UninstallFlow.UninstallHelperRunning(started) ? 0 : idle + 1;
            Thread.Sleep(1000);
        }
        if (ProgramInventory.IsStillInstalled(program))
            return Fail(NotFinished, $"'{program.Name}' is still installed (the uninstaller exited with code {exit?.ToString() ?? "unknown"}). Backup: {backup}");

        ProgramHistoryStore.Add(ProgramChange.Uninstalled, program, "Removed by its own uninstaller (command line)", backup);
        var line = $"Uninstalled '{program.Name}' from the command line. Backup: {backup}";
        Output.Line(line);
        ActivityLog.Write("Programs", line);
        return Ok;
    }

    private static int? TryExitCode(Process p)
    {
        try { return p.ExitCode; }
        catch (InvalidOperationException) { return null; }
    }

    /// <summary>Writes to the console Kodiak Cleaner was started from (it is a window program, so it has to attach to it).</summary>
    private static class Output
    {
        [DllImport("kernel32.dll")] private static extern bool AttachConsole(int processId);
        [DllImport("kernel32.dll")] private static extern IntPtr GetStdHandle(int handle);
        private static bool _on;

        public static bool Attach()
        {
            try
            {
                AttachConsole(-1); // the parent's console; fails harmlessly when output is redirected or there is none
                var h = GetStdHandle(-11);
                _on = h != IntPtr.Zero && h != new IntPtr(-1);
                if (_on) Console.WriteLine();
            }
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or IOException) { _on = false; }
            return _on;
        }

        public static void Line(string text)
        {
            if (!_on) return;
            try { Console.WriteLine(text); }
            catch (IOException) { _on = false; }
        }

        public static void Done()
        {
            if (!_on) return;
            try { Console.Out.Flush(); }
            catch (IOException) { }
        }
    }
}
