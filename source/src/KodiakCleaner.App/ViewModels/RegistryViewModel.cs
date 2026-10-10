using System.Collections.ObjectModel;
using System.Windows.Input;
using KodiakCleaner.Core.RegistryTools;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App.ViewModels;

public sealed class CheckItem : ObservableObject
{
    public CheckItem(string name, bool isChecked = true) { Name = name; _isChecked = isChecked; }
    public string Name { get; }
    /// <summary>False for the blank fillers that keep a grid's columns even.</summary>
    public bool IsVisible { get; init; } = true;
    private bool _isChecked;
    public bool IsChecked { get => _isChecked; set => Set(ref _isChecked, value); }
}

public sealed class IssueItem : ObservableObject
{
    public IssueItem(RegistryIssue issue) => Issue = issue;
    public RegistryIssue Issue { get; }
    public string Category => Issue.Category;
    public string Description => Issue.Description;
    public string Data => Issue.Data;
    public string Key => Issue.Location.ToString();
    /// <summary>Everything about the issue in one place, shown when the row is hovered (Ted, 0.8.63).</summary>
    public string Details => DetailText.Lines(
        ("Type", Category), ("Problem", Description), ("Data", Data), ("Registry Key", Key));
    private bool _isChecked = true;
    public bool IsChecked { get => _isChecked; set => Set(ref _isChecked, value); }
}

