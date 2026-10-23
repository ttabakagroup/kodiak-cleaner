using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using KodiakCleaner.Core.Tools;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App.ViewModels;

public sealed class DuplicateFileItem : ObservableObject
{
    public DuplicateFileItem(DuplicateFile file, DuplicateGroupItem group, SimilarFile? similar = null)
    {
        File = file;
        Group = group;
        if (similar is not null)
        {
            Likeness = similar.Score * 100;
            LikenessText = group.Pattern is null ? KodiakCleaner.Core.Tools.Likeness.Pct(similar.Score) : "";
            Why = similar.Why;
        }
    }

    public DuplicateFile File { get; }
    public DuplicateGroupItem Group { get; }
    public string Name => System.IO.Path.GetFileName(File.Path);
    public string Folder => System.IO.Path.GetDirectoryName(File.Path) ?? "";
    public string Modified => File.LastWriteUtc.ToLocalTime().ToString("g");
    public string Size => ByteSize.Format(File.Size);
    /// <summary>The line under the name: why it's similar (similar sets), size and date.</summary>
    public string Details => string.Join(" · ", new[] { Why, Size, Modified }.Where(s => s.Length > 0));

    /// <summary>Similar sets only: how alike this file is to its closest match (0 to 100), and why.</summary>
    public double Likeness { get; }
    public string LikenessText { get; } = "";
    public string Why { get; } = "";

    private string _hash = "";
    /// <summary>The whole file's SHA-256 after Verify Hashes, shortened for the list; the full value is in <see cref="HashTip"/>.</summary>
    public string Hash { get => _hash; private set => Set(ref _hash, value); }
    private string _hashTip = "";
    public string HashTip { get => _hashTip; private set => Set(ref _hashTip, value); }
    private bool _hashMismatch;
    /// <summary>True when the file's contents no longer match its set.</summary>
    public bool HashMismatch { get => _hashMismatch; private set => Set(ref _hashMismatch, value); }

    internal string? FullHash { get; private set; }

    internal void ShowHash(string? hash, bool mismatch, string note)
    {
        FullHash = hash;
        Hash = hash is null ? note : $"SHA-256 {hash[..12]}… · {note}";
        HashTip = hash is null ? note : $"SHA-256: {hash}";
        HashMismatch = mismatch;
    }

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set { if (Set(ref _isChecked, value)) Group.Owner.UpdateSelection(); }
    }
}

/// <summary>One set in the results: identical copies, or (in-depth) files that are alike.</summary>
public sealed class DuplicateGroupItem : ObservableObject
{
    public DuplicateGroupItem(DuplicateGroup group, DuplicatesViewModel owner)
    {
        Owner = owner;
        Hash = group.Hash;
        Header = $"{group.Files.Count} copies of {System.IO.Path.GetFileName(group.Files[0].Path)} · {ByteSize.Format(group.Size)} each";
        Files = group.Files.Select(f => new DuplicateFileItem(f, this)).ToList();
        _badge = "SHA-256 Match";
        BadgeTip = $"Every copy has the same SHA-256: {group.Hash}";
    }

    public DuplicateGroupItem(SimilarGroup group, DuplicatesViewModel owner)
    {
        Owner = owner;
        IsSimilar = true;
        Pattern = group.Pattern; // before the files, which leave out the percent for pattern sets
        Likeness = group.Score * 100;
        Header = $"{group.Files.Count} similar files · {ByteSize.Format(group.Files.Sum(f => f.File.Size))} in all";
        Files = group.Files.Select(f => new DuplicateFileItem(f.File, this, f)).ToList();
        _badge = $"Up to {KodiakCleaner.Core.Tools.Likeness.Pct(group.Score)} Alike";
        BadgeTip = "How alike the closest two files in this set are. Similar files aren't exact copies, so check them before removing any.";
        if (group.Pattern is { } pattern)
        {
            Header = $"{group.Files.Count} files matching \"{pattern}\" · {ByteSize.Format(group.Files.Sum(f => f.File.Size))} in all";
            _badge = "Name Pattern";
            BadgeTip = "Every file here matches a name pattern you typed. Their contents weren't compared, so check them before removing any.";
        }
    }

