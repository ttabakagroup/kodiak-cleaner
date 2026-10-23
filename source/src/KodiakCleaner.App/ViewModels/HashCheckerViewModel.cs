using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using System.Windows.Input;
using KodiakCleaner.Core.Tools;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App.ViewModels;

/// <summary>
/// Tools › Hash Checker (Ted, 2026-10-23): add or drop files, see their MD5, SHA-1, SHA-256, SHA-384 and SHA-512, and check them
/// against a pasted or loaded list of hashes. Files are only read; nothing goes online. Ctrl+F jumps to the search box.
/// </summary>
public sealed class HashCheckerViewModel : ObservableObject
{
    private CancellationTokenSource? _cts;
    private IReadOnlyList<ExpectedHash> _expected = Array.Empty<ExpectedHash>();
    /// <summary>The folder of a loaded list file: names in the list are relative to it.</summary>
    private string? _listFolder;
    /// <summary>A dropped folder is read to this many files at most, so a whole drive can't be queued by accident.</summary>
    private const int FolderLimit = 5000;

    public HashCheckerViewModel()
    {
        View = CollectionViewSource.GetDefaultView(Files);
        View.Filter = o => o is HashFileRow r && r.Matches(Filter);
        foreach (var kind in HashChecker.Kinds)
            ShownKinds.Add(new Choice<HashKind>(HashChecker.Name(kind), kind, kind == HashKind.Sha256, ShowKind));
        AddFilesCommand = new RelayCommand(AddFiles);
        AddFolderCommand = new RelayCommand(AddFolder);
        LoadListCommand = new RelayCommand(LoadList);
        ClearListCommand = new RelayCommand(() => { _listFolder = null; ExpectedText = ""; }, () => ExpectedText.Length > 0);
        ComputeCommand = new RelayCommand(ComputeAsync, () => !IsBusy && Files.Any(f => f.IsPending));
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy);
        RemoveCommand = new RelayCommand(() => Remove(Picked()), () => !IsBusy && Picked().Count > 0);
        RemoveRowCommand = new RelayCommand(() => Remove(Highlighted.ToList()), () => !IsBusy && Highlighted.Count > 0);
        CopyShownCommand = new RelayCommand(() => Copy(Highlighted, ShownKind), () => Highlighted.Any(r => r.Hashes is not null));
        CopyKindCommand = new RelayCommand<string>(k => Copy(Highlighted, Enum.Parse<HashKind>(k)));
        CopyAllCommand = new RelayCommand(CopyAll, () => Highlighted.Any(r => r.Hashes is not null));
        CopyOneCommand = new RelayCommand<HashLine>(l => ClipboardHelper.CopyOrExplain(l.Hash));
        ShowInFolderCommand = new RelayCommand(() => { if (Current is { } c) Elevation.ShowInExplorer(c.Path); }, () => Current is not null);
        SaveCommand = new RelayCommand(SaveChecksumFile, () => !IsBusy && Picked(all: true).Any(r => r.Hashes is not null));
    }

    public ObservableCollection<HashFileRow> Files { get; } = new();
    public ICollectionView View { get; }
    /// <summary>MD5 | SHA-1 | SHA-256 | SHA-384 | SHA-512: which hash the table shows (all five are always worked out).</summary>
    public ObservableCollection<Choice<HashKind>> ShownKinds { get; } = new();

    public ICommand AddFilesCommand { get; }
    public ICommand AddFolderCommand { get; }
    public ICommand LoadListCommand { get; }
    public ICommand ClearListCommand { get; }
    public ICommand ComputeCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand RemoveRowCommand { get; }
    public ICommand CopyShownCommand { get; }
    /// <summary>Right-click › Copy MD5 (and the other kinds): the parameter is the <see cref="HashKind"/> name.</summary>
    public ICommand CopyKindCommand { get; }
    public ICommand CopyAllCommand { get; }
    public ICommand CopyOneCommand { get; }
    public ICommand ShowInFolderCommand { get; }
    public ICommand SaveCommand { get; }

    private HashKind _shownKind = HashKind.Sha256;
    public HashKind ShownKind { get => _shownKind; private set => Set(ref _shownKind, value); }
    public string ShownKindName => HashChecker.Name(ShownKind);

    private void ShowKind(HashKind kind)
    {
        ShownKind = kind;
        OnPropertyChanged(nameof(ShownKindName));
        foreach (var f in Files) f.Shown = kind;
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set { if (Set(ref _isBusy, value)) OnPropertyChanged(nameof(ShowCompute)); }
    }

    /// <summary>Compute Hashes shows only when files are left waiting (after Cancel).</summary>
    public bool ShowCompute => !IsBusy && Files.Any(f => f.IsPending);

    private string _summary = "Add files, or drop them here, to see their hashes. Files are only read, never changed or sent anywhere.";
    public string Summary { get => _summary; private set => Set(ref _summary, value); }

    private string _filter = "";
    /// <summary>The search box (Ctrl+F): a file name, or any part of any of its hashes (spaces and case don't matter).</summary>
    public string Filter
    {
        get => _filter;
        set
        {
            if (!Set(ref _filter, value)) return;
            View.Refresh();
            OnPropertyChanged(nameof(SearchSummary));
        }
    }

    public string SearchSummary
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Filter)) return "";
            int shown = View.Cast<object>().Count();
            return shown == 0 ? "No file has that name or hash." : $"{shown} of {Files.Count} file(s) match the search.";
        }
    }

    private string _expectedText = "";
    /// <summary>The expected hashes: pasted, typed or loaded from a checksum file. Every file is checked again as it changes.</summary>
    public string ExpectedText
    {
        get => _expectedText;
        set
        {
            if (!Set(ref _expectedText, value ?? "")) return;
            if (_expectedText.Length == 0) _listFolder = null;
            _expected = HashChecker.ParseList(_expectedText);
            CheckAll();
        }
    }

    private string _listSummary = "Paste hashes from a download page, or load a checksum file (.sha256, .md5, SHA256SUMS…). Each file is checked against them.";
    public string ListSummary { get => _listSummary; private set => Set(ref _listSummary, value); }

    /// <summary>The highlighted rows (Ctrl-click, Shift-click or drag to pick several). Set by the view.</summary>
    private IReadOnlyList<HashFileRow> _highlighted = Array.Empty<HashFileRow>();
    public IReadOnlyList<HashFileRow> Highlighted
    {
        get => _highlighted;
        set
        {
            _highlighted = value;
            Current = value.Count == 1 ? value[0] : null;
        }
    }

    private HashFileRow? _current;
    /// <summary>The one highlighted file (Show in Folder); its five hashes open under its row.</summary>
    public HashFileRow? Current
    {
        get => _current;
        private set
        {
            if (!Set(ref _current, value)) return;
            OnPropertyChanged(nameof(HasCurrent));
        }
    }
    public bool HasCurrent => Current is not null;

    /// <summary>The header box: true when every shown file is ticked, null when some are.</summary>
    public bool? AllChecked
    {
        get => Files.Count > 0 && Files.All(f => f.IsChecked) ? true : Files.Any(f => f.IsChecked) ? null : false;
        set
        {
            var on = value != false;
            foreach (var f in View.Cast<HashFileRow>()) f.IsChecked = on;
            if (!on) foreach (var f in Files) f.IsChecked = false;
        }
    }

    /// <summary>Ticked files, else the highlighted ones (or, for Save, every file when nothing is ticked or highlighted).</summary>
    private List<HashFileRow> Picked(bool all = false)
    {
        var ticked = Files.Where(f => f.IsChecked).ToList();
        if (ticked.Count > 0) return ticked;
        if (Highlighted.Count > 0) return Highlighted.ToList();
        return all ? Files.ToList() : new List<HashFileRow>();
    }

    private void AddFiles()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Add Files to Check", Multiselect = true, Filter = "All Files (*.*)|*.*" };
        if (dialog.ShowDialog() == true) _ = AddPathsAsync(dialog.FileNames);
    }

    private void AddFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Add a Folder to Check", Multiselect = true };
        if (dialog.ShowDialog() == true) _ = AddPathsAsync(dialog.FolderNames);
    }

    private void LoadList()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Load a List of Hashes",
            Filter = "Checksum Files (*.sha256, *.sha1, *.md5, *SUMS…)|*.md5;*.sha1;*.sha256;*.sha384;*.sha512;*.md5sum;*.sha1sum;*.sha256sum;*.sha384sum;*.sha512sum;*SUMS;*SUMS.txt;*.txt|All Files (*.*)|*.*",
        };
        if (dialog.ShowDialog() == true) _ = LoadListAsync(dialog.FileName);
    }

    /// <summary>Files and folders dropped on the page. Checksum files (.sha256, SHA256SUMS…) are read as the list instead of hashed.</summary>
    public async Task DropAsync(IReadOnlyList<string> paths)
    {
        var lists = paths.Where(p => File.Exists(p) && HashChecker.IsListFile(p)).ToList();
        foreach (var list in lists) await LoadListAsync(list, append: lists.Count > 1 || ExpectedText.Length > 0);
        await AddPathsAsync(paths.Except(lists).ToList());
    }

    /// <summary>
    /// Reads a checksum file into the list, then adds the files it names that sit beside it (or in the folders it names), so
    /// a downloaded SHA256SUMS checks the whole download in one step.
    /// </summary>
    private async Task LoadListAsync(string path, bool append = false)
    {
        string text;
        try
        {
            if (new FileInfo(path).Length > 16 * 1024 * 1024)
            {
                Dialogs.Error($"'{Path.GetFileName(path)}' is too big to be a list of hashes.");
                return;
            }
            text = await File.ReadAllTextAsync(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Dialogs.Error($"'{Path.GetFileName(path)}' could not be read: {e.Message}");
            return;
        }
        var folder = Path.GetDirectoryName(path);
        var entries = HashChecker.ParseList(text);
        if (entries.Count == 0)
        {
            Dialogs.Info($"No MD5, SHA-1, SHA-256, SHA-384 or SHA-512 hashes were found in '{Path.GetFileName(path)}'.");
            return;
        }
        // Set the folder first: setting the text checks every file, and names in the list are relative to the folder.
        _listFolder = append && _listFolder is not null && !string.Equals(_listFolder, folder, StringComparison.OrdinalIgnoreCase) ? null : folder;
        ExpectedText = append && ExpectedText.Length > 0 ? ExpectedText.TrimEnd() + Environment.NewLine + text : text;
        ActivityLog.Write("Hash Checker", $"List loaded: {Path.GetFileName(path)} ({entries.Count} hash(es))");
        var named = new List<string>();
        foreach (var entry in entries)
        {
            if (entry.Name is not { } name || folder is null) continue;
            try
            {
                var listed = name.Replace('/', Path.DirectorySeparatorChar);
                var full = Path.GetFullPath(Path.IsPathFullyQualified(listed) ? listed : Path.Combine(folder, listed));
                if (File.Exists(full)) named.Add(full);
            }
            catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { }
        }
        await AddPathsAsync(named);
    }

    /// <summary>Adds files (and every file in added folders, subfolders included) and works out their hashes.</summary>
    public async Task AddPathsAsync(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return;
        var known = new HashSet<string>(Files.Select(f => f.Path), StringComparer.OrdinalIgnoreCase);
        int skipped = 0;
        foreach (var path in ExpandAll(paths, ref skipped))
        {
            if (!known.Add(path)) continue;
            var row = new HashFileRow(path, ShownKind);
            row.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(HashFileRow.IsChecked)) OnPropertyChanged(nameof(AllChecked)); };
            Files.Add(row);
        }
        OnPropertyChanged(nameof(AllChecked));
        if (skipped > 0)
            Dialogs.Info($"Only the first {FolderLimit:N0} files of a folder are added, so {skipped:N0} file(s) were left out. Add a smaller folder to check the rest.");
        if (!IsBusy) await ComputeAsync();
    }

    private static List<string> ExpandAll(IReadOnlyList<string> paths, ref int skipped)
    {
        var list = new List<string>();
        foreach (var path in paths)
        {
            if (File.Exists(path)) list.Add(Path.GetFullPath(path));
            else if (Directory.Exists(path))
            {
                var options = new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System,
                };
                int n = 0;
                foreach (var file in Directory.EnumerateFiles(path, "*", options))
                {
                    if (n++ >= FolderLimit) { skipped++; continue; }
                    list.Add(file);
                }
            }
        }
        return list;
    }

    /// <summary>Works out every waiting file's hashes, one file at a time, as a background task with Cancel.</summary>
    private async Task ComputeAsync()
    {
        if (!Files.Any(f => f.IsPending)) return;
        IsBusy = true;
        using var task = BackgroundTasks.Start(this, "Computing Hashes");
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        int done = 0, failed = 0;
        try
        {
            // Files added while this runs are picked up too: the loop asks again after every file.
            while (Files.FirstOrDefault(f => f.IsPending) is { } row)
            {
                var waiting = Files.Where(f => f.IsPending).ToList();
                long total = waiting.Sum(f => f.Bytes) + Files.Where(f => f.DoneThisRun).Sum(f => f.Bytes);
                long before = Files.Where(f => f.DoneThisRun).Sum(f => f.Bytes);
                Summary = waiting.Count == 1 ? $"Computing the hashes of {row.Name}…" : $"Computing hashes: {row.Name} ({waiting.Count:N0} files to go)…";
                row.Status = "Reading…";
                try
                {
                    var path = row.Path;
                    var result = await Task.Run(() => HashChecker.ComputeFile(path, n => task.Step(before + n, total), token), token);
                    row.SetHashes(result);
                    row.Check(_expected, _listFolder);
                    done++;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    row.SetError(e is UnauthorizedAccessException ? "Access denied" : e.Message);
                    failed++;
                }
                row.DoneThisRun = true;
            }
            Summary = Files.Count == 0 ? "No files yet." : $"{Files.Count(f => f.Hashes is not null):N0} file(s) hashed" +
                (failed > 0 ? $", {failed:N0} couldn't be read (see Status)." : ".") +
                " Click a file to see all five hashes, or right-click to copy one.";
        }
        catch (OperationCanceledException)
        {
            foreach (var f in Files.Where(f => f.IsPending)) f.Status = "Not Hashed Yet";
            Summary = "Stopped. Click Compute Hashes to finish the files that are left.";
        }
        finally
        {
            foreach (var f in Files) f.DoneThisRun = false;
            IsBusy = false;
            UpdateListSummary();
            CommandManager.InvalidateRequerySuggested(); // Save and Copy can be used now, without waiting for a click
            if (Filter.Length > 0) View.Refresh(); // hashes found since the search was typed can match it now
        }
    }

    private void CheckAll()
    {
        foreach (var f in Files) f.Check(_expected, _listFolder);
        UpdateListSummary();
    }

    private void UpdateListSummary()
    {
        if (_expected.Count == 0)
        {
            ListSummary = ExpectedText.Trim().Length == 0
                ? "Paste hashes from a download page, or load a checksum file (.sha256, .md5, SHA256SUMS…). Each file is checked against them."
                : "No MD5, SHA-1, SHA-256, SHA-384 or SHA-512 hashes found in the list yet.";
            return;
        }
        var hashed = Files.Where(f => f.Hashes is not null).ToList();
        int good = hashed.Count(f => f.Verdict == HashVerdict.Verified);
        int bad = hashed.Count(f => f.Verdict == HashVerdict.Mismatch);
        var text = $"{_expected.Count} hash(es) in the list. ";
        text += hashed.Count == 0 ? "Add the files to check them." :
            bad > 0 ? $"{good} file(s) verified, {bad} {(bad == 1 ? "doesn't" : "don't")} match: {(bad == 1 ? "it differs" : "they differ")} from the listed file (damaged, changed or a different version)." :
            good == hashed.Count ? $"All {good} file(s) verified." :
            $"{good} file(s) verified, {hashed.Count - good} aren't in the list.";
        // Named entries no added file stands for: say which, so a missing file in a download isn't missed.
        var missing = _expected.Where(e => e.Name is not null && !Files.Any(f => HashChecker.NameMatches(e, f.Path, _listFolder)))
            .Select(e => Path.GetFileName(e.Name!.Replace('/', '\\'))).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (missing.Count > 0 && hashed.Count > 0)
            text += missing.Count <= 3 ? $" Not added: {string.Join(", ", missing)}." : $" {missing.Count} listed files weren't added.";
        ListSummary = text;
    }

    private void Remove(List<HashFileRow> rows)
    {
        foreach (var r in rows) Files.Remove(r);
        OnPropertyChanged(nameof(AllChecked));
        OnPropertyChanged(nameof(ShowCompute));
        OnPropertyChanged(nameof(SearchSummary));
        UpdateListSummary();
    }

    /// <summary>One file: just the hash, so it pastes straight into a website's box. Several: "hash *name" lines, as sha256sum writes them.</summary>
    private static void Copy(IEnumerable<HashFileRow> rows, HashKind kind)
    {
        var withHash = rows.Where(r => r.Hashes?.ContainsKey(kind) == true).ToList();
        if (withHash.Count == 0) return;
        ClipboardHelper.CopyOrExplain(withHash.Count == 1
            ? withHash[0].Hashes![kind]
            : string.Join(Environment.NewLine, withHash.Select(r => HashChecker.ChecksumLine(r.Hashes![kind], r.Name))));
    }

    private void CopyAll()
    {
        var lines = new List<string>();
        foreach (var r in Highlighted.Where(r => r.Hashes is not null))
        {
            if (lines.Count > 0) lines.Add("");
            lines.Add(r.Path);
            lines.AddRange(r.Lines.Select(l => $"{l.Label}: {l.Hash}"));
        }
        if (lines.Count > 0) ClipboardHelper.CopyOrExplain(string.Join(Environment.NewLine, lines));
    }

    /// <summary>Saves a checksum file of the shown kind (game.iso.sha256 for one file, else hashes.sha256), readable by sha256sum -c and by this page.</summary>
    private void SaveChecksumFile()
    {
        var rows = Picked(all: true).Where(r => r.Hashes?.ContainsKey(ShownKind) == true).ToList();
        if (rows.Count == 0) return;
        var folders = rows.Select(r => Path.GetDirectoryName(r.Path)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save a Checksum File",
            FileName = (rows.Count == 1 ? rows[0].Name : "hashes") + HashChecker.Extension(ShownKind),
            InitialDirectory = folders.Count == 1 ? folders[0] : null,
            Filter = $"{ShownKindName} Checksum File (*{HashChecker.Extension(ShownKind)})|*{HashChecker.Extension(ShownKind)}|All Files (*.*)|*.*",
        };
        if (dialog.ShowDialog() != true) return;
        // Names are relative to the checksum file's folder when the files are in it or below it, else full paths.
        var target = Path.GetDirectoryName(dialog.FileName) ?? "";
        string NameFor(HashFileRow r)
        {
            var rel = Path.GetRelativePath(target, r.Path);
            return rel.StartsWith("..") || Path.IsPathRooted(rel) ? r.Path : rel.Replace('\\', '/');
        }
        try
        {
            File.WriteAllLines(dialog.FileName, rows.Select(r => HashChecker.ChecksumLine(r.Hashes![ShownKind], NameFor(r))));
            ActivityLog.Write("Hash Checker", $"Checksum file saved: {Path.GetFileName(dialog.FileName)} ({rows.Count} file(s), {ShownKindName})");
            Summary = $"Saved {Path.GetFileName(dialog.FileName)} with the {ShownKindName} of {rows.Count:N0} file(s).";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Dialogs.Error("The checksum file could not be saved: " + e.Message);
        }
    }
}