public sealed class RegistryViewModel : ObservableObject
{
    public RegistryViewModel()
    {
        // Shown in three columns that read A to Z down each column (the grid itself fills across, row by row).
        var all = RegistryScanner.Categories;
        int rows = (all.Length + LookForColumns - 1) / LookForColumns;
        for (int i = 0; i < rows * LookForColumns; i++)
        {
            int index = (i % LookForColumns) * rows + i / LookForColumns;
            var item = new CheckItem(index < all.Length ? all[index] : "") { IsVisible = index < all.Length };
            item.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(CheckItem.IsChecked)) OnPropertyChanged(nameof(AllCategoriesChecked)); };
            Categories.Add(item);
        }
        ScanCommand = new RelayCommand(ScanAsync, () => !IsBusy && ChosenCategories().Count > 0);
        FixCommand = new RelayCommand(() => FixAsync(Picked()), () => !IsBusy && (Issues.Any(i => i.IsChecked) || Highlighted.Count > 0));
        FixRowCommand = new RelayCommand(() => FixAsync(Highlighted.ToList()), () => !IsBusy && Highlighted.Count > 0);
        OpenBackupsCommand = new RelayCommand(() => Elevation.OpenFolder(RegistryBackup.BackupFolder));
        BackUpNowCommand = new RelayCommand(BackUpNowAsync, () => !IsBusy);
    }

    public const int LookForColumns = 3;

    /// <summary>The Look For checks, laid out for the grid (blank fillers keep the columns even).</summary>
    public ObservableCollection<CheckItem> Categories { get; } = new();

    /// <summary>Select All above the Look For grid: true when every check is on, null when some are.</summary>
    public bool? AllCategoriesChecked
    {
        get
        {
            var shown = Categories.Where(c => c.IsVisible).ToList();
            return shown.All(c => c.IsChecked) ? true : shown.Any(c => c.IsChecked) ? null : false;
        }
        set
        {
            var on = value != false;
            foreach (var c in Categories.Where(c => c.IsVisible)) c.IsChecked = on;
        }
    }

    private HashSet<string> ChosenCategories() => Categories.Where(c => c.IsVisible && c.IsChecked).Select(c => c.Name).ToHashSet();
    public ObservableCollection<IssueItem> Issues { get; } = new();
    public ICommand ScanCommand { get; }
    public ICommand FixCommand { get; }
    /// <summary>Right-click › Fix: fixes every highlighted row (Ctrl-click and Shift-click highlight several).</summary>
    public ICommand FixRowCommand { get; }
    public ICommand OpenBackupsCommand { get; }
    public ICommand BackUpNowCommand { get; }

    private IssueItem? _selected;
    public IssueItem? Selected { get => _selected; set => Set(ref _selected, value); }

    private IReadOnlyList<IssueItem> _highlighted = Array.Empty<IssueItem>();
    /// <summary>The rows highlighted with click, Ctrl-click or Shift-click.</summary>
    public IReadOnlyList<IssueItem> Highlighted
    {
        get => _highlighted;
        set { _highlighted = value; OnPropertyChanged(); OnPropertyChanged(nameof(FixRowLabel)); }
    }

    public string FixRowLabel => Highlighted.Count > 1 ? $"Fix These {Highlighted.Count} Issues" : "Fix This Issue";

    /// <summary>Fix Selected uses the ticked rows; with none ticked it uses the highlighted ones.</summary>
    private List<IssueItem> Picked()
    {
        var ticked = Issues.Where(i => i.IsChecked).ToList();
        return ticked.Count > 0 ? ticked : Highlighted.ToList();
    }

    /// <summary>The header checkbox: true when every issue is ticked, null when some are.</summary>
    public bool? AllChecked
    {
        get => Issues.Count > 0 && Issues.All(i => i.IsChecked) ? true : Issues.Any(i => i.IsChecked) ? null : false;
        set
        {
            var on = value != false;
            foreach (var i in Issues) i.IsChecked = on;
        }
    }

    private void ShowIssues(IEnumerable<RegistryIssue> found)
    {
        Issues.Clear();
        foreach (var issue in found)
        {
            var item = new IssueItem(issue);
            item.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(IssueItem.IsChecked)) OnPropertyChanged(nameof(AllChecked)); };
            Issues.Add(item);
        }
        OnPropertyChanged(nameof(AllChecked));
    }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }

    private string _summary = "Scans for registry entries that point to files or programs that no longer exist. " +
                              "Every fix is backed up to a .reg file first; double-click a backup to restore it.";
    public string Summary { get => _summary; private set => Set(ref _summary, value); }

    /// <summary>Saves every area Kodiak Cleaner can change into one .reg file, same as Settings > Registry.</summary>
    private async Task BackUpNowAsync()
    {
        IsBusy = true;
        Summary = "Backing up the registry…";
        try
        {
            var path = await Task.Run(() => RegistryBackup.Save(RegistryBackup.AreasKodiakCleanerChanges(), "Full"));
            Summary = $"Registry backed up to {Path.GetFileName(path)}. Double-click it in Backups\\Registry to restore.";
            ActivityLog.Write("Backups", $"Registry backed up to {path}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Summary = "The backup couldn't be saved.";
            Dialogs.Error($"The backup couldn't be saved: {e.Message}");
        }
        finally { IsBusy = false; }
    }

    private async Task ScanAsync()
    {
        IsBusy = true;
        ShowIssues(Array.Empty<RegistryIssue>());
        Summary = "Scanning…";
        try
        {
            var cats = ChosenCategories();
            var found = await Task.Run(() => RegistryScanner.Scan(cats));
            ShowIssues(found);
            Summary = found.Count == 0 ? "No issues found." : $"Found {found.Count} issue(s). Review them, then click Fix Selected.";
        }
        finally { IsBusy = false; }
    }

    private async Task FixAsync(List<IssueItem> selected)
    {
        if (selected.Count == 0) return;
        bool backup = true;
        if (Settings.Current.PromptRegistryBackup)
        {
            var answer = Dialogs.YesNoCancel(
                $"Back up the {selected.Count} registry entr{(selected.Count == 1 ? "y" : "ies")} before removing {(selected.Count == 1 ? "it" : "them")}?\n\n" +
                "Yes is recommended: a backup lets you undo the fix by double-clicking the .reg file.");
            if (answer is null) return;
            backup = answer.Value;
        }
        else if (!Dialogs.Confirm($"Remove {selected.Count} registry entr{(selected.Count == 1 ? "y" : "ies")}? A backup is saved first."))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var (backupPath, fixedCount, errors) = await Task.Run(() => RegistryScanner.Fix(selected.Select(i => i.Issue).ToList(), backup));
            Summary = $"Fixed {fixedCount} issue(s)." +
                      (backupPath is null ? " No backup was made." : $" Backup saved to {backupPath}.") +
                      (errors.Count > 0 ? $" {errors.Count} could not be changed (try Restart as administrator)." : "");
            ActivityLog.Write("Registry", Summary);
            if (errors.Count > 0) Dialogs.Error(string.Join(Environment.NewLine, errors.Take(15)));
            // Re-scan so the list shows what is actually left.
            var remaining = await Task.Run(() => RegistryScanner.Scan(ChosenCategories()));
            ShowIssues(remaining);
        }
        finally { IsBusy = false; }
    }
}
