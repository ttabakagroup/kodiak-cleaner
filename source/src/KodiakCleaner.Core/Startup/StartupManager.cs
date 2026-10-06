using System.Runtime.Versioning;
using Microsoft.Win32;
using KodiakCleaner.Core.RegistryTools;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.Core.Startup;

[SupportedOSPlatform("windows")]
public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";

    public static List<StartupItem> List()
    {
        var items = new List<StartupItem>();
        ReadRunKey(items, RegistryHive.CurrentUser, RegistryView.Registry64, StartupSource.UserRunKey);
        ReadRunKey(items, RegistryHive.LocalMachine, RegistryView.Registry64, StartupSource.MachineRunKey);
        ReadRunKey(items, RegistryHive.LocalMachine, RegistryView.Registry32, StartupSource.MachineRunKey32);
        ReadFolder(items, Environment.GetFolderPath(Environment.SpecialFolder.Startup), StartupSource.UserStartupFolder);
        ReadFolder(items, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), StartupSource.CommonStartupFolder);
        return items.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static void SetEnabled(StartupItem item, bool enabled)
    {
        var (hive, sub) = ApprovedLocation(item.Source);
        using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        using var key = root.CreateSubKey(sub, writable: true);
        key.SetValue(ApprovedValueName(item), StartupApprovedValue.Create(enabled), RegistryValueKind.Binary);
        item.Enabled = enabled;
    }

    /// <summary>Removes the entry. Registry entries are backed up to a .reg file first; shortcuts go to the Recycle Bin.</summary>
    public static string? Delete(StartupItem item)
    {
        string? backup;
        // The on/off flag is backed up with the entry, so restoring brings it back in the same state.
        var (aHive, aSub) = ApprovedLocation(item.Source);
        var approved = new RegistryLocation(aHive, RegistryView.Registry64, aSub, ApprovedValueName(item));
        switch (item.Source)
        {
            case StartupSource.UserRunKey or StartupSource.MachineRunKey or StartupSource.MachineRunKey32:
            {
                var (hive, view) = RunLocation(item.Source);
                var location = new RegistryLocation(hive, view, RunKey, item.Name);
                backup = RegistryBackup.Save(new[] { location, approved }, "startup");
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var key = root.OpenSubKey(RunKey, writable: true);
                key?.DeleteValue(item.Name, throwOnMissingValue: false);
                break;
            }
            default:
                backup = RegistryBackup.Save(new[] { approved }, "startup");
                if (item.FilePath is not null && File.Exists(item.FilePath))
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(item.FilePath,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                break;
        }

        // Drop the matching enable/disable flag too, so nothing stale is left behind.
        try
        {
            using var aRoot = RegistryKey.OpenBaseKey(aHive, RegistryView.Registry64);
            using var aKey = aRoot.OpenSubKey(aSub, writable: true);
            aKey?.DeleteValue(ApprovedValueName(item), throwOnMissingValue: false);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException) { }

        return backup;
    }

    /// <summary>The Startup folder for you, or for everyone on the PC (needs administrator).</summary>
    public static string StartupFolder(bool allUsers) =>
        Environment.GetFolderPath(allUsers ? Environment.SpecialFolder.CommonStartup : Environment.SpecialFolder.Startup);

    /// <summary>
    /// Adds a program to startup by putting a shortcut to it in the Startup folder. A shortcut that is picked is
    /// moved or copied there; anything else gets a new shortcut. Returns the shortcut's path.
    /// </summary>
    public static string AddShortcut(string file, string name, bool allUsers, bool moveShortcut)
    {
        if (!File.Exists(file)) throw new FileNotFoundException("That file no longer exists.", file);
        var folder = StartupFolder(allUsers);
        if (string.IsNullOrEmpty(folder)) throw new DirectoryNotFoundException("Windows didn't say where the Startup folder is.");
        Directory.CreateDirectory(folder);
        var safeName = string.Concat(name.Trim().Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim('.', ' ');
        if (safeName.Length == 0) throw new ArgumentException("Give the entry a name.");
        var target = Path.Combine(folder, safeName + ".lnk");
        if (File.Exists(target)) throw new IOException($"Startup already has an entry named '{safeName}'.");

        if (Path.GetExtension(file).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            if (moveShortcut) File.Move(file, target);
            else File.Copy(file, target);
        }
        else ShellLink.Create(target, file);
        return target;
    }

    /// <summary>
    /// Puts a removed entry back where it was: the registry value with its command, or the shortcut from Kodiak Cleaner's saved copy.
    /// It comes back on or off, as it was.
    /// </summary>
    public static void Restore(StartupRecord r, string historyFolder)
    {
        StartupItem restored;
        switch (r.Source)
        {
            case StartupSource.UserRunKey or StartupSource.MachineRunKey or StartupSource.MachineRunKey32:
            {
                var (hive, view) = RunLocation(r.Source);
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var key = root.CreateSubKey(RunKey, writable: true);
                if (key.GetValue(r.Name) is not null) throw new IOException($"Startup already has an entry named '{r.Name}'.");
                key.SetValue(r.Name, r.Command, RegistryValueKind.String);
                restored = new StartupItem { Name = r.Name, Command = r.Command, Source = r.Source };
                break;
            }
            default:
            {
                if (r.ShortcutCopy is null) throw new FileNotFoundException("There's no saved copy of this shortcut.");
                var copy = Path.Combine(historyFolder, r.ShortcutCopy);
                if (!File.Exists(copy)) throw new FileNotFoundException("The saved copy of this shortcut is missing.", copy);
                var folder = StartupFolder(r.Source == StartupSource.CommonStartupFolder);
                Directory.CreateDirectory(folder);
                var target = Path.Combine(folder, r.FileName ?? r.Name + ".lnk");
                if (File.Exists(target)) throw new IOException($"Startup already has an entry named '{Path.GetFileName(target)}'.");
                File.Copy(copy, target);
                restored = new StartupItem { Name = r.Name, Command = target, FilePath = target, Source = r.Source };
                break;
            }
        }
        if (!r.Enabled) SetEnabled(restored, false);
    }

    private static (RegistryHive, RegistryView) RunLocation(StartupSource s) => s switch
    {
        StartupSource.UserRunKey => (RegistryHive.CurrentUser, RegistryView.Registry64),
        StartupSource.MachineRunKey => (RegistryHive.LocalMachine, RegistryView.Registry64),
        StartupSource.MachineRunKey32 => (RegistryHive.LocalMachine, RegistryView.Registry32),
        _ => throw new ArgumentOutOfRangeException(nameof(s)),
    };

    private static (RegistryHive, string) ApprovedLocation(StartupSource s) => s switch
    {
        StartupSource.UserRunKey => (RegistryHive.CurrentUser, ApprovedKey + @"\Run"),
        StartupSource.MachineRunKey => (RegistryHive.LocalMachine, ApprovedKey + @"\Run"),
        StartupSource.MachineRunKey32 => (RegistryHive.LocalMachine, ApprovedKey + @"\Run32"),
        StartupSource.UserStartupFolder => (RegistryHive.CurrentUser, ApprovedKey + @"\StartupFolder"),
        StartupSource.CommonStartupFolder => (RegistryHive.LocalMachine, ApprovedKey + @"\StartupFolder"),
        _ => throw new ArgumentOutOfRangeException(nameof(s)),
    };

    private static string ApprovedValueName(StartupItem item) =>
        item.FilePath is not null ? Path.GetFileName(item.FilePath) : item.Name;

    private static byte[]? ReadApproved(StartupSource source, string valueName)
    {
        var (hive, sub) = ApprovedLocation(source);
        using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        using var key = root.OpenSubKey(sub);
        return key?.GetValue(valueName) as byte[];
    }

    private static void ReadRunKey(List<StartupItem> items, RegistryHive hive, RegistryView view, StartupSource source)
    {
        using var root = RegistryKey.OpenBaseKey(hive, view);
        using var key = root.OpenSubKey(RunKey);
        if (key is null) return;
        foreach (var name in key.GetValueNames())
        {
            if (string.IsNullOrEmpty(name)) continue;
            var command = key.GetValue(name)?.ToString() ?? "";
            items.Add(new StartupItem
            {
                Name = name,
                Command = command,
                Source = source,
                Enabled = StartupApprovedValue.IsEnabled(ReadApproved(source, name)),
                FileMissing = PathChecks.IsMissing(CommandLine.CheckablePath(Environment.ExpandEnvironmentVariables(command))),
            });
        }
    }

    private static void ReadFolder(List<StartupItem> items, string folder, StartupSource source)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
        foreach (var file in Directory.EnumerateFiles(folder))
        {
            var fileName = Path.GetFileName(file);
            if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
            items.Add(new StartupItem
            {
                Name = Path.GetFileNameWithoutExtension(file),
                Command = file,
                FilePath = file,
                Source = source,
                Enabled = StartupApprovedValue.IsEnabled(ReadApproved(source, fileName)),
            });
        }
    }
}
