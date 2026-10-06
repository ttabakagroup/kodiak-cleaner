using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using KodiakCleaner.Core.Programs;
using KodiakCleaner.Core.Startup;
using KodiakCleaner.Core.Util;
using System.IO;

namespace KodiakCleaner.App.ViewModels;

public sealed class ProgramItem : ObservableObject
{
    public ProgramItem(InstalledProgram p) => Program = p;
    private bool _isChecked;
    public bool IsChecked { get => _isChecked; set => Set(ref _isChecked, value); }
    private System.Windows.Media.ImageSource? _icon;
    /// <summary>The program's own icon, loaded in the background; null shows a plain placeholder.</summary>
    public System.Windows.Media.ImageSource? Icon { get => _icon; set { if (Set(ref _icon, value)) OnPropertyChanged(nameof(NoIcon)); } }
    public bool NoIcon => _icon is null;
    public InstalledProgram Program { get; }
    public string Name => Program.Name;
    public string Publisher => Program.Publisher;
    public string Version => Program.Version;
    public DateTime? InstallDate => Program.InstallDate;
    public long SizeBytes => Program.SizeBytes;
    public string Size => Program.SizeBytes > 0 ? ByteSize.Format(Program.SizeBytes) : "";
    public string Scope => Program.PerUser ? "You" : "All Users";
}

public sealed class ProgramsViewModel : ObservableObject, ILoadOnFirstVisit
{
    public ProgramsViewModel()
    {
        View = CollectionViewSource.GetDefaultView(Programs);
        View.Filter = o => o is ProgramItem p && (string.IsNullOrWhiteSpace(Filter) ||
            p.Name.Contains(Filter, StringComparison.CurrentCultureIgnoreCase) ||
            p.Publisher.Contains(Filter, StringComparison.CurrentCultureIgnoreCase));
        RefreshCommand = new RelayCommand(Refresh);
        UninstallCommand = new RelayCommand(UninstallAsync, () => !IsBusy && Targets().Count > 0);
        UninstallSelectedCommand = new RelayCommand(UninstallSelectedAsync, () => !IsBusy && Selected?.Program.CanUninstall == true);
        RepairCommand = new RelayCommand(RepairAsync, () => Selected?.Program.CanRepair == true);
        RenameCommand = new RelayCommand(Rename, () => Selected is not null);
        DeleteCommand = new RelayCommand(DeleteEntry, () => Selected is not null);
        HistoryCommand = new RelayCommand(() => { if (ProgramHistoryWindow.Show(null)) Refresh(); });
    }

