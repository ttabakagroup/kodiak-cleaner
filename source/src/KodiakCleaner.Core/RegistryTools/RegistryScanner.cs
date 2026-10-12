using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.Core.RegistryTools;

/// <summary>
/// Finds registry entries that point at files which no longer exist. Deliberately conservative:
/// it only reports entries whose target is on a present, fixed local drive and is definitely gone.
/// </summary>
[SupportedOSPlatform("windows")]
public static class RegistryScanner
{
    public const string CatActiveX = "ActiveX and Class Issues";
    public const string CatAppPaths = "Application Paths";
    public const string CatApplications = "Applications";
    public const string CatFonts = "Fonts";
    public const string CatHelp = "Help Files";
    public const string CatInstaller = "Installer";
    public const string CatSharedDlls = "Missing Shared DLLs";
    public const string CatMuiCache = "MUI Cache";
    public const string CatUninstall = "Obsolete Software";
    public const string CatStartup = "Run At Startup";
    public const string CatSounds = "Sound Events";
    public const string CatMenuOrder = "Start Menu Ordering";
    public const string CatTypeLibs = "Type Libraries";
    public const string CatExtensions = "Unused File Extensions";
    public const string CatServices = "Windows Services";

    /// <summary>Every check, A to Z (Ted's rule for every list, 0.8.21). The 0.8.30 list follows Ted's screenshot.</summary>
    public static readonly string[] Categories =
        new[] { CatActiveX, CatAppPaths, CatApplications, CatFonts, CatHelp, CatInstaller, CatSharedDlls, CatMuiCache,
                CatUninstall, CatStartup, CatSounds, CatMenuOrder, CatTypeLibs, CatExtensions, CatServices }
            .OrderBy(c => c, StringComparer.CurrentCultureIgnoreCase).ToArray();

    private static readonly (RegistryHive Hive, RegistryView View)[] SoftwareViews =
    {
        (RegistryHive.LocalMachine, RegistryView.Registry64),
        (RegistryHive.LocalMachine, RegistryView.Registry32),
        (RegistryHive.CurrentUser, RegistryView.Registry64),
    };

    private static string WindowsFolder => Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    public static List<RegistryIssue> Scan(ISet<string> categories, CancellationToken ct = default)
    {
        var checks = new (string Category, Action<List<RegistryIssue>> Run)[]
        {
            (CatActiveX, ScanClasses), (CatAppPaths, ScanAppPaths), (CatApplications, ScanApplications), (CatFonts, ScanFonts),
            (CatHelp, ScanHelpFiles), (CatInstaller, ScanInstallerFolders), (CatSharedDlls, ScanSharedDlls), (CatMuiCache, ScanMuiCache),
            (CatUninstall, ScanUninstall), (CatStartup, ScanRunKeys), (CatSounds, ScanSounds), (CatMenuOrder, ScanMenuOrder),
            (CatTypeLibs, ScanTypeLibs), (CatExtensions, ScanExtensions), (CatServices, ScanServices),
        };
        var issues = new List<RegistryIssue>();
        foreach (var (category, run) in checks)
        {
            ct.ThrowIfCancellationRequested();
            if (categories.Contains(category)) Guard(() => run(issues));
        }
        // The same physical key can surface through two views on some systems. Keys Windows protects are its own
        // components, and not even an administrator may change them, so they are never listed (Ted, 0.8.65).
        return issues.GroupBy(i => i.Location.ToString(), StringComparer.OrdinalIgnoreCase).Select(g => g.First())
            .Where(i => !ProtectedByWindows(i.Location)).ToList();
    }

    /// <summary>Backs up every affected key/value to one .reg file (unless told not to), then deletes them.</summary>
    public static (string? BackupPath, int Fixed, List<string> Errors) Fix(IReadOnlyCollection<RegistryIssue> issues, bool backup = true)
    {
        string? backupPath = null;
        if (backup)
        {
            try { backupPath = RegistryBackup.Save(issues.Select(i => i.Location), "registry-fix"); }
            catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException)
            {
                // No backup, no changes: nothing is deleted when the backup can't be saved.
                return (null, 0, new List<string> { $"The backup couldn't be saved, so nothing was changed: {e.Message}" });
            }
        }
        int fixedCount = 0;
        var errors = new List<string>();

