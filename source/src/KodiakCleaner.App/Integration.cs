using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Windows.Shell;
using KodiakCleaner.Core.Util;
using Microsoft.Win32;

namespace KodiakCleaner.App;

/// <summary>Everything Kodiak Cleaner registers with Windows. Each feature can be turned on and off from Settings.</summary>
public static class Integration
{
    public static string ExePath => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "KodiakCleaner.exe");

    /// <summary>The package family name when Kodiak Cleaner runs as a Store (MSIX) app; null for the MSI and portable copies. Declared
    /// before <see cref="Kind"/>, which reads it while the class starts.</summary>
    public static string? PackageFamilyName { get; } = ReadPackageFamilyName();

    /// <summary>Portable, the MSI, or the Microsoft Store package. Worked out once; the rules are in <see cref="InstallKinds"/>.</summary>
    public static InstallKind Kind { get; } = InstallKinds.Detect(ExePath,
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), PackageFamilyName is not null);

    /// <summary>True when running from the MSI install location (Program Files), not a portable or Store copy.</summary>
    public static bool IsInstalled => Kind == InstallKind.Installed;

    /// <summary>True for the Microsoft Store copy.</summary>
    public static bool IsStore => Kind == InstallKind.Store;

    /// <summary>What Windows should start (see <see cref="InstallKinds.LaunchPath"/>): the Store copy's app alias, otherwise this exe.</summary>
    public static string LaunchPath => InstallKinds.LaunchPath(Kind, ExePath,
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    private static string? ReadPackageFamilyName()
    {
        try
        {
            int length = 0;
            // 15700 = APPMODEL_ERROR_NO_PACKAGE (not packaged); 122 = ERROR_INSUFFICIENT_BUFFER (packaged, length now set).
            if (GetCurrentPackageFamilyName(ref length, null) != 122 || length <= 0) return null;
            var name = new char[length];
            return GetCurrentPackageFamilyName(ref length, name) == 0 ? new string(name, 0, Math.Max(0, length - 1)) : null;
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { return null; } // Windows 7, Wine
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int GetCurrentPackageFamilyName(ref int packageFamilyNameLength, char[]? packageFamilyName);

    // ---- Clean when Windows starts -------------------------------------------------------------
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    // Names include the channel so Dev, Beta and Stable copies never overwrite each other's registrations.
    private static string RunValue => RunValueFor(AppInfo.Channel);
    private static string RunValueFor(KodiakCleaner.Core.About.ReleaseChannel channel) =>
        $"{KodiakCleaner.Core.About.ReleaseChannels.AppName(AppInfo.ShortName, channel)} AutoClean";
    public const string AutoCleanArg = "/autoclean";

    public static void SetCleanOnStartup(bool on)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (on) key.SetValue(RunValue, $"\"{LaunchPath}\" {AutoCleanArg}");
        else key.DeleteValue(RunValue, throwOnMissingValue: false);
    }

    // ---- Recycle Bin context menu ---------------------------------------------------------------
    // Per-user registration (HKCU\Software\Classes), so it needs no administrator rights.
    private static string RecycleBinShell => RecycleBinShellFor(AppInfo.Id);
    private static string RecycleBinShellFor(string id) => @"Software\Classes\CLSID\{645FF040-5081-101B-9F08-00AA002F954E}\shell\" + id;

    public static void SetRecycleBinMenu(bool on)
    {
        if (!on)
        {
            Registry.CurrentUser.DeleteSubKeyTree(RecycleBinShell, throwOnMissingSubKey: false);
            return;
        }
        using var verb = Registry.CurrentUser.CreateSubKey(RecycleBinShell, writable: true);
        verb.SetValue("", $"Open {AppInfo.Name}...");
        // The icon comes from the exe itself; SyncAll sets it again at each start, so a Store update's new folder is picked up.
        verb.SetValue("Icon", $"\"{ExePath}\",0");
        using var cmd = verb.CreateSubKey("command", writable: true);
        cmd.SetValue("", $"\"{LaunchPath}\"");
        // Store copy (Ted, 2026-10-26): also hand the verb to the package's Windows 11 menu command (source/shell), in the
        // hope that the short right-click menu lists it. Undocumented; if Windows ignores it, the command key above still
        // works under Show More Options.
        if (IsStore) verb.SetValue("ExplorerCommandHandler", ShellCommandClsid);
        else verb.DeleteValue("ExplorerCommandHandler", throwOnMissingValue: false);
    }

    /// <summary>KodiakCleanerShell.dll's IExplorerCommand class (also in AppxManifest.xml and KodiakCleanerShell.cpp; a test keeps them equal).</summary>
    public const string ShellCommandClsid = "{04C283A9-1A6F-4898-BE4C-60A428E89BD5}";

    // ---- Skip UAC (scheduled task that runs Kodiak Cleaner with highest privileges) --------------------
    private static string TaskName => SkipUacTaskFor(AppInfo.Id);
    private static string SkipUacTaskFor(string id) => $@"{id}\SkipUAC";
    public const string ElevatedArg = "/elevated";

    /// <summary>Only offered for the MSI copy: a task pointing at a user-writable exe (portable, or the Store's alias) would let anything elevate.</summary>
    public static bool CanSkipUac => InstallKinds.MayStartElevatedWithoutPrompt(Kind);

    public static void SetSkipUac(bool on)
    {
        if (!on)
        {
            RunSchtasks($"/Delete /TN \"{TaskName}\" /F", ignoreErrors: true);
            foreach (var c in AppInfo.OtherChannels) RunSchtasks($"/Delete /TN \"{SkipUacTaskFor(AppInfo.IdFor(c))}\" /F", ignoreErrors: true);
            return;
        }
        if (!CanSkipUac) throw new InvalidOperationException("Skipping UAC is only available for the installed version.");
        var user = SecurityElement.Escape(WindowsIdentity.GetCurrent().Name);
        var exe = SecurityElement.Escape(ExePath);
        var xml = $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo><Description>Starts {SecurityElement.Escape(AppInfo.Name)} with administrator rights without a UAC prompt. Turn off in {SecurityElement.Escape(AppInfo.Name)} Settings.</Description></RegistrationInfo>
              <Principals>
                <Principal id="Author"><UserId>{user}</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>Parallel</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <AllowStartOnDemand>true</AllowStartOnDemand>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>5</Priority>
              </Settings>
              <Actions Context="Author"><Exec><Command>{exe}</Command><Arguments>{ElevatedArg}</Arguments></Exec></Actions>
            </Task>
            """;
        var file = Path.Combine(Path.GetTempPath(), $"kodiakcleaner-task-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(file, xml, Encoding.Unicode);
            RunSchtasks($"/Create /TN \"{TaskName}\" /XML \"{file}\" /F", ignoreErrors: false);
            // An earlier channel's task (a Dev install updated to Beta) is replaced by this one.
            foreach (var c in AppInfo.OtherChannels) RunSchtasks($"/Delete /TN \"{SkipUacTaskFor(AppInfo.IdFor(c))}\" /F", ignoreErrors: true);
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// <summary>
    /// Starts the elevated copy through the task. Returns false if the task is missing. A Dev install updated to Beta still
    /// has the Dev build's task (it runs the same exe) until the elevated copy moves it over, so that one is tried too.
    /// </summary>
    public static bool TryLaunchViaTask() =>
        new[] { AppInfo.Id }.Concat(AppInfo.OtherChannels.Select(AppInfo.IdFor))
            .Any(id => RunSchtasks($"/Run /TN \"{SkipUacTaskFor(id)}\"", ignoreErrors: true) == 0);

    internal static int RunSchtasks(string args, bool ignoreErrors)
    {
        var psi = new ProcessStartInfo("schtasks.exe", args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        using var p = Process.Start(psi)!;
        var err = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0 && !ignoreErrors) throw new InvalidOperationException(err.Trim());
        return p.ExitCode;
    }

    // ---- Jump list ------------------------------------------------------------------------------
    public const string PageArgPrefix = "--page=";

    public static void ApplyJumpList(bool on)
    {
        var list = new JumpList { ShowFrequentCategory = false, ShowRecentCategory = false };
        if (on)
        {
            void Add(string title, string args, string description) => list.JumpItems.Add(new JumpTask
            {
                Title = title,
                Arguments = args,
                Description = description,
                ApplicationPath = LaunchPath,
                IconResourcePath = ExePath,
            });
            // Tools hidden in Settings › Tools are left out (Ted, 0.9.1 Beta); Run Cleaner cleans without a window, so it stays.
            void Page(string title, string key, string description)
            {
                if (!Settings.Current.HiddenTools.Contains(key, StringComparer.OrdinalIgnoreCase)) Add(title, PageArgPrefix + key, description);
            }
            Add("Run Cleaner", AutoCleanArg, $"Clean the selected items now without opening {AppInfo.Name}");
            Page("Cleaner", "cleaner", "Open the Cleaner");
            Page("Registry", "registry", "Open the registry scanner");
            Page("Startup", "startup", "Manage startup programs");
            Page("Duplicate Finder", "duplicates", "Find duplicate files");
            Page("System Restore", "restore", "Manage restore points");
        }
        JumpList.SetJumpList(System.Windows.Application.Current, list);
        list.Apply();
    }

    /// <summary>Puts the jump list in step with Settings (a tool hidden or shown again); never shows an error.</summary>
    public static void RefreshJumpList() => TryQuietly(() => ApplyJumpList(Settings.Current.JumpList));

    /// <summary>Re-applies every saved integration setting (paths can change after an update or move).</summary>
    public static void SyncAll()
    {
        var s = Settings.Current;
        TryQuietly(MoveFromOtherChannels);
        TryQuietly(() => SetCleanOnStartup(s.CleanOnStartup));
        TryQuietly(() => SetRecycleBinMenu(s.RecycleBinMenu));
        TryQuietly(() => ApplyJumpList(s.JumpList));
        TryQuietly(ScheduledCleaning.SyncTasks);
    }

    /// <summary>
    /// Every channel installs to Program Files\Kodiak Cleaner and replaces the others (one upgrade code), but Windows
    /// registrations carry the channel ("Kodiak Cleaner Dev AutoClean", Task Scheduler's \KodiakCleaner.Dev folder). After a
    /// Dev install is updated to Beta, this removes the Dev build's per-user entries (SyncAll then makes this channel's
    /// own) and moves its scheduled cleanings and Skip UAC task into this channel's folder, so nothing runs twice, every
    /// schedule can still be changed or deleted, and uninstalling leaves nothing behind. Tasks that need administrator
    /// rights to replace stay where they are (they still run this exe) until Kodiak Cleaner runs as administrator.
    /// </summary>
    public static void MoveFromOtherChannels()
    {
        RemoveOtherChannelEntries();
        var s = Settings.Current;
        foreach (var id in AppInfo.OtherChannels.Select(AppInfo.IdFor).Where(TaskFolderExists))
        {
            if (s.SkipUac && Elevation.IsAdministrator && CanSkipUac && TaskExists(SkipUacTaskFor(id)))
                SetSkipUac(true); // makes this channel's task, then deletes the old one
            foreach (var schedule in s.Schedules)
            {
                var old = $@"{id}\Cleaning {schedule.Id}";
                if (!TaskExists(old) || (schedule.AsAdmin && !ScheduledCleaning.CanRunAsAdmin)) continue;
                ScheduledCleaning.Register(schedule); // also deletes the old channel's task
            }
            s.Save();
            DeleteTaskFolderIfEmpty(id);
        }
    }

    /// <summary>Removes other channels' Clean When Windows Starts value and Recycle Bin menu (per user, no rights needed).</summary>
    public static void RemoveOtherChannelEntries()
    {
        using (var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true))
            foreach (var c in AppInfo.OtherChannels) run?.DeleteValue(RunValueFor(c), throwOnMissingValue: false);
        foreach (var c in AppInfo.OtherChannels) Registry.CurrentUser.DeleteSubKeyTree(RecycleBinShellFor(AppInfo.IdFor(c)), throwOnMissingSubKey: false);
    }

    private static bool TaskExists(string name) => RunSchtasks($"/Query /TN \"{name}\"", ignoreErrors: true) == 0;

    private static dynamic? TaskService()
    {
        var type = Type.GetTypeFromProgID("Schedule.Service");
        if (type is null) return null;
        dynamic service = Activator.CreateInstance(type)!;
        service.Connect();
        return service;
    }

    private static bool TaskFolderExists(string id)
    {
        try { return TaskService()?.GetFolder("\\" + id) is not null; }
        catch (System.Runtime.InteropServices.COMException) { return false; }
    }

    private static void DeleteTaskFolderIfEmpty(string id)
    {
        try
        {
            var service = TaskService();
            if (service is null) return;
            dynamic folder = service.GetFolder("\\" + id);
            if (folder.GetTasks(1).Count == 0 && folder.GetFolders(0).Count == 0) service.GetFolder("\\").DeleteFolder(id, 0);
        }
        catch (System.Runtime.InteropServices.COMException) { }
    }

    private static void TryQuietly(Action a)
    {
        try { a(); }
        catch (Exception e) when (e is UnauthorizedAccessException or SecurityException or IOException or InvalidOperationException
            or System.Runtime.InteropServices.COMException or NotImplementedException) { }
    }
}