    private bool _loaded;
    /// <summary>Reads the list the first time the page is shown, so starting Kodiak Cleaner stays quick.</summary>
    public void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        Refresh();
    }

    public ObservableCollection<ProgramItem> Programs { get; } = new();
    public ICollectionView View { get; }
    public ICommand RefreshCommand { get; }
    public ICommand UninstallCommand { get; }
    public ICommand UninstallSelectedCommand { get; }
    public ICommand RepairCommand { get; }
    public ICommand RenameCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand HistoryCommand { get; }

    private ProgramItem? _selected;
    public ProgramItem? Selected { get => _selected; set => Set(ref _selected, value); }

    private string _filter = "";
    public string Filter
    {
        get => _filter;
        set { if (Set(ref _filter, value)) View.Refresh(); }
    }

    private string _summary = "";
    public string Summary { get => _summary; private set => Set(ref _summary, value); }

    /// <summary>The header box: true when every program is ticked, null when some are.</summary>
    public bool? AllChecked
    {
        get => Programs.Count > 0 && Programs.All(p => p.IsChecked) ? true : Programs.Any(p => p.IsChecked) ? null : false;
        set
        {
            var on = value != false;
            // Select All ticks only what the search shows, so a filtered list can be uninstalled as a group.
            foreach (var p in View.Cast<ProgramItem>()) p.IsChecked = on;
            if (!on) foreach (var p in Programs) p.IsChecked = false;
        }
    }

    private void Refresh()
    {
        var ticked = Programs.Where(p => p.IsChecked).Select(p => p.Program.Location).ToHashSet();
        Programs.Clear();
        foreach (var p in ProgramInventory.List())
        {
            var item = new ProgramItem(p) { IsChecked = ticked.Contains(p.Location) };
            item.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ProgramItem.IsChecked)) OnPropertyChanged(nameof(AllChecked)); };
            Programs.Add(item);
        }
        OnPropertyChanged(nameof(AllChecked));
        Summary = $"{Programs.Count} programs installed, {ByteSize.Format(Programs.Sum(p => p.SizeBytes))} reported size.";
        ProgramIcons.LoadInto(Programs.ToList());
    }

    private async Task RepairAsync()
    {
        if (Selected is not { } s) return;
        if (!Dialogs.Confirm($"Repair '{s.Name}'?\n\n" + (s.Program.IsWindowsInstaller
                ? "Windows Installer reinstalls any missing or damaged files. Your settings in the program are kept."
                : "This opens the program's own change or repair screen."))) return;
        try
        {
            var proc = ProgramInventory.StartRepair(s.Program);
            if (proc is null) { Dialogs.Error($"'{s.Name}' doesn't offer a repair."); return; }
            Summary = $"Waiting for the '{s.Name}' repair to finish…";
            await proc.WaitForExitAsync();
            Summary = proc.ExitCode == 0 ? $"'{s.Name}' was repaired." : $"The '{s.Name}' repair ended with code {proc.ExitCode}.";
            ActivityLog.Write("Programs", Summary);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Dialogs.Error($"The repair couldn't start: {e.Message}");
        }
    }

    private void Rename()
    {
        if (Selected is not { } s) return;
        var name = TextPrompt.Ask("Rename Program",
            "The new name shows here and in Windows' Apps list. The program itself isn't changed, and the old entry is backed up first.",
            s.Name);
        if (name is null || string.IsNullOrWhiteSpace(name) || name.Trim() == s.Name) return;
        try
        {
            var backup = ProgramInventory.Rename(s.Program, name);
            ProgramHistoryStore.Add(ProgramChange.Renamed, s.Program, $"Renamed to '{name.Trim()}'", backup);
            Refresh();
            Summary = $"Renamed '{s.Name}' to '{name.Trim()}'. Backup saved to {backup}.";
            ActivityLog.Write("Programs", Summary);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException)
        {
            Dialogs.Error("Renaming a program installed for all users needs administrator rights. Use Restart as Administrator at the top.");
        }
        catch (Exception e) when (e is InvalidOperationException or IOException)
        {
            Dialogs.Error(e.Message);
        }
    }

    private void DeleteEntry()
    {
        if (Selected is not { } s) return;
        if (!Dialogs.Confirm($"Delete '{s.Name}' from the programs list?\n\n" +
                             "Use this only for leftovers whose uninstaller is gone. It removes the entry without uninstalling, " +
                             "so the program's files stay where they are. The entry is backed up first and can be put back from History.")) return;
        try
        {
            var backup = ProgramInventory.RemoveEntry(s.Program);
            ProgramHistoryStore.Add(ProgramChange.DeletedEntry, s.Program, "Removed from the programs list", backup);
            Refresh();
            Summary = $"Deleted the entry for '{s.Name}'. Backup saved to {backup}.";
            ActivityLog.Write("Programs", Summary);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException)
        {
            Dialogs.Error("Deleting a program installed for all users needs administrator rights. Use Restart as Administrator at the top.");
        }
        catch (IOException e)
        {
            Dialogs.Error(e.Message);
        }
    }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }

    /// <summary>The Uninstall button: every ticked program, or the selected one when none are ticked.</summary>
    private List<ProgramItem> Targets() =>
        Programs.Any(p => p.IsChecked) ? Programs.Where(p => p.IsChecked && p.Program.CanUninstall).ToList()
        : Selected is { } s && s.Program.CanUninstall ? new List<ProgramItem> { s } : new List<ProgramItem>();

    private Task UninstallAsync() => UninstallManyAsync(Targets());

    private Task UninstallSelectedAsync() =>
        UninstallManyAsync(Selected is { } s ? new List<ProgramItem> { s } : new List<ProgramItem>());

    /// <summary>Uninstalls the programs strictly one after another, each with the full backup, uninstall and leftovers steps.</summary>
    private async Task UninstallManyAsync(List<ProgramItem> items)
    {
        if (items.Count == 0 || IsBusy) return;
        var intro = $"{AppInfo.Name} backs up each program's registry entries first, runs its own uninstaller, " +
                    "then looks for anything it left behind so you can choose what to remove.";
        if (items.Count == 1
                ? !Dialogs.Confirm($"Uninstall '{items[0].Name}'?\n\n{intro}")
                : !Dialogs.Confirm($"Uninstall these {items.Count} programs one at a time?\n\n" +
                                   string.Join("\n", items.Take(15).Select(i => "• " + i.Name)) + (items.Count > 15 ? "\n…" : "") +
                                   $"\n\n{intro} Each uninstaller finishes before the next one starts.")) return;
        IsBusy = true;
        var results = new List<string>();
        try
        {
            for (int i = 0; i < items.Count; i++)
            {
                var prefix = items.Count > 1 ? $"({i + 1} of {items.Count}) " : "";
                var outcome = await UninstallOneAsync(items[i].Program, prefix);
                results.Add(outcome);
                ActivityLog.Write("Programs", outcome);
                Refresh();
                if (outcome.StartsWith("Stopped")) break; // the person chose to stop the rest
            }
        }
        finally
        {
            IsBusy = false;
            Refresh();
            Summary = items.Count == 1 ? results.FirstOrDefault() ?? Summary
                : $"Finished uninstalling: {results.Count(r => r.StartsWith("Uninstalled"))} of {items.Count} programs removed.";
            if (items.Count > 1 && results.Count > 0) Dialogs.Info(string.Join("\n\n", results));
        }
    }

    /// <summary>
    /// 1. Note what the program has (folders, shortcuts, registry keys). 2. Back up its registry entries.
    /// 3. Run its uninstaller and wait. 4. Only if it's really gone, look again. 5. Offer to remove what's left.
    /// Returns a one-line outcome.
    /// </summary>
    private async Task<string> UninstallOneAsync(InstalledProgram program, string prefix)
    {
        var name = program.Name;
        // 1 and 2: what it has now, and a backup of its registry entries before anything changes.
        Summary = $"{prefix}Backing up '{name}' and noting what it has installed…";
        var installed = ProgramInventory.List();
        var before = await Task.Run(() => UninstallFlow.Scan(program, UninstallFlow.Places(program, installed)));
        string backup;
        try
        {
            backup = await Task.Run(() => ProgramInventory.BackupBeforeUninstall(program,
                before.Where(b => b.Key is not null).Select(b => b.Key!)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return $"'{name}' wasn't uninstalled: the registry backup couldn't be saved ({e.Message}).";
        }

        // 3: the program's own uninstaller, and nothing else starts until it's done.
        System.Diagnostics.Process? proc;
        var started = DateTime.Now;
        try { proc = ProgramInventory.StartUninstall(program); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            return $"'{name}' wasn't uninstalled: its uninstaller couldn't start ({e.Message}). If it's gone, use Delete Entry.";
        }
        if (proc is null) return $"'{name}' wasn't uninstalled: it has no uninstaller.";
        Summary = $"{prefix}Waiting for the '{name}' uninstaller to finish…";
        await proc.WaitForExitAsync();
        switch (await WaitUntilGoneAsync(program, name, started))
        {
            case false: return $"'{name}' is still installed (the uninstall was cancelled or didn't finish), so nothing else was removed. Backup: {backup}";
            case null: return $"Stopped at '{name}', which is still installed. Nothing else was removed. Backup: {backup}";
        }

        ProgramHistoryStore.Add(ProgramChange.Uninstalled, program, "Removed by its own uninstaller", backup);

        // 4: what's left now that it's gone.
        Summary = $"{prefix}Looking for anything '{name}' left behind…";
        var remaining = ProgramInventory.List();
        var after = await Task.Run(() =>
        {
            var found = UninstallFlow.Scan(program, UninstallFlow.Places(program, remaining));
            var folders = before.Concat(found).Where(f => f.Kind == LeftoverKind.Folder).Select(f => f.Path)
                .Append(UninstallFlow.InstallFolder(program)).Where(f => f.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase);
            found.AddRange(UninstallFlow.DeadStartupEntries(folders));
            return found;
        });
        if (after.Count == 0) return $"Uninstalled '{name}'. Nothing was left behind. Backup: {backup}";

        // 5: the person picks what goes.
        var chosen = LeftoversWindow.Ask(name, after);
        if (chosen.Count == 0) return $"Uninstalled '{name}'. {after.Count} leftover item(s) were kept. Backup: {backup}";
        Summary = $"{prefix}Removing {chosen.Count} leftover item(s) of '{name}'…";
        var result = await Task.Run(() => UninstallFlow.Remove(chosen, LeftoverNames.ForPublisher(program.Publisher)));
        ProgramHistoryStore.Leftovers(program, result);
        if (result.Failed.Count > 0)
            Dialogs.Error($"{result.Failed.Count} leftover item(s) of '{name}' couldn't be removed" +
                          (Elevation.IsAdministrator ? " (in use or protected):" : ". Some may need administrator rights (use Restart as Administrator):") +
                          "\n\n" + string.Join("\n", result.Failed.Take(12)) + (result.Failed.Count > 12 ? "\n…" : ""));
        return $"Uninstalled '{name}' and removed {result.Removed} leftover item(s). Backup: {backup}" +
               (result.RegistryBackup is { } rb ? $", removed keys: {rb}" : "");
    }

    /// <summary>
    /// Some uninstallers hand over to a copy of themselves (often in the Temp folder) and exit at once, so the entry
    /// outlives the first process. Waits while such a helper is still running, then asks.
    /// true = really gone; false = still installed; null = still installed and stop the rest.
    /// </summary>
    private static async Task<bool?> WaitUntilGoneAsync(InstalledProgram program, string name, DateTime started)
    {
        // Some uninstallers remove their entry early and keep working (deleting files, asking questions), so
        // "gone" also needs the uninstaller windows to have closed for a few seconds before leftovers are scanned.
        var deadline = DateTime.Now.AddMinutes(30);
        int quiet = 0;
        while (DateTime.Now < deadline)
        {
            quiet = UninstallHelperRunning(started) ? 0 : quiet + 1;
            bool installed = ProgramInventory.IsStillInstalled(program);
            if (!installed && quiet >= 3) return true;
            if (installed && quiet >= 5) break; // no uninstaller window left, and it's still installed
            await Task.Delay(1000);
        }
        while (ProgramInventory.IsStillInstalled(program))
        {
            var answer = Dialogs.YesNoCancel($"'{name}' is still listed as installed.\n\nIf its uninstaller is still open, finish it, then click Yes to look for leftovers. " +
                                             "Click No if you cancelled this uninstall, or Cancel to stop uninstalling any more programs.");
            if (answer is null) return null;
            if (answer == false) return false;
        }
        return true;
    }

    /// <summary>True while an uninstaller started since <paramref name="since"/> is still running (NSIS, Inno Setup, InstallShield and the like).</summary>
    private static bool UninstallHelperRunning(DateTime since)
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
