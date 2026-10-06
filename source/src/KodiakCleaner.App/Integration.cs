using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Windows.Shell;
using Microsoft.Win32;

namespace KodiakCleaner.App;

/// <summary>Everything Kodiak Cleaner registers with Windows. Each feature can be turned on and off from Settings.</summary>
public static class Integration
{
    public static string ExePath => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "KodiakCleaner.exe");

    /// <summary>True when running from the MSI install location (Program Files), not a portable copy.</summary>
    public static bool IsInstalled
    {
        get
        {
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            return ExePath.StartsWith(pf + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---- Clean when Windows starts -------------------------------------------------------------
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    // Names include the channel so Dev, Beta and Stable copies never overwrite each other's registrations.
    private static string RunValue => $"{AppInfo.RegistrationName} AutoClean";
    public const string AutoCleanArg = "/autoclean";

    public static void SetCleanOnStartup(bool on)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (on) key.SetValue(RunValue, $"\"{ExePath}\" {AutoCleanArg}");
        else key.DeleteValue(RunValue, throwOnMissingValue: false);
    }

    // ---- Recycle Bin context menu ---------------------------------------------------------------
    // Per-user registration (HKCU\Software\Classes), so it needs no administrator rights.
    private static string RecycleBinShell => @"Software\Classes\CLSID\{645FF040-5081-101B-9F08-00AA002F954E}\shell\" + AppInfo.Id;

    public static void SetRecycleBinMenu(bool on)
    {
        if (!on)
        {
            Registry.CurrentUser.DeleteSubKeyTree(RecycleBinShell, throwOnMissingSubKey: false);
            return;
        }
        using var verb = Registry.CurrentUser.CreateSubKey(RecycleBinShell, writable: true);
        verb.SetValue("", $"Open {AppInfo.Name}...");
        verb.SetValue("Icon", $"\"{ExePath}\",0");
        using var cmd = verb.CreateSubKey("command", writable: true);
        cmd.SetValue("", $"\"{ExePath}\"");
    }

    // ---- Skip UAC (scheduled task that runs Kodiak Cleaner with highest privileges) --------------------
    private static string TaskName => $@"{AppInfo.Id}\SkipUAC";
    public const string ElevatedArg = "/elevated";

    /// <summary>Only offered for the installed copy: a task pointing at a user-writable exe would let anything elevate.</summary>
    public static bool CanSkipUac => IsInstalled;

    public static void SetSkipUac(bool on)
    {
        if (!on)
        {
            RunSchtasks($"/Delete /TN \"{TaskName}\" /F", ignoreErrors: true);
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
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// <summary>Starts the elevated copy through the task. Returns false if the task is missing.</summary>
    public static bool TryLaunchViaTask() => RunSchtasks($"/Run /TN \"{TaskName}\"", ignoreErrors: true) == 0;

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
                ApplicationPath = ExePath,
                IconResourcePath = ExePath,
            });
            Add("Run Cleaner", AutoCleanArg, $"Clean the selected items now without opening {AppInfo.Name}");
            Add("Cleaner", PageArgPrefix + "cleaner", "Open the Cleaner");
            Add("Registry", PageArgPrefix + "registry", "Open the registry scanner");
            Add("Startup", PageArgPrefix + "startup", "Manage startup programs");
            Add("Duplicate Finder", PageArgPrefix + "duplicates", "Find duplicate files");
            Add("System Restore", PageArgPrefix + "restore", "Manage restore points");
        }
        JumpList.SetJumpList(System.Windows.Application.Current, list);
        list.Apply();
    }

    /// <summary>Re-applies every saved integration setting (paths can change after an update or move).</summary>
    public static void SyncAll()
    {
        var s = Settings.Current;
        TryQuietly(() => SetCleanOnStartup(s.CleanOnStartup));
        TryQuietly(() => SetRecycleBinMenu(s.RecycleBinMenu));
        TryQuietly(() => ApplyJumpList(s.JumpList));
        TryQuietly(ScheduledCleaning.SyncTasks);
    }

    private static void TryQuietly(Action a)
    {
        try { a(); }
        catch (Exception e) when (e is UnauthorizedAccessException or SecurityException or IOException or InvalidOperationException) { }
    }
}
