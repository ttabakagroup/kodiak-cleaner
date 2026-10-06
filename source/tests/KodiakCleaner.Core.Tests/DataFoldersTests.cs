using KodiakCleaner.Core.Util;

namespace KodiakCleaner.Core.Tests;

public sealed class DataFoldersTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "kc-data-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private string Make(params string[] parts)
    {
        var path = Path.Combine(new[] { _dir }.Concat(parts).ToArray());
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, parts[^1]);
        return path;
    }

    [Fact]
    public void Installed_copies_use_documents_and_keep_each_kind_in_its_own_folder()
    {
        var data = DataFolders.Installed(Path.Combine(_dir, "Documents"));
        Assert.Equal(Path.Combine(_dir, "Documents", "Kodiak Cleaner"), data.Root);
        Assert.Equal(Path.Combine(data.Root, "Backups", "Registry"), data.Registry);
        Assert.Equal(Path.Combine(data.Root, "Logs"), data.Logs);
        var names = data.All.Select(Path.GetFileName).ToList();
        Assert.Equal(new[] { "Event Logs", "Programs History", "Registry", "Settings", "Startup History", "Logs" }, names);
        Assert.Equal(names.Take(5).OrderBy(n => n, StringComparer.OrdinalIgnoreCase), names.Take(5)); // A to Z
    }

    [Fact]
    public void Old_backups_beside_the_program_move_into_the_new_folders()
    {
        Make("Program", "Backups", "2026-10-01_120000_registry-fix.reg");
        Make("Program", "Backups", "2026-10-01_120500_Settings.ini");
        Make("Program", "Backups", "2026-10-01_120500_Startup History.json");
        Make("Program", "Backups", "Startup History", "history.json");
        Make("Program", "Backups", "Event Logs", "Application.evtx");
        var data = DataFolders.Installed(Path.Combine(_dir, "Documents"));

        Assert.Equal(5, data.MoveFromOldLayout(Path.Combine(_dir, "Program", "Backups")));
        Assert.True(File.Exists(Path.Combine(data.Registry, "2026-10-01_120000_registry-fix.reg")));
        Assert.True(File.Exists(Path.Combine(data.Settings, "2026-10-01_120500_Settings.ini")));
        Assert.True(File.Exists(Path.Combine(data.Settings, "2026-10-01_120500_Startup History.json")));
        Assert.True(File.Exists(Path.Combine(data.StartupHistory, "history.json")));
        Assert.True(File.Exists(Path.Combine(data.EventLogs, "Application.evtx")));
        Assert.False(Directory.Exists(Path.Combine(_dir, "Program", "Backups"))); // emptied and removed
        Assert.Equal(0, data.MoveFromOldLayout(Path.Combine(_dir, "Program", "Backups")));
    }

    [Fact]
    public void A_portable_copy_sorts_its_own_backups_folder_and_never_overwrites()
    {
        var data = new DataFolders(Path.Combine(_dir, "Portable"));
        Make("Portable", "Backups", "a.reg");
        Make("Portable", "Backups", "Registry", "b.reg");
        Make("Portable", "Backups", "Programs History", "history.json");
        var clash = Make("Portable", "Backups", "b.reg");

        Assert.Equal(1, data.MoveFromOldLayout(data.Backups));
        Assert.True(File.Exists(Path.Combine(data.Registry, "a.reg")));
        Assert.True(File.Exists(clash)); // the copy already in Registry wins; this one stays put
        Assert.True(File.Exists(Path.Combine(data.ProgramsHistory, "history.json")));
    }

    [Fact]
    public void History_records_still_find_backups_after_the_move()
    {
        var data = new DataFolders(Path.Combine(_dir, "Data"));
        var moved = Make("Data", "Backups", "Registry", "old.reg");
        Assert.Equal(moved, data.Find("old.reg"));
        Assert.Equal(moved, data.Find(@"C:\Program Files\Kodiak Cleaner\Backups\old.reg".Replace('\\', Path.DirectorySeparatorChar)));
        Assert.Null(data.Find("missing.reg"));
        Assert.Null(data.Find(null));
    }

    [Fact]
    public void Delete_all_removes_backups_and_logs_but_nothing_else()
    {
        var data = new DataFolders(Path.Combine(_dir, "Data"));
        Make("Data", "Backups", "Registry", "x.reg");
        Make("Data", "Logs", "Kodiak Cleaner 2026-10.log");
        var mine = Make("Data", "my notes.txt");
        data.DeleteAll();
        Assert.False(Directory.Exists(data.Backups));
        Assert.False(Directory.Exists(data.Logs));
        Assert.True(File.Exists(mine));

        File.Delete(mine);
        Make("Data", "Logs", "Kodiak Cleaner 2026-11.log");
        data.DeleteAll();
        Assert.False(Directory.Exists(data.Root)); // nothing else left: the folder goes too
    }
}

