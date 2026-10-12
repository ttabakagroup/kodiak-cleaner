using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using KodiakCleaner.Core.Tools;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App.ViewModels;

public sealed class RestoreViewModel : ObservableObject, ILoadOnFirstVisit
{
    public RestoreViewModel()
    {
        RefreshCommand = new RelayCommand(RefreshAsync, () => !IsBusy);
        CreateCommand = new RelayCommand(CreateAsync, () => !IsBusy);
        RemoveCommand = new RelayCommand(RemoveAsync, () => !IsBusy && Selected is not null && Selected != Points.FirstOrDefault());
        OpenSystemRestoreCommand = new RelayCommand(() => Process.Start(new ProcessStartInfo("rstrui.exe") { UseShellExecute = true }));
        OpenProtectionCommand = new RelayCommand(() => Process.Start(new ProcessStartInfo("SystemPropertiesProtection.exe") { UseShellExecute = true }));
    }

    /// <summary>Windows 11's Point-in-Time Restore, the page's second tab (read the first time the tab is opened).</summary>
    public PointInTimeRestoreViewModel Pitr { get; } = new();

    private bool _showPitr;
    /// <summary>System Restore | Point-in-Time Restore segments at the top of the page.</summary>
    public bool ShowPitr
    {
        get => _showPitr;
        set
        {
            if (!Set(ref _showPitr, value)) return;
            OnPropertyChanged(nameof(ShowSystemRestore));
            if (value) Pitr.EnsureLoaded();
        }
    }
    public bool ShowSystemRestore { get => !_showPitr; set => ShowPitr = !value; }

    public ObservableCollection<RestorePoint> Points { get; } = new();
    public ICommand RefreshCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand OpenSystemRestoreCommand { get; }
    public ICommand OpenProtectionCommand { get; }

    private RestorePoint? _selected;
    public RestorePoint? Selected { get => _selected; set => Set(ref _selected, value); }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }

    private string _summary = "Restore points let Windows roll back system files and settings. Your documents are not affected.";
    public string Summary { get => _summary; private set => Set(ref _summary, value); }

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
        try
        {
            var list = await Task.Run(RestorePoints.List);
            Points.Clear();
            foreach (var p in list) Points.Add(p);
            Summary = list.Count == 0
                ? "No restore points found. System Protection may be turned off for your drives (Configure… turns it on)."
                : $"{list.Count} restore point(s). The newest one can't be removed, so you always have a way back.";
        }
        catch (Exception e)
        {
            // WMI can fail in several ways (access denied, service stopped, missing components); all mean "can't list".
            Summary = Elevation.IsAdministrator
                ? "Restore points could not be read: " + e.Message
                : "Restore points can only be listed with administrator rights. Use Restart as administrator.";
        }
        finally { IsBusy = false; }
    }

    private async Task CreateAsync()
    {
        if (!Elevation.IsAdministrator)
        {
            Dialogs.Error("Creating a restore point needs administrator rights. Use Restart as administrator.");
            return;
        }
        IsBusy = true;
        Summary = "Creating a restore point. This can take a minute…";
        try
        {
            int before = Points.Count;
            // CreateNow lifts Windows' one-a-day limit for this call, so a point can be made right before a big install.
            var code = await Task.Run(() => RestorePoints.CreateNow($"Kodiak Cleaner {DateTime.Now:g}"));
            IsBusy = false;
            await RefreshAsync();
            Summary = code != 0
                ? $"Windows could not create a restore point (error {code}). Check that System Protection is on."
                : Points.Count > before
                    ? "Restore point created. Windows keeps it until it needs the space, so it's a good way back from a big install."
                    : "Windows didn't add a new restore point. Check that System Protection is on for your system drive (Configure…).";
            ActivityLog.Write("Restore", code == 0 ? "System Restore point created" : $"System Restore point failed (error {code})");
        }
        catch (Exception e)
        {
            Summary = "Windows could not create a restore point: " + e.Message;
        }
        finally { IsBusy = false; }
    }

    private async Task RemoveAsync()
    {
        if (Selected is not { } p) return;
        if (!Dialogs.Confirm($"Remove the restore point \"{p.Description}\" from {p.Created:g}? This can't be undone.")) return;
        IsBusy = true;
        try
        {
            var code = await Task.Run(() => RestorePoints.Remove(p.Sequence));
            if (code != 0) Dialogs.Error($"Windows could not remove it (error {code}). Removing restore points needs administrator rights.");
        }
        catch (Exception e)
        {
            Dialogs.Error("Windows could not remove it: " + e.Message);
        }
        finally { IsBusy = false; }
        await RefreshAsync();
    }
}

