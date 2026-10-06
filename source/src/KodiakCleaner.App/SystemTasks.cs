using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using KodiakCleaner.Core.RegistryTools;
using KodiakCleaner.Core.Util;
using Microsoft.Win32;

namespace KodiakCleaner.App;

/// <summary>
/// The Windows tasks under Advanced Cleaning. Each uses Windows' own tool for the job (DISM, powercfg, compact, vssadmin,
/// wevtutil, netsh) from System32, so nothing is done in a way Windows doesn't support.
/// </summary>
public static class SystemTasks
{
    /// <summary>What a task would do, measured where Windows can tell: the bytes it would free (0 when unknown) and a short note.</summary>
    public sealed record Plan(long Bytes, string Note);

    private static string Sys(string exe) => Path.Combine(Environment.SystemDirectory, exe);
    private static string SystemDrive => (Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\").TrimEnd('\\');

    public static Plan Analyze(string id) => id switch
    {
        "task.hibernate" => HibernationFile() is { } size and > 0
            ? new Plan(size, "Hibernation is on")
            : new Plan(0, "Hibernation is already off"),
        "task.shadows" => ShadowCount() switch
        {
            null => new Plan(0, "Couldn't count shadow copies"),
            <= 1 and var n => new Plan(0, n == 1 ? "Only the newest one exists; it is kept" : "None found"),
            var n => new Plan(0, $"{n - 1} older of {n} would be deleted"),
        },
        "task.eventlogs" => new Plan(EventLogFiles().Sum(f => SafeLength(f)), "Application, System and Setup logs"),
        "task.runmru" => new Plan(0, RunMruCount() is var c and > 0 ? TargetRun.Plural(c, "command") : "Nothing typed yet"),
        "task.components" => new Plan(0, "Windows measures this when it runs"),
        "task.compactos" => new Plan(0, "Windows measures this when it runs"),
        _ => new Plan(0, "Ready"),
    };

    /// <returns>Bytes freed (0 when Windows doesn't say) and a status line.</returns>
    public static (long Freed, string Status) Run(string id, CancellationToken ct)
    {
        switch (id)
        {
            case "task.dns":
                return (0, DnsFlushResolverCache() ? "DNS cache emptied" : "Windows couldn't empty the DNS cache");
            case "task.clipboard":
                var cleared = Application.Current.Dispatcher.Invoke(ClipboardHelper.Clear);
                return (0, cleared ? "Clipboard emptied" : "Another program is using the clipboard");
            case "task.runmru":
                return (0, ClearRunMru());
            case "task.branchcache":
                return (0, Tool(Sys("netsh.exe"), "branchcache flush", TimeSpan.FromMinutes(2), ct) == 0 ? "BranchCache emptied" : "BranchCache isn't in use on this PC");
            case "task.components":
                return Measured(() => Tool(Sys("Dism.exe"), "/Online /Cleanup-Image /StartComponentCleanup /NoRestart", TimeSpan.FromHours(2), ct),
                    "Windows Update Cleanup finished", "DISM reported a problem (code {0})");
            case "task.compactos":
                return Measured(() => Tool(Sys("compact.exe"), "/CompactOS:always", TimeSpan.FromHours(3), ct),
                    "Windows is now compressed", "compact reported a problem (code {0})");
            case "task.hibernate":
                return Measured(() => Tool(Sys("powercfg.exe"), "/hibernate off", TimeSpan.FromMinutes(2), ct),
                    "Hibernation turned off", "powercfg reported a problem (code {0})");
            case "task.shadows":
                return Measured(() => DeleteOlderShadows(ct), "Older shadow copies deleted", "vssadmin reported a problem (code {0})");
            case "task.eventlogs":
                return ClearEventLogs(ct);
            default:
                return (0, "Unknown task");
        }
    }

    /// <summary>Runs a tool and reports how much free space the Windows drive gained.</summary>
    private static (long, string) Measured(Func<int> run, string done, string failed)
    {
        var before = FreeSpace();
        var code = run();
        var gained = Math.Max(0, FreeSpace() - before);
        return code == 0 ? (gained, done) : (gained, string.Format(failed, code));
    }

    private static long FreeSpace()
    {
        try { return new DriveInfo(SystemDrive).AvailableFreeSpace; }
        catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException) { return 0; }
    }

    /// <summary>Runs a Windows tool hidden and waits for it. Returns its exit code (-1 if it couldn't start or was cancelled).</summary>
    private static int Tool(string exe, string arguments, TimeSpan limit, CancellationToken ct) => Tool(exe, arguments, limit, ct, out _);

