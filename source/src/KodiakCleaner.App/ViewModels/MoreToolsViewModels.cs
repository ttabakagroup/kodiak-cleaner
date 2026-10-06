using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using System.Windows.Input;
using KodiakCleaner.Core.Tools;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App.ViewModels;

/// <summary>A Disk Analyzer row with its share of the total, for the bar.</summary>
public sealed class DiskItemRow
{
    public DiskItemRow(DiskItem item, long total)
    {
        Item = item;
        Share = total > 0 ? 100.0 * item.Bytes / total : 0;
    }

    public DiskItem Item { get; }
    public string Name => Item.Name;
    public string Folder => Path.GetDirectoryName(Item.Path) ?? "";
    public long Bytes => Item.Bytes;
    public string Size => ByteSize.Format(Item.Bytes);
    public string Files => Item.Files.ToString("N0");
    public double Share { get; }
    public string ShareText => $"{Share:0.#}%";
}

public sealed class DiskAnalyzerViewModel : ObservableObject
{
    private CancellationTokenSource? _cts;

    public DiskAnalyzerViewModel()
    {
        foreach (var d in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
            Places.Add(d.RootDirectory.FullName);
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(profile)) Places.Insert(0, profile);
        _root = Places.FirstOrDefault() ?? "";

        AnalyzeCommand = new RelayCommand(AnalyzeAsync, () => !IsBusy && Directory.Exists(Root));
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy);
        BrowseCommand = new RelayCommand(Browse, () => !IsBusy);
        PickPlaceCommand = new RelayCommand<string>(p => Root = p);
        OpenCommand = new RelayCommand(OpenSelectedAsync, () => !IsBusy && SelectedFolder?.Item.IsFolder == true);
        UpCommand = new RelayCommand(UpAsync, () => !IsBusy && Directory.Exists(Root) && Directory.GetParent(Root) is not null);
        ShowFileCommand = new RelayCommand(() => Elevation.ShowInExplorer(SelectedFile!.Item.Path), () => SelectedFile is not null);
        RecycleFileCommand = new RelayCommand(RecycleFile, () => !IsBusy && SelectedFile is not null);
    }

    public ObservableCollection<string> Places { get; } = new();
    public ObservableCollection<DiskItemRow> Folders { get; } = new();
    public ObservableCollection<DiskItemRow> LargestFiles { get; } = new();

    public ICommand AnalyzeCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand BrowseCommand { get; }
    public ICommand PickPlaceCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand UpCommand { get; }
    public ICommand ShowFileCommand { get; }
    public ICommand RecycleFileCommand { get; }

    private string _root;
    public string Root { get => _root; set => Set(ref _root, value); }

    private DiskItemRow? _selectedFolder;
    public DiskItemRow? SelectedFolder { get => _selectedFolder; set => Set(ref _selectedFolder, value); }

    private DiskItemRow? _selectedFile;
    public DiskItemRow? SelectedFile { get => _selectedFile; set => Set(ref _selectedFile, value); }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }

    private string _summary = "Choose a drive or folder, then click Analyze to see what's taking up space.";
    public string Summary { get => _summary; private set => Set(ref _summary, value); }

    private void Browse()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Choose a Folder to Analyze" };
        if (dialog.ShowDialog() != true) return;
        if (!Places.Contains(dialog.FolderName)) Places.Add(dialog.FolderName);
        Root = dialog.FolderName;
    }

    private async Task OpenSelectedAsync()
    {
        Root = SelectedFolder!.Item.Path;
        await AnalyzeAsync();
    }

    private async Task UpAsync()
    {
        Root = Directory.GetParent(Root)!.FullName;
        await AnalyzeAsync();
    }

    private async Task AnalyzeAsync()
    {
        IsBusy = true;
        Folders.Clear();
        LargestFiles.Clear();
        _cts = new CancellationTokenSource();
        var root = Root;
        var progress = new Progress<int>(n => Summary = $"Measuring {root}… {n:N0} files so far");
        try
        {
            var report = await Task.Run(() => DiskAnalyzer.Analyze(root, 100, progress, _cts.Token));
            foreach (var c in report.Children) Folders.Add(new DiskItemRow(c, report.TotalBytes));
            foreach (var f in report.LargestFiles) LargestFiles.Add(new DiskItemRow(f, report.TotalBytes));
            Summary = $"{report.Root} holds {ByteSize.Format(report.TotalBytes)} in {report.Files:N0} files." +
                      (report.Skipped > 0 ? $" {report.Skipped:N0} folder(s) couldn't be read{(Elevation.IsAdministrator ? "" : " without administrator rights")}." : "");
        }
        catch (OperationCanceledException) { Summary = "Analysis cancelled."; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Summary = "Couldn't read that folder: " + e.Message; }
        finally { IsBusy = false; }
    }

    private void RecycleFile()
    {
        var row = SelectedFile!;
        if (!Dialogs.Confirm($"Move {row.Name} ({row.Size}) to the Recycle Bin?")) return;
        try
        {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(row.Item.Path,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            LargestFiles.Remove(row);
            Summary = $"Moved {row.Name} to the Recycle Bin. Analyze again to update the folder sizes.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            Dialogs.Error($"{row.Name} couldn't be moved to the Recycle Bin: {e.Message}");
        }
    }
}