/// <summary>One kind of hash of the current file, for the details card (label, hash, Copy).</summary>
public sealed record HashLine(string Label, string Hash, bool IsMatch);

/// <summary>One file in Hash Checker: its hashes once worked out, and how it compares with the list.</summary>
public sealed class HashFileRow : ObservableObject
{
    public HashFileRow(string path, HashKind shown)
    {
        Path = path;
        Name = System.IO.Path.GetFileName(path);
        Folder = System.IO.Path.GetDirectoryName(path) ?? "";
        try { Bytes = new FileInfo(path).Length; } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        _shown = shown;
    }

    public string Path { get; }
    public string Name { get; }
    public string Folder { get; }
    public long Bytes { get; private set; }
    public string Size => ByteSize.Format(Bytes);

    public IReadOnlyDictionary<HashKind, string>? Hashes { get; private set; }
    public bool IsPending => Hashes is null && Error is null;
    /// <summary>Hashed during the current run (counts toward its progress bar).</summary>
    internal bool DoneThisRun { get; set; }
    public string? Error { get; private set; }

    private bool _isChecked;
    public bool IsChecked { get => _isChecked; set => Set(ref _isChecked, value); }

    private HashKind _shown;
    public HashKind Shown
    {
        get => _shown;
        set
        {
            if (!Set(ref _shown, value)) return;
            OnPropertyChanged(nameof(Hash));
        }
    }
    /// <summary>The hash the table shows (the kind picked above it).</summary>
    public string Hash => Hashes is null ? "" : Hashes.TryGetValue(Shown, out var h) ? h : "Not Available";

