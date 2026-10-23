using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using KodiakCleaner.Core.RegistryTools;
using KodiakCleaner.Core.Tools;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App.ViewModels;

/// <summary>One row in the Driver Cleaner list: a driver package, a leftover folder or a leftover registry key.</summary>
public sealed class DriverItem : ObservableObject
{
    public DriverItem(string kind, string name, string driverClass, string version, string location, bool inUse, bool isChecked)
    {
        Kind = kind;
        Name = name;
        DriverClass = driverClass;
        Version = version;
        Location = location;
        InUse = inUse ? "Yes" : "";
        _isChecked = isChecked;
    }

    public string Kind { get; }
    public string Name { get; }
    public string DriverClass { get; }
    public string Version { get; }
    public string Location { get; }
    public string InUse { get; }
    public DriverPackage? Package { get; init; }
    public string? Folder { get; init; }
    public RegistryLocation? Key { get; init; }

    private bool _isChecked;
    public bool IsChecked { get => _isChecked; set => Set(ref _isChecked, value); }
}

/// <summary>A graphics adapter shown at the top of the page, with a link to its maker's newest driver.</summary>
public sealed class AdapterItem
{
    public AdapterItem(GraphicsAdapter adapter)
    {
        Name = adapter.Name;
        Vendor = adapter.Vendor;
        Detail = string.Join("  ·  ", new[] { adapter.Provider, adapter.DriverVersion.Length > 0 ? "Driver " + adapter.DriverVersion : "", adapter.DriverDate }
            .Where(s => s.Length > 0));
        GetDriverCommand = new RelayCommand(() => { if (Vendor is { } v) Shell.OpenUrl(DriverCleaner.DownloadUrl(v)); }, () => Vendor is not null);
    }

    public string Name { get; }
    public string Detail { get; }
    public DriverVendor? Vendor { get; }
    public ICommand GetDriverCommand { get; }
}

/// <summary>
/// Driver Cleaner (Ted, 2026-10-12): removes a graphics driver completely (driver store packages, leftover folders and registry
/// keys) so a fresh driver installs cleanly, optionally from Safe Mode, then restarts or shuts down and links to the maker's driver.
/// </summary>
public sealed class DriverCleanerViewModel : ObservableObject, ILoadOnFirstVisit
{
    public DriverCleanerViewModel()
    {
        RefreshCommand = new RelayCommand(RefreshAsync, () => !IsBusy);
        CleanCommand = new RelayCommand(CleanAsync, () => !IsBusy && Items.Any(i => i.IsChecked));
        SafeModeCommand = new RelayCommand(SafeModeAsync, () => !IsBusy);
        GetDriverCommand = new RelayCommand(() => Shell.OpenUrl(DriverCleaner.DownloadUrl(Vendor)));
        foreach (var v in Enum.GetValues<DriverVendor>().OrderBy(DriverCleaner.Name, StringComparer.OrdinalIgnoreCase))
            Vendors.Add(new Choice<DriverVendor>(v == DriverVendor.Realtek ? "Realtek Audio" : DriverCleaner.Name(v), v, false, PickVendor));
    }

    public ObservableCollection<DriverItem> Items { get; } = new();
    public ObservableCollection<AdapterItem> Adapters { get; } = new();
    public List<Choice<DriverVendor>> Vendors { get; } = new();
    public bool HasAdapters => Adapters.Count > 0;
    public ICommand RefreshCommand { get; }
    public ICommand CleanCommand { get; }
    public ICommand SafeModeCommand { get; }
    public ICommand GetDriverCommand { get; }

    public bool IsSafeMode { get; } = DriverTools.IsSafeMode;
    public string SafeModeTitle => IsSafeMode ? "Running in Safe Mode" : "Safe Mode";
    public string SafeModeText => IsSafeMode
        ? "Windows is in Safe Mode, so no driver is loaded and everything can be removed. Restarting from here starts Windows normally again."
        : "For the cleanest removal, restart in Safe Mode. Kodiak Cleaner opens on this page there. Download the new driver first: Safe Mode has no internet.";
    public string SafeModeButton => IsSafeMode ? "Restart Normally" : "Restart in Safe Mode";

    private DriverVendor _vendor = DriverVendor.Nvidia;
    public DriverVendor Vendor { get => _vendor; private set { Set(ref _vendor, value); OnPropertyChanged(nameof(VendorName)); } }
    public string VendorName => DriverCleaner.Name(Vendor);
    public string GetDriverText => $"Get the Latest {VendorName} Driver";

    private bool _backUpPackages = true;
    /// <summary>Copies each driver package to Backups\Drivers before it is removed, so it can be put back.</summary>
    public bool BackUpPackages { get => _backUpPackages; set => Set(ref _backUpPackages, value); }

