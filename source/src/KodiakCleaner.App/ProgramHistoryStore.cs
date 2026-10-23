using System.Diagnostics;
using System.IO;
using KodiakCleaner.Core.Cleaning;
using KodiakCleaner.Core.Programs;
using KodiakCleaner.Core.Startup;

namespace KodiakCleaner.App;

/// <summary>Kodiak Cleaner's record of what it removed on the Programs page (Ted, 0.8.22), in Backups\Programs History (see Settings.Data).</summary>
public static class ProgramHistoryStore
{
    public static string Folder => Settings.Data.ProgramsHistory;

    private static ProgramHistory? _history;
    public static ProgramHistory Current => _history ??= ProgramHistory.Load(Folder);

    public static void Add(ProgramChange change, InstalledProgram program, string detail, string? registryBackup = null, string? startupKey = null) =>
        Try(() => Current.Add(change, program, detail, DateTime.Now, Relative(registryBackup), startupKey));

    /// <summary>Backups inside the Registry backups folder are kept as a name, so a portable copy that moves still finds them.</summary>
    private static string? Relative(string? file) =>
        file is not null && Path.GetDirectoryName(Path.GetFullPath(file)) is { } dir &&
        string.Equals(Path.TrimEndingDirectorySeparator(dir), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Settings.Data.Registry)), StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileName(file) : file;

    /// <summary>One record for the removed registry keys (one backup holds them all), and one per file, folder or startup entry.</summary>
    public static void Leftovers(InstalledProgram program, UninstallFlow.RemoveResult result) => Try(() =>
    {
        var now = DateTime.Now;
        var keys = result.Done.Where(d => d.Kind == LeftoverKind.RegistryKey).ToList();
        if (keys.Count > 0)
            Current.Add(ProgramChange.LeftoverKeys, program, string.Join("; ", keys.Select(k => k.Path)), now, Relative(result.RegistryBackup));
        foreach (var f in result.Done.Where(d => d.Kind is LeftoverKind.Folder or LeftoverKind.Shortcut))
            Current.Add(ProgramChange.LeftoverFile, program, f.Path, now);
        foreach (var s in result.Done.Where(d => d.Kind == LeftoverKind.StartupEntry && d.Item is StartupItem))
            Current.Add(ProgramChange.LeftoverStartup, program, s.Path, now, startupKey: StartupRecord.KeyOf((StartupItem)s.Item!));
    });

    public static void Forget(IEnumerable<ProgramRecord> records) => Try(() =>
    {
        foreach (var r in records.ToList()) Current.Records.Remove(r);
    });

    /// <summary>Puts a record back. Returns null when it worked, otherwise why it couldn't.</summary>
    public static string? Restore(ProgramRecord r)
    {
        if (r.IsRestored) return "It was already restored.";
        string? problem = r.Change switch
        {
            ProgramChange.Uninstalled => "An uninstalled program can't be put back from here. Install it again from its maker.",
            ProgramChange.DeletedEntry or ProgramChange.Renamed or ProgramChange.LeftoverKeys => ImportBackup(r.RegistryBackup),
            ProgramChange.LeftoverFile => FromRecycleBin(r.Detail, r.When),
            ProgramChange.LeftoverStartup => RestoreStartup(r.StartupKey),
            _ => "Unknown record.",
        };
        if (problem is null) Try(() => r.RestoredOn = DateTime.Now);
        return problem;
    }

    /// <summary>Imports a .reg backup with Windows' own reg.exe.</summary>
    private static string? ImportBackup(string? file)
    {
        file = Settings.Data.Find(file);
        if (file is null) return "Its backup file is missing from the Backups folder.";
        string text;
        try { text = File.ReadAllText(file); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return "Its backup file couldn't be read: " + e.Message; }
        if (text.Contains("[HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase) && !Elevation.IsAdministrator)
            return "It was installed for all users, so putting it back needs administrator rights. Use Restart as Administrator at the top.";
        var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "reg.exe"), $"import \"{file}\"")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true,
        };
        try
        {
            using var p = Process.Start(info);
            if (p is null) return "Windows couldn't start reg.exe.";
            p.StandardOutput.ReadToEndAsync();
            var err = p.StandardError.ReadToEnd();
            if (!p.WaitForExit(60_000)) return "reg.exe didn't finish.";
            return p.ExitCode == 0 ? null : "Windows couldn't import the backup: " + err.Trim();
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { return "Windows couldn't start reg.exe: " + e.Message; }
    }

    /// <summary>Moves a leftover back out of the Recycle Bin to where it was, as the Recycle Bin's own Restore does.</summary>
    private static string? FromRecycleBin(string original, DateTime removed)
    {
        var item = RecycleBinItems.ReadCurrentUser()
            .Where(i => string.Equals(i.OriginalPath, original, StringComparison.OrdinalIgnoreCase) &&
                        i.DeletedUtc >= removed.ToUniversalTime().AddMinutes(-10))
            .OrderByDescending(i => i.DeletedUtc).FirstOrDefault();
        if (item is null) return "It is no longer in the Recycle Bin (the Recycle Bin was emptied since).";
        if (File.Exists(original) || Directory.Exists(original)) return $"Something is already at {original}, so it was left in the Recycle Bin.";
        try
        {
            if (Path.GetDirectoryName(original) is { } parent) Directory.CreateDirectory(parent);
            if (Directory.Exists(item.DataPath)) Directory.Move(item.DataPath, original);
            else File.Move(item.DataPath, original);
            File.Delete(item.InfoFile);
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return $"It couldn't be moved back ({e.Message}). You can still restore it from the Recycle Bin.";
        }
    }

    private static string? RestoreStartup(string? key)
    {
        var record = StartupHistoryStore.Current.Records.LastOrDefault(x => x.Key == key && x.IsRemoved);
        if (record is null || !StartupHistory.CanRestore(record)) return "The Startup history has no saved copy of it.";
        try
        {
            StartupHistoryStore.Restore(record);
            StartupHistoryStore.Record(StartupManager.List());
            return null;
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return "It needs administrator rights. Use Restart as Administrator at the top.";
        }
        catch (IOException e) { return e.Message; }
    }

    private static void Try(Action change)
    {
        try
        {
            change();
            Current.Save(Folder);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
