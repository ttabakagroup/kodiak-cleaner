using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using KodiakCleaner.Core.Tools;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App.ViewModels;

/// <summary>A drive in the Defrag and TRIM list.</summary>
public sealed class OptimizeRow : ObservableObject
{
    public OptimizeRow(OptimizeDrive drive)
    {
        Drive = drive;
        _isChecked = CanOptimize;
        _status = CanOptimize ? "Ready" : "Flash drives don't need it";
    }

    public OptimizeDrive Drive { get; }
    public bool CanOptimize => Drive.Action != OptimizeAction.NotNeeded;
    public string Size => ByteSize.Format(Drive.Size);
    public string Used => Drive.Size > 0 ? $"{100.0 * (Drive.Size - Drive.Free) / Drive.Size:0}%" : "";

    private bool _isChecked;
    public bool IsChecked { get => _isChecked; set => Set(ref _isChecked, value && CanOptimize); }

    private string _status;
    public string Status { get => _status; set => Set(ref _status, value); }

    private double _percent;
    public double Percent
    {
        get => _percent;
        set
        {
            if (Set(ref _percent, value)) OnPropertyChanged(nameof(Started));
        }
    }

    /// <summary>Shows the row's progress bar once work on it has begun.</summary>
    public bool Started => _percent > 0;
}

/// <summary>
/// Defrag and TRIM: every drive gets the right step for its type (defragment a hard drive, TRIM an SSD) from Windows' own
/// optimizer, one drive at a time.
/// </summary>
public sealed class OptimizeViewModel : ObservableObject, ILoadOnFirstVisit
{
    private CancellationTokenSource? _cts;

    public OptimizeViewModel()
    {
        RefreshCommand = new RelayCommand(RefreshAsync, () => !IsBusy);
        AnalyzeCommand = new RelayCommand(AnalyzeAsync, () => !IsBusy && Drives.Any(d => d.IsChecked && d.Drive.Action == OptimizeAction.Defragment));
        OptimizeCommand = new RelayCommand(OptimizeAsync, () => !IsBusy && Drives.Any(d => d.IsChecked));
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy);
    }

    private bool _loaded;
    public void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _ = RefreshAsync();
    }

    public ObservableCollection<OptimizeRow> Drives { get; } = new();
    public ICommand RefreshCommand { get; }
    public ICommand AnalyzeCommand { get; }
    public ICommand OptimizeCommand { get; }
    public ICommand CancelCommand { get; }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }

    private double _percent;
    public double Percent { get => _percent; private set => Set(ref _percent, value); }

    private string _status = "";
    public string Status { get => _status; private set => Set(ref _status, value); }

    public bool? AllChecked
    {
        get => Drives.Any(d => d.CanOptimize) && Drives.Where(d => d.CanOptimize).All(d => d.IsChecked) ? true : Drives.Any(d => d.IsChecked) ? null : false;
        set
        {
            bool on = value ?? false;
            foreach (var d in Drives) d.IsChecked = on;
        }
    }

    private async Task RefreshAsync()
    {
        IsBusy = true;
        using var task = BackgroundTasks.Start(this, "Reading Drives");
        Status = "Reading drives…";
        try
        {
            var drives = await Task.Run(DriveOptimizer.ListDrives);
            Drives.Clear();
            foreach (var d in drives.OrderBy(d => d.Root, StringComparer.OrdinalIgnoreCase))
            {
                var row = new OptimizeRow(d);
                row.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(OptimizeRow.IsChecked)) OnPropertyChanged(nameof(AllChecked)); };
                Drives.Add(row);
            }
            OnPropertyChanged(nameof(AllChecked));
            Status = Drives.Count == 0 ? "No drives found." : "";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CheckAdmin()
    {
        if (Elevation.IsAdministrator) return true;
        Elevation.OfferRestart("Defrag and TRIM uses Windows' own optimizer, which needs administrator rights.");
        return false;
    }

    private async Task AnalyzeAsync()
    {
        if (!CheckAdmin()) return;
        await RunAsync(Drives.Where(d => d.IsChecked && d.Drive.Action == OptimizeAction.Defragment).ToList(), "Analyzing", async (row, pct, ct) =>
        {
            var f = await DriveOptimizer.AnalyzeAsync(row.Drive, pct, ct);
            row.Status = f is { } v ? $"{v}% Fragmented" + (v >= 10 ? ", Defragment Recommended" : "") : "Analyzed";
            return null;
        });
    }

    private async Task OptimizeAsync()
    {
        if (!CheckAdmin()) return;
        var lines = new List<string>();
        await RunAsync(Drives.Where(d => d.IsChecked).ToList(), "Optimizing", async (row, pct, ct) =>
        {
            var code = await DriveOptimizer.OptimizeAsync(row.Drive, pct, new Progress<string>(_ => { }), ct);
            row.Status = code == 0 ? row.Drive.Action switch
            {
                OptimizeAction.Defragment => "Defragmented",
                OptimizeAction.Trim => "Trimmed",
                _ => "Optimized",
            } : $"Not Finished (Code {code})";
            return code == 0 ? null : $"{row.Drive.Letter}: {row.Drive.ActionName} didn't finish (Windows' optimizer stopped with code {code}).";
        });
    }

    /// <summary>Runs <paramref name="step"/> on each drive in turn; a step returns a problem to report, or null.</summary>
    private async Task RunAsync(List<OptimizeRow> rows, string verb, Func<OptimizeRow, IProgress<int>, CancellationToken, Task<string?>> step)
    {
        if (rows.Count == 0) return;
        IsBusy = true;
        using var task = BackgroundTasks.Start(this, $"{verb} Drives");
        _cts = new CancellationTokenSource();
        var problems = new List<string>();
        int done = 0;
        try
        {
            foreach (var row in rows)
            {
                if (_cts.IsCancellationRequested) break;
                row.Status = $"{verb}…";
                row.Percent = 0;
                int index = done;
                task.Step(index, rows.Count);
                var pct = new Progress<int>(v =>
                {
                    row.Percent = v;
                    row.Status = $"{verb}… {v}%";
                    Percent = 100.0 * (index + v / 100.0) / rows.Count;
                    task.Percent = Percent;
                    Status = $"{verb} {row.Drive.Letter}: ({row.Drive.ActionName}), drive {index + 1} of {rows.Count}";
                });
                Status = $"{verb} {row.Drive.Letter}: ({row.Drive.ActionName}), drive {index + 1} of {rows.Count}";
                try
                {
                    if (await step(row, pct, _cts.Token) is { } problem) problems.Add(problem);
                    row.Percent = 100;
                }
                catch (OperationCanceledException)
                {
                    row.Status = "Stopped";
                }
                catch (Exception e) when (e is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    row.Status = "Not Finished";
                    problems.Add($"{row.Drive.Letter}: {e.Message}");
                }
                done++;
            }
            Percent = 100;
            Status = _cts.IsCancellationRequested ? "Stopped. Drives already finished keep their changes."
                : problems.Count > 0 ? string.Join("\n", problems)
                : $"Done. {done} {(done == 1 ? "drive" : "drives")} finished.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
