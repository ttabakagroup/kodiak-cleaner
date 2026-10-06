using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32;

namespace KodiakCleaner.Core.RegistryTools;

[SupportedOSPlatform("windows")]
public static class RegistryBackup
{
    /// <summary>Where .reg backups go. The app points this at Backups\Registry (see DataFolders).</summary>
    public static string BackupFolder { get; set; } = Path.Combine(AppContext.BaseDirectory, "Backups");

    /// <summary>
    /// Exports the given keys/values to a timestamped .reg file and returns its path.
    /// Double-clicking the file in Explorer restores everything it contains.
    /// </summary>
    public static string Save(IEnumerable<RegistryLocation> locations, string label)
    {
        var writer = new RegFileWriter();
        foreach (var group in locations.GroupBy(l => (l.Hive, l.View, l.SubKey)))
        {
            using var root = RegistryKey.OpenBaseKey(group.Key.Hive, group.Key.View);
            using var key = root.OpenSubKey(group.Key.SubKey);
            if (key is null) continue;
            var first = group.First();

            if (group.Any(l => l.IsWholeKey))
            {
                WriteKeyRecursive(writer, key, first.DisplayKeyPath);
                continue;
            }

            writer.BeginKey(first.DisplayKeyPath);
            foreach (var loc in group)
            {
                if (key.GetValue(loc.ValueName) is null) continue; // already gone
                WriteValue(writer, key, loc.ValueName!);
            }
        }

        Directory.CreateDirectory(BackupFolder);
        var stamp = $"{DateTime.Now:yyyy-MM-dd_HHmmss}_{label}";
        for (int i = 0; ; i++)
        {
            var path = Path.Combine(BackupFolder, i == 0 ? $"{stamp}.reg" : $"{stamp}_{i}.reg");
            try
            {
                // CreateNew: never overwrite or follow something already sitting at that name.
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var text = new StreamWriter(stream, Encoding.Unicode); // regedit expects UTF-16 LE with BOM
                text.Write(writer.ToString());
                return path;
            }
            catch (IOException) when (File.Exists(path) && i < 100) { }
        }
    }

    /// <summary>
    /// The registry areas Kodiak Cleaner can change, for Back Up Now. Restoring the file puts all of them back as they were.
    /// The very large areas (COM classes, type libraries, file types and services) are left out here; every fix in them
    /// is still backed up on its own first.
    /// </summary>
    public static IReadOnlyList<RegistryLocation> AreasKodiakCleanerChanges()
    {
        const string cv = @"SOFTWARE\Microsoft\Windows\CurrentVersion";
        var list = new List<RegistryLocation>();
        void Both(string sub)
        {
            list.Add(new RegistryLocation(RegistryHive.LocalMachine, RegistryView.Registry64, sub));
            list.Add(new RegistryLocation(RegistryHive.LocalMachine, RegistryView.Registry32, sub));
        }
        foreach (var sub in new[] { $@"{cv}\Run", $@"{cv}\App Paths", $@"{cv}\Uninstall" })
        {
            list.Add(new RegistryLocation(RegistryHive.CurrentUser, RegistryView.Default, sub));
            Both(sub);
        }
        Both($@"{cv}\SharedDLLs");
        list.Add(new RegistryLocation(RegistryHive.CurrentUser, RegistryView.Default, $@"{cv}\Explorer\StartupApproved"));
        list.Add(new RegistryLocation(RegistryHive.LocalMachine, RegistryView.Registry64, $@"{cv}\Explorer\StartupApproved"));
        list.Add(new RegistryLocation(RegistryHive.CurrentUser, RegistryView.Default, @"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache"));
        // Added with the 0.8.30 checks: fonts, help files, installer folders, sounds and Start menu ordering.
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
            list.Add(new RegistryLocation(hive, RegistryView.Registry64, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts"));
        foreach (var sub in new[] { @"SOFTWARE\Microsoft\Windows\Help", @"SOFTWARE\Microsoft\Windows\HTML Help" })
        {
            list.Add(new RegistryLocation(RegistryHive.CurrentUser, RegistryView.Default, sub));
            Both(sub);
        }
        list.Add(new RegistryLocation(RegistryHive.LocalMachine, RegistryView.Registry64, $@"{cv}\Installer\Folders"));
        list.Add(new RegistryLocation(RegistryHive.CurrentUser, RegistryView.Default, @"AppEvents\Schemes\Apps"));
        list.Add(new RegistryLocation(RegistryHive.CurrentUser, RegistryView.Default, $@"{cv}\Explorer\MenuOrder"));
        return list;
    }

    private static void WriteKeyRecursive(RegFileWriter writer, RegistryKey key, string displayPath)
    {
        writer.BeginKey(displayPath);
        foreach (var name in key.GetValueNames())
            WriteValue(writer, key, name);
        foreach (var subName in key.GetSubKeyNames())
        {
            using var sub = key.OpenSubKey(subName);
            if (sub is not null) WriteKeyRecursive(writer, sub, displayPath + "\\" + subName);
        }
    }

    /// <summary>
    /// Writes one value. Types .NET reports as Unknown (REG_NONE and rare ones such as REG_RESOURCE_LIST, REG_LINK or a
    /// program's own type numbers) are read raw from Windows so the backup keeps their real type and bytes.
    /// </summary>
    private static void WriteValue(RegFileWriter writer, RegistryKey key, string name)
    {
        var kind = key.GetValueKind(name);
        if (kind is RegistryValueKind.Unknown or RegistryValueKind.None && TryReadRaw(key, name, out var type, out var raw))
        {
            writer.WriteRawValue(name, type, raw);
            return;
        }
        writer.WriteValue(name, kind, key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames));
    }

    private static bool TryReadRaw(RegistryKey key, string name, out uint type, out byte[] data)
    {
        data = Array.Empty<byte>();
        type = 0;
        uint size = 0;
        int rc = RegQueryValueExW(key.Handle, name, IntPtr.Zero, out type, null, ref size);
        if (rc != 0) return false;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var buffer = new byte[size];
            rc = RegQueryValueExW(key.Handle, name, IntPtr.Zero, out type, buffer, ref size);
            if (rc == 0)
            {
                data = size == buffer.Length ? buffer : buffer[..(int)size];
                return true;
            }
            if (rc != ErrorMoreData) return false; // the value changed size in between: ask again
        }
        return false;
    }

    private const int ErrorMoreData = 234;

    [System.Runtime.InteropServices.DllImport("advapi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int RegQueryValueExW(Microsoft.Win32.SafeHandles.SafeRegistryHandle key, string? valueName, IntPtr reserved,
        out uint type, byte[]? data, ref uint dataSize);
}
