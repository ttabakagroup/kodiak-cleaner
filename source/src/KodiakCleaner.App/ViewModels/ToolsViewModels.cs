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
        RemoveCommand = new RelayCommand(() => RemoveAsync(Picked()), () => !IsBusy && Picked().Count > 0);
        RemoveRowCommand = new RelayCommand(() => RemoveAsync(Highlighted.Where(r => !r.IsNewest).ToList()), () => !IsBusy && Highlighted.Any(r => !r.IsNewest));
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

    public ObservableCollection<RestorePointRow> Points { get; } = new();
    public ICommand RefreshCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand RemoveRowCommand { get; }
    public ICommand OpenSystemRestoreCommand { get; }
    public ICommand OpenProtectionCommand { get; }

    /// <summary>The highlighted rows (Ctrl-click and Shift-click pick several, as on Startup and Programs). Set by the view.</summary>
    public IReadOnlyList<RestorePointRow> Highlighted { get; set; } = Array.Empty<RestorePointRow>();

    /// <summary>The header checkbox: true when every point that can be deleted is ticked, false when none, null when some.</summary>
    public bool? AllChecked
    {
        get
        {
            var rows = Points.Where(r => !r.IsNewest).ToList();
            return rows.Count > 0 && rows.All(r => r.IsChecked) ? true : rows.Any(r => r.IsChecked) ? null : false;
        }
        set
        {
            var on = value != false;
            foreach (var r in Points.Where(r => !r.IsNewest)) r.IsChecked = on;
        }
    }

    /// <summary>Ticked points, or the highlighted rows when nothing is ticked. Never the newest point, which Windows keeps.</summary>
    private List<RestorePointRow> Picked()
    {
        var ticked = Points.Where(r => r.IsChecked && !r.IsNewest).ToList();
        return ticked.Count > 0 ? ticked : Highlighted.Where(r => !r.IsNewest).ToList();
    }

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
        using var task = BackgroundTasks.Start(this, "Reading Restore Points");
        try
        {
            var list = await Task.Run(RestorePoints.List);
            Points.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                // The list is newest first; Windows always keeps the newest point, so it is shown greyed out and can't be ticked.
                var row = new RestorePointRow(list[i], isNewest: i == 0);
                row.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(RestorePointRow.IsChecked)) OnPropertyChanged(nameof(AllChecked)); };
                Points.Add(row);
            }
            OnPropertyChanged(nameof(AllChecked));
            Summary = list.Count == 0
                ? "No restore points found. System Protection may be turned off for your drives (Configure… turns it on)."
                : $"{list.Count} restore point(s). The newest one is greyed out: Windows keeps it, so you always have a way back.";
        }
        catch (Exception e)
        {
            // WMI can fail in several ways (access denied, service stopped, missing components); all mean "can't list".
            Summary = Elevation.IsAdministrator
                ? "Restore points could not be read: " + e.Message
                : "Restore points can only be listed with administrator rights. Use Restart as Administrator at the top.";
        }
        finally { IsBusy = false; }
    }

    private async Task CreateAsync()
    {
        if (!Elevation.IsAdministrator)
        {
            Elevation.OfferRestart("Creating a restore point needs administrator rights.");
            return;
        }
        IsBusy = true;
        using var task = BackgroundTasks.Start(this, "Creating a Restore Point");
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

    private async Task RemoveAsync(List<RestorePointRow> rows)
    {
        if (rows.Count == 0) return;
        var question = rows.Count == 1
            ? $"Delete the restore point \"{rows[0].Description}\" from {rows[0].Created:g}? This can't be undone."
            : $"Delete {rows.Count} restore points? This can't be undone.";
        if (!Dialogs.Confirm(question)) return;
        IsBusy = true;
        using var task = BackgroundTasks.Start(this, rows.Count == 1 ? "Deleting a Restore Point" : "Deleting Restore Points");
        var failed = new List<string>();
        foreach (var row in rows)
        {
            try
            {
                var code = await Task.Run(() => RestorePoints.Remove(row.Sequence));
                if (code != 0) failed.Add($"{row.Description} (error {code})");
                else ActivityLog.Write("Restore", $"System Restore point deleted: {row.Description} ({row.Created:g})");
            }
            catch (Exception e)
            {
                failed.Add($"{row.Description} ({e.Message})");
            }
        }
        IsBusy = false;
        if (failed.Count > 0)
            Dialogs.Error("Windows could not delete " + (failed.Count == 1 ? "this restore point" : "these restore points") + ":\n\n" +
                          string.Join("\n", failed) + "\n\nDeleting restore points needs administrator rights.");
        await RefreshAsync();
    }
}

