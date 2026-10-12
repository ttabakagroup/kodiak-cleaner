using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using KodiakCleaner.Core.RegistryTools;
using KodiakCleaner.Core.Util;
using Microsoft.Win32;

namespace KodiakCleaner.Core.Tools;

/// <summary>One Point-in-Time Restore snapshot: the shadow copy Windows recorded under PITR\Snapshots.</summary>
public sealed record PitrSnapshot(Guid Id, DateTime? Created, DateTime? KeptUntil);

/// <summary>The four Point-in-Time Restore settings as Windows will use them. Minutes and megabytes, like the registry.</summary>
public sealed record PitrSettings(bool Enabled, int FrequencyMinutes, int RetentionMinutes, int MaxSpaceMb, bool SetByOrganization);

/// <summary>
/// Windows 11's Point-in-Time Restore (24H2/25H2 build x.8737 and later, June 2026). Windows takes a full shadow copy of the
/// system drive on a schedule and keeps it at most 72 hours; restoring only happens in the Windows Recovery Environment.
/// Microsoft documents the settings for Intune (Recovery CSP); Settings writes the same values with a _UX suffix under
/// PITR\Settings, which is what Kodiak Cleaner reads and writes (backed up first). There is no documented way to take a
/// snapshot on demand: Snapshot Now starts Windows' own snapshot task, ignoring its wait-until-idle condition.
/// </summary>
[SupportedOSPlatform("windows")]
public static class PointInTimeRestore
{
    public const string SettingsKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\Recovery\PITR\Settings";
    public const string SnapshotsKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\Recovery\PITR\Snapshots";
    public const string TaskFolder = @"\Microsoft\Windows\Setup";
    public const string TaskName = "PITRTask";

    public const string EnabledValue = "Active";
    public const string FrequencyValue = "SnapshotInterval";
    public const string RetentionValue = "MaxTimespan";
    public const string MaxSpaceValue = "MaxGlobalSize";

    // Windows' defaults (Microsoft Learn): every 24 hours, kept 72 hours, 2% of the disk (2 GB to 50 GB).
    public const int DefaultFrequencyMinutes = 24 * 60;
    public const int DefaultRetentionMinutes = 72 * 60;
    public const int MinSpaceMb = 2 * 1024;
    public const int MaxSpaceMb = 50 * 1024;

    /// <summary>The choices Windows' own Settings offers, in hours, shortest first.</summary>
    public static readonly int[] FrequencyHours = { 4, 6, 12, 16, 24 };
    public static readonly int[] RetentionHours = { 4, 6, 12, 16, 24, 72 };
    public static readonly int[] SpaceGb = { 2, 5, 10, 15, 20, 30, 40, 50 };

    /// <summary>"4 Hours", "1 Hour", "90 Minutes".</summary>
    public static string MinutesLabel(int minutes) =>
        minutes % 60 == 0 ? (minutes == 60 ? "1 Hour" : $"{minutes / 60} Hours") : $"{minutes} Minutes";

    /// <summary>2% of the disk, held to 2 GB to 50 GB, which is what Windows uses until someone picks a size.</summary>
    public static int DefaultSpaceMb(long diskBytes) =>
        (int)Math.Clamp(diskBytes / 50 / (1024 * 1024), MinSpaceMb, MaxSpaceMb);

    /// <summary>An organization's policy (_GPO) wins over the Settings choice (_UX), which wins over Windows' default.</summary>
    public static int Effective(int? policy, int? chosen, int fallback) =>
        policy is > 0 ? policy.Value : chosen is > 0 ? chosen.Value : fallback;

    /// <summary>When Windows removes a snapshot by age. It can go sooner when space runs low.</summary>
    public static DateTime KeptUntil(DateTime created, int retentionMinutes) => created.AddMinutes(retentionMinutes);