    private static int Tool(string exe, string arguments, TimeSpan limit, CancellationToken ct, out string output)
    {
        output = "";
        if (!File.Exists(exe)) return -1;
        var info = new ProcessStartInfo(exe, arguments)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        try
        {
            using var p = Process.Start(info);
            if (p is null) return -1;
            var read = p.StandardOutput.ReadToEndAsync();
            p.StandardError.ReadToEndAsync();
            var deadline = DateTime.UtcNow + limit;
            // A cancel stops waiting, not the tool: stopping DISM or compact part way could leave Windows half done.
            while (!p.WaitForExit(500))
                if (ct.IsCancellationRequested || DateTime.UtcNow > deadline) return -1;
            output = read.Result;
            return p.ExitCode;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException) { return -1; }
    }

    private static long? HibernationFile()
    {
        try
        {
            var file = new FileInfo(Path.Combine(SystemDrive + "\\", "hiberfil.sys"));
            return file.Exists ? file.Length : 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    private static int? ShadowCount()
    {
        if (Tool(Sys("vssadmin.exe"), $"list shadows /for={SystemDrive}", TimeSpan.FromMinutes(1), CancellationToken.None, out var text) is not (0 or 1))
            return null;
        return Regex.Matches(text, @"Shadow Copy ID:", RegexOptions.IgnoreCase).Count;
    }

    /// <summary>Deletes the oldest shadow copy until one is left. Never deletes the newest.</summary>
    private static int DeleteOlderShadows(CancellationToken ct)
    {
        for (var guard = 0; guard < 64; guard++)
        {
            if (ShadowCount() is not { } n) return -1;
            if (n <= 1) return 0;
            var code = Tool(Sys("vssadmin.exe"), $"delete shadows /for={SystemDrive} /oldest /quiet", TimeSpan.FromMinutes(5), ct);
            if (code != 0) return code;
        }
        return 0;
    }

    private static readonly string[] EventLogs = { "Application", "System", "Setup" };

    private static IEnumerable<string> EventLogFiles() =>
        EventLogs.Select(l => Path.Combine(Environment.SystemDirectory, @"winevt\Logs", l + ".evtx"));

    private static long SafeLength(string file)
    {
        try { return File.Exists(file) ? new FileInfo(file).Length : 0; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return 0; }
    }

    /// <summary>Clears each log with wevtutil after exporting it to Backups\Event Logs. A log whose copy fails is left alone.</summary>
    private static (long, string) ClearEventLogs(CancellationToken ct)
    {
        var folder = Settings.Data.EventLogs;
        Directory.CreateDirectory(folder);
        var before = EventLogFiles().Sum(SafeLength);
        var failed = new List<string>();
        foreach (var log in EventLogs)
        {
            // Export first and check the copy exists; only then clear. No copy, no clear.
            var backup = Path.Combine(folder, $"{DateTime.Now:yyyy-MM-dd_HHmmss}_{log}.evtx");
            if (Tool(Sys("wevtutil.exe"), $"epl {log} \"{backup}\"", TimeSpan.FromMinutes(5), ct) != 0 || !File.Exists(backup) ||
                Tool(Sys("wevtutil.exe"), $"cl {log}", TimeSpan.FromMinutes(5), ct) != 0)
                failed.Add(log);
        }
        var freed = Math.Max(0, before - EventLogFiles().Sum(SafeLength));
        return failed.Count == 0
            ? (freed, "Cleared; copies saved in Backups\\Event Logs")
            : (freed, $"Not cleared (no copy could be saved): {string.Join(", ", failed)}");
    }

    private const string RunMruKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU";

    private static int RunMruCount()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunMruKey);
            return key?.GetValueNames().Count(n => n.Length > 0) ?? 0;
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { return 0; }
    }

    /// <summary>Backs up the Run box history to a .reg file, then empties it. Nothing is changed if the backup fails.</summary>
    private static string ClearRunMru()
    {
        if (RunMruCount() == 0) return "Nothing to clear";
        try
        {
            RegistryBackup.Save(new[] { new RegistryLocation(RegistryHive.CurrentUser, RegistryView.Default, RunMruKey) }, "Run_History");
            using var key = Registry.CurrentUser.OpenSubKey(RunMruKey, writable: true);
            if (key is null) return "Nothing to clear";
            foreach (var name in key.GetValueNames().Where(n => n.Length > 0)) key.DeleteValue(name, throwOnMissingValue: false);
            return "Cleared; backup saved";
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return "Couldn't be backed up, so nothing was changed";
        }
    }

    [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")]
    private static extern bool DnsFlushResolverCacheNative();

    private static bool DnsFlushResolverCache()
    {
        try { return DnsFlushResolverCacheNative(); }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { return false; }
    }
}