/// <summary>One row of the System Restore list: the point, its tick box, and whether it is the newest (which Windows keeps).</summary>
public sealed class RestorePointRow : ObservableObject
{
    public RestorePointRow(RestorePoint point, bool isNewest)
    {
        Point = point;
        IsNewest = isNewest;
    }

    public RestorePoint Point { get; }
    public int Sequence => Point.Sequence;
    public DateTime Created => Point.Created;
    public string Description => Point.Description;
    public string Kind => Point.Kind;

    /// <summary>Windows always keeps the newest restore point, so its row is greyed out and can't be ticked or deleted.</summary>
    public bool IsNewest { get; }
    public bool CanCheck => !IsNewest;
    public string? Details => IsNewest ? "This is your newest restore point. Windows keeps it, so it can't be deleted." : null;

    private bool _isChecked;
    public bool IsChecked { get => _isChecked; set => Set(ref _isChecked, value && !IsNewest); }
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
        using var task = BackgroundTasks.Start(this, "Reading Snapshots");
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
                        : "Snapshots can only be listed with administrator rights. Use Restart as Administrator at the top."
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
            Elevation.OfferRestart("Changing Point-in-Time Restore needs administrator rights.");
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
            Elevation.OfferRestart("Taking a snapshot needs administrator rights.");
            return;
        }
        IsBusy = true;
        using var task = BackgroundTasks.Start(this, "Taking a Snapshot");
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
    public string Method
    {
        get => _method;
        set
        {
            Set(ref _method, value);
            OnPropertyChanged(nameof(MethodTip));
        }
    }

    /// <summary>The Method column's tooltip: what that method does to this drive, in plain words (Ted, 0.8.115).</summary>
    public string MethodTip => Method == WiperViewModel.QuickName
        ? "Only the partition data is removed. The drive looks empty, but the old files could still be recovered."
        : Method == WiperViewModel.SecureEraseName
        ? "The drive runs its own built-in secure erase. It wipes the data, not the drive's firmware, so the drive keeps working. If the drive doesn't offer one, it's overwritten with random data instead."
        : $"Every part of the drive is overwritten ({Method}).";

    private string _status = "";
    public string Status { get => _status; set => Set(ref _status, value); }
}

/// <summary>One drive's check result in the progress card: the drive, its verdict chip, the reasons and the facts behind them.</summary>
public sealed class DriveCheckRow(string drive, DriveCheckResult result)
{
    public string Drive { get; } = drive;
    public DriveCheckResult Result { get; } = result;
    public string Tone => Result.Tone;
    public bool HasReasons => Result.Reasons.Count > 0;
}

public sealed class WiperViewModel : ObservableObject, ILoadOnFirstVisit
{
    private CancellationTokenSource? _cts;

