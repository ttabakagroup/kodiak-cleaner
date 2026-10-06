using System.Runtime.Versioning;
using Microsoft.Win32;
using KodiakCleaner.Core.RegistryTools;

namespace KodiakCleaner.Core.Programs;

/// <summary>Finds and removes the registry keys a program leaves under SOFTWARE. Exact names only.</summary>
[SupportedOSPlatform("windows")]
public static class LeftoverRegistry
{
    private static readonly (RegistryHive Hive, RegistryView View)[] Places =
    {
        (RegistryHive.CurrentUser, RegistryView.Default),
        (RegistryHive.LocalMachine, RegistryView.Registry64),
        (RegistryHive.LocalMachine, RegistryView.Registry32),
    };

    public static List<Leftover> Find(string name, string publisher, IEnumerable<string> otherNames)
    {
        var others = new HashSet<string>(otherNames.SelectMany(LeftoverNames.ForProgram), StringComparer.OrdinalIgnoreCase);
        var names = LeftoverNames.ForProgram(name).Where(n => !others.Contains(n)).ToList();
        var publishers = LeftoverNames.ForPublisher(publisher);
        var found = new List<Leftover>();
        foreach (var (hive, view) in Places)
        {
            using var root = RegistryKey.OpenBaseKey(hive, view);
            foreach (var n in names)
            {
                Add(root, hive, view, $@"SOFTWARE\{n}");
                foreach (var p in publishers) Add(root, hive, view, $@"SOFTWARE\{p}\{n}");
            }
        }
        return found.GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();

        void Add(RegistryKey root, RegistryHive hive, RegistryView view, string sub)
        {
            try
            {
                using var key = root.OpenSubKey(sub);
                if (key is null) return;
                var loc = new RegistryLocation(hive, view, sub);
                found.Add(new Leftover(LeftoverKind.RegistryKey, loc.DisplayKeyPath, "Registry", loc));
            }
            catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
        }
    }

    /// <summary>Backs up every key to one .reg file, then deletes them. Returns the backup path and the keys that couldn't be removed.</summary>
    public static (string Backup, List<string> Failed) Remove(IReadOnlyList<Leftover> keys)
    {
        var locations = keys.Where(k => k.Key is not null).Select(k => k.Key!).ToList();
        var backup = RegistryBackup.Save(locations, "Uninstall_Leftovers");
        var failed = new List<string>();
        foreach (var loc in locations)
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(loc.Hive, loc.View);
                root.DeleteSubKeyTree(loc.SubKey, throwOnMissingSubKey: false);
                // SOFTWARE\Publisher is removed too, but only when nothing at all is left in it.
                var parent = loc.SubKey[..Math.Max(0, loc.SubKey.LastIndexOf('\\'))];
                if (parent.Count(c => c == '\\') == 1 && !string.Equals(parent, "SOFTWARE", StringComparison.OrdinalIgnoreCase))
                {
                    bool empty;
                    using (var p = root.OpenSubKey(parent)) empty = p is not null && p.SubKeyCount == 0 && p.ValueCount == 0;
                    if (empty) root.DeleteSubKey(parent, throwOnMissingSubKey: false);
                }
            }
            catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException or ArgumentException)
            {
                failed.Add(loc.DisplayKeyPath);
            }
        }
        return (backup, failed);
    }
}