[Collection("ActivityLog")]
public sealed class ActivityLogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "kc-log-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        ActivityLog.Folder = null;
        ActivityLog.Enabled = true;
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Writes_one_file_per_month_with_a_header()
    {
        ActivityLog.Folder = _dir;
        ActivityLog.Write("Cleaner", "Cleaned 3 item(s).\nSecond line", new DateTime(2026, 10, 6, 9, 5, 7));
        ActivityLog.Write("Registry", "Fixed 2 issue(s).", new DateTime(2026, 10, 6, 9, 6, 0));
        var text = File.ReadAllText(Path.Combine(_dir, "Kodiak Cleaner 2026-10.log"));
        Assert.StartsWith("Kodiak Cleaner activity log for October 2026. It stays on this PC", text);
        Assert.Contains("2026-10-06 09:05:07  Cleaner     Cleaned 3 item(s). Second line\r\n", text);
        Assert.Contains("2026-10-06 09:06:00  Registry    Fixed 2 issue(s).\r\n", text);
    }

    [Fact]
    public void Nothing_is_written_when_off_and_only_twelve_months_are_kept()
    {
        ActivityLog.Folder = _dir;
        ActivityLog.Enabled = false;
        ActivityLog.Write("Cleaner", "x", new DateTime(2026, 1, 1));
        Assert.False(Directory.Exists(_dir));

        ActivityLog.Enabled = true;
        for (int m = 0; m < 14; m++) ActivityLog.Write("Start", "started", new DateTime(2025, 9, 1).AddMonths(m));
        var files = Directory.GetFiles(_dir).Select(Path.GetFileName).OrderBy(f => f).ToList();
        Assert.Equal(ActivityLog.MonthsKept, files.Count);
        Assert.Equal("Kodiak Cleaner 2025-11.log", files[0]);
        Assert.Equal("Kodiak Cleaner 2026-10.log", files[^1]);
    }
}

public class UninstallTests
{
    private static readonly string Wxs = Repo.Read("installer", "Package.wxs");

    [Fact]
    public void Removing_runs_the_cleanup_but_upgrading_never_does()
    {
        foreach (var action in new[] { "UninstallUserCleanup", "UninstallSystemCleanup" })
            Assert.Contains($"<Custom Action=\"{action}\" Before=\"", Wxs);
        Assert.Contains("ExeCommand=\"--uninstall-user [UILevel] [DELETEUSERDATA]\"", Wxs);
        Assert.Contains("ExeCommand=\"--uninstall-system\"", Wxs);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(Wxs, "Condition='REMOVE~=\"ALL\" AND NOT UPGRADINGPRODUCTCODE'").Count);
        var app = Repo.Read("src", "KodiakCleaner.App", "UninstallCleanup.cs");
        Assert.Contains("UserArg = \"--uninstall-user\", SystemArg = \"--uninstall-system\"", app);
    }

    [Fact]
    public void The_start_menu_shortcut_keeps_its_machine_key_and_the_ice_checks_that_disagree_are_off()
    {
        // ICE38/ICE43/ICE57 (only run by the Windows build) want an HKCU key path for a Start menu shortcut, which is wrong
        // for an all-users install and would make upgrades delete the shortcut, so they are suppressed (0.8.38).
        var shortcut = System.Xml.Linq.XDocument.Parse(Wxs).Descendants().Single(e => e.Name.LocalName == "Component" && (string?)e.Attribute("Id") == "StartMenuShortcut");
        var keyPath = shortcut.Elements().Single(e => (string?)e.Attribute("KeyPath") == "yes");
        Assert.Equal("RegistryValue", keyPath.Name.LocalName);
        Assert.Equal("HKLM", (string?)keyPath.Attribute("Root"));
        Assert.Contains("Scope=\"perMachine\"", Wxs);
        Assert.Contains("<SuppressIces>ICE38;ICE43;ICE57</SuppressIces>", Repo.Read("installer", "KodiakCleaner.Installer.wixproj"));
    }

    [Fact]
    public void Program_files_hold_no_writable_backups_folder_and_old_backups_are_removed()
    {
        // Only KodiakCleaner.ini is writable for normal users; backups and logs live in Documents\Kodiak Cleaner.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(Wxs, "<PermissionEx "));
        Assert.DoesNotContain("<CreateFolder", Wxs);
        foreach (var dir in new[] { "BACKUPSFOLDER", "OLDEVENTLOGS", "OLDPROGRAMSHISTORY", "OLDSTARTUPHISTORY" })
        {
            Assert.Contains($"Directory=\"{dir}\" Name=\"*\" On=\"uninstall\"", Wxs);
            Assert.Contains($"Directory=\"{dir}\" On=\"uninstall\"", Wxs);
        }
    }
}
