using System.IO;
using KodiakCleaner.App.ViewModels;
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

    /// <summary>
    /// Notes the scheduled tasks that start a program at sign-in or when Windows starts (Ted, 0.8.92), called whenever the
    /// Startup page reads them. Only call it after a full read: a task missing here is marked removed.
    /// </summary>
    public static void RecordTasks(IEnumerable<ScheduledTaskItem> tasks) =>
        Try(() => ExplainReturns(Current.Observe(tasks.Where(IsStartupTask).Select(ItemOf).ToList(), DateTime.Now, _ => null, tasks: true)));

    /// <summary>Reads every scheduled task again and notes the ones that start with Windows; quietly does nothing if Task Scheduler can't be read.</summary>
    public static void RecordTasksNow()
    {
        try { RecordTasks(ScheduledTaskTools.ReadAll()); }
        catch (Exception e) when (ScheduledTasksViewModel.IsSchedulerError(e)) { }
    }

    /// <summary>The tasks the Startup page and its history cover: ones that start with Windows, not Windows' own.</summary>
    public static bool IsStartupTask(ScheduledTaskItem t) => t.RunsAtStartup && !t.IsWindowsTask;

    /// <summary>Notes that Kodiak Cleaner deleted a task that started with Windows (it was backed up first).</summary>
    public static void TaskRemoved(ScheduledTaskItem task)
    {
        if (IsStartupTask(task)) Removed(ItemOf(task), null);
    }

    /// <summary>Notes that Kodiak Cleaner put a task back from its backup, so it isn't counted as coming back by itself.</summary>
    public static void TaskRestored(string taskPath) => Try(() =>
    {
        var key = StartupRecord.KeyOf(StartupSource.ScheduledTask, taskPath);
        if (Current.Records.LastOrDefault(r => r.Key == key && r.IsRemoved) is { } r) Current.MarkRestored(r);
    });

    /// <summary>
    /// Puts a removed entry back: a startup entry from its saved copy, or a scheduled task from its newest backup. Throws
    /// IOException (with the reason) or UnauthorizedAccessException when it can't.
    /// </summary>
    public static void Restore(StartupRecord r)
    {
        if (r.Source == StartupSource.ScheduledTask)
        {
            var backup = ScheduledTaskTools.Backups.List()
                .Where(b => string.Equals(b.TaskPath, r.FileName ?? r.Name, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(b => b.SavedAt)
                .FirstOrDefault() ?? throw new IOException("There's no backup of this task in Backups\\Scheduled Tasks.");
            // A task of that name made again since is backed up before it's replaced.
            ScheduledTaskItem? current = null;
            try { current = ScheduledTaskTools.ReadAll().FirstOrDefault(t => string.Equals(t.TaskPath, backup.TaskPath, StringComparison.OrdinalIgnoreCase)); }
            catch (Exception e) when (ScheduledTasksViewModel.IsSchedulerError(e)) { }
            if (ScheduledTaskTools.Restore(backup, current) is { } problem) throw new IOException(problem);
            RecordTasksNow();
            return;
        }
        StartupManager.Restore(r, Folder);
        Try(() => Current.MarkRestored(r));
    }

    private static StartupItem ItemOf(ScheduledTaskItem t)
    {
        var program = t.ProgramPath.Length > 0 ? $"\"{t.ProgramPath}\"" : t.Command;
        return new StartupItem
        {
            Name = t.Name,
            Command = t.Arguments.Length > 0 ? $"{program} {t.Arguments}" : program,
            Source = StartupSource.ScheduledTask,
            TaskPath = t.TaskPath,
            Enabled = t.Enabled,
        };
    }

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