public sealed class EmptyFolderItem : ObservableObject
{
    public EmptyFolderItem(string path) => Path = path;
    public string Path { get; }
    private bool _isChecked = true;
    public bool IsChecked { get => _isChecked; set => Set(ref _isChecked, value); }
}

public sealed class EmptyFoldersViewModel : ObservableObject
{
    private CancellationTokenSource? _cts;

    public EmptyFoldersViewModel()
    {
        _root = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        BrowseCommand = new RelayCommand(Browse, () => !IsBusy);
        SearchCommand = new RelayCommand(SearchAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(Root));
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy);
        SelectAllCommand = new RelayCommand(() => { foreach (var f in Found) f.IsChecked = true; }, () => Found.Count > 0);
        ClearCommand = new RelayCommand(() => { foreach (var f in Found) f.IsChecked = false; }, () => Found.Count > 0);
        RemoveCommand = new RelayCommand(RemoveAsync, () => !IsBusy && Found.Any(f => f.IsChecked));
        ShowCommand = new RelayCommand<EmptyFolderItem>(f => Elevation.OpenFolder(f.Path));
    }

    public ObservableCollection<EmptyFolderItem> Found { get; } = new();
    public ICommand BrowseCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand ShowCommand { get; }

    private string _root;
    public string Root { get => _root; set => Set(ref _root, value); }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }

    private string _summary = "Finds folders with nothing in them. Windows, program folders and AppData are never searched, because programs can need their empty folders, and your Desktop, Documents and other Windows folders are never removed.";
    public string Summary { get => _summary; private set => Set(ref _summary, value); }

    /// <summary>Folders where an empty folder can matter to Windows or a program.</summary>
    private static IEnumerable<string> ProtectedFolders()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData");
        yield return AppContext.BaseDirectory;
    }

    /// <summary>Windows' own user folders: searched inside, but never offered for removal even when empty.</summary>
    private static IEnumerable<string> KeptFolders()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var f in new[] { Environment.SpecialFolder.Desktop, Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.MyMusic,
                     Environment.SpecialFolder.MyPictures, Environment.SpecialFolder.MyVideos, Environment.SpecialFolder.Favorites,
                     Environment.SpecialFolder.Templates, Environment.SpecialFolder.CommonDesktopDirectory, Environment.SpecialFolder.CommonDocuments })
            yield return Environment.GetFolderPath(f);
        if (string.IsNullOrEmpty(profile)) yield break;
        foreach (var name in new[] { "Desktop", "Documents", "Downloads", "Music", "Pictures", "Videos", "Favorites", "Contacts",
                     "Links", "Saved Games", "Searches", "3D Objects", "OneDrive", "Pictures\\Screenshots", "Pictures\\Camera Roll", "Pictures\\Saved Pictures" })
            yield return Path.Combine(profile, name);
    }

    private void Browse()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Choose a Folder to Search" };
        if (dialog.ShowDialog() == true) Root = dialog.FolderName;
    }

    private async Task SearchAsync()
    {
        if (EmptyFolderFinder.WhyNotAllowed(Root, ProtectedFolders()) is { } why)
        {
            Summary = why;
            return;
        }
        if (!Directory.Exists(Root))
        {
            Summary = "That folder doesn't exist.";
            return;
        }
        IsBusy = true;
        Found.Clear();
        _cts = new CancellationTokenSource();
        Summary = $"Searching {Root}…";
        try
        {
            var root = Root;
            var guarded = ProtectedFolders().ToList();
            var kept = KeptFolders().ToList();
            var list = await Task.Run(() => EmptyFolderFinder.Find(root, guarded, kept, _cts.Token));
            foreach (var p in list) Found.Add(new EmptyFolderItem(p));
            Summary = list.Count == 0 ? "No empty folders found." : $"Found {list.Count:N0} empty folder(s). Untick any you want to keep.";
        }
        catch (OperationCanceledException) { Summary = "Search cancelled."; }
        finally { IsBusy = false; }
    }

    private async Task RemoveAsync()
    {
        var chosen = Found.Where(f => f.IsChecked).ToList();
        if (!Dialogs.Confirm($"Move {chosen.Count} empty folder(s) to the Recycle Bin?")) return;
        IsBusy = true;
        int moved = 0, skipped = 0;
        var guarded = ProtectedFolders().ToList();
        var kept = KeptFolders().ToList();
        try
        {
            await Task.Run(() =>
            {
                foreach (var f in chosen)
                {
                    // Something may have been saved into it since the search.
                    if (!EmptyFolderFinder.IsStillEmpty(f.Path, guarded, kept)) { skipped++; continue; }
                    try
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(f.Path,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                        moved++;
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException or OperationCanceledException) { skipped++; }
                }
            });
            foreach (var f in chosen.Where(f => !Directory.Exists(f.Path))) Found.Remove(f);
            Summary = $"Moved {moved} folder(s) to the Recycle Bin." + (skipped > 0 ? $" {skipped} were skipped (no longer empty, or in use)." : "");
        }
        finally { IsBusy = false; }
    }
}

