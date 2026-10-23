using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using System.Windows.Input;
using KodiakCleaner.Core.Tools;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App.ViewModels;

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
        Places = new SearchPlaces(new[] { Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) });
        Places.Changed += System.Windows.Input.CommandManager.InvalidateRequerySuggested;
        BrowseCommand = new RelayCommand(Browse, () => !IsBusy);
        SearchCommand = new RelayCommand(SearchAsync, () => !IsBusy && Places.AnyTicked);
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

    /// <summary>The drives and folders to search; any mix can be ticked (Ted, 0.8.65).</summary>
    public SearchPlaces Places { get; }

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
        // Kodiak Cleaner's own Backups, Logs and Reports folders, which are empty until something is saved in them.
        yield return Settings.Data.Root;
        yield return DataFolders.Installed(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)).Root;
        yield return Path.GetTempPath();
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
        // Every profile's own folders (other people's and Public too), now that a whole drive can be searched.
        var profiles = new List<string> { profile };
        try { if (Path.GetDirectoryName(profile) is { } users) profiles.AddRange(Directory.EnumerateDirectories(users)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        foreach (var p in profiles.Distinct(StringComparer.OrdinalIgnoreCase))
            foreach (var name in new[] { "Desktop", "Documents", "Downloads", "Music", "Pictures", "Videos", "Favorites", "Contacts",
                         "Links", "Saved Games", "Searches", "3D Objects", "OneDrive", "Libraries", "AccountPictures",
                         "Pictures\\Screenshots", "Pictures\\Camera Roll", "Pictures\\Saved Pictures" })
                yield return Path.Combine(p, name);
    }

    private void Browse()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Choose a Folder to Search" };
        if (dialog.ShowDialog() == true) Places.AddFolder(dialog.FolderName);
    }

    private async Task SearchAsync()
    {
        var roots = Places.Ticked;
        foreach (var root in roots)
        {
            if (EmptyFolderFinder.WhyNotAllowed(root, ProtectedFolders(), allowDrive: true) is { } why)
            {
                Summary = $"{root}: {why}";
                return;
            }
        }
        IsBusy = true;
        using var task = BackgroundTasks.Start(this, "Searching for Empty Folders");
        Found.Clear();
        _cts = new CancellationTokenSource();
        Summary = roots.Count == 1 ? $"Searching {roots[0]}…" : $"Searching {roots.Count} places…";
        try
        {
            var guarded = ProtectedFolders().ToList();
            var kept = KeptFolders().ToList();
            var token = _cts.Token;
            var list = await Task.Run(() =>
            {
                // A folder inside another ticked place is searched once.
                var all = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var root in roots.Where(Directory.Exists))
                {
                    if (roots.Any(r => r != root && Core.Util.PathSafety.IsInside(root, r))) continue;
                    foreach (var p in EmptyFolderFinder.Find(root, guarded, kept, token)) all.Add(p);
                }
                return all.ToList();
            });
            foreach (var p in list) Found.Add(new EmptyFolderItem(p));
            var where = roots.Count == 1 ? "" : $" in {roots.Count} places";
            Summary = list.Count == 0 ? $"No empty folders found{where}." : $"Found {list.Count:N0} empty folder(s){where}. Untick any you want to keep.";
        }
        catch (OperationCanceledException) { Summary = "Search cancelled."; }
        finally { IsBusy = false; }
    }

    private async Task RemoveAsync()
    {
        var chosen = Found.Where(f => f.IsChecked).ToList();
        if (!Dialogs.Confirm($"Move {chosen.Count} empty folder(s) to the Recycle Bin?")) return;
        IsBusy = true;
        using var task = BackgroundTasks.Start(this, "Moving Folders to the Recycle Bin");
        int moved = 0, skipped = 0;
        var guarded = ProtectedFolders().ToList();
        var kept = KeptFolders().ToList();
        try
        {
            await Task.Run(() =>
            {
                foreach (var f in chosen)
                {
                    task.Step(moved + skipped, chosen.Count);
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
    public ExtensionRow(BrowserExtension e, bool removing)
    {
        Extension = e;
        Removing = removing;
    }

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
    /// <summary>Asked to be removed; the browser uninstalls it when it next starts (or at once).</summary>
    public bool Removing { get; }
    public string Status => Removing ? "Removing" : Extension.Enabled switch { true => "On", false => "Off", null => "" };
    public string ManageAt => BrowserExtensions.ManagePage(Extension.Browser);

    /// <summary>The On/Off switch. Set by the page, which changes the browser and reads the list again.</summary>
    public bool IsOn { get => Extension.Enabled != false; set { if (value != IsOn) Toggle?.Invoke(this, value); } }
    public bool CanToggle => !Removing && ExtensionControl.CanTurnOff(Extension);
    public string ToggleTip => CanToggle ? (IsOn ? "On: click to turn it off" : "Off: click to turn it on") : ExtensionControl.WhyNot(Extension);
    internal Action<ExtensionRow, bool>? Toggle { get; set; }

    /// <summary>Hover details, one "Label: value" line each.</summary>
    public string Details => DetailText.Lines(("Name", Name), ("Browser", Browser), ("Profile", Profile), ("Version", Version),
        ("Status", Status), ("ID", Extension.Id), ("Folder", Extension.Folder), ("Manage at", ManageAt));
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
        TurnOnCommand = new RelayCommand(() => SetOn(Highlighted, true), () => Highlighted.Any(r => r.CanToggle && !r.IsOn));
        TurnOffCommand = new RelayCommand(() => SetOn(Highlighted, false), () => Highlighted.Any(r => r.CanToggle && r.IsOn));
        RemoveCommand = new RelayCommand(() => Remove(Highlighted), () => Highlighted.Any(r => !r.Removing));
    }

    public ObservableCollection<ExtensionRow> Extensions { get; } = new();
    public ICollectionView View { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ShowFolderCommand { get; }
    public ICommand TurnOnCommand { get; }
    public ICommand TurnOffCommand { get; }
    public ICommand RemoveCommand { get; }

    private ExtensionRow? _selected;
    public ExtensionRow? Selected { get => _selected; set => Set(ref _selected, value); }

    /// <summary>The highlighted rows (Ctrl-click and Shift-click pick several). Set by the view.</summary>
    public IReadOnlyList<ExtensionRow> Highlighted { get; set; } = Array.Empty<ExtensionRow>();

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
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)).ToList();
        // Extensions Kodiak Cleaner turned off are on the browser's block list; the browser may not say so in its own files yet.
        var blocked = list.Select(e => e.Browser).Distinct().ToDictionary(b => b, ExtensionControl.Blocked);
        list = list.Select(e => blocked[e.Browser].Contains(e.Id) ? e with { Enabled = false } : e).ToList();
        TidyRemoved(list);
        foreach (var e in list.OrderBy(e => e.Browser).ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var row = new ExtensionRow(e, ExtensionControl.RemovalPending(e.Browser, e.Id)) {
                // After the switch has finished updating, since the list is read again.
                Toggle = (r, on) => System.Windows.Application.Current.Dispatcher.BeginInvoke(() => SetOn(new[] { r }, on)),
            };
            Extensions.Add(row);
        }
        LoadIcons(Extensions.ToList());
        Summary = list.Count == 0
            ? "No extensions found in Chrome, Edge, Brave, Vivaldi, Helium, Firefox, Ablaze Floorp or Mullvad Browser."
            : $"{TargetRun.Plural(list.Count, "extension")} across {TargetRun.Plural(list.Select(e => e.Browser).Distinct().Count(), "browser")}. Right-click to turn one off or remove it.";
    }

    private void SetOn(IEnumerable<ExtensionRow> rows, bool on)
    {
        var errors = new List<string>();
        foreach (var r in rows.Where(r => r.IsOn != on).ToList())
        {
            var error = r.CanToggle ? ExtensionControl.SetOn(r.Extension, on) : ExtensionControl.WhyNot(r.Extension);
            if (error is not null) errors.Add($"{r.Name}: {error}");
            else ActivityLog.Write("Extensions", $"Turned {(on ? "on" : "off")} {r.Name} ({r.Browser})");
        }
        Finish(errors);
    }

    private void Remove(IReadOnlyList<ExtensionRow> rows)
    {
        rows = rows.Where(r => !r.Removing).ToList();
        if (rows.Count == 0) return;
        var names = rows.Count == 1 ? $"{rows[0].Name} from {rows[0].Browser}" : TargetRun.Plural(rows.Count, "extension");
        if (!Dialogs.Confirm($"Remove {names}? Its settings in the browser are removed with it. You can install it again from the browser's store.",
                "Remove Extension")) return;
        var errors = new List<string>();
        var removed = Settings.Current.RemovedExtensions;
        foreach (var r in rows)
        {
            var error = ExtensionControl.CanRemove(r.Extension) ? ExtensionControl.Remove(r.Extension) : ExtensionControl.WhyNot(r.Extension);
            if (error is not null) { errors.Add($"{r.Name}: {error}"); continue; }
            ActivityLog.Write("Extensions", $"Removed {r.Name} ({r.Browser})");
            if (ExtensionControl.PolicyKey(r.Browser) is not null) removed.Add($"{r.Browser}|{r.Extension.Id}");
        }
        Settings.Current.RemovedExtensions = removed.Distinct().ToList();
        Settings.Current.Save();
        Finish(errors);
    }

    private void Finish(List<string> errors)
    {
        Refresh();
        if (errors.Count == 0) return;
        var text = string.Join("\n\n", errors);
        if (errors.Any(e => e.EndsWith(ExtensionControl.NeedsAdmin))) Elevation.OfferRestart(text);
        else Dialogs.Error(text);
    }

    /// <summary>Once the browser has uninstalled an extension Kodiak Cleaner removed, its remove mark is cleared again.</summary>
    private static void TidyRemoved(IReadOnlyCollection<BrowserExtension> installed)
    {
        var mine = Settings.Current.RemovedExtensions;
        if (mine.Count == 0) return;
        var pairs = mine.Select(x => x.Split('|')).Where(p => p.Length == 2).Select(p => (p[0], p[1])).ToList();
        ExtensionControl.Tidy(pairs, installed);
        var left = pairs.Where(p => installed.Any(e => e.Browser == p.Item1 && e.Id == p.Item2)).Select(p => $"{p.Item1}|{p.Item2}").ToList();
        if (left.Count == mine.Count) return;
        Settings.Current.RemovedExtensions = left;
        Settings.Current.Save();
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
