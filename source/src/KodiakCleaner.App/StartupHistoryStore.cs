using System.IO;
using KodiakCleaner.Core.Startup;

namespace KodiakCleaner.App;

/// <summary>Kodiak Cleaner's record of startup entries, kept in Backups\Startup History beside it.</summary>
public static class StartupHistoryStore
{
    private const long LargestCopy = 10L << 20; // shortcuts and scripts; a big program in the Startup folder isn't copied

    public static string Folder => Settings.Data.StartupHistory;

    private static StartupHistory? _history;
    public static StartupHistory Current => _history ??= StartupHistory.Load(Folder);

    /// <summary>Notes what is in startup now (called whenever the Startup list is read).</summary>
    public static void Record(IReadOnlyList<StartupItem> items) => Try(() => Current.Observe(items, DateTime.Now, CopyShortcut));

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
