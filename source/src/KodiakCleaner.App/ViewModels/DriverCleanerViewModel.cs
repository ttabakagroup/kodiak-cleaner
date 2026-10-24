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
    public string GetDriverText => Vendor is { } v ? $"Get the Latest {DriverCleaner.Name(v)} Driver" : "";
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
        BitLockerPageCommand = new RelayCommand(OpenBitLockerPage);
        ResumeBitLockerCommand = new RelayCommand(ResumeBitLockerAsync, () => !IsBusy && BitLocker == BitLockerState.Suspended);
        ShowRecoveryKeyCommand = new RelayCommand(ShowRecoveryKeyAsync, () => !IsBusy);
        SuspendBitLockerCommand = new RelayCommand(SuspendBitLockerAsync, () => !IsBusy && BitLocker == BitLockerState.On);
        foreach (var v in Enum.GetValues<DriverVendor>().OrderBy(DriverCleaner.Name, StringComparer.OrdinalIgnoreCase))
            Vendors.Add(new Choice<DriverVendor>(v == DriverVendor.Realtek ? "Realtek Audio" : DriverCleaner.Name(v), v, false, PickVendor));
    }

    public ObservableCollection<DriverItem> Items { get; } = new();
    public ObservableCollection<AdapterItem> Adapters { get; } = new();
    public List<Choice<DriverVendor>> Vendors { get; } = new();
    public bool HasAdapters => Adapters.Count > 0;
    /// <summary>One Get the Latest Driver button per maker found (A to Z), shown in the Graphics card's button row.</summary>
    public ObservableCollection<AdapterItem> DriverLinks { get; } = new();
    public ICommand RefreshCommand { get; }
    public ICommand CleanCommand { get; }
    public ICommand SafeModeCommand { get; }
    public ICommand GetDriverCommand { get; }
    public ICommand BitLockerPageCommand { get; }
    public ICommand ResumeBitLockerCommand { get; }
    public ICommand ShowRecoveryKeyCommand { get; }
    public ICommand SuspendBitLockerCommand { get; }

    // BitLocker on the Windows drive (Ted, 2026-10-23). Read with administrator rights only, so without them the card stays hidden.
    private BitLockerState _bitLocker;
    public BitLockerState BitLocker
    {
        get => _bitLocker;
        private set
        {
            Set(ref _bitLocker, value);
            OnPropertyChanged(nameof(HasBitLocker));
            OnPropertyChanged(nameof(BitLockerTitle));
            OnPropertyChanged(nameof(BitLockerText));
            OnPropertyChanged(nameof(IsBitLockerOn));
            OnPropertyChanged(nameof(IsBitLockerSuspended));
            if (IsBitLockerConverting) WatchConversion();
        }
    }
    // Encrypting or decrypting: how far along, refreshed every 15 seconds while the page is open.
    private int? _bitLockerPercent;
    public int? BitLockerPercent
    {
        get => _bitLockerPercent;
        private set { Set(ref _bitLockerPercent, value); OnPropertyChanged(nameof(BitLockerTitle)); OnPropertyChanged(nameof(BitLockerText)); }
    }
    public bool HasBitLocker => BitLocker is BitLockerState.On or BitLockerState.Suspended or BitLockerState.Encrypting or BitLockerState.Decrypting;
    public bool IsBitLockerOn => BitLocker == BitLockerState.On;
    public bool IsBitLockerSuspended => BitLocker == BitLockerState.Suspended;
    private bool IsBitLockerConverting => BitLocker is BitLockerState.Encrypting or BitLockerState.Decrypting;
    private string PercentText => BitLockerPercent is int p ? $" ({p}%)" : "";
    public string BitLockerTitle => BitLocker switch
    {
        BitLockerState.Suspended => "BitLocker Is Suspended",
        BitLockerState.Encrypting => "BitLocker Is Encrypting" + PercentText,
        BitLockerState.Decrypting => "BitLocker Is Turning Off" + PercentText,
        _ => "BitLocker Is On",
    };
    public string BitLockerText => BitLocker switch
    {
        BitLockerState.Suspended =>
            $"Drive {BitLockerTools.WindowsDrive} is still encrypted, but Windows starts without asking for the recovery key until its restarts run out. Then protection turns back on by itself.",
        BitLockerState.Encrypting =>
            $"Windows is encrypting drive {BitLockerTools.WindowsDrive} in the background{(BitLockerPercent is int p ? $" ({p}% done)" : "")}, and protection turns on when it finishes. " +
            "You can keep using the PC. Save the recovery key now, and wait until encrypting finishes before you restart in Safe Mode.",
        BitLockerState.Decrypting =>
            $"Windows is decrypting drive {BitLockerTools.WindowsDrive} because BitLocker is being turned off{(BitLockerPercent is int p ? $" ({p}% done)" : "")}. " +
            "You can keep using the PC. Wait until it finishes before you restart in Safe Mode.",
        _ => $"Drive {BitLockerTools.WindowsDrive} is encrypted. Driver changes and Safe Mode can make Windows ask for the 48-digit recovery key at start-up. " +
             "Save the key first. Suspend keeps the drive encrypted and only skips the key for the next two restarts (Safe Mode and back), " +
             "then turns itself back on. Turning BitLocker off decrypts the whole drive, takes a long time and isn't needed for drivers.",
    };

    private bool _watching;
    private async void WatchConversion()
    {
        if (_watching) return;
        _watching = true;
        try
        {
            BitLockerPercent = await Task.Run(BitLockerTools.Percent);
            while (IsBitLockerConverting)
            {
                await Task.Delay(TimeSpan.FromSeconds(15));
                var (state, percent) = await Task.Run(() => (BitLockerTools.State(), BitLockerTools.Percent()));
                BitLockerPercent = percent;
                if (state != BitLockerState.Unknown) { _watching = state is BitLockerState.Encrypting or BitLockerState.Decrypting; BitLocker = state; }
            }
        }
        finally { _watching = false; }
    }

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
                Elevation.OfferRestart("Changing this needs administrator rights.");
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
            BitLocker = Elevation.IsAdministrator ? await Task.Run(() => BitLockerTools.State()) : BitLockerState.Unknown;
            _packages = packages;
            _inUse = inUse;
            Adapters.Clear();
            foreach (var a in adapters) Adapters.Add(new AdapterItem(a));
            DriverLinks.Clear();
            foreach (var a in Adapters.Where(a => a.Vendor is not null).DistinctBy(a => a.Vendor).OrderBy(a => a.GetDriverText, StringComparer.OrdinalIgnoreCase))
                DriverLinks.Add(a);
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
            Elevation.OfferRestart("Switching Safe Mode needs administrator rights.");
            return;
        }
        BitLocker = await Task.Run(() => BitLockerTools.State());
        if (BitLocker == BitLockerState.On)
        {
            // Suspending covers both restarts: into Safe Mode and back to a normal start.
            int choice = Dialogs.Choose(MessageKind.Warning, "BitLocker Is On",
                $"Drive {BitLockerTools.WindowsDrive} is encrypted with BitLocker. Starting in Safe Mode can make Windows ask for the 48-digit recovery key.\n\n" +
                "Suspend and Restart keeps the drive encrypted and skips the key for the next two restarts (into Safe Mode and back), then BitLocker turns itself back on.",
                "Save your work and close other programs first. Safe Mode has no internet, so download the new driver before you restart. " +
                "No saved recovery key yet? Click Cancel, then Show Recovery Key on this page.",
                new[] { "Cancel", "Restart Without Suspending", "Suspend and Restart" }, 2, 0);
            if (choice == 0) return;
            if (choice == 2 && !await SuspendAsync()) return;
        }
        else if (IsBitLockerConverting)
        {
            string doing = BitLocker == BitLockerState.Encrypting ? "encrypting" : "decrypting";
            if (Dialogs.Choose(MessageKind.Warning, "BitLocker Is Busy",
                    $"Windows is still {doing} drive {BitLockerTools.WindowsDrive}{PercentText}. It carries on after a restart, but it's safer to wait until it finishes before you restart in Safe Mode.",
                    "No saved recovery key yet? Click Wait, then Show Recovery Key on this page.",
                    new[] { "Wait", "Restart Anyway" }, 0, 0) == 0)
                return;
        }
        else if (!Dialogs.Warn($"Restart in Safe Mode now? Kodiak Cleaner opens on the Driver Cleaner there so you can remove the {VendorName} driver.",
                "Restart in Safe Mode",
                "Save your work and close other programs first. Safe Mode has no internet, so download the new driver before you restart."))
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

    /// <summary>Suspends BitLocker for two restarts. Returns false when Windows refused and the person chose to stop.</summary>
    private async Task<bool> SuspendAsync()
    {
        if (await Task.Run(() => BitLockerTools.Suspend(2)))
        {
            ActivityLog.Write("Driver Cleaner", $"Suspended BitLocker on {BitLockerTools.WindowsDrive} for two restarts");
            BitLocker = BitLockerState.Suspended;
            return true;
        }
        return Dialogs.Warn("Windows didn't suspend BitLocker. Continue anyway? Windows may ask for the recovery key when it starts.", "BitLocker Is On",
            "Have the recovery key ready before you restart.");
    }

    private async Task SuspendBitLockerAsync()
    {
        if (!Dialogs.Confirm($"Suspend BitLocker on drive {BitLockerTools.WindowsDrive} for the next two restarts? The drive stays encrypted, " +
                "and protection turns back on by itself after that.", "Suspend BitLocker"))
            return;
        IsBusy = true;
        using var task = BackgroundTasks.Start(this, "Suspending BitLocker");
        try
        {
            if (await Task.Run(() => BitLockerTools.Suspend(2)))
            {
                ActivityLog.Write("Driver Cleaner", $"Suspended BitLocker on {BitLockerTools.WindowsDrive} for two restarts");
                BitLocker = BitLockerState.Suspended;
            }
            else Dialogs.Error("Windows didn't suspend BitLocker. Try Open BitLocker Settings and Suspend Protection there.");
        }
        finally { IsBusy = false; }
    }

    private async Task ResumeBitLockerAsync()
    {
        IsBusy = true;
        using var task = BackgroundTasks.Start(this, "Resuming BitLocker");
        try
        {
            if (await Task.Run(() => BitLockerTools.Resume()))
            {
                ActivityLog.Write("Driver Cleaner", $"Resumed BitLocker on {BitLockerTools.WindowsDrive}");
                BitLocker = BitLockerState.On;
            }
            else Dialogs.Error("Windows didn't resume BitLocker. Try Open BitLocker Settings and Resume Protection there.");
        }
        finally { IsBusy = false; }
    }

    private static void OpenBitLockerPage()
    {
        if (!BitLockerTools.OpenWindowsPage())
            Dialogs.Error("Windows' BitLocker page couldn't be opened. Search the Start menu for Manage BitLocker.");
    }

    /// <summary>
    /// Shows the recovery key so it can be written down, copied or saved to a USB drive. The key only lives in this dialog: it is
    /// never logged or stored. Reading it needs administrator rights, the same Windows password check Windows' own page uses.
    /// </summary>
    private async Task ShowRecoveryKeyAsync()
    {
        if (!Elevation.IsAdministrator)
        {
            Elevation.OfferRestart("Showing the recovery key needs administrator rights. Open BitLocker Settings shows it too.");
            return;
        }
        var keys = await Task.Run(() => BitLockerTools.RecoveryKeys());
        if (keys.Count == 0)
        {
            int open = Dialogs.Choose(MessageKind.Info, "BitLocker Recovery Key",
                "Kodiak Cleaner couldn't read a recovery key for this drive. Windows' BitLocker page can back one up, or you can find keys " +
                "saved to your Microsoft account online.", null, new[] { "Close", "Microsoft Account Keys", "Open BitLocker Settings" }, 2, 0);
            if (open == 1) Shell.OpenUrl(BitLockerTools.AccountKeysUrl);
            else if (open == 2) OpenBitLockerPage();
            return;
        }
        var text = string.Join("\n\n", keys.Select(k => $"Key ID: {k.Id.Trim('{', '}')}\nRecovery key: {k.Key}"));
        while (true)
        {
            int choice = Dialogs.Choose(MessageKind.Info, "BitLocker Recovery Key",
                $"Drive {BitLockerTools.WindowsDrive}\n\n{text}\n\nWrite it down, or save it to a USB drive. If Windows asks for it at start-up, " +
                "match the first characters of the Key ID on that screen.",
                "Keep it away from this PC: a key saved only on this drive can't help when the drive is locked.",
                new[] { "Close", "Copy", "Save to File" }, 0, 0);
            if (choice == 0) return;
            if (choice == 1) { if (ClipboardHelper.CopyOrExplain(text)) Dialogs.Info("The recovery key was copied. Paste it somewhere safe, then clear the clipboard.", "BitLocker Recovery Key"); }
            else if (SaveRecoveryKey(keys[0].Id, text)) return;
        }
    }

    private static bool SaveRecoveryKey(string id, string text)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save the Recovery Key (a USB Drive Is Best)",
            FileName = $"BitLocker Recovery Key {id.Trim('{', '}').Split('-')[0]}.txt",
            Filter = "Text Files (*.txt)|*.txt",
        };
        if (dialog.ShowDialog() != true) return false;
        if (string.Equals(Path.GetPathRoot(dialog.FileName)?.TrimEnd('\\'), BitLockerTools.WindowsDrive, StringComparison.OrdinalIgnoreCase)
            && !Dialogs.Warn("This saves the key on the drive it unlocks, so it won't be there when you need it. Save it here anyway?",
                "Save the Recovery Key", "A USB drive, a printout or your Microsoft account is safer."))
            return false;
        try
        {
            File.WriteAllText(dialog.FileName, $"BitLocker recovery key for drive {BitLockerTools.WindowsDrive} on {Environment.MachineName}\r\n\r\n" +
                text.Replace("\n", "\r\n") + "\r\n");
            Dialogs.Info($"The recovery key was saved to {dialog.FileName}.", "BitLocker Recovery Key");
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Dialogs.Error("The recovery key couldn't be saved: " + e.Message);
            return false;
        }
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
            Elevation.OfferRestart("Removing drivers needs administrator rights.");
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
            var backupFolder = Path.Combine(Settings.Data.Drivers, $"{DateTime.Now:yyyy-MM-dd_HHmmss}_{VendorName}");
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
