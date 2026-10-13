using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KodiakCleaner.App.Views;
using KodiakCleaner.Core.Tools;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App.ViewModels;

/// <summary>What to scan, as picked in <see cref="DiskAnalyzerPickWindow"/>: one or more drives, or one folder.</summary>
public sealed record DiskTarget(IReadOnlyList<(string Path, string Name)> Roots, string Description);

/// <summary>A folder, a file, or a group of small files in the open folder: one box in the map and one row in the list.</summary>
public sealed class DiskEntry
{
    private IReadOnlyList<DiskEntry>? _inner;

    public DiskEntry(DiskNode folder, long whole) : this(folder.Name, folder.Bytes, folder.Files, folder.MainKind, whole) => Folder = folder;

    public DiskEntry(DiskFile file, long whole) : this(file.Name, file.Bytes, 1, file.Kind, whole) => File = file;

    public DiskEntry(string name, long bytes, long files, FileKind kind, long whole)
    {
        Name = name;
        Bytes = bytes;
        FileCount = files;
        Kind = kind;
        Share = whole > 0 ? 100.0 * bytes / whole : 0;
    }

    public DiskNode? Folder { get; }
    public DiskFile? File { get; }
    public string Name { get; }
    public long Bytes { get; }
    public long FileCount { get; }
    public FileKind Kind { get; }
    public double Share { get; }

    public string Size => ByteSize.Format(Bytes);
    public string ShareText => Share >= 0.1 || Share == 0 ? $"{Share:0.#}%" : "<0.1%";
    public string Files => File is null ? FileCount.ToString("N0") : "";
    public string Location => File is not null ? Path.GetDirectoryName(File.Path) ?? "" : Folder?.Path ?? "";
    public string? FullPath => File?.Path ?? (Folder is { IsGroup: false } f ? f.Path : null);
    public Brush Fill => DiskColors.Fill(Kind);
    public string Glyph => Folder is { IsDrive: true } ? "" : Folder is not null ? "" : File is not null ? "" : "";

    /// <summary>What a folder's box shows inside it: its biggest folders and one box for the files right in it.</summary>
    public IReadOnlyList<DiskEntry> Inner => _inner ??= BuildInner();

    /// <summary>The files right in this folder, read when its parent was opened (only for folders in the open folder), else null.</summary>
    public IReadOnlyList<DiskFile>? LooseFiles { get; init; }

    /// <summary>Whether the map has anything to show inside this box: folders, or more than one file.</summary>
    public bool HasInside => Folder is { } f && (f.Children.Count > 0 || LooseFiles is { Count: > 1 });

    private IReadOnlyList<DiskEntry> BuildInner()
    {
        if (Folder is not { } f) return Array.Empty<DiskEntry>();
        var list = f.Children.Take(120).Where(c => c.Bytes > 0).Select(c => new DiskEntry(c, f.Bytes)).ToList();
        if (LooseFiles is { } files)
        {
            list.AddRange(files.Take(60).Where(x => x.Bytes > 0).Select(x => new DiskEntry(x, f.Bytes)));
            if (files.Count > 60) list.Add(Rest(files.Skip(60).ToList(), f.Bytes));
        }
        else if (f.OwnBytes > 0) list.Add(new DiskEntry(f.OwnFiles == 1 ? "1 File" : $"{f.OwnFiles:N0} Files", f.OwnBytes, f.OwnFiles, f.OwnMainKind, f.Bytes));
        return list;
    }

    /// <summary>One box for many small files, colored by the kind that takes most of their space.</summary>
    public static DiskEntry Rest(IReadOnlyList<DiskFile> files, long whole)
    {
        var kinds = new long[FileKinds.All.Count];
        foreach (var f in files) kinds[(int)f.Kind] += f.Bytes;
        var main = Enumerable.Range(0, kinds.Length).OrderByDescending(i => kinds[i]).First();
        return new DiskEntry($"{files.Count:N0} Smaller Files", files.Sum(f => f.Bytes), files.Count, (FileKind)main, whole);
    }

    /// <summary>The lines under the name in the map's tooltip.</summary>
    public IEnumerable<string> Details
    {
        get
        {
            yield return $"{Size}, {ShareText} of this folder";
            if (Folder is { } f)
            {
                var files = f.Files == 1 ? "1 file" : $"{f.Files:N0} files";
                yield return (f.Folders == 0 ? files : $"{files} in {(f.Folders == 1 ? "1 folder" : $"{f.Folders:N0} folders")}") +
                             (f.Files > 0 ? $", mostly {f.MainKind}" : "");
                if (!f.IsGroup) yield return f.Path;
                yield return "Click to look inside.";
            }
            else if (File is { } file)
            {
                yield return $"{file.Kind}, changed {file.Modified:d}";
                yield return Location;
            }
            else yield return $"Smaller files kept together, mostly {Kind}";
        }
    }
}

