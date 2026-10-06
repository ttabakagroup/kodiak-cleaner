namespace KodiakCleaner.Core.Util;

/// <summary>
/// Where Kodiak Cleaner keeps the files it makes (Ted, 0.8.36). An installed copy uses Documents\Kodiak Cleaner, so nothing
/// it writes needs administrator rights or special permissions in Program Files; a portable copy keeps them beside
/// KodiakCleaner.exe. Each kind of backup has its own folder:
/// <code>
/// Kodiak Cleaner\
///   Backups\Event Logs\        Windows event logs saved before they are cleared
///   Backups\Programs History\  what the Programs page removed, and how to put it back
///   Backups\Registry\          a .reg file before every registry change (double-click to undo)
///   Backups\Settings\          Back Up Settings copies of KodiakCleaner.ini and both histories
///   Backups\Startup History\   startup entries seen and removed, with copies of their shortcuts
///   Logs\                      one activity log per month
/// </code>
/// </summary>
public sealed class DataFolders
{
    public const string FolderName = "Kodiak Cleaner";
    public const string EventLogsName = "Event Logs", ProgramsHistoryName = "Programs History", RegistryName = "Registry",
        SettingsName = "Settings", StartupHistoryName = "Startup History";

    public DataFolders(string root)
    {
        Root = Path.TrimEndingDirectorySeparator(root);
        Backups = Path.Combine(Root, "Backups");
        Logs = Path.Combine(Root, "Logs");
    }

    /// <summary>Documents\Kodiak Cleaner for an installed copy.</summary>
    public static DataFolders Installed(string documents) => new(Path.Combine(documents, FolderName));

    public string Root { get; }
    public string Backups { get; }
    public string Logs { get; }
    public string EventLogs => Path.Combine(Backups, EventLogsName);
    public string ProgramsHistory => Path.Combine(Backups, ProgramsHistoryName);
    public string Registry => Path.Combine(Backups, RegistryName);
    public string Settings => Path.Combine(Backups, SettingsName);
    public string StartupHistory => Path.Combine(Backups, StartupHistoryName);

    /// <summary>The folders Kodiak Cleaner creates, A to Z.</summary>
    public IEnumerable<string> All => new[] { EventLogs, ProgramsHistory, Registry, Settings, StartupHistory, Logs };

    /// <summary>
    /// Moves backups from an older version's single Backups folder (beside KodiakCleaner.exe, everything in one place)
    /// into these folders: .reg files to Registry, settings and history copies to Settings, the history and event log
    /// folders as they are. Nothing that is already at the new place is overwritten; a file that can't be moved (no
    /// rights to delete it) is copied instead. Returns how many files moved.
    /// </summary>
    public int MoveFromOldLayout(string oldBackups)
    {
        if (!Directory.Exists(oldBackups)) return 0;
        int moved = 0;
        foreach (var file in Directory.EnumerateFiles(oldBackups))
        {
            var name = Path.GetFileName(file);
            var target = Path.GetExtension(name).ToLowerInvariant() switch
            {
                ".reg" => Registry,
                ".ini" or ".json" => Settings,
                _ => null,
            };
            if (target is not null && Move(file, Path.Combine(target, name))) moved++;
        }
        foreach (var dir in new[] { EventLogsName, ProgramsHistoryName, StartupHistoryName })
        {
            var from = Path.Combine(oldBackups, dir);
            if (!Directory.Exists(from) || SamePath(from, Path.Combine(Backups, dir))) continue;
            foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
                if (Move(file, Path.Combine(Backups, dir, Path.GetRelativePath(from, file)))) moved++;
            TryDeleteEmpty(from);
        }
        if (!SamePath(oldBackups, Backups)) TryDeleteEmpty(oldBackups);
        return moved;
    }

    /// <summary>Finds a backup a history record names: as saved, or by its name in Registry or Backups (after a move).</summary>
    public string? Find(string? file)
    {
        if (string.IsNullOrWhiteSpace(file)) return null;
        if (Path.IsPathRooted(file) && File.Exists(file)) return file;
        var name = Path.GetFileName(file);
        return new[] { Path.Combine(Registry, name), Path.Combine(Backups, name) }.FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// Deletes the backups and logs (Ted: uninstalling must be able to leave nothing behind). Only Kodiak Cleaner's own
    /// Backups and Logs folders are removed; the main folder goes too when nothing else is left in it.
    /// </summary>
    public void DeleteAll()
    {
        foreach (var dir in new[] { Backups, Logs })
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        TryDeleteEmpty(Root);
    }

    private static bool Move(string from, string to)
    {
        try
        {
            if (File.Exists(to)) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            try { File.Move(from, to); }
            catch (UnauthorizedAccessException) { File.Copy(from, to); }
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    private static void TryDeleteEmpty(string dir)
    {
        try
        {
            foreach (var sub in Directory.EnumerateDirectories(dir)) TryDeleteEmpty(sub);
            if (!Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
            StringComparison.OrdinalIgnoreCase);
}