/// <summary>
/// Point-in-Time Restore (Windows 11 24H2 and later): the on/off switch and the three settings Windows' own Settings shows,
/// the snapshots Windows keeps, Snapshot Now (Windows' own task, started right away) and Restart to Recovery, where the
/// restore itself happens. Windows doesn't let a program restore a snapshot or keep one past its 72-hour limit.
/// </summary>
public sealed class PointInTimeRestoreViewModel : ObservableObject
{
    private PitrSettings? _settings;
    private bool _applying;
    private CancellationTokenSource? _cts;

    public PointInTimeRestoreViewModel()
    {
        RefreshCommand = new RelayCommand(RefreshAsync, () => !IsBusy);
        SnapshotCommand = new RelayCommand(SnapshotAsync, () => !IsBusy && IsAvailable && Enabled);
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy && _cts is not null);
        RecoveryCommand = new RelayCommand(RestartToRecovery, () => !IsBusy && IsAvailable);
        OpenSettingsCommand = new RelayCommand(() => Process.Start(new ProcessStartInfo("ms-settings:recovery") { UseShellExecute = true }));
    }

    public ObservableCollection<PitrSnapshot> Snapshots { get; } = new();
    public sealed record Option(string Label, int Value)
    {
        public override string ToString() => Label; // the closed dropdown shows this
    }
    public ObservableCollection<Option> Frequencies { get; } = new();
    public ObservableCollection<Option> Retentions { get; } = new();
    public ObservableCollection<Option> Spaces { get; } = new();

    private int _frequency, _retention, _space;
    /// <summary>Minutes between snapshots (SnapshotInterval_UX).</summary>
    public int Frequency { get => _frequency; set => Change(ref _frequency, value, PointInTimeRestore.FrequencyValue, m => "takes a snapshot every " + PointInTimeRestore.MinutesLabel(m).ToLowerInvariant()); }
    /// <summary>Minutes a snapshot is kept (MaxTimespan_UX).</summary>
    public int Retention { get => _retention; set => Change(ref _retention, value, PointInTimeRestore.RetentionValue, m => "keeps snapshots for " + PointInTimeRestore.MinutesLabel(m).ToLowerInvariant()); }
    /// <summary>Most space all snapshots may use, in MB (MaxGlobalSize_UX).</summary>
    public int Space { get => _space; set => Change(ref _space, value, PointInTimeRestore.MaxSpaceValue, mb => $"uses up to {SpaceLabel(mb)}"); }

    private static string SpaceLabel(int mb) => mb % 1024 == 0 ? $"{mb / 1024} GB" : $"{mb / 1024.0:0.#} GB";

    private void Change(ref int field, int value, string name, Func<int, string> what, [System.Runtime.CompilerServices.CallerMemberName] string? prop = null)
    {
        if (field == value || value <= 0) return;
        if (_settings is not null && !Apply(name, value, what(value)))
        {
            // Put the dropdown back to what Windows really has, after WPF finishes this change.
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => OnPropertyChanged(prop));
            return;
        }
        field = value;
        OnPropertyChanged(prop);
    }
    public ICommand RefreshCommand { get; }
    public ICommand SnapshotCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand RecoveryCommand { get; }
    public ICommand OpenSettingsCommand { get; }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }

    private bool _isAvailable = true;
    public bool IsAvailable { get => _isAvailable; private set { if (Set(ref _isAvailable, value)) OnPropertyChanged(nameof(CanChange)); } }

    private bool _setByOrganization;
    public bool SetByOrganization { get => _setByOrganization; private set { if (Set(ref _setByOrganization, value)) OnPropertyChanged(nameof(CanChange)); } }

    /// <summary>Settings can be changed: the feature is here, nothing manages them and Kodiak Cleaner runs as administrator.</summary>
    public bool CanChange => IsAvailable && !SetByOrganization && Elevation.IsAdministrator;

    private string _summary = "Point-in-Time Restore takes a full snapshot of your system drive on a schedule. Windows keeps each one for up to 72 hours.";
    public string Summary { get => _summary; private set => Set(ref _summary, value); }

    private bool _enabled;
    /// <summary>Point-in-Time Restore on or off (Windows' Active_UX). Changing it writes the registry after a .reg backup.</summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            if (!Apply(PointInTimeRestore.EnabledValue, value ? 1 : 0, value ? "turned on" : "turned off")) { OnPropertyChanged(); return; }
            Set(ref _enabled, value);
        }
    }

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
        try
        {
            var (available, settings, snapshots) = await Task.Run(() =>
            {
                if (!PointInTimeRestore.IsAvailable()) return (false, (PitrSettings?)null, new List<PitrSnapshot>());
                var st = PointInTimeRestore.ReadSettings();
                return (true, st, PointInTimeRestore.ListSnapshots(st.RetentionMinutes));
            });
            IsAvailable = available;
            Snapshots.Clear();
            if (!available || settings is null)
            {
                Summary = "Point-in-Time Restore isn't on this PC yet. It comes with Windows 11 24H2 or 25H2 and the June 2026 update or later. Until then, use the System Restore tab.";
                return;
            }
            _settings = settings;
            SetByOrganization = settings.SetByOrganization;
            _enabled = settings.Enabled;
            OnPropertyChanged(nameof(Enabled));
            Fill(Frequencies, PointInTimeRestore.FrequencyHours.Select(h => h * 60), settings.FrequencyMinutes, PointInTimeRestore.MinutesLabel);
            Fill(Retentions, PointInTimeRestore.RetentionHours.Select(h => h * 60), settings.RetentionMinutes, PointInTimeRestore.MinutesLabel);
            Fill(Spaces, PointInTimeRestore.SpaceGb.Select(g => g * 1024), settings.MaxSpaceMb, SpaceLabel);
            _frequency = settings.FrequencyMinutes; _retention = settings.RetentionMinutes; _space = settings.MaxSpaceMb;
            OnPropertyChanged(nameof(Frequency)); OnPropertyChanged(nameof(Retention)); OnPropertyChanged(nameof(Space));
            foreach (var s in snapshots) Snapshots.Add(s);
            Summary = !settings.Enabled
                ? "Point-in-Time Restore is off. Turn it on and Windows starts taking snapshots of your system drive."
                : snapshots.Count == 0
                    ? Elevation.IsAdministrator
                        ? "No snapshots yet. Windows takes the first one when the PC is idle, or use Snapshot Now."
                        : "Snapshots can only be listed with administrator rights. Use Restart as administrator."
                    : $"{snapshots.Count} snapshot(s). To go back to one, use Restart to Recovery and pick Troubleshoot, then Point-in-Time Restore.";
        }
        catch (Exception e)
        {
            Summary = "Point-in-Time Restore could not be read: " + e.Message;
        }
        finally { IsBusy = false; }
    }

    /// <summary>Fills a dropdown with Windows' choices, adding the current value when it was set some other way (for example by Intune).</summary>
    private static void Fill(ObservableCollection<Option> list, IEnumerable<int> values, int current, Func<int, string> label)
    {
        list.Clear();
        foreach (var v in values.Append(current).Distinct().Order()) list.Add(new Option(label(v), v));
    }

    private bool Apply(string name, int value, string what)
    {
        if (_applying || _settings is null) return false;
        if (!Elevation.IsAdministrator)
        {
            Dialogs.Error("Changing Point-in-Time Restore needs administrator rights. Use Restart as administrator.");
            return false;
        }
        _applying = true;
        try
        {
            PointInTimeRestore.Write(name, value);
            Summary = $"Point-in-Time Restore now {what}. The old settings were saved to Backups\\Registry first.";
            return true;
        }
        catch (Exception e)
        {
            Dialogs.Error("Windows didn't accept the change: " + e.Message);
            return false;
        }
        finally { _applying = false; }
    }

    private async Task SnapshotAsync()
    {
        if (!Elevation.IsAdministrator)
        {
            Dialogs.Error("Taking a snapshot needs administrator rights. Use Restart as administrator.");
            return;
        }
        IsBusy = true;
        _cts = new CancellationTokenSource();
        Summary = "Windows is taking a snapshot of your system drive. This can take a few minutes; you can keep using your PC.";
        int before = Snapshots.Count;
        try
        {
            var result = await Task.Run(() => PointInTimeRestore.SnapshotNow(TimeSpan.FromMinutes(15), _cts.Token));
            IsBusy = false;
            await RefreshAsync();
            Summary = Snapshots.Count > before
                ? $"Snapshot taken. Windows keeps it for up to {PointInTimeRestore.MinutesLabel(_settings?.RetentionMinutes ?? PointInTimeRestore.DefaultRetentionMinutes).ToLowerInvariant()}, or less if space runs low."
                : result != 0
                    ? $"Windows' snapshot task finished with error 0x{result:X8} and didn't add a snapshot."
                    : "Windows ran its snapshot task but didn't add a new snapshot. It may skip one when the last is very recent or space is low.";
            ActivityLog.Write("Restore", Snapshots.Count > before ? "Point-in-Time Restore snapshot taken" : $"Point-in-Time Restore snapshot not added (0x{result:X8})");
        }
        catch (OperationCanceledException)
        {
            Summary = "Stopped waiting. Windows may still finish the snapshot; use Refresh to check.";
        }
        catch (Exception e)
        {
            Summary = "Windows could not take a snapshot: " + e.Message;
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            IsBusy = false;
        }
    }

    private void RestartToRecovery()
    {
        if (!Dialogs.Warn(
                "Restart into Windows Recovery now? Save your work first. Then choose Troubleshoot, then Point-in-Time Restore, and pick a snapshot.",
                "Restart to Recovery",
                "Everything changed after the snapshot you pick is lost, including files. If your drive uses BitLocker, have your recovery key ready."))
            return;
        try
        {
            ActivityLog.Write("Restore", "Restarting to Windows Recovery for Point-in-Time Restore");
            PointInTimeRestore.RestartToRecovery();
        }
        catch (Exception e)
        {
            Dialogs.Error("Windows could not restart into Recovery: " + e.Message);
        }
    }
}

