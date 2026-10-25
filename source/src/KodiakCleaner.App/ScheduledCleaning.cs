using System.IO;
using System.Security.Principal;
using System.Text;
using KodiakCleaner.Core.Cleaning;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App;

/// <summary>
/// Scheduled cleanings (Ted, 0.8.21): each <see cref="CleaningSchedule"/> has a Windows Task Scheduler task that starts
/// Kodiak Cleaner with <see cref="Arg"/> and its id. Kodiak Cleaner cleans those items with no window and exits; nothing
/// of Kodiak Cleaner runs in between.
/// </summary>
public static class ScheduledCleaning
{
    public const string Arg = "/schedule:";

    private static string TaskName(CleaningSchedule s) => TaskName(AppInfo.Id, s);
    private static string TaskName(string id, CleaningSchedule s) => $@"{id}\Cleaning {s.Id}";

    /// <summary>The same schedule's task under another channel's folder (a Dev install updated to Beta).</summary>
    private static IEnumerable<string> OtherChannelNames(CleaningSchedule s) => AppInfo.OtherChannels.Select(c => TaskName(AppInfo.IdFor(c), s));

    /// <summary>Administrator schedules are only offered to an administrator on the installed copy (a task that runs a
    /// user-writable exe with full rights would let anything elevate).</summary>
    public static bool CanRunAsAdmin => Elevation.IsAdministrator && Integration.IsInstalled;

    /// <summary>Makes (or replaces) the Windows task. Throws InvalidOperationException with Windows' message when it can't.</summary>
    public static void Register(CleaningSchedule s)
    {
        if (s.AsAdmin && !CanRunAsAdmin) s.AsAdmin = false;
        var xml = s.TaskXml(Integration.ExePath, Arg + s.Id, WindowsIdentity.GetCurrent().Name,
            $"{s.Name}: {s.Describe()}. A scheduled cleaning made in {AppInfo.Name}; change or delete it there, under Cleaner > Schedule.", DateTime.Now);
        var file = Path.Combine(Path.GetTempPath(), $"kodiakcleaner-schedule-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(file, xml, Encoding.Unicode);
            Integration.RunSchtasks($"/Create /TN \"{TaskName(s)}\" /XML \"{file}\" /F", ignoreErrors: false);
            s.RegisteredFor = Integration.ExePath;
            foreach (var old in OtherChannelNames(s)) Integration.RunSchtasks($"/Delete /TN \"{old}\" /F", ignoreErrors: true);
        }
        finally
        {
            try { File.Delete(file); } catch (IOException) { }
        }
    }

    public static void Unregister(CleaningSchedule s)
    {
        foreach (var name in OtherChannelNames(s).Prepend(TaskName(s)))
            Integration.RunSchtasks($"/Delete /TN \"{name}\" /F", ignoreErrors: true);
    }

    /// <summary>At startup: a copy that moved (portable) or was updated to a new folder points its tasks at itself again.</summary>
    public static void SyncTasks()
    {
        var changed = false;
        foreach (var s in Settings.Current.Schedules.Where(s => !string.Equals(s.RegisteredFor, Integration.ExePath, StringComparison.OrdinalIgnoreCase)))
        {
            if (s.AsAdmin && !CanRunAsAdmin) continue; // only an administrator can remake an administrator task
            Register(s);
            changed = true;
        }
        if (changed) Settings.Current.Save();
    }

    /// <summary>The Cleaner items a schedule can include: everything except items with a caution.</summary>
    public static IEnumerable<CleanTarget> Schedulable(IEnumerable<CleanTarget> catalog) => catalog.Where(t => t.Warning.Length == 0);

    /// <summary>Runs one schedule now, with no window. Returns a short result line, also saved as the schedule's last result.</summary>
    public static string Run(string id)
    {
        var s = Settings.Current.Schedules.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (s is null) return "This schedule no longer exists.";
        var wanted = s.Targets.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var (freed, cleaned) = AutoClean.RunItems(Schedulable(TargetRun.Catalog()).Where(t => wanted.Contains(t.Id)));
        s.LastRunUtc = DateTime.UtcNow;
        s.LastResult = $"{ByteSize.Format(freed)} freed from {TargetRun.Plural(cleaned, "item")}";
        ActivityLog.Write("Schedule", $"Ran '{s.Name}': {s.LastResult}");
        Settings.Current.Save();
        return s.LastResult;
    }
}
