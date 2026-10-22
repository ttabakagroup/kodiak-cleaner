namespace KodiakCleaner.Core.Startup;

/// <summary>A program running when a removed startup entry came back: its file name, full path (if Windows shares it) and start time.</summary>
public sealed record RunningProgram(string Name, string? Path, DateTime? Started);

/// <summary>
/// Works out what most likely put a removed startup entry back (Ted, 0.8.71). Windows doesn't record who writes a Run value
/// or drops a shortcut in the Startup folder, so this looks at what was running: the program the entry starts, or another
/// program from its folder (an updater, a helper), started before the entry came back.
/// </summary>
public static class StartupComeback
{
    private const string TurnOffTip =
        "Turn it off here instead of removing it: programs put their own entry back, but leave Windows' on/off switch alone, so off stays off. " +
        "Or turn off the program's own \"start with Windows\" setting.";

    /// <param name="program">The program the entry starts (its full path), or "" when it isn't known.</param>
    /// <param name="writtenAt">When the entry was written back, if Windows says.</param>
    public static string Explain(string program, DateTime? writtenAt, IEnumerable<RunningProgram> running)
    {
        if (string.IsNullOrWhiteSpace(program))
            return "Kodiak Cleaner couldn't tell which program this entry starts, so it can't say what put it back. " + TurnOffTip;

        var exe = FileName(program);
        var list = running.ToList();
        // Started after the entry came back means it can't have put it back.
        bool Before(RunningProgram p) => writtenAt is null || p.Started is null || p.Started <= writtenAt;

        var self = list.Where(p => SameName(p.Name, exe) && Before(p)).OrderBy(p => p.Started ?? DateTime.MaxValue).FirstOrDefault();
        if (self is not null)
            return $"{exe} was running when it came back{StartedText(self)}. It most likely puts its entry back each time it starts. " + TurnOffTip;

        var folder = Folder(program);
        var parent = Folder(folder);
        // Programs in the Windows folder share it with hundreds of others, so a neighbor there says nothing.
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var shared = windows.Length > 0 && InFolder(program, windows);
        var helper = shared ? null : list
            .Where(p => p.Path is { } path && !SameName(p.Name, exe) && Before(p) && (InFolder(path, folder) || InFolder(path, parent, deep: false)))
            .OrderBy(p => p.Started ?? DateTime.MaxValue)
            .FirstOrDefault();
        if (helper is not null)
            return $"{helper.Name}, from the same program folder as {exe}, was running when it came back{StartedText(helper)}. " +
                   "It is most likely the program's updater or helper putting the entry back. " + TurnOffTip;

        return $"{exe} wasn't running when Kodiak Cleaner noticed, so an updater, a background service or a scheduled task most likely " +
               "put it back (often after an update). " + TurnOffTip;
    }

    private static string StartedText(RunningProgram p) => p.Started is { } t ? $" (it started {t:g})" : "";

    private static bool SameName(string a, string b) =>
        string.Equals(WithoutExtension(FileName(a)), WithoutExtension(FileName(b)), StringComparison.OrdinalIgnoreCase);

    // Windows paths, split by hand so the same answer comes out wherever the tests run.
    private static string FileName(string path) => path[(path.LastIndexOfAny(new[] { '\\', '/' }) + 1)..];
    private static string Folder(string path) => path.LastIndexOfAny(new[] { '\\', '/' }) is var i and > 0 ? path[..i] : "";
    private static string WithoutExtension(string name) => name.LastIndexOf('.') is var i and > 0 ? name[..i] : name;

    /// <summary>True when <paramref name="file"/> is in <paramref name="folder"/> (or below it, unless <paramref name="deep"/> is false).</summary>
    private static bool InFolder(string file, string folder, bool deep = true)
    {
        if (folder.Length < 4) return false; // never a whole drive like C:\
        var dir = Folder(file);
        if (string.Equals(dir.TrimEnd('\\'), folder.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return true;
        return deep && dir.StartsWith(folder.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
    }
}