public sealed class ExtensionRow : ObservableObject
{
    public ExtensionRow(BrowserExtension e) => Extension = e;

    private System.Windows.Media.ImageSource? _icon;
    /// <summary>The extension's own icon, read from its files; null shows a plain placeholder.</summary>
    public System.Windows.Media.ImageSource? Icon { get => _icon; set { if (Set(ref _icon, value)) OnPropertyChanged(nameof(NoIcon)); } }
    public bool NoIcon => _icon is null;

    private System.Windows.Media.ImageSource? _browserIcon;
    /// <summary>The installed browser's own icon; null shows a plain globe.</summary>
    public System.Windows.Media.ImageSource? BrowserIcon { get => _browserIcon; set { if (Set(ref _browserIcon, value)) OnPropertyChanged(nameof(NoBrowserIcon)); } }
    public bool NoBrowserIcon => _browserIcon is null;
    public BrowserExtension Extension { get; }
    public string Browser => Extension.Browser;
    public string Profile => Extension.Profile;
    public string Name => Extension.Name;
    public string Version => Extension.Version;
    public string Status => Extension.Enabled switch { true => "On", false => "Off", null => "" };
    public string ManageAt => BrowserExtensions.ManagePage(Extension.Browser);
}

public sealed class ExtensionsViewModel : ObservableObject, ILoadOnFirstVisit
{
    public ExtensionsViewModel()
    {
        View = CollectionViewSource.GetDefaultView(Extensions);
        View.Filter = o => o is ExtensionRow r && (string.IsNullOrWhiteSpace(Filter) ||
            r.Name.Contains(Filter, StringComparison.CurrentCultureIgnoreCase) ||
            r.Browser.Contains(Filter, StringComparison.CurrentCultureIgnoreCase));
        RefreshCommand = new RelayCommand(Refresh);
        ShowFolderCommand = new RelayCommand(() => Elevation.OpenFolder(Selected!.Extension.Folder), () => Selected is not null);
    }

    public ObservableCollection<ExtensionRow> Extensions { get; } = new();
    public ICollectionView View { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ShowFolderCommand { get; }

    private ExtensionRow? _selected;
    public ExtensionRow? Selected { get => _selected; set => Set(ref _selected, value); }

    private string _filter = "";
    public string Filter { get => _filter; set { if (Set(ref _filter, value)) View.Refresh(); } }

    private string _summary = "";
    public string Summary { get => _summary; private set => Set(ref _summary, value); }

    private bool _loaded;
    public void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        Refresh();
    }

    private void Refresh()
    {
        Extensions.Clear();
        var list = BrowserExtensions.FindAll(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        foreach (var e in list.OrderBy(e => e.Browser).ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)) Extensions.Add(new ExtensionRow(e));
        LoadIcons(Extensions.ToList());
        Summary = list.Count == 0
            ? "No extensions found in Chrome, Edge, Brave, Vivaldi, Helium, Firefox, Ablaze Floorp or Mullvad Browser."
            : $"{TargetRun.Plural(list.Count, "extension")} across {TargetRun.Plural(list.Select(e => e.Browser).Distinct().Count(), "browser")}. Remove ones you don't recognize from the browser's own Extensions page.";
    }

    /// <summary>Each extension's own icon and each browser's icon, read in the background.</summary>
    private static void LoadIcons(IReadOnlyList<ExtensionRow> rows)
    {
        var dispatcher = System.Windows.Application.Current.Dispatcher;
        Task.Run(() =>
        {
            var browsers = new Dictionary<string, System.Windows.Media.ImageSource?>();
            foreach (var row in rows)
            {
                if (!browsers.TryGetValue(row.Browser, out var browserIcon))
                    browsers[row.Browser] = browserIcon = FileIcons.For(FileIcons.ProgramOfBrowser(row.Browser));
                var icon = FileIcons.FromBytes(BrowserExtensions.ReadIcon(row.Extension));
                dispatcher.BeginInvoke(() =>
                {
                    if (icon is not null) row.Icon = icon;
                    if (browserIcon is not null) row.BrowserIcon = browserIcon;
                });
            }
        });
    }
}