    private bool _createRestorePoint = true;
    public bool CreateRestorePoint { get => _createRestorePoint; set => Set(ref _createRestorePoint, value); }

    /// <summary>Live Windows setting, not a Kodiak Cleaner one: whether Windows Update may put a driver back on its own.</summary>
    public bool WindowsUpdateDrivers
    {
        get => DriverTools.WindowsUpdateInstallsDrivers;
        set
        {
            if (value == DriverTools.WindowsUpdateInstallsDrivers) return;
            if (!Elevation.IsAdministrator)
                Dialogs.Error("Changing this needs administrator rights. Use Restart as Administrator.");
            else if (DriverTools.SetWindowsUpdateInstallsDrivers(value))
                ActivityLog.Write("Driver Cleaner", value ? "Windows Update may install drivers again" : "Windows Update driver installs turned off");
            OnPropertyChanged();
        }
    }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }

    private string _status = "";
    public string Status { get => _status; private set => Set(ref _status, value); }

    private bool _justCleaned;
    /// <summary>After a clean, the next step (install the new driver) is shown at the top.</summary>
    public bool JustCleaned { get => _justCleaned; private set => Set(ref _justCleaned, value); }

    public bool? AllChecked
    {
        get => Items.Count == 0 || Items.All(i => !i.IsChecked) ? false : Items.All(i => i.IsChecked) ? true : null;
        set
        {
            foreach (var i in Items) i.IsChecked = value == true;
            OnPropertyChanged();
        }
    }

    private List<DriverPackage> _packages = new();
    private HashSet<string> _inUse = new(StringComparer.OrdinalIgnoreCase);
    private bool _loaded;

    public void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        RefreshCommand.Execute(null);
    }

    private async Task RefreshAsync()
    {
        IsBusy = true;
        using var task = BackgroundTasks.Start(this, "Reading Drivers");
        Status = "Reading drivers…";
        try
        {
            var (packages, adapters, inUse) = await Task.Run(() => (DriverTools.ListPackages(), DriverTools.ListAdapters(), DriverTools.PackagesInUse()));
            _packages = packages;
            _inUse = inUse;
            Adapters.Clear();
            foreach (var a in adapters) Adapters.Add(new AdapterItem(a));
            OnPropertyChanged(nameof(HasAdapters));
            if (!_vendorPicked)
            {
                // Start on the add-in card's maker (NVIDIA or AMD) when there is one, else the first adapter's.
                var vendors = adapters.Select(a => a.Vendor).OfType<DriverVendor>().ToList();
                if (vendors.Count > 0)
                    Vendor = vendors.FirstOrDefault(v => v != DriverVendor.Intel, vendors[0]);
            }
            foreach (var c in Vendors) c.IsSelected = c.Value == Vendor;
            Fill();
        }
        finally { IsBusy = false; }
    }

    private bool _vendorPicked;
    private void PickVendor(DriverVendor v)
    {
        _vendorPicked = true;
        if (v == Vendor && Items.Count > 0) return;
        Vendor = v;
        Fill();
    }

    /// <summary>Lists what the chosen maker left on this PC: packages, then folders, then keys, each A to Z.</summary>
    private void Fill()
    {
        foreach (var i in Items) i.PropertyChanged -= ItemChanged;
        Items.Clear();
        var rows = new List<DriverItem>();
        foreach (var p in _packages.Where(p => DriverCleaner.IsCandidate(p, Vendor)))
            rows.Add(new DriverItem("Driver Package", p.Description, ClassLabel(p), p.Version, p.PublishedName, _inUse.Contains(p.PublishedName),
                DriverCleaner.TickedByDefault(p, Vendor)) { Package = p });
        foreach (var f in DriverCleaner.Folders(Vendor))
            if (DriverTools.FolderPath(f) is { } path && Directory.Exists(path))
                rows.Add(new DriverItem("Folder", Path.GetFileName(path), "", "", path, false, true) { Folder = path });
        foreach (var k in DriverCleaner.RegistryKeys(Vendor))
            foreach (var loc in DriverTools.ExistingKeys(k))
                rows.Add(new DriverItem("Registry Key", k.Relative[(k.Relative.IndexOf('\\') + 1)..], "", "", loc.DisplayKeyPath, false, true) { Key = loc });
        foreach (var r in rows.OrderBy(r => r.Kind).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Location, StringComparer.OrdinalIgnoreCase))
        {
            r.PropertyChanged += ItemChanged;
            Items.Add(r);
        }
        OnPropertyChanged(nameof(AllChecked));
        OnPropertyChanged(nameof(GetDriverText));
        int packs = Items.Count(i => i.Package is not null);
        Status = Items.Count == 0
            ? $"Nothing from {VendorName} was found. There's nothing to clean."
            : $"{TargetRun.Plural(packs, "driver package")} and {TargetRun.Plural(Items.Count - packs, "leftover")} from {VendorName}. " +
              "Ticked items belong to the driver; unticked ones are other parts from the same maker for you to decide.";
    }

    private void ItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DriverItem.IsChecked)) OnPropertyChanged(nameof(AllChecked));
    }

    private static string ClassLabel(DriverPackage p) => p.ClassGuid switch
    {
        DriverCleaner.DisplayClass => "Display",
        DriverCleaner.MediaClass => "Sound",
        DriverCleaner.SoftwareComponentClass => "Software Component",
        DriverCleaner.ExtensionClass => "Extension",
        DriverCleaner.AudioProcessingObjectClass => "Audio Effects",
        _ => p.ClassName,
    };

    private async Task SafeModeAsync()
    {
        if (IsSafeMode)
        {
            if (!Dialogs.Confirm("Restart Windows normally now? Save anything you have open first.", "Restart Normally")) return;
            // Without admin rights bcdedit can't clear Safe Mode, but msconfig asks for them itself.
            if (!Elevation.IsAdministrator || !DriverTools.Restart()) ShowNormalStartSteps();
            return;
        }
        if (!Elevation.IsAdministrator)
        {
            Dialogs.Error("Switching Safe Mode needs administrator rights. Use Restart as Administrator.");
            return;
        }
        if (!Dialogs.Warn($"Restart in Safe Mode now? Kodiak Cleaner opens on the Driver Cleaner there so you can remove the {VendorName} driver.",
                "Restart in Safe Mode",
                "Save your work and close other programs first. Safe Mode has no internet, so download the new driver before you restart. " +
                "If your drive is encrypted with BitLocker, have its recovery key handy."))
            return;
        IsBusy = true;
        using var task = BackgroundTasks.Start(this, "Preparing Safe Mode");
        try
        {
            if (CreateRestorePoint && !await MakeRestorePointAsync()) return;
            if (!DriverTools.ScheduleSafeMode())
            {
                Dialogs.Error("Windows could not be set to start in Safe Mode.");
                return;
            }
            ActivityLog.Write("Driver Cleaner", "Restarting in Safe Mode");
            if (!DriverTools.Restart(toSafeMode: true))
                Dialogs.Info("Windows is set to start in Safe Mode, but it didn't restart. Restart it from the Start menu.", "Restart in Safe Mode");
        }
        finally { IsBusy = false; }
    }

    /// <returns>false when the restore point failed and the person chose to stop.</returns>
    private async Task<bool> MakeRestorePointAsync()
    {
        Status = "Creating a restore point. This can take a minute…";
        string problem;
        try
        {
            var code = await Task.Run(() => RestorePoints.Create($"Kodiak Cleaner Driver Cleaner {DateTime.Now:g}"));
            if (code == 0) return true;
            problem = $"Windows could not create a restore point (error {code}). System Protection may be off.";
        }
        catch (Exception e)
        {
            problem = "Windows could not create a restore point: " + e.Message;
        }
        Status = problem;
        return Dialogs.Warn(problem + "\n\nContinue without one?", "No Restore Point");
    }

    private async Task CleanAsync()
    {
        var chosen = Items.Where(i => i.IsChecked).ToList();
        if (chosen.Count == 0) return;
        if (!Elevation.IsAdministrator)
        {
            Dialogs.Error("Removing drivers needs administrator rights. Use Restart as Administrator.");
            return;
        }
        int packs = chosen.Count(i => i.Package is not null);
        string caution = (IsAudio ? "Sound may stop until a new driver is installed. " : "The screen may flicker and drop to a low resolution until a new driver is installed. ")
            + "Save your work and close other programs first."
            + (IsSafeMode ? "" : " For the cleanest result, use Restart in Safe Mode instead.");
        if (!Dialogs.Warn($"Remove {TargetRun.Plural(packs, "driver package")} and {TargetRun.Plural(chosen.Count - packs, "leftover")} from {VendorName}?",
                "Clean Drivers", caution))
            return;

        IsBusy = true;
        using var task = BackgroundTasks.Start(this, "Removing Drivers");
        try
        {
            // Restore points can't be made in Safe Mode (the shadow copy service doesn't run there).
            if (CreateRestorePoint && !IsSafeMode && !await MakeRestorePointAsync()) return;

            var failed = new List<string>();
            int removed = 0, backedUp = 0;
            var backupFolder = Path.Combine(Settings.Data.Backups, "Drivers", $"{DateTime.Now:yyyy-MM-dd_HHmmss}_{VendorName}");
            int step = 0, steps = chosen.Count;
            foreach (var item in chosen.Where(i => i.Package is not null))
            {
                task.Step(step++, steps);
                var p = item.Package!;
                if (BackUpPackages)
                {
                    Status = $"Backing up {p.PublishedName}…";
                    var saved = await Task.Run(() => DriverTools.ExportPackage(p.PublishedName, Path.Combine(backupFolder, Path.GetFileNameWithoutExtension(p.PublishedName))));
                    if (!saved)
                    {
                        // Never remove a package whose backup didn't work while backups are on.
                        failed.Add($"{p.Description} ({p.PublishedName}): the backup didn't work, so it was kept");
                        continue;
                    }
                    backedUp++;
                }
                Status = $"Removing {p.Description} ({p.PublishedName})…";
                var (ok, _) = await Task.Run(() => DriverTools.DeletePackage(p.PublishedName));
                if (ok) removed++;
                else failed.Add($"{p.Description} ({p.PublishedName})");
            }

            var keys = chosen.Where(i => i.Key is not null).Select(i => i.Key!).ToList();
            string? regBackup = null;
            if (keys.Count > 0)
            {
                task.Step(step, steps);
                Status = "Backing up and removing registry keys…";
                var (backup, keyFails) = await Task.Run(() => DriverTools.RemoveKeys(keys));
                regBackup = backup;
                step += keys.Count;
                removed += keys.Count - keyFails.Count;
                failed.AddRange(keyFails);
            }

            foreach (var item in chosen.Where(i => i.Folder is not null))
            {
                task.Step(step++, steps);
                Status = $"Moving {item.Folder} to the Recycle Bin…";
                try
                {
                    var folder = item.Folder!;
                    await Task.Run(() => Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(folder,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin));
                    removed++;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or OperationCanceledException)
                {
                    failed.Add(item.Folder!);
                }
            }

            ActivityLog.Write("Driver Cleaner", $"{VendorName}: removed {removed} of {chosen.Count} item(s)" +
                (failed.Count > 0 ? "; not removed: " + string.Join(", ", failed) : ""));
            JustCleaned = true;
            await RefreshAsync();
            Status = $"Removed {TargetRun.Plural(removed, "item")} from {VendorName}." + (failed.Count > 0 ? $" {failed.Count} couldn't be removed." : "")
                + (backedUp > 0 ? $" Driver packages were backed up to {backupFolder}." : "")
                + (regBackup is not null ? " Registry keys were backed up first." : "");
            AskToRestart(failed);
        }
        finally { IsBusy = false; }
    }

    private bool IsAudio => DriverCleaner.IsAudio(Vendor);

    /// <summary>Finishes like a card swap needs: restart now, shut down to change the card, or later.</summary>
    private void AskToRestart(List<string> failed)
    {
        var message = (failed.Count > 0 ? "Some items couldn't be removed:\n" + string.Join("\n", failed.Take(8)) + "\n\n" : "")
            + "Restart Windows to finish. If you're changing the " + (IsAudio ? "sound card" : "graphics card") + ", shut down instead and swap it now.\n\n"
            + $"After Windows starts, install the newest driver: Get the Latest {VendorName} Driver on this page opens {VendorName}'s download page.";
        int choice = Dialogs.Choose(MessageKind.Question, "Finish Cleaning", message, null, new[] { "Later", "Shut Down", "Restart" }, 2, 0);
        if (choice == 0)
        {
            // In Safe Mode, still make sure the next start is a normal one.
            if (IsSafeMode && !DriverTools.ClearSafeMode()) ShowNormalStartSteps();
            return;
        }
        if (DriverTools.Restart(shutDown: choice == 1)) return;
        if (IsSafeMode) ShowNormalStartSteps();
        else Dialogs.Error("Windows didn't " + (choice == 1 ? "shut down" : "restart") + ". Use the Start menu instead.");
    }

    /// <summary>
    /// Shown when Kodiak Cleaner couldn't set Windows back to a normal start from Safe Mode: the same switch by hand in System
    /// Configuration (msconfig), with a button that opens it.
    /// </summary>
    private static void ShowNormalStartSteps()
    {
        const string steps = "Kodiak Cleaner couldn't set Windows to start normally, so it would come back in Safe Mode. Switch it back in System Configuration instead:\n\n"
            + "1. Press the Windows key + R, type msconfig and press Enter (or click Open System Configuration below).\n"
            + "2. Go to the Boot tab.\n"
            + "3. Under Boot options, untick Safe boot.\n"
            + "4. Click OK, then click Restart.";
        ActivityLog.Write("Driver Cleaner", "Couldn't clear Safe Mode; showed the System Configuration steps");
        int choice = Dialogs.Choose(MessageKind.Warning, "Restart Normally", steps,
            "If Windows asks for permission, click Yes. Save anything you have open before you click Restart.",
            new[] { "Close", "Open System Configuration" }, 1, 0);
        if (choice == 1 && !DriverTools.OpenSystemConfiguration())
            Dialogs.Error("System Configuration couldn't be opened. Press the Windows key + R, type msconfig and press Enter.");
    }
}
