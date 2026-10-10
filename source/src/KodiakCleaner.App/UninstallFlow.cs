using System.IO;
using KodiakCleaner.Core.Programs;
using KodiakCleaner.Core.Startup;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App;

/// <summary>
/// Programs > Uninstall: back up the program's registry entries, note what it has on disk, run its uninstaller,
/// then, only if the uninstall really happened, look again and offer to remove whatever it left behind.
/// </summary>
public static class UninstallFlow
{
    /// <summary>Where a program keeps its folders, and what must never be offered (see <see cref="LeftoverPlaces"/>).</summary>
    public static LeftoverPlaces Places(InstalledProgram program, IEnumerable<InstalledProgram> allPrograms)
    {
        static string F(Environment.SpecialFolder f) => Environment.GetFolderPath(f);
        var profile = F(Environment.SpecialFolder.UserProfile);
        var local = F(Environment.SpecialFolder.LocalApplicationData);
        var roaming = F(Environment.SpecialFolder.ApplicationData);
        var others = allPrograms.Where(p => p.Location != program.Location).ToList();
        return new LeftoverPlaces
        {
            AppFolders = new (string, string)[]
            {
                (F(Environment.SpecialFolder.ProgramFiles), "Program Files"),
                (F(Environment.SpecialFolder.ProgramFilesX86), "Program Files (x86)"),
                (F(Environment.SpecialFolder.CommonApplicationData), "ProgramData"),
                (roaming, "AppData\\Roaming"),
                (local, "AppData\\Local"),
                (Path.Combine(local, "Programs"), "AppData\\Local\\Programs"),
                (string.IsNullOrEmpty(profile) ? "" : Path.Combine(profile, "AppData", "LocalLow"), "AppData\\LocalLow"),
            }.Where(a => !string.IsNullOrEmpty(a.Item1)).ToList(),
            StartMenus = new[] { F(Environment.SpecialFolder.Programs), F(Environment.SpecialFolder.CommonPrograms) }.Where(s => s.Length > 0).ToList(),
            Desktops = new[] { F(Environment.SpecialFolder.DesktopDirectory), F(Environment.SpecialFolder.CommonDesktopDirectory) }.Where(s => s.Length > 0).ToList(),
            Protected = new[]
            {
                F(Environment.SpecialFolder.Windows), profile, F(Environment.SpecialFolder.CommonProgramFiles),
                F(Environment.SpecialFolder.CommonProgramFilesX86), Path.Combine(local, "Microsoft"), Path.Combine(roaming, "Microsoft"),
                Path.Combine(local, "Packages"), Path.Combine(local, "Temp"), Path.GetTempPath(), AppContext.BaseDirectory,
                Path.Combine(F(Environment.SpecialFolder.CommonApplicationData), "Microsoft"),
                Path.Combine(F(Environment.SpecialFolder.CommonApplicationData), "Package Cache"),
            }.Where(s => !string.IsNullOrEmpty(s)).ToList(),
            OtherPrograms = others.SelectMany(p => new[] { p.InstallLocation, p.UninstallerFolder }).Where(s => !string.IsNullOrWhiteSpace(s)).ToList(),
            OtherNames = others.Select(p => p.Name).ToList(),
        };
    }