/// <summary>A disk in the Entire Drive list, with its tick box and what will be done to it.</summary>
public sealed class WipeDiskRow : ObservableObject
{
    public WipeDiskRow(WipeDisk disk) => Disk = disk;
    public WipeDisk Disk { get; }
    public bool CanWipe => Disk.CanWipe;
    public string Size => ByteSize.Format(Disk.Size);

    private bool _isChecked;
    public bool IsChecked { get => _isChecked; set => Set(ref _isChecked, value && CanWipe); }

    private string _method = "";
    public string Method { get => _method; set => Set(ref _method, value); }

    private string _status = "";
    public string Status { get => _status; set => Set(ref _status, value); }
}

public sealed class WiperViewModel : ObservableObject, ILoadOnFirstVisit
{
    private CancellationTokenSource? _cts;

    public WiperViewModel()
    {
        RefreshCommand = new RelayCommand(RefreshAsync, () => !IsBusy);
        WipeCommand = new RelayCommand(WipeAsync, () => !IsBusy && (EntireDrive ? Disks.Any(d => d.IsChecked) : SelectedDrive is not null));
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy);
        foreach (var (label, value) in new[] { ("Free Space Only", false), ("Entire Drive", true) })
            Modes.Add(new Choice<bool>(label, value, !value, v => { EntireDrive = v; }));
        foreach (var m in new[] { WipeMethod.Zeros, WipeMethod.Dod3, WipeMethod.Dod7, WipeMethod.Pass30 })
            MethodChoices.Add(new Choice<WipeMethod>(WipeMethods.Name(m), m, m == Method, v => Method = v));
        foreach (var l in new[] { WipeLevel.Quick, WipeLevel.Thorough, WipeLevel.Secure })
            LevelChoices.Add(new Choice<WipeLevel>(WipeMethods.LevelName(l), l, l == Level, v => Level = v));
        foreach (var fs in DiskFormat.FileSystems)
            FileSystemChoices.Add(new Choice<string>(fs, fs, fs == FileSystem, v => FileSystem = v));
        foreach (var (label, gpt) in new[] { ("GPT", true), ("MBR", false) })
            StyleChoices.Add(new Choice<bool>(label, gpt, gpt == Gpt, v => Gpt = v));
    }

    private bool _loaded;
    /// <summary>Reads the list the first time the page is shown, so starting Kodiak Cleaner stays quick.</summary>
    public void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _ = RefreshAsync();
    }

    public ObservableCollection<WipeableDrive> Drives { get; } = new();
    public ObservableCollection<WipeDiskRow> Disks { get; } = new();
    public List<Choice<bool>> Modes { get; } = new();
    public List<Choice<WipeMethod>> MethodChoices { get; } = new();
    public List<Choice<WipeLevel>> LevelChoices { get; } = new();
    public List<Choice<string>> FileSystemChoices { get; } = new();
    public List<Choice<bool>> StyleChoices { get; } = new();

    private WipeLevel _level = WipeLevel.Secure;
    /// <summary>Quick (partition data only), Thorough (one pass or firmware erase) or Secure (the chosen passes or firmware erase).</summary>
    public WipeLevel Level
    {
        get => _level;
        private set
        {
            Set(ref _level, value);
            OnPropertyChanged(nameof(ShowSecurity));
            OnPropertyChanged(nameof(ShowVerify));
            OnPropertyChanged(nameof(MethodHint));
            foreach (var d in Disks) d.Method = MethodFor(d.Disk);
        }
    }

    /// <summary>The pass choice applies to free space, and to Secure entire-drive wipes.</summary>
    public bool ShowSecurity => !EntireDrive || Level == WipeLevel.Secure;
    public bool ShowVerify => EntireDrive && Level != WipeLevel.Quick;

    private bool _leaveUninitialized;
    /// <summary>On: no partition table at all afterwards, so the drive must be initialized like a new one. Off: it's formatted.</summary>
    public bool LeaveUninitialized
    {
        get => _leaveUninitialized;
        set
        {
            Set(ref _leaveUninitialized, value);
            OnPropertyChanged(nameof(ShowFormat));
        }
    }

    public bool ShowFormat => !LeaveUninitialized;

    private string _fileSystem = "NTFS";
    public string FileSystem { get => _fileSystem; private set => Set(ref _fileSystem, value); }

    private bool _gpt = true;
    public bool Gpt { get => _gpt; private set => Set(ref _gpt, value); }

    private string _label = "Wiped";
    public string Label { get => _label; set => Set(ref _label, value); }

    private DiskFormat? Format => LeaveUninitialized ? null : new DiskFormat(FileSystem, Gpt, Label);
    public ICommand RefreshCommand { get; }
    public ICommand WipeCommand { get; }
    public ICommand CancelCommand { get; }

    private WipeableDrive? _selectedDrive;
    public WipeableDrive? SelectedDrive { get => _selectedDrive; set { Set(ref _selectedDrive, value); OnPropertyChanged(nameof(DriveNote)); } }

    private bool _entireDrive;
    public bool EntireDrive
    {
        get => _entireDrive;
        private set
        {
            Set(ref _entireDrive, value);
            OnPropertyChanged(nameof(DriveNote));
            OnPropertyChanged(nameof(MethodHint));
            OnPropertyChanged(nameof(ShowSecurity));
            OnPropertyChanged(nameof(ShowVerify));
        }
    }

    private WipeMethod _method = WipeMethod.Dod3;
    public WipeMethod Method
    {
        get => _method;
        private set
        {
            Set(ref _method, value);
            OnPropertyChanged(nameof(DriveNote));
            OnPropertyChanged(nameof(MethodHint));
            foreach (var d in Disks) d.Method = MethodFor(d.Disk);
        }
    }

    private bool _verify = true;
    /// <summary>Reads the last pass back and compares every byte (the DoD standard's verification step).</summary>
    public bool Verify { get => _verify; set => Set(ref _verify, value); }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }

    private double _percent;
    public double Percent { get => _percent; private set => Set(ref _percent, value); }

    private string _status = "";
    public string Status { get => _status; private set => Set(ref _status, value); }

    private string _listNote = "";
    public string ListNote { get => _listNote; private set => Set(ref _listNote, value); }

    public bool? AllChecked
    {
        get => Disks.Any(d => d.CanWipe) && Disks.Where(d => d.CanWipe).All(d => d.IsChecked) ? true : Disks.Any(d => d.IsChecked) ? null : false;
        set
        {
            bool on = value ?? false;
            foreach (var d in Disks) d.IsChecked = on;
        }
    }

    public string MethodHint => EntireDrive && Level == WipeLevel.Quick
        ? "Quick removes the partition tables (MBR or GPT) and boot sectors in seconds. The drive looks empty, but recovery tools could still find the old files. Use it to reuse a drive you keep."
        : EntireDrive && Level == WipeLevel.Thorough
        ? "Thorough writes zeros over every sector of hard drives and flash drives (NIST SP 800-88 Clear), enough for any modern drive. SSD and NVMe drives get their firmware erase."
        : Method switch
        {
            WipeMethod.Zeros => "One pass of zeros (NIST SP 800-88 Clear) is enough for any modern hard drive and is the quickest.",
            WipeMethod.Dod3 => "DoD 5220.22-M: zeros, then ones, then random data, and the last pass is read back to check it.",
            WipeMethod.Dod7 => "DoD 5220.22-M ECE: the three DoD passes, a random pass, then the three DoD passes again. Takes about seven times as long.",
            _ => "30 passes: the DoD pattern (zeros, ones, random) ten times over. It isn't a published standard and no modern drive needs it; it takes about 30 times as long as one pass.",
        } + (EntireDrive ? " SSD and NVMe drives always get their firmware erase instead." : "");

    public string DriveNote => EntireDrive
        ? "Everything on the ticked drives is destroyed and can't be recovered. Drives that hold Windows, Kodiak Cleaner, your user folder or programs can't be ticked."
        : SelectedDrive is not { } d ? ""
        : $"{ByteSize.Format(d.Free)} of free space on {d.Letter}: will be overwritten. Your files are not touched." +
          (d.Kind is MediaKind.Ssd or MediaKind.NvmeSsd
              ? " This is an SSD: Windows already clears deleted data on it in the background, so overwriting free space mostly adds wear."
              : "");

    private string MethodFor(WipeDisk d) => WipeMethods.ForLevel(Level, d.Kind, Method) switch
    {
        null => "Partition Data Only",
        WipeMethod.Firmware => "Firmware Erase",
        WipeMethod m => WipeMethods.Name(m),
    };

    private string AfterText => Format is { } f
        ? $"Afterwards: one {f.PartitionStyle} partition, formatted {f.FileSystem}" + (f.SafeLabel.Length > 0 ? $" and named {f.SafeLabel}." : ".")
        : "Afterwards: no partitions and no MBR or GPT at all. Windows will ask to initialize it, like a new drive.";

    private async Task RefreshAsync()
    {
        IsBusy = true;
        ListNote = "Reading drives…";
        try
        {
            // Drives are read in the background: a sleeping disk can take seconds to answer.
            var (drives, disks, problem) = await Task.Run(() =>
            {
                List<WipeableDrive> v = new();
                List<WipeDisk> d = new();
                string? error = null;
                try { v = DriveWiper.ListDrives(); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { error = e.Message; }
                try { d = DriveWiper.ListDisks(); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or EntryPointNotFoundException) { error = e.Message; }
                return (v, d, error);
            });
            Drives.Clear();
            foreach (var d in drives) Drives.Add(d);
            SelectedDrive = Drives.FirstOrDefault(d => d.CanWipeEntireDrive) ?? Drives.FirstOrDefault();
            Disks.Clear();
            foreach (var d in disks.OrderBy(d => d.Number))
            {
                var row = new WipeDiskRow(d) { Method = MethodFor(d), Status = d.Reason ?? "Ready" };
                row.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(WipeDiskRow.IsChecked)) OnPropertyChanged(nameof(AllChecked)); };
                Disks.Add(row);
            }
            OnPropertyChanged(nameof(AllChecked));
            ListNote = problem is not null ? "Drives couldn't be read: " + problem
                : Disks.Count == 0 ? "No drives found."
                : Disks.Any(d => d.CanWipe) ? "" : "Only drives Windows or Kodiak Cleaner need are attached. Plug in the drive you want to erase and press Refresh.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task WipeAsync()
    {
        if (EntireDrive) await WipeDisksAsync();
        else await WipeFreeSpaceAsync();
    }

    private async Task WipeDisksAsync()
    {
        var chosen = Disks.Where(d => d.IsChecked && d.CanWipe).ToList();
        if (chosen.Count == 0) return;
        if (!Elevation.IsAdministrator) { Dialogs.Error("Erasing a whole drive needs administrator rights. Use Restart as Administrator, then try again."); return; }
        foreach (var row in chosen)
            if (WipeMethods.FormatProblem(row.Disk.Size, Format) is { } problem) { Dialogs.Error($"{row.Disk.Name} ({row.Size}): {problem}"); return; }

        // Three checks (Ted): the summary, then each drive by its number, then the word ERASE.
        var level = WipeMethods.LevelName(Level);
        var list = string.Join("\n", chosen.Select(d => $"•  {d.Disk.Name} ({d.Size}, {d.Disk.KindName}, {d.Disk.Drives}): {d.Method}"));
        if (!Dialogs.Warn($"{level} wipe of {(chosen.Count == 1 ? "this drive" : $"these {chosen.Count} drives")}?\n\n{list}\n\n{AfterText}",
                "Check 1 of 3: Erase Entire Drive",
                "Every file and every partition on them is deleted, and nothing can be recovered afterwards." +
                (Level == WipeLevel.Quick ? " Quick only removes the partition data, so recovery tools might still find old files." : "")))
            return;
        foreach (var row in chosen)
        {
            var d = row.Disk;
            var typedNumber = TextPrompt.Ask("Check 2 of 3: Confirm the Drive",
                $"Check 2 of 3 ({chosen.IndexOf(row) + 1} of {chosen.Count} drives)\n\n{d.Name}\n{row.Size}, {d.KindName}, {d.Drives}\n{level} wipe: {row.Method}\n\n" +
                $"All data on this drive will be deleted and can't be recovered. Type its disk number ({d.Number}) to confirm this is the right drive:");
            if (typedNumber?.Trim() != d.Number.ToString(System.Globalization.CultureInfo.InvariantCulture)) return;
        }
        var typed = TextPrompt.Ask("Check 3 of 3: Start the Wipe",
            $"Check 3 of 3. {chosen.Count} {(chosen.Count == 1 ? "drive" : "drives")} will be wiped ({level}) and the data can never be recovered.\n\nType ERASE to start:");
        if (!string.Equals(typed?.Trim(), "ERASE", StringComparison.OrdinalIgnoreCase)) return;

        IsBusy = true;
        _cts = new CancellationTokenSource();
        var done = new List<string>();
        try
        {
            foreach (var row in chosen)
            {
                if (_cts.IsCancellationRequested) break;
                var d = row.Disk;
                Percent = 0;
                row.Status = "Erasing…";
                var status = new Progress<string>(s => Status = $"{d.Name}: {s}");
                var progress = new Progress<OverwriteProgress>(p =>
                {
                    int steps = p.Passes + (Verify ? 1 : 0);
                    double fraction = p.Total > 0 ? (double)p.Done / p.Total : 0;
                    Percent = 100.0 * ((p.Verifying ? p.Passes : p.Pass - 1) + fraction) / steps;
                    Status = p.Verifying
                        ? $"{d.Name}: checking the last pass, {fraction:P0}"
                        : $"{d.Name}: pass {p.Pass} of {p.Passes} ({p.PassName}), {fraction:P0}";
                    row.Status = $"{Percent:0}%";
                });
                try
                {
                    var r = await DriveWiper.EraseAsync(d, Level, Method, Verify, Format, status, progress, _cts.Token);
                    Percent = 100;
                    row.Status = r.Verified == false ? $"Erased, {r.Mismatches:N0} Bytes Didn't Verify" : "Erased";
                    var how = r.Level == WipeLevel.Quick ? "cleared of its partition data (Quick)"
                        : r.FirmwareFellBack ? "overwritten with random data (it offers no firmware erase)"
                        : r.Method == WipeMethod.Firmware ? "erased by its firmware" : $"erased with {WipeMethods.Name(r.Method)}";
                    var after = !r.Reformatted
                        ? ". Windows couldn't finish setting it up afterwards; open Disk Management to initialize and format it."
                        : r.Format is { } f
                            ? $". It's now an empty {f.PartitionStyle} drive formatted {f.FileSystem}{(r.NewLetter is { } l ? $" ({l})" : "")}."
                            : ". It has no partitions or MBR/GPT left; Windows will ask to initialize it, like a new drive.";
                    done.Add($"{d.Name} was {how}" + (r.Verified == true ? " and verified" : r.Verified == false ? $", but {r.Mismatches:N0} bytes didn't verify (the drive may be failing)" : "") + after);
                }
                catch (OperationCanceledException)
                {
                    row.Status = "Stopped";
                    done.Add($"{d.Name} was stopped part way. It's only partly erased; erase it again before giving it away.");
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                    row.Status = "Not Erased";
                    done.Add($"{d.Name} wasn't erased: {e.Message}");
                }
            }
            Status = string.Join("\n", done);
        }
        finally
        {
            IsBusy = false;
            await RefreshAsync();
        }
        if (done.Count > 0) Dialogs.Info(string.Join("\n\n", done), "Drive Wiper");
    }

    private async Task WipeFreeSpaceAsync()
    {
        if (SelectedDrive is not { } d) return;
        var passes = WipeMethods.Passes(Method).Count;
        if (!Dialogs.Confirm($"Overwrite the {ByteSize.Format(d.Free)} of free space on {d.Letter}: with {WipeMethods.Name(Method)}?\n\n" +
                             "This takes a while and the drive will be full until it finishes." +
                             (d.Kind is MediaKind.Ssd or MediaKind.NvmeSsd ? " On SSDs, Windows already erases deleted data in the background, so this mostly adds wear." : "")))
            return;

        IsBusy = true;
        Percent = 0;
        _cts = new CancellationTokenSource();
        try
        {
            var progress = new Progress<WipeProgress>(p =>
            {
                Percent = p.BytesTotal > 0 ? 100.0 * ((p.Pass - 1) + (double)p.BytesWritten / p.BytesTotal) / p.Passes : 0;
                Status = $"Pass {p.Pass} of {p.Passes}: {ByteSize.Format(p.BytesWritten)} of {ByteSize.Format(p.BytesTotal)}";
            });
            var root = d.Root;
            await Task.Run(() => FreeSpaceWiper.Wipe(root, passes, () => new DriveInfo(root).AvailableFreeSpace, progress, _cts.Token));
            Percent = 100;
            Status = $"Free space on {d.Letter}: has been wiped with {WipeMethods.Name(Method)}.";
            ActivityLog.Write("Drive Wiper", $"Wiped free space on {d.Letter}: with {WipeMethods.Name(Method)}");
        }
        catch (OperationCanceledException)
        {
            Status = "Stopped. Temporary filler files were removed.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Status = "Wipe failed: " + e.Message;
        }
        finally
        {
            IsBusy = false;
            await RefreshAsync();
        }
    }
}