    public DuplicatesViewModel Owner { get; }
    public bool IsSimilar { get; }
    /// <summary>Identical sets: the SHA-256 every copy had when searched.</summary>
    public string? Hash { get; }
    public double Likeness { get; }
    /// <summary>The name pattern that made this set, for a set made by a pattern.</summary>
    public string? Pattern { get; }
    /// <summary>The likeness meter is shown for similar sets made by comparing files, not for pattern sets.</summary>
    public bool ShowMeter => IsSimilar && Pattern is null;
    public string Header { get; }
    public List<DuplicateFileItem> Files { get; }

    private string _badge;
    public string Badge { get => _badge; set => Set(ref _badge, value); }
    private string _badgeTip = "";
    public string BadgeTip { get => _badgeTip; set => Set(ref _badgeTip, value); }
    private bool _badgeWarn;
    /// <summary>True when a hash check found a copy that no longer matches.</summary>
    public bool BadgeWarn { get => _badgeWarn; set => Set(ref _badgeWarn, value); }
}

/// <summary>A drive offered as a place to search: its name, how full it is and whether Windows is on it.</summary>
public sealed class DriveChoice : ObservableObject
{
    public DriveChoice(string root, string label, string detail, double usedPercent, bool isWindows, Action changed)
    {
        Root = root;
        Label = label;
        Detail = detail;
        UsedPercent = usedPercent;
        IsWindows = isWindows;
        _changed = changed;
    }

    private readonly Action _changed;
    public string Root { get; }
    public string Label { get; }
    public string Detail { get; }
    public double UsedPercent { get; }
    public bool IsWindows { get; }

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set { if (Set(ref _isChecked, value)) _changed(); }
    }
}

public sealed class DuplicatesViewModel : ObservableObject, ILoadOnFirstVisit
{
    private CancellationTokenSource? _cts;
    private bool _drivesLoaded;

    /// <summary>How long one drive gets to answer before it's left out (a sleeping disk or an empty card reader).</summary>
    private static readonly TimeSpan DriveWait = TimeSpan.FromSeconds(5);

