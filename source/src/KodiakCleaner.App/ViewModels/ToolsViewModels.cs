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
            var code = await Task.Run(() => RestorePoints.Create($"Kodiak Cleaner {DateTime.Now:g}"));
            IsBusy = false;
            await RefreshAsync();
            Summary = code != 0
                ? $"Windows could not create a restore point (error {code}). Check that System Protection is on."
                : Points.Count > before
                    ? "Restore point created."
                    : "Windows skipped it because a restore point was already made in the last 24 hours.";
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

public sealed class WiperViewModel : ObservableObject, ILoadOnFirstVisit
{
    private CancellationTokenSource? _cts;

    public WiperViewModel()
    {
        RefreshCommand = new RelayCommand(Refresh, () => !IsBusy);
        WipeCommand = new RelayCommand(WipeAsync, () => !IsBusy && SelectedDrive is not null);
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy);
        foreach (var (label, value) in new[] { ("Free Space Only", false), ("Entire Drive", true) })
            Modes.Add(new Choice<bool>(label, value, !value, v => { EntireDrive = v; }));
        foreach (var (label, value) in new[] { ("1 Pass (Zeros)", 1), ("3 Passes", 3), ("7 Passes", 7) })
            PassChoices.Add(new Choice<int>(label, value, value == 1, v => Passes = v));
    }

    private bool _loaded;
    /// <summary>Reads the list the first time the page is shown, so starting Kodiak Cleaner stays quick.</summary>
    public void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        Refresh();
    }

    public ObservableCollection<WipeableDrive> Drives { get; } = new();
    public List<Choice<bool>> Modes { get; } = new();
    public List<Choice<int>> PassChoices { get; } = new();
    public ICommand RefreshCommand { get; }
    public ICommand WipeCommand { get; }
    public ICommand CancelCommand { get; }

    private WipeableDrive? _selectedDrive;
    public WipeableDrive? SelectedDrive { get => _selectedDrive; set { Set(ref _selectedDrive, value); OnPropertyChanged(nameof(DriveNote)); } }

    private bool _entireDrive;
    public bool EntireDrive { get => _entireDrive; private set { Set(ref _entireDrive, value); OnPropertyChanged(nameof(DriveNote)); } }

    public int Passes { get; private set; } = 1;

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }

    private double _percent;
    public double Percent { get => _percent; private set => Set(ref _percent, value); }

    private string _status = "";
    public string Status { get => _status; private set => Set(ref _status, value); }

    public string DriveNote => SelectedDrive is not { } d ? ""
        : EntireDrive
            ? d.CanWipeEntireDrive
                ? $"Everything on {d.Letter}: ({ByteSize.Format(d.Size - d.Free)} of files) will be destroyed and can't be recovered."
                : $"{d.Letter}: can't be wiped entirely. {d.Reason}"
            : $"{ByteSize.Format(d.Free)} of free space on {d.Letter}: will be overwritten. Your files are not touched.";

    private void Refresh()
    {
        Drives.Clear();
        foreach (var d in DriveWiper.ListDrives()) Drives.Add(d);
        SelectedDrive = Drives.FirstOrDefault();
    }

    private async Task WipeAsync()
    {
        if (SelectedDrive is not { } d) return;
        if (EntireDrive)
        {
            if (!d.CanWipeEntireDrive) { Dialogs.Error(DriveNote); return; }
            if (!Elevation.IsAdministrator) { Dialogs.Error("Wiping an entire drive needs administrator rights. Use Restart as administrator."); return; }
            var typed = TextPrompt.Ask("Erase Entire Drive",
                $"This erases EVERYTHING on {d.Display} ({ByteSize.Format(d.Size)}). It can't be undone.\n\nType the drive letter ({d.Letter}) to confirm:");
            if (!string.Equals(typed?.Trim().TrimEnd(':'), d.Letter, StringComparison.OrdinalIgnoreCase)) return;
        }
        else if (!Dialogs.Confirm($"Overwrite the {ByteSize.Format(d.Free)} of free space on {d.Letter}: with {Passes} pass(es)?\n\n" +
                                  "This takes a while and the drive will be full until it finishes. On SSDs, Windows already erases deleted data in the background, so this mostly adds wear."))
        {
            return;
        }

        IsBusy = true;
        Percent = 0;
        _cts = new CancellationTokenSource();
        try
        {
            if (EntireDrive)
            {
                var output = new Progress<string>(line =>
                {
                    Status = line;
                    var pct = new string(line.TakeWhile(char.IsDigit).ToArray());
                    if (double.TryParse(pct, out var v)) Percent = v;
                });
                var code = await DriveWiper.WipeEntireDriveAsync(d, Passes, "Wiped", output, _cts.Token);
                Status = code == 0 ? $"{d.Letter}: has been erased." : $"Format stopped with exit code {code}. {Status}";
            }
            else
            {
                var progress = new Progress<WipeProgress>(p =>
                {
                    Percent = p.BytesTotal > 0 ? 100.0 * ((p.Pass - 1) + (double)p.BytesWritten / p.BytesTotal) / p.Passes : 0;
                    Status = $"Pass {p.Pass} of {p.Passes}: {ByteSize.Format(p.BytesWritten)} of {ByteSize.Format(p.BytesTotal)}";
                });
                var root = d.Root;
                await Task.Run(() => FreeSpaceWiper.Wipe(root, Passes, () => new DriveInfo(root).AvailableFreeSpace, progress, _cts.Token));
                Percent = 100;
                Status = $"Free space on {d.Letter}: has been wiped.";
            }
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
            Refresh();
        }
    }
}