    private string _status = "Waiting";
    public string Status { get => _status; set => Set(ref _status, value); }

    public HashVerdict Verdict { get; private set; } = HashVerdict.NoList;
    private HashCheck? _check;
    public bool IsVerified => Verdict == HashVerdict.Verified;
    public bool IsMismatch => Verdict == HashVerdict.Mismatch || Error is not null;

    /// <summary>All five hashes for the details card; the one the list matched is marked.</summary>
    public IReadOnlyList<HashLine> Lines => Hashes is null
        ? Array.Empty<HashLine>()
        : HashChecker.Kinds.Where(Hashes.ContainsKey)
            .Select(k => new HashLine(HashChecker.Name(k), Hashes[k], IsVerified && _check?.Kind == k)).ToList();

    /// <summary>The Status cell's tooltip: which kind of hash decided it, or why the file couldn't be read.</summary>
    public string StatusTip => Error is not null ? Status : _check?.Kind is { } k ? $"{Status} by its {HashChecker.Name(k)}. {CheckDetails}" : Status;

    /// <summary>Details card text under the hashes: what the list said about this file.</summary>
    public string CheckDetails => _check?.Entry is not { } entry ? "" : Verdict switch
    {
        HashVerdict.Verified => entry.Name is { } n && !n.Replace('\\', '/').EndsWith(Name, StringComparison.OrdinalIgnoreCase)
            ? $"Its {HashChecker.Name(entry.Kind)} matches line {entry.Line} of the list, which names it {n}."
            : $"Its {HashChecker.Name(entry.Kind)} matches line {entry.Line} of the list.",
        HashVerdict.Mismatch => $"Line {entry.Line} of the list expects {HashChecker.Name(entry.Kind)} {entry.Hash}.",
        _ => "",
    };

