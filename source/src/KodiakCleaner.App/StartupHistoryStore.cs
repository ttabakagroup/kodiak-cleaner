using System.IO;
using KodiakCleaner.Core.Startup;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App;

/// <summary>Kodiak Cleaner's record of startup entries, kept in Backups\Startup History beside it.</summary>
public static class StartupHistoryStore
{
    private const long LargestCopy = 10L << 20; // shortcuts and scripts; a big program in the Startup folder isn't copied

    public static string Folder => Settings.Data.StartupHistory;

    private static StartupHistory? _history;
    public static StartupHistory Current => _history ??= StartupHistory.Load(Folder);

    /// <summary>
    /// Notes what is in startup now (called whenever the Startup list is read). An entry Kodiak Cleaner removed that is back
    /// gets a note of when it came back and what most likely put it back.
    /// </summary>
    public static void Record(IReadOnlyList<StartupItem> items) => Try(() => ExplainReturns(Current.Observe(items, DateTime.Now, CopyShortcut)));

    /// <summary>The history's record of an entry in startup now, or null.</summary>
    public static StartupRecord? Find(StartupItem item) => Current.Find(item);

    private static void ExplainReturns(List<(StartupItem Item, StartupRecord Record)> returned)
    {
        if (returned.Count == 0) return;
        List<RunningProgram> running;
        try
        {
            running = BootProbe.Processes()
                .Select(p => new RunningProgram(Path.GetFileName(p.Path), p.Path.Length > 0 ? p.Path : null, p.StartUtc.ToLocalTime()))
                .Where(p => p.Name.Length > 0)
                .ToList();
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException) { running = new(); }

        foreach (var (item, record) in returned)
        {
            if (StartupManager.WrittenAt(item) is { } written && written <= DateTime.Now) record.LastReturned = written;
            var program = ProgramOf(item);
            record.ReturnedBy = StartupComeback.Explain(program, record.LastReturned, running);
            ActivityLog.Write("Startup", $"'{item.Name}' came back after it was removed ({record.TimesReturned}x). {record.ReturnedBy}");
        }
    }

    /// <summary>Notes that Kodiak Cleaner removed an entry, and the .reg backup it made first.</summary>
    public static void Removed(StartupItem item, string? registryBackup) => Try(() => Current.MarkRemoved(item, registryBackup, DateTime.Now));

    /// <summary>Drops records from the history, with their saved shortcut copies.</summary>
    public static void Forget(IEnumerable<StartupRecord> records) => Try(() =>
    {
        foreach (var r in records.ToList())
        {
            Current.Records.Remove(r);
            if (r.ShortcutCopy is { } copy && !Current.Records.Any(x => x.ShortcutCopy == copy))
                File.Delete(Path.Combine(Folder, copy));
        }
    });

    public static void Save() => Try(() => { });

    private static void Try(Action change)
    {
        try
        {
            change();
            Current.Save(Folder);
        }
        // The history is a convenience: if it can't be saved (a read-only folder), startup itself still works.
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>The program an entry starts: the Run command's program, or a shortcut's target.</summary>
    private static string ProgramOf(StartupItem item)
    {
        try
        {
            var program = FileIcons.ProgramOf(item.Command);
            return program.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ? ShellLink.TargetOf(program) : program;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or System.Runtime.InteropServices.COMException) { return ""; }
    }

    private static string? CopyShortcut(StartupItem item)
    {
        try
        {
            if (item.FilePath is not { } file || !File.Exists(file) || new FileInfo(file).Length > LargestCopy) return null;
            Directory.CreateDirectory(Folder);
            var name = Guid.NewGuid().ToString("N") + Path.GetExtension(file);
            File.Copy(file, Path.Combine(Folder, name));
            return name;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }
}
