using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using KodiakCleaner.Core.Tools;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App.ViewModels;

public sealed class DuplicateFileItem : ObservableObject
{
    public DuplicateFileItem(DuplicateFile file, DuplicateGroupItem group)
    {
        File = file;
        Group = group;
    }

    public DuplicateFile File { get; }
    public DuplicateGroupItem Group { get; }
    public string Name => System.IO.Path.GetFileName(File.Path);
    public string Folder => System.IO.Path.GetDirectoryName(File.Path) ?? "";
    public string Modified => File.LastWriteUtc.ToLocalTime().ToString("g");

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set { if (Set(ref _isChecked, value)) Group.Owner.UpdateSelection(); }
    }
}

public sealed class DuplicateGroupItem
{
    public DuplicateGroupItem(DuplicateGroup group, DuplicatesViewModel owner)
    {
        Owner = owner;
        Header = $"{group.Files.Count} copies of {System.IO.Path.GetFileName(group.Files[0].Path)} · {ByteSize.Format(group.Size)} each";
        Files = group.Files.Select(f => new DuplicateFileItem(f, this)).ToList();
    }

    public DuplicatesViewModel Owner { get; }
    public string Header { get; }
    public List<DuplicateFileItem> Files { get; }
}

public sealed class DuplicatesViewModel : ObservableObject
{
    private CancellationTokenSource? _cts;

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
        SearchCommand = new RelayCommand(SearchAsync, () => !IsBusy && Folders.Any(f => f.IsChecked));
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy);
        SelectAllButNewestCommand = new RelayCommand(() => AutoSelect(keepNewest: true), () => !IsBusy && Groups.Count > 0);
        SelectAllButOldestCommand = new RelayCommand(() => AutoSelect(keepNewest: false), () => !IsBusy && Groups.Count > 0);
        ClearSelectionCommand = new RelayCommand(() => { foreach (var f in AllFiles) f.IsChecked = false; }, () => Groups.Count > 0);
        DeleteCommand = new RelayCommand(DeleteAsync, () => !IsBusy && AllFiles.Any(f => f.IsChecked));
        OpenFolderCommand = new RelayCommand<DuplicateFileItem>(f => Elevation.ShowInExplorer(f.File.Path));
    }

    public ObservableCollection<CheckItem> Folders { get; } = new();
    public ObservableCollection<DuplicateGroupItem> Groups { get; } = new();
    private IEnumerable<DuplicateFileItem> AllFiles => Groups.SelectMany(g => g.Files);

    public ICommand AddFolderCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand SelectAllButNewestCommand { get; }
    public ICommand SelectAllButOldestCommand { get; }
    public ICommand ClearSelectionCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand OpenFolderCommand { get; }

    private string _minSizeMb = "1";
    public string MinSizeMb { get => _minSizeMb; set => Set(ref _minSizeMb, value); }

    private bool _matchName;
    public bool MatchName { get => _matchName; set => Set(ref _matchName, value); }

    private bool _includeHidden;
    public bool IncludeHidden { get => _includeHidden; set => Set(ref _includeHidden, value); }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }

    private string _summary = "Finds files with identical contents. Choose where to look, then click Search.";
    public string Summary { get => _summary; private set => Set(ref _summary, value); }

    private string _selection = "";
    public string Selection { get => _selection; private set => Set(ref _selection, value); }

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

    private async Task SearchAsync()
    {
        IsBusy = true;
        Groups.Clear();
        Selection = "";
        _cts = new CancellationTokenSource();
        var options = new DuplicateSearchOptions
        {
            Folders = Folders.Where(f => f.IsChecked).Select(f => f.Name).ToList(),
            MinSize = (long)(double.TryParse(MinSizeMb, out var mb) && mb > 0 ? mb * 1024 * 1024 : 1),
            MatchName = MatchName,
            IncludeHidden = IncludeHidden,
        };
        var progress = new Progress<DuplicateProgress>(p => Summary = p.Stage == "Scanning"
            ? $"Scanning… {p.FilesScanned:N0} files so far"
            : $"Comparing contents… {p.Done:N0} of {p.Total:N0} candidate files");
        try
        {
            var found = await Task.Run(() => DuplicateFinder.Find(options, progress, _cts.Token));
            foreach (var g in found) Groups.Add(new DuplicateGroupItem(g, this));
            Summary = found.Count == 0
                ? "No duplicates found."
                : $"Found {found.Count:N0} {(found.Count == 1 ? "set" : "sets")} of duplicates using {ByteSize.Format(found.Sum(g => g.WastedBytes))} of extra space. Tick the copies to remove.";
        }
        catch (OperationCanceledException)
        {
            Summary = "Search cancelled.";
        }
        finally { IsBusy = false; }
    }

    private void AutoSelect(bool keepNewest)
    {
        foreach (var g in Groups)
        {
            var keep = keepNewest ? g.Files.MaxBy(f => f.File.LastWriteUtc) : g.Files.MinBy(f => f.File.LastWriteUtc);
            foreach (var f in g.Files) f.IsChecked = f != keep;
        }
    }

    private async Task DeleteAsync()
    {
        var chosen = AllFiles.Where(f => f.IsChecked).ToList();
        // Never let every copy in a set go.
        var wipedOut = Groups.Where(g => g.Files.All(f => f.IsChecked)).ToList();
        if (wipedOut.Count > 0)
        {
            Dialogs.Error($"Every copy is ticked in {wipedOut.Count} set(s), for example \"{wipedOut[0].Header}\". Leave at least one copy unticked in each set.");
            return;
        }
        if (!Dialogs.Confirm($"Move {chosen.Count} file(s) ({ByteSize.Format(chosen.Sum(f => f.File.Size))}) to the Recycle Bin?")) return;

        IsBusy = true;
        int failed = 0;
        try
        {
            await Task.Run(() =>
            {
                foreach (var f in chosen)
                {
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
            foreach (var g in Groups.ToList())
            {
                g.Files.RemoveAll(f => !System.IO.File.Exists(f.File.Path));
                if (g.Files.Count < 2) Groups.Remove(g);
            }
            // Re-create remaining groups so their lists refresh.
            var remaining = Groups.ToList();
            Groups.Clear();
            foreach (var g in remaining) Groups.Add(g);
            UpdateSelection();
            Summary = $"Moved {chosen.Count - failed} file(s) to the Recycle Bin." + (failed > 0 ? $" {failed} could not be moved." : "");
        }
        finally { IsBusy = false; }
    }
}