    public void SetHashes(FileHashes result)
    {
        Hashes = result.Hashes;
        Bytes = result.Size;
        OnPropertyChanged(nameof(Hashes));
        OnPropertyChanged(nameof(Hash));
        OnPropertyChanged(nameof(Size));
        OnPropertyChanged(nameof(Lines));
    }

    public void SetError(string message)
    {
        Error = message;
        Status = "Can't Read: " + message;
        OnPropertyChanged(nameof(IsMismatch));
    }

    public void Check(IReadOnlyList<ExpectedHash> list, string? listFolder)
    {
        if (Hashes is null) return;
        _check = HashChecker.Check(new FileHashes(Path, Bytes, Hashes), list, listFolder);
        Verdict = _check.Verdict;
        Status = Verdict switch
        {
            HashVerdict.Verified => "Verified",
            HashVerdict.Mismatch => "Doesn't Match",
            HashVerdict.NotInList => "Not in the List",
            _ => "Hashed",
        };
        OnPropertyChanged(nameof(Verdict));
        OnPropertyChanged(nameof(IsVerified));
        OnPropertyChanged(nameof(IsMismatch));
        OnPropertyChanged(nameof(Lines));
        OnPropertyChanged(nameof(CheckDetails));
        OnPropertyChanged(nameof(StatusTip));
    }

    /// <summary>Search: the name, or any part of any hash (a pasted hash may have spaces, dashes or capitals).</summary>
    public bool Matches(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) return true;
        if (Name.Contains(filter.Trim(), StringComparison.CurrentCultureIgnoreCase)) return true;
        var hex = HashChecker.Normalize(filter);
        return hex.Length > 0 && Hashes is not null && Hashes.Values.Any(h => h.Contains(hex, StringComparison.Ordinal));
    }
}