    public DuplicatesViewModel()
    {
        foreach (var (name, folder) in new[]
                 {
                     ("Documents", Environment.SpecialFolder.MyDocuments),
                     ("Pictures", Environment.SpecialFolder.MyPictures),
                     ("Music", Environment.SpecialFolder.MyMusic),
                     ("Videos", Environment.SpecialFolder.MyVideos),
                     ("Desktop", Environment.SpecialFolder.Desktop),
                 })
        {
            var path = Environment.GetFolderPath(folder);
            if (!string.IsNullOrEmpty(path)) Folders.Add(new CheckItem(path, isChecked: name is "Documents" or "Pictures"));
        }
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (Directory.Exists(downloads)) Folders.Add(new CheckItem(downloads, isChecked: true));

        AddFolderCommand = new RelayCommand(AddFolder, () => !IsBusy);
        SearchCommand = new RelayCommand(SearchAsync, () => !IsBusy && (Folders.Any(f => f.IsChecked) || Drives.Any(d => d.IsChecked)));
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy);
        SelectAllButNewestCommand = new RelayCommand(() => AutoSelect(keepNewest: true), () => !IsBusy && !ShowSimilar && Groups.Count > 0);
        SelectAllButOldestCommand = new RelayCommand(() => AutoSelect(keepNewest: false), () => !IsBusy && !ShowSimilar && Groups.Count > 0);
        ClearSelectionCommand = new RelayCommand(() => { foreach (var f in AllFiles) f.IsChecked = false; }, () => AllFiles.Any(f => f.IsChecked));
        VerifyCommand = new RelayCommand(VerifyAsync, () => !IsBusy && ShownGroups.Count > 0);
        DeleteCommand = new RelayCommand(DeleteAsync, () => !IsBusy && AllFiles.Any(f => f.IsChecked));
        OpenFolderCommand = new RelayCommand<DuplicateFileItem>(f => Elevation.ShowInExplorer(f.File.Path));
    }

    public ObservableCollection<DriveChoice> Drives { get; } = new();
    public ObservableCollection<CheckItem> Folders { get; } = new();
    public ObservableCollection<DuplicateGroupItem> Groups { get; } = new();
    public ObservableCollection<DuplicateGroupItem> SimilarGroups { get; } = new();
    private IEnumerable<DuplicateFileItem> AllFiles => Groups.Concat(SimilarGroups).SelectMany(g => g.Files);

    public ICommand AddFolderCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand SelectAllButNewestCommand { get; }
    public ICommand SelectAllButOldestCommand { get; }
    public ICommand ClearSelectionCommand { get; }
    public ICommand VerifyCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand OpenFolderCommand { get; }

    private string _minSizeMb = "1";
    public string MinSizeMb { get => _minSizeMb; set => Set(ref _minSizeMb, value); }

    private bool _matchName;
    public bool MatchName { get => _matchName; set => Set(ref _matchName, value); }

    private bool _includeHidden;
    public bool IncludeHidden { get => _includeHidden; set => Set(ref _includeHidden, value); }

    private bool _inDepth;
    /// <summary>In-depth search: also find files that are alike without being identical.</summary>
    public bool InDepth
    {
        get => _inDepth;
        set { if (Set(ref _inDepth, value)) OnPropertyChanged(nameof(Identical)); }
    }
    /// <summary>The other segment of the mode switch.</summary>
    public bool Identical { get => !InDepth; set => InDepth = !value; }

    private bool _matchNames;
    /// <summary>In-depth: files with alike names count as similar even when their contents differ.</summary>
    public bool MatchNames { get => _matchNames; set => Set(ref _matchNames, value); }

    private string _minNameLikeness = "80";
    /// <summary>Lowest name likeness, in percent, for Match Similar Names.</summary>
    public string MinNameLikeness { get => _minNameLikeness; set => Set(ref _minNameLikeness, value); }

    private bool _anyFileType;
    public bool AnyFileType { get => _anyFileType; set => Set(ref _anyFileType, value); }

    private bool _ignoreCopyMarks = true;
    public bool IgnoreCopyMarks { get => _ignoreCopyMarks; set => Set(ref _ignoreCopyMarks, value); }

    private bool _ignoreDates;
    public bool IgnoreDates { get => _ignoreDates; set => Set(ref _ignoreDates, value); }

    private bool _ignoreNumbers;
    public bool IgnoreNumbers { get => _ignoreNumbers; set => Set(ref _ignoreNumbers, value); }

    private bool _ignoreVersionWords = true;
    public bool IgnoreVersionWords { get => _ignoreVersionWords; set => Set(ref _ignoreVersionWords, value); }

    private string _extraWords = "";
    /// <summary>The person's own words to leave out of names, separated by commas.</summary>
    public string ExtraWords { get => _extraWords; set => Set(ref _extraWords, value); }

    private string _namePatterns = "";
    /// <summary>Name patterns, one per line (or separated by semicolons).</summary>
    public string NamePatterns { get => _namePatterns; set => Set(ref _namePatterns, value); }

    private string _minLikeness = "60";
    /// <summary>Lowest likeness, in percent, for a pair of files to be listed as similar.</summary>
    public string MinLikeness { get => _minLikeness; set => Set(ref _minLikeness, value); }

    private bool _showSimilar;
    /// <summary>Which results are shown: identical sets or similar sets.</summary>
    public bool ShowSimilar
    {
        get => _showSimilar;
        set
        {
            if (!Set(ref _showSimilar, value)) return;
            OnPropertyChanged(nameof(ShowIdentical));
            OnPropertyChanged(nameof(ShownGroups));
            OnPropertyChanged(nameof(ResultsHint));
        }
    }
    public bool ShowIdentical { get => !ShowSimilar; set => ShowSimilar = !value; }
    public ObservableCollection<DuplicateGroupItem> ShownGroups => ShowSimilar ? SimilarGroups : Groups;

    private bool _hasSimilarResults;
    /// <summary>True after an in-depth search, so the Identical | Similar switch is shown.</summary>
    public bool HasSimilarResults { get => _hasSimilarResults; private set => Set(ref _hasSimilarResults, value); }

    public string IdenticalLabel => $"Identical ({Groups.Count:N0})";
    public string SimilarLabel => $"Similar ({SimilarGroups.Count:N0})";
    public string ResultsHint => ShowSimilar
        ? "Similar files aren't exact copies: an edited version, a re-saved picture or an older draft. Nothing is ticked for you; check each file before removing it."
        : "";

    public bool? AllDrives
    {
        get => Drives.Count > 0 && Drives.All(d => d.IsChecked) ? true : Drives.Any(d => d.IsChecked) ? null : false;
        set
        {
            var on = value != false;
            foreach (var d in Drives) d.IsChecked = on;
        }
    }

    private string _drivesNote = "Looking for drives…";
    public string DrivesNote { get => _drivesNote; private set => Set(ref _drivesNote, value); }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }

    private string _summary = "Finds files with identical contents. Choose drives or folders, then click Search.";
    public string Summary { get => _summary; private set => Set(ref _summary, value); }

    private string _selection = "";
    public string Selection { get => _selection; private set => Set(ref _selection, value); }

    public void EnsureLoaded()
    {
        if (_drivesLoaded) return;
        _drivesLoaded = true;
        _ = LoadDrivesAsync();
    }

    /// <summary>Reads the drives in the background, each given <see cref="DriveWait"/>, so a sleeping disk never freezes the page.</summary>
    private async Task LoadDrivesAsync()
    {
        var windowsRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? "";
        DriveInfo[] all;
        try { all = await Task.Run(DriveInfo.GetDrives).WaitAsync(DriveWait); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or TimeoutException) { all = Array.Empty<DriveInfo>(); }

        var found = await Task.WhenAll(all.Where(d => d.DriveType is DriveType.Fixed or DriveType.Removable).Select(async d =>
        {
            try { return await Task.Run(() => Describe(d, windowsRoot)).WaitAsync(DriveWait); }
            catch (TimeoutException) { return null; }
        }));
        foreach (var d in found.Where(d => d is not null).OrderBy(d => d!.Root, StringComparer.OrdinalIgnoreCase))
            Drives.Add(d!);
        DrivesNote = Drives.Count == 0
            ? "No drives answered. Folders below can still be searched."
            : "Windows, program folders and every user's AppData are skipped, so Windows and your programs keep their files.";
        OnPropertyChanged(nameof(AllDrives));
    }

    private DriveChoice? Describe(DriveInfo d, string windowsRoot)
    {
        try
        {
            if (!d.IsReady) return null;
            var letter = d.Name.TrimEnd('\\');
            var name = string.IsNullOrWhiteSpace(d.VolumeLabel) ? (d.DriveType == DriveType.Removable ? "USB Drive" : "Local Disk") : d.VolumeLabel;
            var used = d.TotalSize > 0 ? 100.0 * (d.TotalSize - d.TotalFreeSpace) / d.TotalSize : 0;
            return new DriveChoice(d.RootDirectory.FullName, $"{name} ({letter})",
                $"{ByteSize.Format(d.AvailableFreeSpace)} free of {ByteSize.Format(d.TotalSize)}", used,
                string.Equals(d.RootDirectory.FullName, windowsRoot, StringComparison.OrdinalIgnoreCase),
                () => OnPropertyChanged(nameof(AllDrives)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { return null; }
    }

    internal void UpdateSelection()
    {
        var chosen = AllFiles.Where(f => f.IsChecked).ToList();
        Selection = chosen.Count == 0 ? "" : $"{chosen.Count} selected · {ByteSize.Format(chosen.Sum(f => f.File.Size))}";
    }

    private void AddFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Add a Folder to Search", Multiselect = true };
        if (dialog.ShowDialog() != true) return;
        foreach (var path in dialog.FolderNames)
            if (!Folders.Any(f => string.Equals(f.Name, path, StringComparison.OrdinalIgnoreCase)))
                Folders.Add(new CheckItem(path));
    }

    /// <summary>Where duplicates can matter to Windows or a program, never searched (whole drives included).</summary>
    private static IEnumerable<string> ProtectedFolders()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return AppContext.BaseDirectory;
        // Where .NET unpacks single-file programs (Kodiak Cleaner included) while they run.
        yield return Path.Combine(Path.GetTempPath(), ".net");
    }

    private void ShowResults(IReadOnlyList<DuplicateGroup> identical, IReadOnlyList<SimilarGroup> similar, bool inDepth)
    {
        Groups.Clear();
        SimilarGroups.Clear();
        foreach (var g in identical) Groups.Add(new DuplicateGroupItem(g, this));
        foreach (var g in similar) SimilarGroups.Add(new DuplicateGroupItem(g, this));
        HasSimilarResults = inDepth;
        if (!inDepth) ShowSimilar = false;
        else if (Groups.Count == 0 && SimilarGroups.Count > 0) ShowSimilar = true;
        OnPropertyChanged(nameof(ShownGroups));
        OnPropertyChanged(nameof(IdenticalLabel));
        OnPropertyChanged(nameof(SimilarLabel));
        UpdateSelection();
    }

    private async Task SearchAsync()
    {
        IsBusy = true;
        using var task = BackgroundTasks.Start(this, "Scanning for Files");
        ShowResults(Array.Empty<DuplicateGroup>(), Array.Empty<SimilarGroup>(), inDepth: false);
        _cts = new CancellationTokenSource();
        var inDepth = InDepth;
        var options = new DuplicateSearchOptions
        {
            Folders = Drives.Where(d => d.IsChecked).Select(d => d.Root).Concat(Folders.Where(f => f.IsChecked).Select(f => f.Name)).ToList(),
            MinSize = (long)(double.TryParse(MinSizeMb, out var mb) && mb > 0 ? mb * 1024 * 1024 : 1),
            MatchName = MatchName,
            IncludeHidden = IncludeHidden,
            Protected = ProtectedFolders().ToList(),
            FindSimilar = inDepth,
            MinSimilarity = double.TryParse(MinLikeness, out var pct) ? Math.Clamp(pct, 30, 99) / 100 : 0.6,
            MatchNames = MatchNames,
            MinNameSimilarity = double.TryParse(MinNameLikeness, out var namePct) ? Math.Clamp(namePct, 50, 100) / 100 : 0.8,
            Names = new NameRules
            {
                AnyFileType = AnyFileType,
                IgnoreCopyMarks = IgnoreCopyMarks,
                IgnoreDates = IgnoreDates,
                IgnoreNumbers = IgnoreNumbers,
                IgnoreVersionWords = IgnoreVersionWords,
                ExtraWords = ExtraWords.Split(',', ';').Select(w => w.Trim()).Where(w => w.Length > 0).ToList(),
                Patterns = NamePatterns.Split('\n', '\r', ';').Select(p => p.Trim()).Where(p => p.Length > 0).ToList(),
            },
            PictureHash = PictureHasher.Hash,
        };
        var progress = new Progress<DuplicateProgress>(p =>
        {
            Summary = p.Stage switch
            {
                "FileTable" => $"Listing files in {p.Folder}… {p.FilesScanned:N0} files so far",
                "Scanning" => $"Scanning… {p.FilesScanned:N0} files so far",
                "Comparing" => $"Comparing contents… {p.Done:N0} of {p.Total:N0} candidate files",
                "Pictures" => $"Looking at pictures… {p.Done:N0} of {p.Total:N0}",
                _ => $"Comparing similar files… {p.Done:N0} of {p.Total:N0} likely pairs",
            };
            task.Title = p.Stage switch
            {
                "FileTable" or "Scanning" => "Scanning for Files",
                "Comparing" => "Comparing Files",
                "Pictures" => "Comparing Pictures",
                _ => "Comparing Similar Files",
            };
            // No total while the folders or the file table are still being listed.
            if (p.Stage is "FileTable" or "Scanning") task.Percent = null;
            else task.Step(p.Done, p.Total);
        });
        try
        {
            var found = await Task.Run(() => DuplicateFinder.Search(options, progress, _cts.Token));
            ShowResults(found.Identical, found.Similar, inDepth);
            var identical = found.Identical.Count == 0
                ? "No identical files found"
                : $"Found {found.Identical.Count:N0} {(found.Identical.Count == 1 ? "set" : "sets")} of identical files using {ByteSize.Format(found.Identical.Sum(g => g.WastedBytes))} of extra space";
            var similar = !inDepth ? "" : found.Similar.Count == 0
                ? ", and no similar files."
                : $", and {found.Similar.Count:N0} {(found.Similar.Count == 1 ? "set" : "sets")} of similar files.";
            var skipped = found.SkippedRoots > 0 ? $" {found.SkippedRoots} chosen folder(s) were skipped because Windows or a program keeps its own files there." : "";
            var fast = found.FastDrives > 0 ? $" {found.FastDrives} {(found.FastDrives == 1 ? "drive was" : "drives were")} read straight from the drive's file table." : "";
            var slow = found.SlowDrives > 0 && !Elevation.IsAdministrator ? " Restart as Administrator to read whole drives straight from their file table, which is much faster." : "";
            Summary = $"{identical}{(inDepth ? similar : ".")} Searched {found.FilesScanned:N0} files.{fast} Tick the files to remove.{skipped}{slow}";
        }
        catch (OperationCanceledException)
        {
            Summary = "Search cancelled.";
        }
        finally { IsBusy = false; }
    }

    private void AutoSelect(bool keepNewest)
    {
        // Identical sets only: which similar file to keep is always the person's call.
        foreach (var g in Groups)
        {
            var keep = keepNewest ? g.Files.MaxBy(f => f.File.LastWriteUtc) : g.Files.MinBy(f => f.File.LastWriteUtc);
            foreach (var f in g.Files) f.IsChecked = f != keep;
        }
    }

    /// <summary>
    /// Hash check on request: reads every file in the shown sets again in full and compares SHA-256s, so a set is confirmed
    /// identical (or shown as changed since the search). A copy that no longer matches is unticked. In similar sets it shows
    /// whether any of the files are in fact identical.
    /// </summary>
    private async Task VerifyAsync()
    {
        var sets = ShownGroups.ToList();
        var files = sets.SelectMany(g => g.Files).ToList();
        IsBusy = true;
        using var task = BackgroundTasks.Start(this, "Verifying Hashes");
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var bytes = files.Sum(f => f.File.Size);
        long read = 0;
        var progress = new Progress<long>(r =>
        {
            Summary = $"Checking SHA-256 hashes… {ByteSize.Format(r)} of {ByteSize.Format(bytes)}";
            task.Step(r, bytes);
        });
        try
        {
            var hashes = await Task.Run(() =>
            {
                var result = new Dictionary<DuplicateFileItem, string?>();
                foreach (var f in files)
                {
                    token.ThrowIfCancellationRequested();
                    result[f] = DuplicateFinder.FullHash(f.File.Path);
                    ((IProgress<long>)progress).Report(read += f.File.Size);
                }
                return result;
            }, token);

            int confirmed = 0, changed = 0;
            foreach (var g in sets)
            {
                if (!g.IsSimilar)
                {
                    foreach (var f in g.Files)
                    {
                        var h = hashes[f];
                        var match = h is not null && h == g.Hash;
                        f.ShowHash(h, !match, h is null ? "Couldn't be read" : match ? "Matches" : "Changed since the search");
                        if (!match) f.IsChecked = false;
                    }
                    var matching = g.Files.Count(f => !f.HashMismatch);
                    var ok = matching == g.Files.Count;
                    g.Badge = ok ? "SHA-256 Verified" : matching >= 2 ? $"{matching} of {g.Files.Count} Still Match" : "No Longer Identical";
                    g.BadgeTip = ok ? $"Read again just now: every copy has SHA-256 {g.Hash}" : "Some copies changed since the search; they were unticked. Search again for an up-to-date list.";
                    g.BadgeWarn = !ok;
                    if (ok) confirmed++; else changed++;
                }
                else
                {
                    var same = g.Files.Where(f => hashes[f] is not null).GroupBy(f => hashes[f]).Where(s => s.Count() > 1).SelectMany(s => s).ToHashSet();
                    foreach (var f in g.Files)
                        f.ShowHash(hashes[f], false, hashes[f] is null ? "Couldn't be read" : same.Contains(f) ? "Identical to another file here" : "Unique contents");
                    if (same.Count > 0)
                    {
                        g.Badge = $"{same.Count} Identical";
                        g.BadgeTip = "Some of these files have the same SHA-256, so they are exact copies of each other.";
                    }
                    confirmed++;
                }
            }
            Summary = ShowSimilar
                ? $"Checked {files.Count:N0} files in {sets.Count:N0} similar {(sets.Count == 1 ? "set" : "sets")}. Each file's SHA-256 is shown beside it."
                : changed == 0
                    ? $"Verified: all {confirmed:N0} {(confirmed == 1 ? "set is" : "sets are")} identical, byte for byte (SHA-256)."
                    : $"{confirmed:N0} {(confirmed == 1 ? "set is" : "sets are")} identical (SHA-256). {changed:N0} changed since the search; their changed copies were unticked.";
        }
        catch (OperationCanceledException)
        {
            Summary = "Hash check cancelled.";
        }
        finally { IsBusy = false; }
    }

    private static bool StillThere(DuplicateFile f)
    {
        try
        {
            var info = new FileInfo(f.Path);
            return info.Exists && info.Length == f.Size && info.LastWriteTimeUtc == f.LastWriteUtc;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return false; }
    }

    private async Task DeleteAsync()
    {
        var chosen = AllFiles.Where(f => f.IsChecked).ToList();
        // Never let every copy in an identical set go.
        // The copy that stays must still be there: one deleted or changed since the search doesn't count.
        var wipedOut = Groups.Where(g => g.Files.Any(f => f.IsChecked) && !g.Files.Any(f => !f.IsChecked && StillThere(f.File))).ToList();
        if (wipedOut.Count > 0)
        {
            Dialogs.Error($"No unticked copy is left in {wipedOut.Count} set(s), for example \"{wipedOut[0].Header}\". Leave at least one copy unticked in each set, or search again if files changed since.");
            return;
        }
        var size = ByteSize.Format(chosen.Sum(f => f.File.Size));
        var similarCount = chosen.Count(f => f.Group.IsSimilar);
        var wholeSimilarSets = SimilarGroups.Count(g => g.Files.All(f => f.IsChecked));
        if (similarCount == 0)
        {
            if (!Dialogs.Confirm($"Move {chosen.Count} file(s) ({size}) to the Recycle Bin?")) return;
        }
        else if (!Dialogs.Warn($"Move {chosen.Count} file(s) ({size}) to the Recycle Bin?", caution:
                     $"{similarCount} of them are similar files, not exact copies, so they may hold something the others don't." +
                     (wholeSimilarSets > 0 ? $" Every file in {wholeSimilarSets} similar set(s) is ticked." : "")))
            return;

        IsBusy = true;
        using var task = BackgroundTasks.Start(this, "Moving Files to the Recycle Bin");
        int failed = 0;
        try
        {
            await Task.Run(() =>
            {
                int done = 0;
                foreach (var f in chosen)
                {
                    task.Step(done++, chosen.Count);
                    try
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(f.File.Path,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException or OperationCanceledException)
                    {
                        Interlocked.Increment(ref failed);
                    }
                }
            });
            foreach (var list in new[] { Groups, SimilarGroups })
            {
                foreach (var g in list.ToList())
                {
                    g.Files.RemoveAll(f => !System.IO.File.Exists(f.File.Path));
                    if (g.Files.Count < 2) list.Remove(g);
                }
                // Re-create remaining groups so their lists refresh.
                var remaining = list.ToList();
                list.Clear();
                foreach (var g in remaining) list.Add(g);
            }
            OnPropertyChanged(nameof(IdenticalLabel));
            OnPropertyChanged(nameof(SimilarLabel));
            UpdateSelection();
            ActivityLog.Write("Duplicate Finder", $"Moved {chosen.Count - failed} file(s) ({size}) to the Recycle Bin");
            Summary = $"Moved {chosen.Count - failed} file(s) to the Recycle Bin." + (failed > 0 ? $" {failed} could not be moved." : "");
        }
        finally { IsBusy = false; }
    }
}