    /// <summary>The program's install folder: its own entry, or the uninstaller's folder when that is inside Program Files.</summary>
    public static string InstallFolder(InstalledProgram p)
    {
        if (!string.IsNullOrWhiteSpace(p.InstallLocation)) return p.InstallLocation;
        var u = p.UninstallerFolder;
        if (u.Length == 0) return "";
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
        };
        return roots.Any(r => r.Length > 0 && LeftoverFinder.IsInside(Path.GetFullPath(u), Path.TrimEndingDirectorySeparator(Path.GetFullPath(r)))) ? u : "";
    }

    /// <summary>Everything the program has that it could leave behind: folders, shortcuts and registry keys.</summary>
    public static List<Leftover> Scan(InstalledProgram program, LeftoverPlaces places)
    {
        var list = LeftoverFinder.FindFiles(program.Name, program.Publisher, InstallFolder(program), places);
        list.AddRange(LeftoverRegistry.Find(program.Name, program.Publisher, places.OtherNames));
        return list;
    }

    /// <summary>Startup entries whose program was inside one of the program's folders and is now gone.</summary>
    public static List<Leftover> DeadStartupEntries(IEnumerable<string> folders)
    {
        var roots = folders.Where(f => Path.IsPathFullyQualified(f)).Select(f => Path.TrimEndingDirectorySeparator(Path.GetFullPath(f))).ToList();
        if (roots.Count == 0) return new();
        var found = new List<Leftover>();
        var all = StartupManager.List();
        StartupHistoryStore.Record(all);
        foreach (var item in all)
        {
            var exe = CommandLine.CheckablePath(Environment.ExpandEnvironmentVariables(item.Command));
            if (exe.Length == 0 || !Path.IsPathFullyQualified(exe) || File.Exists(exe)) continue;
            var full = Path.GetFullPath(exe);
            if (roots.Any(r => LeftoverFinder.IsInside(full, r)))
                found.Add(new Leftover(LeftoverKind.StartupEntry, $"{item.Name} ({item.Location})", "Startup", Item: item));
        }
        return found;
    }

    /// <param name="Done">The leftovers really removed, for the Programs history.</param>
    public sealed record RemoveResult(int Removed, List<string> Failed, string? RegistryBackup, List<Leftover> Done);

    /// <summary>Removes the chosen leftovers: registry keys (backed up first), startup entries (backed up), files to the Recycle Bin.</summary>
    public static RemoveResult Remove(IReadOnlyList<Leftover> chosen, IReadOnlyList<string> publishers)
    {
        int removed = 0;
        var failed = new List<string>();
        var done = new List<Leftover>();
        string? backup = null;

        var keys = chosen.Where(c => c.Kind == LeftoverKind.RegistryKey).ToList();
        if (keys.Count > 0)
        {
            try
            {
                var (b, keyFailures) = LeftoverRegistry.Remove(keys);
                backup = b;
                removed += keys.Count - keyFailures.Count;
                failed.AddRange(keyFailures);
                done.AddRange(keys.Where(k => !keyFailures.Contains(k.Key!.DisplayKeyPath)));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                failed.AddRange(keys.Select(k => k.Path)); // no backup, so nothing was deleted
            }
        }

        foreach (var s in chosen.Where(c => c.Kind == LeftoverKind.StartupEntry && c.Item is StartupItem))
        {
            try
            {
                var item = (StartupItem)s.Item!;
                StartupHistoryStore.Removed(item, StartupManager.Delete(item));
                removed++;
                done.Add(s);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException or OperationCanceledException) { failed.Add(s.Path); }
        }

        var parents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in chosen.Where(c => c.Kind is LeftoverKind.Folder or LeftoverKind.Shortcut))
        {
            try
            {
                if (f.Kind == LeftoverKind.Folder)
                {
                    if (!Directory.Exists(f.Path)) continue;
                    if (new DirectoryInfo(f.Path).Attributes.HasFlag(FileAttributes.ReparsePoint)) { failed.Add(f.Path); continue; }
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(f.Path,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                }
                else
                {
                    if (!File.Exists(f.Path)) continue;
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(f.Path,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                }
                removed++;
                done.Add(f);
                if (Path.GetDirectoryName(f.Path) is { } parent) parents.Add(parent);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or OperationCanceledException or System.Security.SecurityException)
            {
                failed.Add(f.Path);
            }
        }

        // A publisher folder (Program Files\Publisher) that's now empty goes too; one that still holds anything stays.
        foreach (var parent in parents.Where(p => publishers.Contains(Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)))
        {
            try
            {
                var dir = new DirectoryInfo(parent);
                if (!dir.Exists || dir.Attributes.HasFlag(FileAttributes.ReparsePoint) || dir.EnumerateFileSystemInfos().Any()) continue;
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(parent,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or OperationCanceledException) { }
        }
        return new RemoveResult(removed, failed, backup, done);
    }

    /// <summary>True while an uninstaller started since <paramref name="since"/> is still running (NSIS, Inno Setup, InstallShield and the like).</summary>
    public static bool UninstallHelperRunning(DateTime since)
    {
        var temp = Path.GetFullPath(Path.GetTempPath());
        foreach (var p in System.Diagnostics.Process.GetProcesses())
        {
            using (p)
            {
                try
                {
                    var n = p.ProcessName;
                    // NSIS runs "Un.exe" or "Au_.exe" from Temp; Inno Setup "_iu*.tmp"; others call themselves unins*/uninst*.
                    if (n.Equals("Un", StringComparison.OrdinalIgnoreCase) || n.StartsWith("Au_", StringComparison.OrdinalIgnoreCase) || n.StartsWith("Un_", StringComparison.OrdinalIgnoreCase) ||
                        n.StartsWith("_iu", StringComparison.OrdinalIgnoreCase) || n.Contains("unins", StringComparison.OrdinalIgnoreCase) ||
                        n.Contains("uninst", StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (p.StartTime >= since.AddSeconds(-2) && p.MainModule?.FileName is { } file &&
                        file.StartsWith(temp, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException) { }
            }
        }
        return false;
    }
}
