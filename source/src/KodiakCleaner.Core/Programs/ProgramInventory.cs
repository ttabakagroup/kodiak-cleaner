using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Win32;
using KodiakCleaner.Core.RegistryTools;

namespace KodiakCleaner.Core.Programs;

[SupportedOSPlatform("windows")]
public static class ProgramInventory
{
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    public static List<InstalledProgram> List()
    {
        var result = new List<InstalledProgram>();
        Read(result, RegistryHive.LocalMachine, RegistryView.Registry64);
        Read(result, RegistryHive.LocalMachine, RegistryView.Registry32);
        Read(result, RegistryHive.CurrentUser, RegistryView.Registry64);
        return result
            .GroupBy(p => (p.Name.ToLowerInvariant(), p.Version))
            .Select(g => g.First())
            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>Starts the program's own uninstaller and returns the process (or null if Windows refused).</summary>
    public static Process? StartUninstall(InstalledProgram program)
    {
        // Started directly, like Windows' Apps page, not through cmd (where & | ^ % in a path are commands).
        // Windows Installer programs often list "MsiExec /I" (the change screen); /x goes straight to uninstalling, as Windows does.
        var (exe, args) = program.IsWindowsInstaller && program.IsProductCode
            ? ("msiexec.exe", $"/x {program.KeyName}")
            : Util.CommandLine.Split(Environment.ExpandEnvironmentVariables(program.UninstallString ?? ""));
        if (exe.Length == 0) return null;
        return Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = true });
    }

    /// <summary>
    /// Starts the program's uninstaller with no questions (for the command line's /quiet): msiexec /x {ProductCode} /qn for
    /// Windows Installer programs, otherwise the program's own QuietUninstallString. Null when it has neither.
    /// </summary>
    public static Process? StartQuietUninstall(InstalledProgram program)
    {
        var (exe, args) = program.IsWindowsInstaller && program.IsProductCode
            ? ("msiexec.exe", $"/x {program.KeyName} /qn /norestart")
            : Util.CommandLine.Split(Environment.ExpandEnvironmentVariables(program.QuietUninstallString ?? ""));
        if (exe.Length == 0) return null;
        return Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = true });
    }

    public static bool CanUninstallQuietly(InstalledProgram program) =>
        (program.IsWindowsInstaller && program.IsProductCode) || !string.IsNullOrWhiteSpace(program.QuietUninstallString);

    /// <summary>True while the program's uninstall entry is still there, which means it hasn't been uninstalled.</summary>
    public static bool IsStillInstalled(InstalledProgram program)
    {
        using var root = RegistryKey.OpenBaseKey(program.Location.Hive, program.Location.View);
        using var key = root.OpenSubKey(program.Location.SubKey);
        return key is not null;
    }

    /// <summary>Backs up the uninstall entry and any other keys to one .reg file before an uninstall; returns its path.</summary>
    public static string BackupBeforeUninstall(InstalledProgram program, IEnumerable<RegistryLocation> extraKeys) =>
        RegistryBackup.Save(extraKeys.Prepend(program.Location), "Before_Uninstall");

    /// <summary>Starts the program's repair (see <see cref="InstalledProgram.RepairCommand"/>), or returns null.</summary>
    public static Process? StartRepair(InstalledProgram program)
    {
        if (program.RepairCommand() is not { } cmd) return null;
        return Process.Start(new ProcessStartInfo(cmd.File, cmd.Arguments) { UseShellExecute = true });
    }

    /// <summary>
    /// Changes the name shown for the program here and in Windows' Apps list. The entry is backed up to a .reg
    /// file first; returns the backup path.
    /// </summary>
    public static string Rename(InstalledProgram program, string newName)
    {
        newName = newName.Trim();
        if (newName.Length == 0) throw new ArgumentException("The new name can't be empty.", nameof(newName));
        var backup = RegistryBackup.Save(new[] { program.Location }, "Rename");
        using var root = RegistryKey.OpenBaseKey(program.Location.Hive, program.Location.View);
        using var key = root.OpenSubKey(program.Location.SubKey, writable: true)
                        ?? throw new InvalidOperationException("This program's entry is no longer there.");
        key.SetValue("DisplayName", newName, RegistryValueKind.String);
        return backup;
    }

    /// <summary>
    /// Removes the program's entry from the installed programs list without running its uninstaller (for leftovers
    /// whose uninstaller is gone). The program's files are not touched. Backed up to a .reg file first; returns its path.
    /// </summary>
    public static string RemoveEntry(InstalledProgram program)
    {
        var backup = RegistryBackup.Save(new[] { program.Location }, "Remove_Program_Entry");
        using var root = RegistryKey.OpenBaseKey(program.Location.Hive, program.Location.View);
        root.DeleteSubKeyTree(program.Location.SubKey, throwOnMissingSubKey: false);
        return backup;
    }

    private static void Read(List<InstalledProgram> result, RegistryHive hive, RegistryView view)
    {
        using var root = RegistryKey.OpenBaseKey(hive, view);
        using var key = root.OpenSubKey(UninstallKey);
        if (key is null) return;
        foreach (var sub in key.GetSubKeyNames())
        {
            using var e = key.OpenSubKey(sub);
            if (e is null) continue;
            var name = e.GetValue("DisplayName")?.ToString();
            if (string.IsNullOrWhiteSpace(name)) continue;
            // Hidden components and updates that belong to another product, like Windows' own list.
            if (e.GetValue("SystemComponent") is int sc && sc == 1) continue;
            if (e.GetValue("ParentKeyName") is not null) continue;
            if (e.GetValue("ReleaseType")?.ToString() is "Update" or "Hotfix" or "Security Update") continue;

            long size = e.GetValue("EstimatedSize") is int kb ? kb * 1024L : 0;
            result.Add(new InstalledProgram
            {
                Name = name.Trim(),
                Publisher = e.GetValue("Publisher")?.ToString()?.Trim() ?? "",
                Version = e.GetValue("DisplayVersion")?.ToString()?.Trim() ?? "",
                InstallDate = ParseDate(e.GetValue("InstallDate")?.ToString()),
                SizeBytes = size,
                UninstallString = e.GetValue("UninstallString")?.ToString() ?? "",
                QuietUninstallString = e.GetValue("QuietUninstallString")?.ToString() ?? "",
                ModifyPath = e.GetValue("ModifyPath")?.ToString() ?? "",
                InstallLocation = e.GetValue("InstallLocation")?.ToString() ?? "",
                DisplayIcon = e.GetValue("DisplayIcon")?.ToString() ?? "",
                KeyName = sub,
                IsWindowsInstaller = e.GetValue("WindowsInstaller") is int wi && wi == 1,
                NoRepair = e.GetValue("NoRepair") is int nr && nr == 1,
                NoModify = e.GetValue("NoModify") is int nm && nm == 1,
                PerUser = hive == RegistryHive.CurrentUser,
                Is32Bit = view == RegistryView.Registry32,
                Location = new RegistryLocation(hive, view, $@"{UninstallKey}\{sub}"),
            });
        }
    }

    /// <summary>
    /// The icon Windows Installer saved for a product (Installer\Products\{packed code}\ProductIcon), used when the
    /// uninstall entry names none. "" when there's none.
    /// </summary>
    public static string InstallerProductIcon(InstalledProgram program)
    {
        if (!program.IsProductCode) return "";
        var packed = PackProductCode(program.KeyName);
        foreach (var (hive, sub) in new[]
                 {
                     (RegistryHive.LocalMachine, $@"SOFTWARE\Classes\Installer\Products\{packed}"),
                     (RegistryHive.CurrentUser, $@"Software\Microsoft\Installer\Products\{packed}"),
                 })
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var key = root.OpenSubKey(sub);
                if (key?.GetValue("ProductIcon") is string icon && icon.Length > 0) return icon;
            }
            catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
        }
        return "";
    }

    /// <summary>Windows Installer's packed form of a product code: each part reversed, the last two bytes-groups pair-swapped.</summary>
    public static string PackProductCode(string productCode)
    {
        var hex = productCode.Trim('{', '}').Replace("-", "");
        if (hex.Length != 32) return "";
        static string Rev(string s) => new(s.Reverse().ToArray());
        var sb = new System.Text.StringBuilder(32);
        sb.Append(Rev(hex[..8])).Append(Rev(hex[8..12])).Append(Rev(hex[12..16]));
        for (int i = 16; i < 32; i += 2) sb.Append(hex[i + 1]).Append(hex[i]);
        return sb.ToString().ToUpperInvariant();
    }

    internal static DateTime? ParseDate(string? s) =>
        DateTime.TryParseExact(s?.Trim(), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
}