    public WiperViewModel()
    {
        RefreshCommand = new RelayCommand(() => { ClearResult(); return RefreshAsync(); }, () => !IsBusy);
        WipeCommand = new RelayCommand(WipeAsync, () => !IsBusy && (EntireDrive ? Disks.Any(d => d.IsChecked) : SelectedDrive is not null));
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy);
        CheckCommand = new RelayCommand(CheckDisksAsync, () => !IsBusy && EntireDrive && Disks.Any(d => d.IsChecked));
        // Open Reports (Ted, 0.8.115): every drive check is saved there; the folder is made if it isn't there yet.
        OpenReportsCommand = new RelayCommand(() => Elevation.OpenFolder(ReportsFolder));
        UseRecommendedCommand = new RelayCommand(UseRecommended, () => !IsBusy && IsCustomFormat);
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
    /// <summary>Quick (partition data only), Thorough (one pass or built-in secure erase) or Secure (the chosen passes or built-in secure erase).</summary>
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
            OnPropertyChanged(nameof(FormatEnabled));
        }
    }

    /// <summary>The format choices stay on screen but are grayed out while the drive is to be left uninitialized (Ted).</summary>
    public bool FormatEnabled => !LeaveUninitialized;

    private string _fileSystem = "NTFS";
    public string FileSystem { get => _fileSystem; private set { Set(ref _fileSystem, value); OnRecommendChanged(); } }

    private bool _gpt = true;
    public bool Gpt { get => _gpt; private set { Set(ref _gpt, value); OnRecommendChanged(); } }

    private string _label = "Wiped";
    public string Label { get => _label; set => Set(ref _label, value); }

    private DiskFormat? Format => LeaveUninitialized ? null : new DiskFormat(FileSystem, Gpt, Label);

    private WipeDisk? _recommendedFor;
    private DiskFormat? _recommended;

    /// <summary>Ticking a drive picks the format that suits its type (Ted): flash drive, SSD or large storage drive. It can still be changed.</summary>
    private void Recommend(WipeDisk d)
    {
        _recommendedFor = d;
        _recommended = WipeMethods.Recommended(d.Kind, d.Removable, d.Size, Label);
        foreach (var c in StyleChoices) c.IsRecommended = c.Value == _recommended.Gpt;
        foreach (var c in FileSystemChoices) c.IsRecommended = c.Value == _recommended.FileSystem;
        UseRecommended();
    }

    private void UseRecommended()
    {
        if (_recommended is not { } f) return;
        foreach (var c in StyleChoices) c.IsSelected = c.Value == f.Gpt;
        foreach (var c in FileSystemChoices) c.IsSelected = c.Value == f.FileSystem;
        OnRecommendChanged();
    }

    private void OnRecommendChanged()
    {
        OnPropertyChanged(nameof(RecommendTitle));
        OnPropertyChanged(nameof(RecommendText));
        OnPropertyChanged(nameof(IsCustomFormat));
    }

    /// <summary>The notice above the format choices: which drive type it's for, or the defaults before a drive is ticked.</summary>
    public string RecommendTitle => _recommendedFor is { } d
        ? $"Recommended for {d.Name} ({d.KindName}, {ByteSize.Format(d.Size)})"
        : "Recommended Format for Each Drive Type";

    public string RecommendText => _recommendedFor is { } d && _recommended is not null
        ? WipeMethods.RecommendedWhy(d.Kind, d.Removable, d.Size) + (IsCustomFormat
            ? " You changed it, so your own choice will be used."
            : " It was picked when you ticked the drive, and the choices marked Recommended below show it. You can still change them.")
        : "Ticking a drive picks its format for you: flash drives get MBR with FAT32 (32 GB or smaller) or exFAT; SSDs and internal hard drives get GPT with NTFS; other USB drives get GPT with exFAT.";

    /// <summary>The format differs from the one recommended for the ticked drive.</summary>
    public bool IsCustomFormat => _recommended is { } f && (f.Gpt != Gpt || f.FileSystem != FileSystem);

    public ICommand UseRecommendedCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand WipeCommand { get; }
    public ICommand CancelCommand { get; }
    /// <summary>Check Drive: the ticked drives' health check, without erasing anything (Ted, 0.8.114).</summary>
    public ICommand CheckCommand { get; }
    /// <summary>Opens Reports\Integrity Check in File Explorer, where each drive check's report is saved (Ted, 0.8.115).</summary>
    public ICommand OpenReportsCommand { get; }

    private static string ReportsFolder => Path.Combine(Settings.Data.Reports, "Integrity Check");

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

    private bool _checkAfterwards = true;
    /// <summary>
    /// Check the Drive Afterwards (Ted, 0.8.114): after the wipe and setup, each drive gets the same health check as Check Drive.
    /// When the wipe already read its last pass back, that counts as the surface test and the sectors aren't read again.
    /// </summary>
    public bool CheckAfterwards { get => _checkAfterwards; set => Set(ref _checkAfterwards, value); }

    /// <summary>Each checked drive's verdict, shown in the progress card with the result until the next wipe, check or Refresh.</summary>
    public ObservableCollection<DriveCheckRow> HealthResults { get; } = new();
    public bool HasHealthResults => HealthResults.Count > 0;

    /// <summary>Open Reports shows in the progress card once a drive check has finished and its report is saved.</summary>
    public bool ShowOpenReports => HasResult && HasHealthResults;

    private void OnHealthResultsChanged()
    {
        OnPropertyChanged(nameof(HasHealthResults));
        OnPropertyChanged(nameof(ShowOpenReports));
    }

    private string _stopName = "Stop the Wipe";
    /// <summary>The Stop button's name for screen readers: it stops a wipe or a check.</summary>
    public string StopName { get => _stopName; private set => Set(ref _stopName, value); }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }

    private double _percent;
    public double Percent { get => _percent; private set => Set(ref _percent, value); }

    private string _status = "";
    /// <summary>The finished wipe's summary, shown in the progress card until the next wipe or Refresh.</summary>
    public string Status { get => _status; private set => Set(ref _status, value); }

    // The wipe's own card at the top of the page (Ted, 0.8.113): only wipe progress, shown while wiping and, afterwards,
    // with the result until the next wipe or Refresh. Reading the drives (IsBusy too) never shows it.
    private bool _isWiping;
    public bool IsWiping
    {
        get => _isWiping;
        private set
        {
            Set(ref _isWiping, value);
            OnPropertyChanged(nameof(ShowWipeCard));
            OnPropertyChanged(nameof(HasResult));
            OnPropertyChanged(nameof(ShowOpenReports));
        }
    }

    private bool _hasResult;
    /// <summary>A wipe has finished (or stopped) and its summary is still shown.</summary>
    public bool HasResult
    {
        get => _hasResult && !IsWiping;
        private set
        {
            Set(ref _hasResult, value);
            OnPropertyChanged(nameof(ShowWipeCard));
            OnPropertyChanged(nameof(ShowOpenReports));
        }
    }

    public bool ShowWipeCard => IsWiping || HasResult;

    private RunningTask? _wipeTask;
    /// <summary>The running wipe's task: its percentage, and elapsed time and time left (the same as the corner card shows).</summary>
    public RunningTask? WipeTask { get => _wipeTask; private set => Set(ref _wipeTask, value); }

    private string _wipeTitle = "";
    /// <summary>"Wiping Free Space", "Erasing Entire Drive"; afterwards "Wipe Finished", "Wipe Stopped" or "Wipe Failed".</summary>
    public string WipeTitle { get => _wipeTitle; private set => Set(ref _wipeTitle, value); }

    private string _wipeDrive = "";
    /// <summary>The drive being wiped now: letter or disk, size, type, and "Drive 1 of 2" when several are erased.</summary>
    public string WipeDrive { get => _wipeDrive; private set => Set(ref _wipeDrive, value); }

    private string _wipeStep = "";
    /// <summary>Where the wipe is: "Pass 2 of 3 (Ones): 12.0 GB of 40.0 GB", "Checking the last pass", "Dismounting volumes".</summary>
    public string WipeStep { get => _wipeStep; private set => Set(ref _wipeStep, value); }

    private string _wipeTime = "";
    /// <summary>After the wipe: how long it took.</summary>
    public string WipeTime { get => _wipeTime; private set => Set(ref _wipeTime, value); }

    private string _wipeGlyph = "\uE74D";
    public string WipeGlyph { get => _wipeGlyph; private set => Set(ref _wipeGlyph, value); }

    /// <summary>Starts the card for a new wipe; the summary of the one before goes away.</summary>
    private void BeginWipe(RunningTask task, string title, string drive)
    {
        Status = "";
        HealthResults.Clear();
        OnHealthResultsChanged();
        StopName = "Stop the Wipe";
        WipeTime = "";
        WipeStep = "Starting…";
        WipeDrive = drive;
        WipeTitle = title;
        WipeGlyph = "\uE74D";
        WipeTask = task;
        HasResult = false;
        IsWiping = true;
    }

    /// <summary>Turns the card into the result: what happened, the summary in <see cref="Status"/> and how long it took.</summary>
    private void EndWipe(string title, string glyph)
    {
        if (WipeTask is { } t) WipeTime = "Took " + ProgressClock.FormatElapsed(DateTime.UtcNow - t.Started);
        WipeTitle = title;
        WipeGlyph = glyph;
        WipeStep = "";
        WipeTask = null;
        HasResult = true;
        IsWiping = false;
    }

    private void ClearResult()
    {
        if (IsWiping) return;
        HasResult = false;
        Status = "";
        HealthResults.Clear();
        OnHealthResultsChanged();
    }

    private const string GlyphDone = "\uE73E", GlyphStopped = "\uE711", GlyphFailed = "\uE783", GlyphWarning = "\uE7BA", GlyphCheck = "\uE9D9";

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

    // Plain words (Ted, 0.8.115): a built-in secure erase wipes the data, never the drive's firmware, and the drive keeps working.
    private const string SsdNote = " SSDs and NVMe drives run their own built-in secure erase instead: it wipes the data, not the drive, which keeps working and is set up again in Step 3.";

    public string MethodHint => EntireDrive && Level == WipeLevel.Quick
        ? "Quick only removes the partition tables (MBR or GPT) and boot sectors, in seconds. The drive looks empty, but recovery tools could still find the old files, so use it only for a drive you're keeping."
        : EntireDrive && Level == WipeLevel.Thorough
        ? "Thorough writes zeros once over every sector of hard drives and flash drives (NIST SP 800-88 Clear), which is enough for any modern drive." + SsdNote
        : Method switch
        {
            WipeMethod.Zeros => "One pass of zeros (NIST SP 800-88 Clear) is enough for any modern hard drive and is the quickest.",
            WipeMethod.Dod3 => "DoD 5220.22-M: zeros, then ones, then random data, and the last pass is read back to check it.",
            WipeMethod.Dod7 => "DoD 5220.22-M ECE: the three DoD passes, a random pass, then the three DoD passes again. Takes about seven times as long.",
            _ => "30 passes: the DoD pattern (zeros, ones, random) ten times over. It isn't a published standard and no modern drive needs it; it takes about 30 times as long as one pass.",
        } + (EntireDrive ? SsdNote : "");

    public string DriveNote => EntireDrive
        ? "Everything on the ticked drives is destroyed and can't be recovered. Drives that hold Windows, Kodiak Cleaner, your user folder or programs can't be ticked."
        : SelectedDrive is not { } d ? ""
        : $"{ByteSize.Format(d.Free)} of free space on {d.Letter}: will be overwritten. Your files are not touched." +
          (d.Kind is MediaKind.Ssd or MediaKind.NvmeSsd
              ? " This is an SSD: Windows already clears deleted data on it in the background, so overwriting free space mostly adds wear."
              : "");

    internal const string QuickName = "Partition Data Only";
    /// <summary>What users see for <see cref="WipeMethod.Firmware"/> (Ted, 0.8.115: the old name, Firmware Erase, read as if the firmware itself were erased).</summary>
    internal static readonly string SecureEraseName = WipeMethods.Name(WipeMethod.Firmware);

    private string MethodFor(WipeDisk d) => WipeMethods.ForLevel(Level, d.Kind, Method) switch
    {
        null => QuickName,
        WipeMethod m => WipeMethods.Name(m),
    };

    private string AfterText => Format is { } f
        ? $"Afterwards: one {f.PartitionStyle} partition, formatted {f.FileSystem}" + (f.SafeLabel.Length > 0 ? $" and named {f.SafeLabel}." : ".")
        : "Afterwards: no partitions and no MBR or GPT at all. Windows will ask to initialize it, like a new drive.";

    private async Task RefreshAsync()
    {
        IsBusy = true;
        using var task = BackgroundTasks.Start(this, "Reading Drives");
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
                row.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName != nameof(WipeDiskRow.IsChecked)) return;
                    OnPropertyChanged(nameof(AllChecked));
                    if (row.IsChecked) Recommend(row.Disk);
                };
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
        if (!Elevation.IsAdministrator) { Elevation.OfferRestart("Erasing a whole drive needs administrator rights."); return; }
        foreach (var row in chosen)
            if (WipeMethods.FormatProblem(row.Disk.Size, Format) is { } problem) { Dialogs.Error($"{row.Disk.Name} ({row.Size}): {problem}"); return; }

        // Three checks (Ted): the summary, then each drive by its number, then the word ERASE.
        var level = WipeMethods.LevelName(Level);
        var list = string.Join("\n", chosen.Select(d => $"•  {d.Disk.Name} ({d.Size}, {d.Disk.KindName}, {d.Disk.Drives}): {d.Method}"));
        if (!Dialogs.Warn($"{level} wipe of {(chosen.Count == 1 ? "this drive" : $"these {chosen.Count} drives")}?\n\n{list}\n\n{AfterText}" +
                (CheckAfterwards ? "\nThen each drive's health is checked (Check the Drive Afterwards)." : ""),
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
        using var task = BackgroundTasks.Start(this, chosen.Count == 1 ? "Erasing a Drive" : "Erasing Drives");
        var wipeTitle = chosen.Count == 1 ? $"Erasing Entire Drive ({level})" : $"Erasing Entire Drives ({level})";
        BeginWipe(task, wipeTitle, "");
        _cts = new CancellationTokenSource();
        var done = new List<string>();
        int stopped = 0, failed = 0, checkStopped = 0;
        var verdicts = new List<DriveVerdict>();
        try
        {
            foreach (var row in chosen)
            {
                if (_cts.IsCancellationRequested) break;
                var d = row.Disk;
                int index = chosen.IndexOf(row);
                // A Quick wipe or a built-in secure erase doesn't say how far along it is; with several drives, each finished one counts.
                task.Percent = chosen.Count > 1 ? 100.0 * index / chosen.Count : null;
                Percent = 0;
                (WipeTitle, WipeGlyph, StopName) = (wipeTitle, "\uE74D", "Stop the Wipe"); // the previous drive's check may have changed them
                row.Status = "Erasing…";
                WipeDrive = $"{d.Name} · {row.Size} · {d.KindName} · {row.Method}" + (chosen.Count > 1 ? $" · Drive {index + 1} of {chosen.Count}" : "");
                WipeStep = "Starting…";
                var status = new Progress<string>(s => WipeStep = s);
                var progress = new Progress<OverwriteProgress>(p =>
                {
                    int steps = p.Passes + (Verify ? 1 : 0);
                    double fraction = p.Total > 0 ? (double)p.Done / p.Total : 0;
                    Percent = 100.0 * ((p.Verifying ? p.Passes : p.Pass - 1) + fraction) / steps;
                    WipeStep = p.Verifying
                        ? $"Checking the last pass: {ByteSize.Format(p.Done)} of {ByteSize.Format(p.Total)}"
                        : $"Pass {p.Pass} of {p.Passes} ({p.PassName}): {ByteSize.Format(p.Done)} of {ByteSize.Format(p.Total)}";
                    row.Status = $"{Percent:0}%";
                    task.Percent = (index + Percent / 100) * 100 / chosen.Count;
                });
                try
                {
                    var r = await DriveWiper.EraseAsync(d, Level, Method, Verify, Format, status, progress, _cts.Token);
                    Percent = 100;
                    row.Status = r.Verified == false ? $"Erased, {r.Mismatches:N0} Bytes Didn't Verify" : "Erased";
                    var how = r.Level == WipeLevel.Quick ? "cleared of its partition data (Quick)"
                        : r.FirmwareFellBack ? "overwritten with random data (it doesn't offer a built-in secure erase)"
                        : r.Method == WipeMethod.Firmware ? "erased with its own built-in secure erase" : $"erased with {WipeMethods.Name(r.Method)}";
                    var after = !r.Reformatted
                        ? ". Windows couldn't finish setting it up afterwards" + (r.SetupError is { } why ? $" ({why})" : "") + "; open Disk Management to initialize and format it."
                        : r.Format is { } f
                            ? $". It's now an empty {f.PartitionStyle} drive formatted {f.FileSystem}{(r.NewLetter is { } l ? $" ({l})" : "")}."
                            : ". It has no partitions or MBR/GPT left; Windows will ask to initialize it, like a new drive.";
                    done.Add($"{d.Name} was {how}" + (r.Verified == true ? " and verified" : r.Verified == false ? $", but {r.Mismatches:N0} bytes didn't verify (the drive may be failing)" : "") + after);
                    if (CheckAfterwards && !_cts.IsCancellationRequested)
                    {
                        // The wipe's read-back already wrote and read every sector, so it stands in for the surface scan.
                        WipeTitle = $"Checking {d.Name}";
                        var check = await RunCheckAsync(row, r.Verified is null ? null : new WipeReadBack(r.Mismatches), index, chosen.Count, task, _cts.Token);
                        if (check is null)
                        {
                            checkStopped++;
                            done[^1] += " Its check was stopped.";
                        }
                        else
                        {
                            verdicts.Add(check.Verdict);
                            done[^1] += $" Drive check: {check.VerdictName}. {check.Summary}";
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    stopped++;
                    row.Status = "Stopped";
                    done.Add($"{d.Name} was stopped part way. It's only partly erased; erase it again before giving it away.");
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                    failed++;
                    row.Status = "Not Erased";
                    done.Add($"{d.Name} wasn't erased: {e.Message}");
                }
            }
            Status = string.Join("\n", done);
        }
        finally
        {
            EndWipe(stopped > 0 ? "Wipe Stopped" : checkStopped > 0 ? "Wipe Finished, Check Stopped"
                    : failed == 0 ? "Wipe Finished" : failed < done.Count ? "Some Drives Weren't Erased" : "Wipe Failed",
                stopped > 0 || checkStopped > 0 ? GlyphStopped : failed > 0 || verdicts.Contains(DriveVerdict.Failing) ? GlyphFailed
                    : verdicts.Contains(DriveVerdict.Warning) ? GlyphWarning : GlyphDone);
            IsBusy = false;
            await RefreshAsync();
        }
        if (done.Count > 0) Dialogs.Info(string.Join("\n\n", done), "Drive Wiper");
    }

    /// <summary>Check Drive: the ticked drives' health report, logged errors and a read-only scan of every sector. Never writes.</summary>
    private async Task CheckDisksAsync()
    {
        var chosen = Disks.Where(d => d.IsChecked && d.CanWipe).ToList();
        if (chosen.Count == 0) return;
        if (!Elevation.IsAdministrator)
        {
            Elevation.OfferRestart("Checking a drive reads every sector straight from the disk and asks it for its error counts, which needs administrator rights.");
            return;
        }

        IsBusy = true;
        using var task = BackgroundTasks.Start(this, chosen.Count == 1 ? "Checking a Drive" : "Checking Drives");
        BeginWipe(task, chosen.Count == 1 ? "Checking Drive Health" : $"Checking Drive Health ({chosen.Count} Drives)", "");
        StopName = "Stop the Check";
        _cts = new CancellationTokenSource();
        var verdicts = new List<DriveVerdict>();
        bool stopped = false;
        try
        {
            foreach (var row in chosen)
            {
                if (_cts.IsCancellationRequested) break;
                var check = await RunCheckAsync(row, null, chosen.IndexOf(row), chosen.Count, task, _cts.Token);
                if (check is null)
                {
                    stopped = true;
                    break;
                }
                verdicts.Add(check.Verdict);
            }
            Status = verdicts.Count == 0 ? "The check was stopped before any drive was finished. Nothing was written."
                : $"Checked {verdicts.Count} {(verdicts.Count == 1 ? "drive" : "drives")}" + (stopped ? " before the check was stopped" : "") +
                  $". Nothing was written to {(verdicts.Count == 1 ? "it" : "them")}; a report is saved in Reports\\Integrity Check.";
        }
        finally
        {
            EndWipe(stopped ? "Check Stopped" : verdicts.Contains(DriveVerdict.Failing) ? "Check Finished: Problems Found"
                    : verdicts.Contains(DriveVerdict.Warning) ? "Check Finished: Worth Watching" : "Check Finished",
                stopped ? GlyphStopped : verdicts.Contains(DriveVerdict.Failing) ? GlyphFailed
                    : verdicts.Contains(DriveVerdict.Warning) ? GlyphWarning : GlyphDone);
            IsBusy = false;
        }
    }

    /// <summary>
    /// Checks one drive with the progress card showing the scan; adds its verdict to <see cref="HealthResults"/>, logs it and saves a
    /// report under Reports\Integrity Check. Null when it was stopped.
    /// </summary>
    private async Task<DriveCheckResult?> RunCheckAsync(WipeDiskRow row, WipeReadBack? readBack, int index, int count, RunningTask task, CancellationToken ct)
    {
        var d = row.Disk;
        row.Status = "Checking…";
        WipeGlyph = GlyphCheck;
        StopName = "Stop the Check";
        WipeDrive = $"{d.Name} · {row.Size} · {d.KindName}" + (count > 1 ? $" · Drive {index + 1} of {count}" : "");
        WipeStep = "Starting the check…";
        Percent = 0;
        // With the wipe's read-back standing in for the scan there's nothing to measure, so the bar just moves.
        task.Percent = count > 1 ? 100.0 * index / count : readBack is null ? 0 : null;
        var status = new Progress<string>(s => WipeStep = s);
        var progress = new Progress<SurfaceProgress>(p =>
        {
            Percent = p.Total > 0 ? 100.0 * p.Done / p.Total : 0;
            var found = (p.UnreadableAreas, p.SlowAreas) switch
            {
                (0, 0) => "",
                var (bad, slow) => $" · {bad} unreadable, {slow} slow so far",
            };
            WipeStep = $"Reading every sector (nothing is written): {ByteSize.Format(p.Done)} of {ByteSize.Format(p.Total)}{found}";
            task.Percent = (index + Percent / 100) * 100 / count;
        });
        try
        {
            var result = await DriveHealthCheck.CheckAsync(d.Number, readBack, status, progress, ct);
            var name = $"{d.Name} ({row.Size}, {d.KindName})";
            HealthResults.Add(new DriveCheckRow(name, result));
            OnHealthResultsChanged();
            row.Status = result.VerdictName;
            ActivityLog.Write("Drive Wiper", $"Checked {name}: {result.VerdictName}" +
                (result.Reasons.Count > 0 ? " (" + string.Join(" ", result.Reasons) + ")" : "") + (readBack is null ? "" : ", surface from the wipe's read-back"));
            var verdict = result.Verdict switch
            {
                DriveVerdict.Healthy => IntegrityResult.Healthy,
                DriveVerdict.Warning => IntegrityResult.Attention,
                DriveVerdict.Failing => IntegrityResult.ProblemsFound,
                _ => IntegrityResult.CouldNotCheck,
            };
            try { IntegrityCheck.SaveReport(Settings.Data.Reports, $"Drive Check Disk {d.Number}", name, DriveHealthCheck.ReportLines(name, result), verdict, DateTime.Now); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            return result;
        }
        catch (OperationCanceledException)
        {
            row.Status = "Check Stopped";
            return null;
        }
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
        using var task = BackgroundTasks.Start(this, "Wiping Free Space");
        var method = Method;
        var patterns = WipeMethods.Passes(method);
        BeginWipe(task, "Wiping Free Space", $"{d.Display} · {ByteSize.Format(d.Free)} free · {d.KindName} · {WipeMethods.Name(method)}");
        Percent = 0;
        task.Percent = 0;
        _cts = new CancellationTokenSource();
        string title = "Wipe Finished", glyph = GlyphDone;
        try
        {
            var progress = new Progress<WipeProgress>(p =>
            {
                Percent = p.BytesTotal > 0 ? 100.0 * ((p.Pass - 1) + (double)p.BytesWritten / p.BytesTotal) / p.Passes : 0;
                task.Percent = Percent;
                var name = p.Pass >= 1 && p.Pass <= patterns.Count ? $" ({patterns[p.Pass - 1].Name})" : "";
                WipeStep = $"Pass {p.Pass} of {p.Passes}{name}: {ByteSize.Format(p.BytesWritten)} of {ByteSize.Format(p.BytesTotal)}";
            });
            var root = d.Root;
            await Task.Run(() => FreeSpaceWiper.Wipe(root, passes, () => new DriveInfo(root).AvailableFreeSpace, progress, _cts.Token));
            Percent = 100;
            task.Percent = 100;
            Status = $"Free space on {d.Letter}: has been wiped with {WipeMethods.Name(method)}.";
            ActivityLog.Write("Drive Wiper", $"Wiped free space on {d.Letter}: with {WipeMethods.Name(method)}");
        }
        catch (OperationCanceledException)
        {
            (title, glyph) = ("Wipe Stopped", GlyphStopped);
            Status = "Stopped. Temporary filler files were removed.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            (title, glyph) = ("Wipe Failed", GlyphFailed);
            Status = "Wipe failed: " + e.Message;
        }
        finally
        {
            EndWipe(title, glyph);
            IsBusy = false;
            await RefreshAsync();
        }
    }
}