/// <summary>One kind of file in the open folder, for the bar and its legend.</summary>
public sealed record KindShare(FileKind Kind, long Bytes, double Share)
{
    public string Name => Kind.ToString();
    public string Size => ByteSize.Format(Bytes);
    public string ShareText => $"{Share:0.#}%";
    public Brush Fill => DiskColors.Fill(Kind);
}

/// <summary>
/// Disk Analyzer (Ted, 2026-10-12): pick one drive, several, all of them or a folder; then a map of what's using the space, colored by
/// kind of file, that you click through folder by folder, with a list of the same and the largest files. Read-only, except that
/// a file can be moved to the Recycle Bin after a confirm.
/// </summary>
public sealed class DiskAnalyzerViewModel : ObservableObject
{
    /// <summary>Files listed one by one in a folder; the rest share one "Smaller Files" box.</summary>
    private const int FilesShown = 150;
    /// <summary>Folders inside the open one with at most this many files get their files read too, so their boxes show them.</summary>
    private const int LooseFilesRead = 500;

    private CancellationTokenSource? _cts;
    private DiskScan? _scan;
    private DiskTarget? _target;
    private DiskScanProgress? _progress;
    private readonly DispatcherTimer _ticker = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private int _openVersion;

    public DiskAnalyzerViewModel()
    {
        _ticker.Tick += (_, _) => ShowProgress();
        // A new scheme or accent recolors the map at once; the rows and legend are rebuilt so their color dots follow too.
        DiskColors.Changed += () => { if (Current is { } open && !IsScanning) _ = OpenAsync(open); };
        ChooseCommand = new RelayCommand(ChooseAsync, () => !IsScanning);
        RescanCommand = new RelayCommand(() => ScanAsync(_target!), () => !IsScanning && _target is not null);
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsScanning);
        OpenCommand = new RelayCommand<DiskEntry>(e => { if (e.Folder is { } f) _ = OpenAsync(f); });
        LookInsideCommand = new RelayCommand(() => OpenAsync(SelectedEntry!.Folder!), () => SelectedEntry?.Folder is not null);
        GoToCommand = new RelayCommand<DiskNode>(n => _ = OpenAsync(n));
        UpCommand = new RelayCommand(() => OpenAsync(Current!.Parent!), () => Current?.Parent is not null);
        ShowInExplorerCommand = new RelayCommand(ShowInExplorer, () => SelectedEntry?.FullPath is not null);
        CopyPathCommand = new RelayCommand(() => ClipboardHelper.CopyOrExplain(SelectedEntry!.FullPath!), () => SelectedEntry?.FullPath is not null);
        RecycleCommand = new RelayCommand(RecycleAsync, () => SelectedEntry?.File is not null && !IsScanning);
        ShowContentsCommand = new RelayCommand(() => ShowLargest = false);
        ShowLargestCommand = new RelayCommand(() => ShowLargest = true);
    }

    public ObservableCollection<DiskNode> Trail { get; } = new();
    public ObservableCollection<DiskEntry> Entries { get; } = new();
    public ObservableCollection<DiskEntry> LargestFiles { get; } = new();
    public ObservableCollection<KindShare> Kinds { get; } = new();

    public ICommand ChooseCommand { get; }
    public ICommand RescanCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand LookInsideCommand { get; }
    public ICommand GoToCommand { get; }
    public ICommand UpCommand { get; }
    public ICommand ShowInExplorerCommand { get; }
    public ICommand CopyPathCommand { get; }
    public ICommand RecycleCommand { get; }
    public ICommand ShowContentsCommand { get; }
    public ICommand ShowLargestCommand { get; }

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (!Set(ref _isScanning, value)) return;
            OnPropertyChanged(nameof(ShowWelcome));
            OnPropertyChanged(nameof(ShowResults));
        }
    }

    public bool ShowWelcome => !IsScanning && Current is null;
    public bool ShowResults => !IsScanning && Current is not null;

    private DiskNode? _current;
    public DiskNode? Current
    {
        get => _current;
        private set
        {
            if (!Set(ref _current, value)) return;
            OnPropertyChanged(nameof(ShowWelcome));
            OnPropertyChanged(nameof(ShowResults));
        }
    }

    private DiskEntry? _selectedEntry;
    public DiskEntry? SelectedEntry { get => _selectedEntry; set => Set(ref _selectedEntry, value); }

    private FileKind? _highlightKind;
    public FileKind? HighlightKind { get => _highlightKind; set => Set(ref _highlightKind, value); }

    private bool _showLargest;
    public bool ShowLargest { get => _showLargest; set => Set(ref _showLargest, value); }

    private string _title = "";
    public string Title { get => _title; private set => Set(ref _title, value); }

    private string _stats = "";
    public string Stats { get => _stats; private set => Set(ref _stats, value); }

    /// <summary>"1.2 TB free of 3.6 TB" for a drive; empty elsewhere (the bar hides).</summary>
    private string _driveText = "";
    public string DriveText { get => _driveText; private set { if (Set(ref _driveText, value)) OnPropertyChanged(nameof(HasDrive)); } }
    public bool HasDrive => DriveText.Length > 0;

    private double _driveUsed;
    public double DriveUsed { get => _driveUsed; private set => Set(ref _driveUsed, value); }

    private string _scanStatus = "";
    public string ScanStatus { get => _scanStatus; private set => Set(ref _scanStatus, value); }

    private string _scanCounts = "";
    public string ScanCounts { get => _scanCounts; private set => Set(ref _scanCounts, value); }

    private string _scanFolder = "";
    public string ScanFolder { get => _scanFolder; private set => Set(ref _scanFolder, value); }

    private string _notice = "";
    /// <summary>A line under the results: skipped folders, a cancelled scan, a removed file.</summary>
    public string Notice { get => _notice; private set { if (Set(ref _notice, value)) OnPropertyChanged(nameof(HasNotice)); } }
    public bool HasNotice => Notice.Length > 0;

    private bool _isEmpty;
    /// <summary>The open folder holds nothing (the map shows a line saying so).</summary>
    public bool IsEmpty { get => _isEmpty; private set => Set(ref _isEmpty, value); }

    private async Task ChooseAsync()
    {
        var picker = new DiskAnalyzerPickWindow(_target) { Owner = Application.Current.MainWindow };
        if (picker.ShowDialog() == true && picker.Target is { } target) await ScanAsync(target);
    }

    private async Task ScanAsync(DiskTarget target)
    {
        _target = target;
        OnPropertyChanged(nameof(HasTarget));
        _cts = new CancellationTokenSource();
        _progress = new DiskScanProgress();
        var progress = _progress;
        ScanStatus = $"Scanning {target.Description}";
        ScanCounts = "Starting…";
        ScanFolder = "";
        IsScanning = true;
        _ticker.Start();
        try
        {
            var token = _cts.Token;
            _scan = await Task.Run(() => DiskAnalyzer.Scan(target.Roots, 1000, progress, token));
            var notes = new List<string>();
            if (_scan.Root.Skipped > 0)
                notes.Add($"{_scan.Root.Skipped:N0} folder(s) couldn't be read{(Elevation.IsAdministrator ? "" : " without administrator rights")}, so they're left out.");
            // The fast scan (the drive's file table) needs administrator rights; say so once a whole drive was walked instead.
            if (!Elevation.IsAdministrator && Drives(_scan.Root).Any(d => !d.FileTable))
                notes.Add("Run Kodiak Cleaner as administrator to scan whole NTFS drives much faster.");
            Notice = string.Join(" ", notes);
            SelectedEntry = null;
            HighlightKind = null;
            await OpenAsync(_scan.Root);
            ActivityLog.Write("Disk Analyzer", $"Scanned {target.Description}: {ByteSize.Format(_scan.Root.Bytes)} in {_scan.Root.Files:N0} files ({_scan.Took.TotalSeconds:0.#} s{(Drives(_scan.Root).Any(d => d.FileTable) ? ", fast scan" : "")})");
        }
        catch (OperationCanceledException)
        {
            Notice = _scan is null ? "" : "Scan cancelled. These are the results from before.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Notice = "Couldn't read that: " + e.Message;
        }
        finally
        {
            _ticker.Stop();
            IsScanning = false;
        }
    }

    private void ShowProgress()
    {
        if (_progress is not { } p) return;
        ScanCounts = $"{p.Files:N0} files, {ByteSize.Format(p.Bytes)} so far";
        ScanFolder = p.Current;
    }

    /// <summary>Shows a folder: its folders and files in the map and list, its kinds of files, and its largest files.</summary>
    private async Task OpenAsync(DiskNode node)
    {
        int version = ++_openVersion;
        // The files in this folder, and in the biggest folders inside it so their boxes can show their files too.
        var (files, inside) = await Task.Run(() =>
        {
            var own = DiskAnalyzer.FilesIn(node);
            var subs = node.Children.Where(c => c.OwnFiles is > 1 and <= LooseFilesRead).Take(40)
                .ToDictionary(c => c, c => DiskAnalyzer.FilesIn(c));
            return (own, subs);
        });
        if (version != _openVersion) return; // another folder was opened meanwhile

        Current = node;
        Trail.Clear();
        foreach (var n in node.Trail()) Trail.Add(n);

        var entries = node.Children.Where(c => c.Bytes > 0)
            .Select(c => new DiskEntry(c, node.Bytes) { LooseFiles = inside.TryGetValue(c, out var loose) ? loose : null })
            .Concat(files.Take(FilesShown).Select(f => new DiskEntry(f, node.Bytes))).ToList();
        if (files.Count > FilesShown) entries.Add(DiskEntry.Rest(files.Skip(FilesShown).ToList(), node.Bytes));
        entries.Sort((a, b) => b.Bytes.CompareTo(a.Bytes));
        Entries.Clear();
        foreach (var e in entries) Entries.Add(e);
        IsEmpty = entries.Count == 0;

        Kinds.Clear();
        long total = node.KindBytes.Sum();
        foreach (var k in FileKinds.All.Where(k => node.KindBytes[(int)k] > 0).OrderByDescending(k => node.KindBytes[(int)k]))
            Kinds.Add(new KindShare(k, node.KindBytes[(int)k], 100.0 * node.KindBytes[(int)k] / total));

        LargestFiles.Clear();
        if (_scan is not null)
            foreach (var f in _scan.LargestFiles.Where(f => DiskAnalyzer.IsUnder(f.Path, node)).Take(200))
                LargestFiles.Add(new DiskEntry(f, node.Bytes));

        Title = node.IsGroup ? DiskAnalyzer.AllDrivesName : node.IsDrive ? node.Name : node.Path;
        Stats = $"{ByteSize.Format(node.Bytes)} in {(node.Files == 1 ? "1 file" : $"{node.Files:N0} files")} and {(node.Folders == 1 ? "1 folder" : $"{node.Folders:N0} folders")}" +
                (_scan is not null && ReferenceEquals(node, _scan.Root) ? $", scanned in {Took(_scan.Took)}{(Drives(node).Any(d => d.FileTable) ? " with the fast scan" : "")}" : "");
        UpdateDrive(node);
        SelectedEntry = null;
    }

    /// <summary>The drives in a scan: the root itself, or each drive under All Drives.</summary>
    private static IEnumerable<DiskNode> Drives(DiskNode root) => (root.IsGroup ? root.Children : new List<DiskNode> { root }).Where(d => d.IsDrive);

    private static string Took(TimeSpan t) => t.TotalSeconds < 60 ? $"{Math.Max(0.1, t.TotalSeconds):0.#} seconds" : $"{(int)t.TotalMinutes} min {t.Seconds} s";

    /// <summary>Whether there's a scan to repeat (Scan Again shows then).</summary>
    public bool HasTarget => _target is not null;

    private void UpdateDrive(DiskNode node)
    {
        DriveText = "";
        DriveUsed = 0;
        if (!node.IsDrive) return;
        try
        {
            var d = new DriveInfo(node.Path);
            if (!d.IsReady || d.TotalSize <= 0) return;
            DriveUsed = 100.0 * (d.TotalSize - d.TotalFreeSpace) / d.TotalSize;
            DriveText = $"{ByteSize.Format(d.TotalFreeSpace)} free of {ByteSize.Format(d.TotalSize)} ({DriveUsed:0}% used)";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { }
    }

    private void ShowInExplorer()
    {
        var e = SelectedEntry!;
        if (e.File is not null) Elevation.ShowInExplorer(e.File.Path);
        else if (e.Folder is { IsGroup: false } f && Directory.Exists(f.Path)) Elevation.OpenFolder(f.Path);
    }

    private async Task RecycleAsync()
    {
        var entry = SelectedEntry!;
        var file = entry.File!;
        if (!Dialogs.Confirm($"Move {file.Name} ({entry.Size}) to the Recycle Bin?")) return;
        try
        {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(file.Path,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            Dialogs.Error($"{file.Name} couldn't be moved to the Recycle Bin: {e.Message}");
            return;
        }
        ActivityLog.Write("Disk Analyzer", $"Moved {file.Name} ({entry.Size}) to the Recycle Bin");
        if (_scan is not null)
        {
            if (FolderOf(_scan.Root, Path.GetDirectoryName(file.Path) ?? "") is { } folder) folder.FileRemoved(file);
            _scan = _scan with { LargestFiles = _scan.LargestFiles.Where(f => !string.Equals(f.Path, file.Path, StringComparison.OrdinalIgnoreCase)).ToList() };
        }
        if (Current is not null) await OpenAsync(Current);
        Notice = $"Moved {file.Name} to the Recycle Bin.";
    }

    /// <summary>The scanned folder at a path, found by walking down from the top.</summary>
    private static DiskNode? FolderOf(DiskNode root, string path)
    {
        var node = root;
        while (true)
        {
            if (!node.IsGroup && string.Equals(node.Path.TrimEnd('\\'), path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return node;
            var next = node.Children.FirstOrDefault(c => string.Equals(c.Path.TrimEnd('\\'), path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) ||
                                                         DiskAnalyzer.IsUnder(path, c));
            if (next is null) return null;
            node = next;
        }
    }
}