        foreach (var issue in issues)
        {
            var loc = issue.Location;
            if (ProtectedByWindows(loc))
            {
                errors.Add($"{loc}: {ProtectedMessage}");
                continue;
            }
            try
            {
                using var root = RegistryKey.OpenBaseKey(loc.Hive, loc.View);
                if (loc.IsWholeKey)
                {
                    root.DeleteSubKeyTree(loc.SubKey, throwOnMissingSubKey: false);
                }
                else
                {
                    using var key = root.OpenSubKey(loc.SubKey, writable: true);
                    key?.DeleteValue(loc.ValueName!, throwOnMissingValue: false);
                }
                fixedCount++;
            }
            catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException)
            {
                errors.Add($"{loc}: Windows didn't allow this change, even as administrator. The entry, or a key inside it, is protected, so it was left as it is.");
            }
            catch (IOException e)
            {
                errors.Add($"{loc}: {e.Message}");
            }
        }
        return (backupPath, fixedCount, errors);
    }

    /// <summary>NT SERVICE\TrustedInstaller, the owner of Windows' own registry keys and files.</summary>
    public const string TrustedInstallerSid = "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464";

    public const string ProtectedMessage =
        "Windows protects this entry (it belongs to Windows itself, through TrustedInstaller), so not even an administrator " +
        "may remove it. It was left as it is; Windows repairs or replaces its own entries with updates.";

    /// <summary>
    /// True when the key (or, for a value, the key holding it) is owned by TrustedInstaller. Kodiak Cleaner never takes
    /// ownership of Windows' own keys: removing a Windows component's registration can break Windows features.
    /// </summary>
    public static bool ProtectedByWindows(RegistryLocation loc)
    {
        try
        {
            using var root = RegistryKey.OpenBaseKey(loc.Hive, loc.View);
            using var key = root.OpenSubKey(loc.SubKey, RegistryKeyPermissionCheck.Default, RegistryRights.ReadPermissions);
            var owner = key?.GetAccessControl(AccessControlSections.Owner).GetOwner(typeof(SecurityIdentifier));
            return string.Equals(owner?.Value, TrustedInstallerSid, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException or InvalidOperationException)
        {
            return false;
        }
    }

    private static void Guard(Action a)
    {
        try { a(); }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException) { }
    }

    /// <summary>Opens a key read-only, or null when it isn't there or can't be read.</summary>
    private static RegistryKey? Open(RegistryKey parent, string name)
    {
        try { return parent.OpenSubKey(name); }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException) { return null; }
    }

    private static string? DefaultValue(RegistryKey? key) =>
        key?.GetValue(null, null, RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString();

    /// <summary>Missing, and not a Windows file (those are Windows' own business and may come back with a feature or update).</summary>
    private static bool MissingOutsideWindows(string? path) =>
        !string.IsNullOrWhiteSpace(path) && !RegistryPaths.IsInFolder(path, WindowsFolder) && PathChecks.IsMissing(path);

    /// <summary>
    /// COM classes (ActiveX controls and other components) whose program file is gone. The whole class key is removed,
    /// but only when every server it lists is missing.
    /// </summary>
    private static void ScanClasses(List<RegistryIssue> issues)
    {
        foreach (var (hive, view, sub) in ClassesRoots(@"CLSID"))
        {
            using var root = RegistryKey.OpenBaseKey(hive, view);
            using var clsids = Open(root, sub);
            if (clsids is null) continue;
            foreach (var id in clsids.GetSubKeyNames())
            {
                using var cls = Open(clsids, id);
                if (cls is null) continue;
                var servers = new List<string>();
                foreach (var kind in new[] { "InprocServer32", "LocalServer32" })
                {
                    using var server = Open(cls, kind);
                    var path = CommandLine.CheckablePath(DefaultValue(server));
                    if (server is not null) servers.Add(path);
                }
                if (servers.Count == 0 || !servers.All(MissingOutsideWindows)) continue;
                var name = DefaultValue(cls);
                issues.Add(new RegistryIssue
                {
                    Category = CatActiveX,
                    Description = string.IsNullOrWhiteSpace(name) ? "Component points to a missing file" : $"'{name}' points to a missing file",
                    Data = servers[0],
                    Location = new RegistryLocation(hive, view, $@"{sub}\{id}"),
                });
            }
        }
    }

    /// <summary>HKCU and HKLM (both views) Classes keys for one area, such as CLSID or TypeLib.</summary>
    private static IEnumerable<(RegistryHive, RegistryView, string)> ClassesRoots(string area)
    {
        yield return (RegistryHive.CurrentUser, RegistryView.Registry64, $@"Software\Classes\{area}");
        yield return (RegistryHive.LocalMachine, RegistryView.Registry64, $@"SOFTWARE\Classes\{area}");
        yield return (RegistryHive.LocalMachine, RegistryView.Registry32, $@"SOFTWARE\Classes\{area}");
    }

    /// <summary>"Open with" registrations (Classes\Applications\app.exe) for programs that are gone.</summary>
    private static void ScanApplications(List<RegistryIssue> issues)
    {
        foreach (var (hive, view, sub) in ClassesRoots("Applications"))
        {
            using var root = RegistryKey.OpenBaseKey(hive, view);
            using var apps = Open(root, sub);
            if (apps is null) continue;
            foreach (var app in apps.GetSubKeyNames())
            {
                using var command = Open(apps, $@"{app}\shell\open\command");
                var exe = CommandLine.CheckablePath(DefaultValue(command));
                if (command is null || !MissingOutsideWindows(exe)) continue;
                issues.Add(new RegistryIssue
                {
                    Category = CatApplications,
                    Description = $"'{app}' is in Open With but no longer exists",
                    Data = exe,
                    Location = new RegistryLocation(hive, view, $@"{sub}\{app}"),
                });
            }
        }
    }

    /// <summary>Installed fonts whose font file is gone (for everyone, and fonts installed for this user).</summary>
    private static void ScanFonts(List<RegistryIssue> issues)
    {
        const string sub = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts";
        var fontsFolder = Path.Combine(WindowsFolder, "Fonts");
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var key = Open(root, sub);
            if (key is null) continue;
            foreach (var name in key.GetValueNames())
            {
                var file = RegistryPaths.FontFile(key.GetValue(name)?.ToString(), fontsFolder);
                if (!PathChecks.IsMissing(file)) continue;
                issues.Add(new RegistryIssue
                {
                    Category = CatFonts,
                    Description = $"Font '{name}' is missing its file",
                    Data = file,
                    Location = new RegistryLocation(hive, RegistryView.Registry64, sub, name),
                });
            }
        }
    }

    /// <summary>Help and HTML Help registrations ("file" = "folder") whose help file is gone.</summary>
    private static void ScanHelpFiles(List<RegistryIssue> issues)
    {
        foreach (var (hive, view) in SoftwareViews)
        foreach (var sub in new[] { @"SOFTWARE\Microsoft\Windows\Help", @"SOFTWARE\Microsoft\Windows\HTML Help" })
        {
            using var root = RegistryKey.OpenBaseKey(hive, view);
            using var key = Open(root, sub);
            if (key is null) continue;
            foreach (var name in key.GetValueNames())
            {
                var file = RegistryPaths.HelpFile(name, key.GetValue(name)?.ToString());
                if (!MissingOutsideWindows(file)) continue;
                issues.Add(new RegistryIssue
                {
                    Category = CatHelp,
                    Description = $"Help file '{name}' is missing",
                    Data = file,
                    Location = new RegistryLocation(hive, view, sub, name),
                });
            }
        }
    }

    /// <summary>Windows Installer's list of folders it created (Installer\Folders) that are already gone.</summary>
    private static void ScanInstallerFolders(List<RegistryIssue> issues)
    {
        const string sub = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\Folders";
        using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = Open(root, sub);
        if (key is null) return;
        foreach (var folder in key.GetValueNames())
        {
            // Names end in a backslash ("C:\App\"); the folder is checked without it (never a bare drive).
            var path = folder.TrimEnd('\\');
            if (path.Length <= 3 || !MissingOutsideWindows(path)) continue;
            issues.Add(new RegistryIssue
            {
                Category = CatInstaller,
                Description = "Installer folder no longer exists",
                Data = folder,
                Location = new RegistryLocation(RegistryHive.LocalMachine, RegistryView.Registry64, sub, folder),
            });
        }
    }

    /// <summary>Sound scheme entries that play a sound file that's gone. Fixing clears the sound (Windows plays none).</summary>
    private static void ScanSounds(List<RegistryIssue> issues)
    {
        const string sub = @"AppEvents\Schemes\Apps";
        using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        using var apps = Open(root, sub);
        if (apps is null) return;
        foreach (var app in apps.GetSubKeyNames())
        {
            using var appKey = Open(apps, app);
            if (appKey is null) continue;
            foreach (var evt in appKey.GetSubKeyNames())
            {
                using var evtKey = Open(appKey, evt);
                if (evtKey is null) continue;
                foreach (var scheme in evtKey.GetSubKeyNames())
                {
                    using var schemeKey = Open(evtKey, scheme);
                    var file = DefaultValue(schemeKey)?.Trim().Trim('"');
                    if (!PathChecks.IsMissing(file)) continue;
                    issues.Add(new RegistryIssue
                    {
                        Category = CatSounds,
                        Description = $"Sound for '{evt}' is missing",
                        Data = file!,
                        Location = new RegistryLocation(RegistryHive.CurrentUser, RegistryView.Registry64, $@"{sub}\{app}\{evt}\{scheme}", ""),
                    });
                }
            }
        }
    }

    /// <summary>Saved Start menu ordering for Start menu folders that no longer exist, for this user or for everyone.</summary>
    private static void ScanMenuOrder(List<RegistryIssue> issues)
    {
        const string sub = @"Software\Microsoft\Windows\CurrentVersion\Explorer\MenuOrder\Start Menu2\Programs";
        var mine = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        var everyone = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
        if (string.IsNullOrEmpty(mine) || string.IsNullOrEmpty(everyone)) return;
        using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        using var key = Open(root, sub);
        if (key is null) return;
        foreach (var folder in key.GetSubKeyNames())
        {
            var own = Path.Combine(mine, folder);
            if (!PathChecks.IsMissing(own) || !PathChecks.IsMissing(Path.Combine(everyone, folder))) continue;
            issues.Add(new RegistryIssue
            {
                Category = CatMenuOrder,
                Description = $"Start menu folder '{folder}' no longer exists",
                Data = own,
                Location = new RegistryLocation(RegistryHive.CurrentUser, RegistryView.Registry64, $@"{sub}\{folder}"),
            });
        }
    }

    /// <summary>Type library versions whose library files are all gone. Only that version's key is removed.</summary>
    private static void ScanTypeLibs(List<RegistryIssue> issues)
    {
        foreach (var (hive, view, sub) in ClassesRoots("TypeLib"))
        {
            using var root = RegistryKey.OpenBaseKey(hive, view);
            using var libs = Open(root, sub);
            if (libs is null) continue;
            foreach (var id in libs.GetSubKeyNames())
            {
                using var lib = Open(libs, id);
                if (lib is null) continue;
                foreach (var version in lib.GetSubKeyNames())
                {
                    using var ver = Open(lib, version);
                    if (ver is null) continue;
                    var files = new List<string>();
                    foreach (var lcid in ver.GetSubKeyNames().Where(n => n.All(char.IsAsciiHexDigit)))
                    {
                        using var lang = Open(ver, lcid);
                        if (lang is null) continue;
                        foreach (var platform in lang.GetSubKeyNames())
                        {
                            using var plat = Open(lang, platform);
                            files.Add(RegistryPaths.TypeLibFile(DefaultValue(plat)));
                        }
                    }
                    if (files.Count == 0 || !files.All(MissingOutsideWindows)) continue;
                    var name = DefaultValue(ver);
                    issues.Add(new RegistryIssue
                    {
                        Category = CatTypeLibs,
                        Description = string.IsNullOrWhiteSpace(name) ? "Type library file is missing" : $"Type library '{name}' is missing its file",
                        Data = files[0],
                        Location = new RegistryLocation(hive, view, $@"{sub}\{id}\{version}"),
                    });
                }
            }
        }
    }

    /// <summary>
    /// This user's file extensions that only name a file type that no longer exists anywhere: nothing opens them.
    /// Only plain extension keys with nothing else in them are reported, and only this user's (never Windows' own).
    /// </summary>
    private static void ScanExtensions(List<RegistryIssue> issues)
    {
        const string sub = @"Software\Classes";
        using var user = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var classes = Open(user, sub);
        using var machineClasses = Open(machine, @"SOFTWARE\Classes");
        if (classes is null) return;
        foreach (var ext in classes.GetSubKeyNames().Where(n => n.Length > 1 && n[0] == '.'))
        {
            using var key = Open(classes, ext);
            if (key is null || key.SubKeyCount > 0 || key.ValueCount != 1) continue;
            var progId = DefaultValue(key);
            if (string.IsNullOrWhiteSpace(progId)) continue;
            using var mine = Open(classes, progId);
            using var everyone = machineClasses is null ? null : Open(machineClasses, progId);
            if (mine is not null || everyone is not null) continue;
            issues.Add(new RegistryIssue
            {
                Category = CatExtensions,
                Description = $"'{ext}' files point to a file type that no longer exists",
                Data = progId,
                Location = new RegistryLocation(RegistryHive.CurrentUser, RegistryView.Registry64, $@"{sub}\{ext}"),
            });
        }
    }

    /// <summary>
    /// Services whose program is gone. Only ordinary program services (never drivers), never ones in the Windows folder,
    /// and never ones set to start with Windows' core (boot or system start).
    /// </summary>
    private static void ScanServices(List<RegistryIssue> issues)
    {
        const string sub = @"SYSTEM\CurrentControlSet\Services";
        using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var services = Open(root, sub);
        if (services is null) return;
        foreach (var name in services.GetSubKeyNames())
        {
            using var svc = Open(services, name);
            if (svc is null) continue;
            if (svc.GetValue("Type") is not int type || (type & 0x30) == 0) continue;   // own or shared process programs only
            if (svc.GetValue("Start") is int start && start <= 1) continue;          // boot and system start
            var exe = CommandLine.CheckablePath(svc.GetValue("ImagePath", null, RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString());
            if (!MissingOutsideWindows(exe)) continue;
            var display = svc.GetValue("DisplayName")?.ToString();
            issues.Add(new RegistryIssue
            {
                Category = CatServices,
                Description = $"Service '{(string.IsNullOrWhiteSpace(display) || display.StartsWith('@') ? name : display)}' points to a missing program",
                Data = exe,
                Location = new RegistryLocation(RegistryHive.LocalMachine, RegistryView.Registry64, $@"{sub}\{name}"),
            });
        }
    }

    private static void ScanSharedDlls(List<RegistryIssue> issues)
    {
        const string sub = @"SOFTWARE\Microsoft\Windows\CurrentVersion\SharedDLLs";
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var key = root.OpenSubKey(sub);
            if (key is null) continue;
            foreach (var name in key.GetValueNames())
            {
                if (PathChecks.IsMissing(name))
                    issues.Add(new RegistryIssue
                    {
                        Category = CatSharedDlls,
                        Description = "Shared file is missing",
                        Data = name,
                        Location = new RegistryLocation(RegistryHive.LocalMachine, view, sub, name),
                    });
            }
        }
    }

    private static void ScanAppPaths(List<RegistryIssue> issues)
    {
        const string sub = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
        foreach (var (hive, view) in SoftwareViews)
        {
            using var root = RegistryKey.OpenBaseKey(hive, view);
            using var key = root.OpenSubKey(sub);
            if (key is null) continue;
            foreach (var appName in key.GetSubKeyNames())
            {
                using var app = key.OpenSubKey(appName);
                var target = app?.GetValue(null, null, RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString();
                if (string.IsNullOrWhiteSpace(target)) continue;
                var exe = CommandLine.CheckablePath(target);
                if (PathChecks.IsMissing(exe))
                    issues.Add(new RegistryIssue
                    {
                        Category = CatAppPaths,
                        Description = $"'{appName}' points to a missing program",
                        Data = exe,
                        Location = new RegistryLocation(hive, view, $@"{sub}\{appName}"),
                    });
            }
        }
    }

    private static void ScanRunKeys(List<RegistryIssue> issues)
    {
        const string sub = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        foreach (var (hive, view) in SoftwareViews)
        {
            using var root = RegistryKey.OpenBaseKey(hive, view);
            using var key = root.OpenSubKey(sub);
            if (key is null) continue;
            foreach (var name in key.GetValueNames())
            {
                if (string.IsNullOrEmpty(name)) continue;
                var cmd = key.GetValue(name)?.ToString();
                var exe = CommandLine.CheckablePath(cmd);
                if (PathChecks.IsMissing(exe))
                    issues.Add(new RegistryIssue
                    {
                        Category = CatStartup,
                        Description = $"Startup entry '{name}' points to a missing program",
                        Data = cmd ?? "",
                        Location = new RegistryLocation(hive, view, sub, name),
                    });
            }
        }
    }

    private static void ScanUninstall(List<RegistryIssue> issues)
    {
        const string sub = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        foreach (var (hive, view) in SoftwareViews)
        {
            using var root = RegistryKey.OpenBaseKey(hive, view);
            using var key = root.OpenSubKey(sub);
            if (key is null) continue;
            foreach (var entry in key.GetSubKeyNames())
            {
                using var e = key.OpenSubKey(entry);
                if (e is null) continue;
                var name = e.GetValue("DisplayName")?.ToString();
                var uninstall = e.GetValue("UninstallString")?.ToString();
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(uninstall)) continue;
                if (e.GetValue("WindowsInstaller") is int wi && wi == 1) continue; // MSI-managed: msiexec handles these

                var exe = CommandLine.CheckablePath(uninstall);
                var installLocation = e.GetValue("InstallLocation")?.ToString();
                bool locationGone = string.IsNullOrWhiteSpace(installLocation) || PathChecks.IsMissing(installLocation);
                if (PathChecks.IsMissing(exe) && locationGone)
                    issues.Add(new RegistryIssue
                    {
                        Category = CatUninstall,
                        Description = $"'{name}' is no longer installed but still listed",
                        Data = uninstall,
                        Location = new RegistryLocation(hive, view, $@"{sub}\{entry}"),
                    });
            }
        }
    }

    private static void ScanMuiCache(List<RegistryIssue> issues)
    {
        const string sub = @"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache";
        using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        using var key = root.OpenSubKey(sub);
        if (key is null) return;
        foreach (var name in key.GetValueNames())
        {
            // Value names look like "C:\path\app.exe.FriendlyAppName" or "...exe.ApplicationCompany".
            int dot = name.LastIndexOf('.');
            if (dot <= 0) continue;
            var path = name[..dot];
            if (PathChecks.IsMissing(path))
                issues.Add(new RegistryIssue
                {
                    Category = CatMuiCache,
                    Description = "Cached name for a program that no longer exists",
                    Data = path,
                    Location = new RegistryLocation(RegistryHive.CurrentUser, RegistryView.Registry64, sub, name),
                });
        }
    }
}
