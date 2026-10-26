using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using KodiakCleaner.Core.RegistryTools;
using KodiakCleaner.Core.Tools;
using Microsoft.Win32;

namespace KodiakCleaner.App;

/// <summary>
/// The Windows side of the Driver Cleaner. Driver packages are read from C:\Windows\INF and removed with Windows' own pnputil;
/// Safe Mode is switched with bcdedit. Every registry change is backed up first and folders go to the Recycle Bin.
/// </summary>
public static partial class DriverTools
{
    private static string Sys(string exe) => Path.Combine(Environment.SystemDirectory, exe);
    private const string ClassRoot = @"SYSTEM\CurrentControlSet\Control\Class\";
    private const string RunOnceKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce";
    // A leading * makes Windows run the entry in Safe Mode too.
    private const string RunOnceName = "*Kodiak Cleaner Driver Cleaner";
    private const string DriverSearchingKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching";

    /// <summary>Every third-party driver package in the driver store (oemNN.inf), read from the copies Windows keeps in C:\Windows\INF.</summary>
    public static List<DriverPackage> ListPackages()
    {
        var list = new List<DriverPackage>();
        IEnumerable<string> files;
        try { files = Directory.EnumerateFiles(InfFolder, "oem*.inf"); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return list; }
        foreach (var file in files)
        {
            try { list.Add(InfReader.Parse(Path.GetFileName(file).ToLowerInvariant(), File.ReadAllText(file))); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return list;
    }

    /// <summary>Graphics adapters with a driver installed (Microsoft's basic driver left out).</summary>
    public static List<GraphicsAdapter> ListAdapters()
    {
        var list = new List<GraphicsAdapter>();
        try
        {
            using var cls = Registry.LocalMachine.OpenSubKey(ClassRoot + DriverCleaner.DisplayClass);
            if (cls is null) return list;
            foreach (var name in cls.GetSubKeyNames().Where(n => n.All(char.IsDigit)))
            {
                using var k = cls.OpenSubKey(name);
                if (k is null) continue;
                string S(string v) => k.GetValue(v)?.ToString() ?? "";
                var provider = S("ProviderName");
                if (provider.Length == 0 || provider.Equals("Microsoft", StringComparison.OrdinalIgnoreCase)) continue;
                list.Add(new GraphicsAdapter(S("DriverDesc"), provider, S("DriverVersion"), S("DriverDate"), S("MatchingDeviceId"), S("InfPath").ToLowerInvariant()));
            }
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
        return list.GroupBy(a => (a.Name, a.DriverVersion)).Select(g => g.First()).OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Driver packages a device is using right now (the InfPath of every device under the classes the cleaner looks at).</summary>
    public static HashSet<string> PackagesInUse()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var guid in new[] { DriverCleaner.DisplayClass, DriverCleaner.MediaClass, DriverCleaner.SoftwareComponentClass,
                                     DriverCleaner.ExtensionClass, DriverCleaner.AudioProcessingObjectClass })
        {
            try
            {
                using var cls = Registry.LocalMachine.OpenSubKey(ClassRoot + guid);
                if (cls is null) continue;
                foreach (var name in cls.GetSubKeyNames().Where(n => n.All(char.IsDigit)))
                {
                    using var k = cls.OpenSubKey(name);
                    if (k?.GetValue("InfPath") is string inf) used.Add(inf);
                }
            }
            catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
        }
        return used;
    }

    public static string? FolderPath(DriverLeftover f)
    {
        string root = f.Root switch
        {
            "SystemDrive" => Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\",
            "LocalLow" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow"),
            _ => Enum.TryParse<Environment.SpecialFolder>(f.Root, out var sf) ? Environment.GetFolderPath(sf) : "",
        };
        return root.Length == 0 ? null : Path.Combine(root, f.Relative);
    }

    /// <summary>The registry places a leftover key can be in (HKLM in both the 64-bit and 32-bit views), only those that exist.</summary>
    public static IEnumerable<RegistryLocation> ExistingKeys(DriverLeftover key)
    {
        var places = key.Root == "HKCU"
            ? new[] { (RegistryHive.CurrentUser, RegistryView.Default) }
            : new[] { (RegistryHive.LocalMachine, RegistryView.Registry64), (RegistryHive.LocalMachine, RegistryView.Registry32) };
        var seen = new HashSet<string>();
        foreach (var (hive, view) in places)
        {
            bool exists;
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var k = root.OpenSubKey(key.Relative);
                exists = k is not null;
            }
            catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { exists = false; }
            var loc = new RegistryLocation(hive, view, key.Relative);
            if (exists && seen.Add(loc.DisplayKeyPath)) yield return loc;
        }
    }

    /// <summary>Backs up every key to one .reg file, then deletes them. Returns the backup path and the keys that couldn't be removed.</summary>
    public static (string Backup, List<string> Failed) RemoveKeys(IReadOnlyList<RegistryLocation> keys)
    {
        var backup = RegistryBackup.Save(keys, "Driver_Cleaner");
        var failed = new List<string>();
        foreach (var loc in keys)
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(loc.Hive, loc.View);
                root.DeleteSubKeyTree(loc.SubKey, throwOnMissingSubKey: false);
            }
            catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException or ArgumentException)
            {
                failed.Add(loc.DisplayKeyPath);
            }
        }
        return (backup, failed);
    }

    private static string InfFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "INF");

    /// <summary>
    /// Copies a driver package out of the driver store so it can be put back with pnputil /add-driver. Only counts as done when
    /// the copy really holds the package's .inf.
    /// </summary>
    public static bool ExportPackage(string publishedName, string folder)
    {
        Directory.CreateDirectory(folder);
        if (Pnputil($"/export-driver {publishedName} \"{folder}\"", out _) != 0) return false;
        try { return Directory.EnumerateFiles(folder, "*.inf", SearchOption.AllDirectories).Any(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>
    /// Removes a package from the driver store and from every device using it. Done when its oemNN.inf is gone, or when pnputil
    /// says it finishes at the next restart (3010).
    /// </summary>
    public static (bool Ok, string Output) DeletePackage(string publishedName)
    {
        int code = Pnputil($"/delete-driver {publishedName} /uninstall /force", out var output);
        bool gone = !File.Exists(Path.Combine(InfFolder, publishedName));
        return (code == 3010 || (code == 0 && gone), output.Trim());
    }

    private static int Pnputil(string arguments, out string output) => Tool(Sys("pnputil.exe"), arguments, TimeSpan.FromMinutes(5), out output);

    private static int Tool(string exe, string arguments, TimeSpan limit, out string output)
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
            var err = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit((int)limit.TotalMilliseconds)) return -1;
            output = read.Result + err.Result;
            return p.ExitCode;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException) { return -1; }
    }

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int index);

    /// <summary>True while Windows is running in Safe Mode (SM_CLEANBOOT).</summary>
    public static bool IsSafeMode
    {
        get
        {
            try { return GetSystemMetrics(67) != 0; }
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { return false; }
        }
    }

    /// <summary>
    /// Sets the next start to Safe Mode and has Kodiak Cleaner open on the Driver Cleaner there. Windows deletes the RunOnce
    /// entry as it runs it, so nothing is left behind.
    /// </summary>
    public static bool ScheduleSafeMode()
    {
        if (Tool(Sys("bcdedit.exe"), "/set {current} safeboot minimal", TimeSpan.FromMinutes(1), out _) != 0) return false;
        try
        {
            using var k = Registry.LocalMachine.CreateSubKey(RunOnceKey);
            k.SetValue(RunOnceName, $"\"{Integration.LaunchPath}\" {Integration.PageArgPrefix}drivers");
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
        return true;
    }

    /// <summary>
    /// Makes the next start a normal one again (harmless when Safe Mode wasn't set) and drops the RunOnce entry. Safe Mode is cleared
    /// from both the running Windows entry and the default one (they can differ), then read back. Returns false when Windows is still
    /// set to start in Safe Mode or bcdedit couldn't be read (no administrator rights), so the caller can show the msconfig steps.
    /// </summary>
    public static bool ClearSafeMode()
    {
        foreach (var entry in BootEntries)
            foreach (var value in new[] { "safeboot", "safebootalternateshell" })
                Tool(Sys("bcdedit.exe"), $"/deletevalue {entry} {value}", TimeSpan.FromMinutes(1), out _);
        RemoveSafeModeStart();
        return BootEntries.All(entry => SafeBootSet(entry) == false);
    }

    private static readonly string[] BootEntries = { "{current}", "{default}" };

    /// <summary>Whether a boot entry still has safeboot set; null when bcdedit couldn't list it. Element names aren't translated.</summary>
    private static bool? SafeBootSet(string entry)
    {
        if (Tool(Sys("bcdedit.exe"), $"/enum {entry}", TimeSpan.FromMinutes(1), out var output) != 0) return null;
        return output.Split('\n').Any(line => line.TrimStart().StartsWith("safeboot", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Opens System Configuration (msconfig), where Safe boot can be unticked by hand. Windows asks for admin rights itself.</summary>
    public static bool OpenSystemConfiguration()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Sys("msconfig.exe")) { UseShellExecute = true });
            return true;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { return false; }
    }

    /// <summary>Drops the RunOnce entry that opens the Driver Cleaner in Safe Mode (also run on uninstall).</summary>
    public static void RemoveSafeModeStart()
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(RunOnceKey, writable: true);
            k?.DeleteValue(RunOnceName, throwOnMissingValue: false);
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
    }

    /// <summary>
    /// Restarts (or shuts down) Windows right away. Safe Mode is cleared first unless <paramref name="toSafeMode"/>. Returns false,
    /// without restarting, when Safe Mode couldn't be cleared (the next start would be Safe Mode again) or Windows refused to restart.
    /// </summary>
    public static bool Restart(bool shutDown = false, bool toSafeMode = false)
    {
        if (!toSafeMode && IsSafeMode && !ClearSafeMode()) return false;
        return Tool(Sys("shutdown.exe"), shutDown ? "/s /t 0" : "/r /t 0", TimeSpan.FromSeconds(30), out _) == 0;
    }

    /// <summary>Whether Windows Update may install drivers for devices (DriverSearching\SearchOrderConfig; missing = yes).</summary>
    public static bool WindowsUpdateInstallsDrivers
    {
        get
        {
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey(DriverSearchingKey);
                return k?.GetValue("SearchOrderConfig") is not int v || v != 0;
            }
            catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { return true; }
        }
    }

    /// <summary>Turns Windows Update driver installs on or off, backing up the value first. Returns false without admin rights.</summary>
    public static bool SetWindowsUpdateInstallsDrivers(bool on)
    {
        try
        {
            var loc = new RegistryLocation(RegistryHive.LocalMachine, RegistryView.Registry64, DriverSearchingKey, "SearchOrderConfig");
            RegistryBackup.Save(new[] { loc }, "Driver_Updates");
            using var k = Registry.LocalMachine.CreateSubKey(DriverSearchingKey);
            k.SetValue("SearchOrderConfig", on ? 1 : 0, RegistryValueKind.DWord);
            return true;
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { return false; }
    }
}