    /// <summary>True when this copy of Windows has Point-in-Time Restore: its settings key or its snapshot task exists.</summary>
    public static bool IsAvailable()
    {
        using (var key = OpenLocalMachine(SettingsKey)) if (key is not null) return true;
        try { return GetTask() is not null; }
        catch (COMException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    public static PitrSettings ReadSettings()
    {
        using var key = OpenLocalMachine(SettingsKey);
        int? Read(string name) => key?.GetValue(name) is int i ? i : null;
        var systemDisk = SystemDriveSize();

        int? activePolicy = Read(EnabledValue + "_GPO"), active = Read(EnabledValue + "_UX");
        // With nothing chosen yet, Windows turns it on for system drives of 200 GB or more on PCs nobody manages.
        bool enabled = activePolicy is { } p ? p != 0
            : active is { } a ? a != 0
            : HasSnapshots() || systemDisk >= 200_000_000_000L;

        bool managed = activePolicy is not null || Read(FrequencyValue + "_GPO") is not null
            || Read(RetentionValue + "_GPO") is not null || Read(MaxSpaceValue + "_GPO") is not null;

        return new PitrSettings(
            enabled,
            Effective(Read(FrequencyValue + "_GPO"), Read(FrequencyValue + "_UX"), DefaultFrequencyMinutes),
            Effective(Read(RetentionValue + "_GPO"), Read(RetentionValue + "_UX"), DefaultRetentionMinutes),
            Effective(Read(MaxSpaceValue + "_GPO"), Read(MaxSpaceValue + "_UX"), DefaultSpaceMb(systemDisk)),
            managed);
    }

    /// <summary>
    /// Writes one setting the way Windows' Settings does (the _UX value, DWORD). The whole Settings key is saved to a .reg
    /// file first. Needs administrator rights. Windows puts these back to its defaults after a feature update.
    /// </summary>
    public static string Write(string name, int value)
    {
        var backup = RegistryBackup.Save(new[] { new RegistryLocation(RegistryHive.LocalMachine, RegistryView.Registry64, SettingsKey) },
            "Point-in-Time_Restore_Settings");
        using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = root.CreateSubKey(SettingsKey, writable: true);
        key.SetValue(name + "_UX", value, RegistryValueKind.DWord);
        ActivityLog.Write("Restore", $"Point-in-Time Restore {name}_UX set to {value} (backup {Path.GetFileName(backup)})");
        return backup;
    }

    /// <summary>The snapshots Windows lists under PITR\Snapshots, newest first, with times from the shadow copies themselves.</summary>
    public static List<PitrSnapshot> ListSnapshots(int retentionMinutes)
    {
        var ids = new List<Guid>();
        using (var key = OpenLocalMachine(SnapshotsKey))
            if (key is not null)
                foreach (var name in key.GetSubKeyNames())
                    if (Guid.TryParse(name.Trim('{', '}'), out var id)) ids.Add(id);
        if (ids.Count == 0) return new();

        var times = ShadowCopyTimes();
        return ids
            .Where(id => times is null || times.ContainsKey(id)) // a snapshot whose shadow copy is gone can't be restored
            .Select(id =>
            {
                DateTime? created = times is not null && times.TryGetValue(id, out var t) ? t : null;
                return new PitrSnapshot(id, created, created is { } c ? KeptUntil(c, retentionMinutes) : null);
            })
            .OrderByDescending(s => s.Created ?? DateTime.MinValue)
            .ToList();
    }

    public static bool HasSnapshots()
    {
        using var key = OpenLocalMachine(SnapshotsKey);
        return key?.SubKeyCount > 0;
    }

    /// <summary>
    /// Starts Windows' snapshot task now, ignoring its wait-until-idle condition (TASK_RUN_IGNORE_CONSTRAINTS), and waits for
    /// it to finish. The task itself is not changed. Returns the task's result code (0 = ran fine).
    /// </summary>
    public static int SnapshotNow(TimeSpan timeout, CancellationToken ct)
    {
        dynamic task = GetTask() ?? throw new InvalidOperationException("Windows' Point-in-Time Restore task isn't on this PC.");
        if (!(bool)task.Enabled) throw new InvalidOperationException("Windows' Point-in-Time Restore task is turned off. Turn Point-in-Time Restore on first.");
        const int ignoreConstraints = 0x2;
        task.RunEx(null, ignoreConstraints, 0, "");

        var watch = Stopwatch.StartNew();
        Thread.Sleep(2000);
        while ((int)task.State == 4 /* TASK_STATE_RUNNING */ && watch.Elapsed < timeout)
        {
            ct.ThrowIfCancellationRequested();
            Thread.Sleep(2000);
        }
        if ((int)task.State == 4) throw new TimeoutException("Windows is still taking the snapshot. Check back with Refresh in a few minutes.");
        return (int)task.LastTaskResult;
    }

    /// <summary>
    /// Restarts into the Windows Recovery Environment (Advanced startup), where Troubleshoot > Point-in-Time Restore lives.
    /// Windows' own shutdown.exe; Windows doesn't let a program start the restore itself.
    /// </summary>
    public static void RestartToRecovery() =>
        Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "shutdown.exe"), "/r /o /t 0")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        })?.Dispose();

    private static object? GetTask()
    {
        var type = Type.GetTypeFromProgID("Schedule.Service") ?? throw new COMException("Task Scheduler isn't available.");
        dynamic service = Activator.CreateInstance(type)!;
        service.Connect();
        try { return service.GetFolder(TaskFolder).GetTask(TaskName); }
        catch (COMException) { return null; }
        catch (FileNotFoundException) { return null; }
    }

    /// <summary>Shadow copy ID to creation time (local), or null when Windows won't say (it needs administrator rights).</summary>
    private static Dictionary<Guid, DateTime>? ShadowCopyTimes()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\cimv2", "SELECT ID, InstallDate FROM Win32_ShadowCopy");
            var map = new Dictionary<Guid, DateTime>();
            foreach (ManagementObject mo in searcher.Get())
                using (mo)
                {
                    if (mo["ID"] is string s && Guid.TryParse(s.Trim('{', '}'), out var id) && mo["InstallDate"] is string cim)
                        map[id] = ManagementDateTimeConverter.ToDateTime(cim);
                }
            return map;
        }
        // Access denied, a stopped service or missing WMI parts (PlatformNotSupportedException) all mean "Windows won't say".
        catch (Exception) { return null; }
    }

    private static long SystemDriveSize()
    {
        try { return new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\").TotalSize; }
        catch (IOException) { return 0; }
        catch (UnauthorizedAccessException) { return 0; }
    }

    private static RegistryKey? OpenLocalMachine(string subKey)
    {
        using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        try { return root.OpenSubKey(subKey); }
        catch (System.Security.SecurityException) { return null; }
    }
}
