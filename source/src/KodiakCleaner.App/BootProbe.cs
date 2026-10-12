using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using KodiakCleaner.Core.Startup;
using KodiakCleaner.Core.Tools;
using Microsoft.Win32;

namespace KodiakCleaner.App;

/// <summary>
/// Boot Timer's measurements, read from Windows only (no drivers, no extra installs): when Windows started (tick count),
/// when this person signed in (the session's logon time), each process's start time, processor time and disk use, how
/// busy the processor is, and Windows' own boot log (wevtutil, needs administrator rights).
/// </summary>
public static class BootProbe
{
    /// <summary>The RunOnce value that reopens Kodiak Cleaner on the Boot Timer page after the restart (removed by Windows when it runs).</summary>
    public const string RunOnceName = "Kodiak Cleaner Boot Timer";
    private const string RunOnceKey = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";

    /// <summary>When Windows last started, in UTC.</summary>
    public static DateTime BootUtc => DateTime.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64);

    public static string StateFile => Path.Combine(Settings.Data.Reports, "Boot Timer", "boot-timer.json");

    /// <summary>Opens Kodiak Cleaner on the Boot Timer page once, at the next sign-in.</summary>
    public static void SetReopen(bool on)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunOnceKey);
        if (on && Environment.ProcessPath is { } exe) key.SetValue(RunOnceName, $"\"{exe}\" {Integration.PageArgPrefix}boottimer");
        else key.DeleteValue(RunOnceName, throwOnMissingValue: false);
    }

    /// <summary>Asks Windows to restart now. Programs with unsaved work may still hold it up or cancel it.</summary>
    public static void Restart()
    {
        var shutdown = Path.Combine(Environment.SystemDirectory, "shutdown.exe");
        using var p = Process.Start(new ProcessStartInfo(shutdown, "/r /t 0") { UseShellExecute = false, CreateNoWindow = true })
            ?? throw new Win32Exception("Windows didn't start the restart.");
    }

    /// <summary>When this person signed in to Windows (the session's logon time), or null when Windows doesn't say.</summary>
    public static DateTime? SignInUtc()
    {
        try
        {
            if (WTSQuerySessionInformationW(IntPtr.Zero, -1, 24 /* WTSSessionInfo */, out var buffer, out var bytes) && buffer != IntPtr.Zero)
            {
                try
                {
                    // WTSINFOW: 8 DWORD counters, WinStationName[32], Domain[17], UserName[21], then 8-byte aligned times.
                    if (bytes >= 216)
                    {
                        long logon = Marshal.ReadInt64(buffer, 200);
                        if (logon > 0) return DateTime.FromFileTimeUtc(logon);
                    }
                }
                finally { WTSFreeMemory(buffer); }
            }
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { }
        // Fallback: the desktop (Explorer) starts right after sign-in.
        var mine = Process.GetCurrentProcess().SessionId;
        return Processes().Where(p => p.SessionId == mine && Path.GetFileName(p.Path).Equals("explorer.exe", StringComparison.OrdinalIgnoreCase))
                          .Select(p => (DateTime?)p.StartUtc).Min();
    }

    public sealed record ProcessInfo(int Id, int SessionId, string Path, DateTime StartUtc, long CpuMs, long DiskBytes);

    /// <summary>Every process Windows lets a standard user look at, with its program file, start time, processor time and disk use.</summary>
    public static List<ProcessInfo> Processes()
    {
        var list = new List<ProcessInfo>();
        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                if (p.Id is 0 or 4) continue; // Idle and System
                var h = OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, p.Id);
                if (h == IntPtr.Zero) continue;
                try
                {
                    var sb = new System.Text.StringBuilder(1024);
                    int size = sb.Capacity;
                    var path = QueryFullProcessImageNameW(h, 0, sb, ref size) ? sb.ToString() : "";
                    if (!GetProcessTimes(h, out var created, out _, out var kernel, out var user)) continue;
                    long io = GetProcessIoCounters(h, out var c) ? (long)(c.ReadTransferCount + c.WriteTransferCount) : 0;
                    int session;
                    try { session = p.SessionId; } catch (InvalidOperationException) { session = -1; }
                    list.Add(new ProcessInfo(p.Id, session, path, DateTime.FromFileTimeUtc(created), (kernel + user) / 10_000, io));
                }
                catch (Exception e) when (e is ArgumentException or Win32Exception) { }
                finally { CloseHandle(h); }
            }
        }
        return list;
    }

    private static long _lastIdle, _lastTotal;

    /// <summary>How busy the processor was (0 to 1) since the previous call; the first call only takes a reading.</summary>
    public static double? ProcessorBusy()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return null;
        long total = kernel + user; // kernel time includes idle time
        long dIdle = idle - _lastIdle, dTotal = total - _lastTotal;
        bool first = _lastTotal == 0;
        _lastIdle = idle;
        _lastTotal = total;
        return first || dTotal <= 0 ? null : Math.Clamp(1 - (double)dIdle / dTotal, 0, 1);
    }

    /// <summary>Windows' own record of this start (Diagnostics-Performance events), or null when it can't be read or isn't written yet.</summary>
    public static BootTimer.WindowsBootData? ReadWindowsLog(DateTime bootUtc, out bool readable)
    {
        readable = false;
        try
        {
            var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "wevtutil.exe"))
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
            };
            foreach (var a in BootTimer.QueryArgs) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            if (p is null) return null;
            var errors = p.StandardError.ReadToEndAsync();
            var xml = p.StandardOutput.ReadToEnd();
            if (!p.WaitForExit(15_000)) { try { p.Kill(); } catch (InvalidOperationException) { } return null; }
            _ = errors.Result;
            if (p.ExitCode != 0) return null; // access denied for standard users
            readable = true;
            return BootTimer.ParseWindowsEvents(xml, bootUtc);
        }
        catch (Exception e) when (e is Win32Exception or IOException or InvalidOperationException) { return null; }
    }

    /// <summary>The program file each turned-on startup entry runs (a startup-folder shortcut's target), keyed by that file.</summary>
    public static Dictionary<string, StartupItem> StartupPrograms()
    {
        var map = new Dictionary<string, StartupItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in StartupManager.List().Where(i => i.Enabled && !i.FileMissing))
        {
            var file = FileIcons.ProgramOf(item.Command);
            if (file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) file = ShortcutTarget(file);
            if (file.Length > 0) map.TryAdd(Path.GetFullPath(file), item);
        }
        return map;
    }

    private static string ShortcutTarget(string lnk)
    {
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type is null) return "";
            dynamic shell = Activator.CreateInstance(type)!;
            string target = shell.CreateShortcut(lnk).TargetPath;
            return File.Exists(target) ? target : "";
        }
        catch (Exception e) when (e is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or ArgumentException or InvalidCastException) { return ""; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageNameW(IntPtr h, int flags, System.Text.StringBuilder name, ref int size);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetProcessTimes(IntPtr h, out long creation, out long exit, out long kernel, out long user);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetProcessIoCounters(IntPtr h, out IoCounters counters);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQuerySessionInformationW(IntPtr server, int session, int infoClass, out IntPtr buffer, out int bytes);
    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr memory);
}
