using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using KodiakCleaner.Core.Cleaning;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App.ViewModels;

public sealed class CleanTargetItem : ObservableObject
{
    public CleanTargetItem(CleanTarget target, bool isChecked)
    {
        Run = new TargetRun(target);
        _isChecked = isChecked;
    }

    public TargetRun Run { get; }
    public CleanTarget Target => Run.Target;
    public string Group => Target.Group;
    public string Name => Target.Name;
    public string Description => Target.Description + (Target.RequiresAdmin ? " (Needs administrator.)" : "");
    /// <summary>The item's caution, shown under its description; empty for most items.</summary>
    public string Warning => Target.Warning.Length > 0 ? "Caution: " + Target.Warning : "";
    public bool HasWarning => Target.Warning.Length > 0;

    private bool _isChecked;
    public bool IsChecked { get => _isChecked; set => Set(ref _isChecked, value); }

    private string _status = "";
    public string Status { get => _status; set => Set(ref _status, value); }

    private string _size = "";
    public string Size { get => _size; set => Set(ref _size, value); }

    /// <summary>The Last Cleaned column: "Today", "3 days ago", "Never".</summary>
    public string LastCleaned => CleanerHistory.Describe(Settings.Current.History[Target.Id], DateTime.Now);
    public void RefreshLastCleaned() => OnPropertyChanged(nameof(LastCleaned));

    /// <summary>What the latest analysis found, for the Largest Found order (0 until analyzed).</summary>
    public long Found { get; set; }

    private ImageSource? _groupIcon;
    /// <summary>The icon of the program this item's group belongs to (Chrome, Edge, ...), read from the installed program.</summary>
    public ImageSource? GroupIcon { get => _groupIcon; set { if (Set(ref _groupIcon, value)) OnPropertyChanged(nameof(NoGroupIcon)); } }
    public bool NoGroupIcon => _groupIcon is null;

    /// <summary>Placeholder for groups whose program wasn't found: a globe for browsers, a broom for Advanced Cleaning.</summary>
    public string GroupGlyph => Target.Advanced ? "\uE8B7" : "\uE774";

    public bool Matches(string search) =>
        string.IsNullOrWhiteSpace(search) ||
        Name.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
        Group.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
        Target.Description.Contains(search, StringComparison.CurrentCultureIgnoreCase);
}

public sealed class CleanerViewModel : ObservableObject
{
    private CancellationTokenSource? _cts;

    public CleanerViewModel()
    {
        View = new ListCollectionView(Targets);
        View.GroupDescriptions.Add(new PropertyGroupDescription(nameof(CleanTargetItem.Group)));
        View.Filter = o => o is CleanTargetItem t && (ShowAdvanced || !t.Target.Advanced) && t.Matches(Filter);
        ReloadTargets();
        ToggleAdvancedCommand = new RelayCommand(() => { if (ShowAdvanced || ConfirmAdvanced()) ShowAdvanced = !ShowAdvanced; });
        AnalyzeCommand = new RelayCommand(AnalyzeAsync, () => !IsBusy);
        CleanCommand = new RelayCommand(CleanAsync, () => !IsBusy);
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy);
        ResetCommand = new RelayCommand(ResetToDefault, () => !IsBusy);
        ScheduleCommand = new RelayCommand(() =>
        {
            new ScheduleWindow(Targets.Select(t => t.Target).ToList()).ShowDialog();
            // Run Now may have cleaned some items.
            foreach (var t in Targets) t.RefreshLastCleaned();
            ApplyOrder();
        }, () => !IsBusy);
        Sorts = new[]
        {
            new SortOption(this, CleanSort.Default, "Default"), new SortOption(this, CleanSort.AToZ, "A to Z"), new SortOption(this, CleanSort.LastCleaned, "Last Cleaned"),
            new SortOption(this, CleanSort.MostSpaceFreed, "Most Space Freed"), new SortOption(this, CleanSort.LargestFound, "Largest Found"),
        };
    }

    public ICommand ScheduleCommand { get; }

    /// <summary>The Sort By choices above the table (Ted, 0.8.21).</summary>
    public IReadOnlyList<SortOption> Sorts { get; }

    public CleanSort Sort
    {
        get => Settings.Current.CleanerSort;
        set
        {
            if (Settings.Current.CleanerSort == value) return;
            Settings.Current.CleanerSort = value;
            Settings.Current.Save();
            OnPropertyChanged();
            foreach (var o in Sorts) o.Changed();
            ApplyOrder();
        }
    }

    /// <summary>Clicking the selected Sort By option again re-sorts (Default then brings newly ticked items up).</summary>
    public void Resort(CleanSort sort)
    {
        if (Sort == sort) ApplyOrder();
        else Sort = sort;
    }

    /// <summary>Puts the list in the chosen order: groups and their items by the chosen value, A to Z for ties.</summary>
    private void ApplyOrder()
    {
        var history = Settings.Current.History;
        var byTarget = Targets.ToDictionary(t => t.Target);
        Func<CleanTarget, long> value = Sort switch
        {
            CleanSort.LastCleaned => t => history[t.Id]?.LastCleanedUtc.Ticks ?? 0,
            CleanSort.MostSpaceFreed => t => history[t.Id]?.TotalFreed ?? 0,
            CleanSort.LargestFound => t => byTarget[t].Found,
            _ => _ => 0,
        };
        // Default puts ticked items first in each group. It is applied when the list loads and when Default is clicked,
        // not on every tick, so rows don't jump while you're ticking them.
        var ordered = CleanCatalog.Order(Targets.Select(t => t.Target), value, windowsFirst: Sort != CleanSort.AToZ,
            ticked: Sort == CleanSort.Default ? t => byTarget[t].IsChecked : null);
        for (int i = 0; i < ordered.Count; i++)
        {
            var at = Targets.IndexOf(byTarget[ordered[i]]);
            if (at != i) Targets.Move(at, i);
        }
        View.Refresh();
    }

    public ObservableCollection<CleanTargetItem> Targets { get; } = new();
    /// <summary>What the table shows: grouped, searched, and without Advanced Cleaning's items until it's turned on.</summary>
    public ListCollectionView View { get; }
    public ICommand ToggleAdvancedCommand { get; }

    private string _filter = "";
    public string Filter
    {
        get => _filter;
        set { if (Set(ref _filter, value)) { View.Refresh(); OnPropertyChanged(nameof(AllChecked)); } }
    }

    private bool _showAdvanced;
    /// <summary>
    /// Advanced Cleaning: shows the deeper temporary-file items. Turning it off unticks them, so nothing hidden is ever cleaned.
    /// </summary>
    public bool ShowAdvanced
    {
        get => _showAdvanced;
        set
        {
            if (!Set(ref _showAdvanced, value)) return;
            if (!value)
            {
                _bulk = true;
                try { foreach (var t in Targets.Where(t => t.Target.Advanced)) t.IsChecked = false; }
                finally { _bulk = false; }
                SaveChoices();
            }
            AdvancedHint = "";
            View.Refresh();
            OnPropertyChanged(nameof(AllChecked));
            OnPropertyChanged(nameof(AdvancedButtonText));
        }
    }

    /// <summary>The warning shown each time Advanced Cleaning is opened (Ted, 0.8.17).</summary>
    private static bool ConfirmAdvanced() => Dialogs.Warn(
        "Advanced Cleaning lists deeper items: temp folders Windows' own services use, old Windows logs, diagnostic reports, driver caches " +
        "and Windows tasks. Nothing is ticked until you choose it.\n\nOpen Advanced Cleaning?",
        "Advanced Cleaning",
        "Cleaning these can harm your computer or make a problem harder to troubleshoot, for example if a program or update is using them, " +
        "or if you need a log later. Be mindful: read each description, tick only what you understand, and close other programs first.");

    public string AdvancedButtonText => ShowAdvanced ? "Hide Advanced Cleaning" : "Advanced Cleaning";

    private string _advancedHint = "";
    /// <summary>Set after an analysis only when Advanced Cleaning would make a real difference; empty otherwise.</summary>
    public string AdvancedHint { get => _advancedHint; private set { if (Set(ref _advancedHint, value)) OnPropertyChanged(nameof(HasAdvancedHint)); } }
    public bool HasAdvancedHint => AdvancedHint.Length > 0;

    private IEnumerable<CleanTargetItem> Visible => View.Cast<CleanTargetItem>();
    public ICommand AnalyzeCommand { get; }
    public ICommand CleanCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ResetCommand { get; }

    /// <summary>
    /// Reset to Default (Ted, 0.8.20): ticks only the basic items Kodiak Cleaner starts with. Nothing from Advanced Cleaning
    /// and nothing with a caution is ever ticked by it.
    /// </summary>
    private void ResetToDefault()
    {
        if (!Dialogs.Confirm("Tick only the basic items Kodiak Cleaner starts with, and untick everything else, including Advanced Cleaning?",
                "Reset to Default")) return;
        _bulk = true;
        try { foreach (var t in Targets) t.IsChecked = t.Target.EnabledByDefault && !t.Target.Advanced && !t.HasWarning; }
        finally { _bulk = false; }
        SaveChoices();
        foreach (var t in Targets) t.Status = t.Size = "";
        AdvancedHint = "";
        OnPropertyChanged(nameof(AllChecked));
        TotalText = "Ready";
        TotalLabel = "to analyze";
        Summary = "Back to the basic items. Click Analyze to see how much space you would get back.";
        if (Sort == CleanSort.Default) ApplyOrder();
    }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }

    private string _summary = "Choose what to clean, then click Analyze to see how much space you would get back.";
    public string Summary { get => _summary; private set => Set(ref _summary, value); }

    private string _totalText = "Ready";
    /// <summary>The big number at the top of the page.</summary>
    public string TotalText { get => _totalText; private set => Set(ref _totalText, value); }

    private string _totalLabel = "to analyze";
    public string TotalLabel { get => _totalLabel; private set => Set(ref _totalLabel, value); }

    /// <summary>The header checkbox: true when every item shown is ticked, null when some are. It ticks only what is shown.</summary>
    public bool? AllChecked
    {
        get
        {
            var shown = Visible.ToList();
            return shown.Count > 0 && shown.All(t => t.IsChecked) ? true : shown.Any(t => t.IsChecked) ? null : false;
        }
        set
        {
            var on = value != false;
            _bulk = true;
            // Items with a caution are never ticked in bulk: each one is ticked on its own, after its warning.
            try { foreach (var t in Visible.ToList()) t.IsChecked = on && !t.HasWarning; }
            finally { _bulk = false; }
            SaveChoices();
        }
    }

    private bool _bulk; // Select All saves once at the end, not once per item

    /// <summary>Rebuilds the list (e.g. after an age setting changes), keeping the ticked boxes.</summary>
    public void ReloadTargets()
    {
        var checkedIds = Targets.Count > 0
            ? Targets.Where(t => t.IsChecked).Select(t => t.Target.Id).ToHashSet()
            : Settings.Current.CheckedTargets?.ToHashSet();
        Targets.Clear();
        foreach (var t in TargetRun.Catalog())
        {
            var item = new CleanTargetItem(t, checkedIds?.Contains(t.Id) ?? t.EnabledByDefault);
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(CleanTargetItem.IsChecked)) return;
                if (!_bulk && item.IsChecked && item.HasWarning &&
                    !Dialogs.Warn($"{item.Target.Description}\n\nTick it anyway?", item.Name, item.Target.Warning))
                {
                    item.IsChecked = false;
                    return;
                }
                OnPropertyChanged(nameof(AllChecked));
                // Saved right away so Clean When Windows Starts, the jump list and the tray use what's ticked now.
                if (!_bulk) SaveChoices();
            };
            Targets.Add(item);
        }
        // Advanced Cleaning opens by itself when something in it was left ticked last time, so nothing ticked is hidden.
        if (Targets.Any(t => t.Target.Advanced && t.IsChecked)) ShowAdvanced = true;
        LoadGroupIcons();
        ApplyOrder();
        OnPropertyChanged(nameof(AllChecked));
        TotalText = "Ready";
        TotalLabel = "to analyze";
    }

    /// <summary>Each group's program icon (Chrome's own icon beside Google Chrome, and so on), read in the background.</summary>
    private void LoadGroupIcons()
    {
        var groups = Targets.Where(t => !t.Target.Advanced).GroupBy(t => t.Group).Select(g => g.ToList()).ToList();
        FileIcons.LoadInto(groups, g => FileIcons.ProgramOf(g[0].Target), (g, icon) => { foreach (var t in g) t.GroupIcon = icon; });
    }

    private void SaveChoices()
    {
        Settings.Current.CheckedTargets = Targets.Where(t => t.IsChecked).Select(t => t.Target.Id).ToList();
        Settings.Current.Save();
    }

    private async Task AnalyzeAsync()
    {
        SaveChoices();
        IsBusy = true;
        using var task = BackgroundTasks.Start(this, "Analyzing Files");
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        long total = 0;
        try
        {
            var targets = Targets.ToList(); // a settings change can rebuild the list mid-analysis
            foreach (var item in targets) { item.Status = item.Size = ""; item.Found = 0; }
            var chosen = targets.Where(t => t.IsChecked).ToList();
            int done = 0;
            foreach (var item in chosen)
            {
                task.Step(done++, chosen.Count);
                item.Status = "Analyzing…";
                Summary = $"Analyzing {item.Group}: {item.Name}…";
                await Task.Run(() => item.Run.Analyze(ct), ct);
                if (item.Run.SkippedBecause is { } why)
                {
                    item.Status = why;
                    continue;
                }
                item.Size = item.Run.Plan is { Bytes: 0 } ? "" : ByteSize.Format(item.Run.Bytes); // a task whose size Windows doesn't say
                item.Found = item.Run.Bytes;
                item.Status = item.Run.AnalyzedStatus;
                total += item.Run.Bytes;
            }
            Summary = $"Analysis complete. Click Run Cleaner to remove {ByteSize.Format(total)}.";
            TotalText = ByteSize.Format(total);
            TotalLabel = "can be freed";
            if (Sort == CleanSort.LargestFound) ApplyOrder();
            if (!ShowAdvanced) await CheckAdvancedAsync(targets, ct);
        }
        catch (OperationCanceledException)
        {
            Summary = "Analysis cancelled.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Quietly measures Advanced Cleaning's items (nothing is deleted) and suggests it only when it would free a lot,
    /// or a fair amount while the Windows drive is nearly full. Otherwise nothing is said.
    /// </summary>
    private async Task CheckAdvancedAsync(List<CleanTargetItem> targets, CancellationToken ct)
    {
        AdvancedHint = "";
        // Only file items count: Windows tasks and items with a caution are never suggested this way.
        var advanced = targets.Where(t => t.Target.Advanced && t.Target.Kind == CleanTargetKind.Files && !t.HasWarning &&
                                          (!t.Target.RequiresAdmin || Elevation.IsAdministrator)).ToList();
        if (advanced.Count == 0) return;
        Summary += " Checking whether Advanced Cleaning would help…";
        long found = 0;
        foreach (var item in advanced)
        {
            await Task.Run(() => item.Run.Analyze(ct), ct);
            if (item.Run.SkippedBecause is not null) continue;
            found += item.Run.Bytes;
            item.Size = ByteSize.Format(item.Run.Bytes); // shown if Advanced Cleaning is opened
            item.Status = item.Run.AnalyzedStatus;
        }
        Summary = Summary.Replace(" Checking whether Advanced Cleaning would help…", "");
        if (AdvancedAdvice.ShouldRecommend(found, SystemDriveFree()))
            AdvancedHint = $"Advanced Cleaning found {ByteSize.Format(found)} more in other temporary files, old logs and caches. " +
                           "Open it to review those items; none are ticked until you choose them.";
    }

    private static (long Free, long Total)? SystemDriveFree()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\");
            return (drive.AvailableFreeSpace, drive.TotalSize);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
    }

    private async Task CleanAsync()
    {
        var selected = Targets.Where(t => t.IsChecked).ToList();
        if (selected.Count == 0) return;
        if (selected.Any(t => !t.Run.Analyzed && t.Run.SkippedBecause is null)) await AnalyzeAsync();

        var toClean = selected.Where(t => t.Run.Analyzed).ToList();
        long planned = toClean.Sum(t => t.Run.Bytes);
        if (Settings.Current.ConfirmBeforeCleaning &&
            !Dialogs.Confirm($"This permanently deletes {ByteSize.Format(planned)} of files from {toClean.Count} selected item(s). Continue?"))
            return;

        IsBusy = true;
        using var task = BackgroundTasks.Start(this, "Cleaning Files");
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        long freed = 0;
        int failed = 0;
        try
        {
            int done = 0;
            foreach (var item in toClean)
            {
                task.Step(done++, toClean.Count);
                item.Status = "Cleaning…";
                Summary = $"Cleaning {item.Group}: {item.Name}…";
                var (f, fail, status) = await Task.Run(() => item.Run.Clean(ct), ct);
                freed += f;
                failed += fail;
                if (item.Run.Ran)
                {
                    Settings.Current.History.Record(item.Target.Id, DateTime.UtcNow, f);
                    item.RefreshLastCleaned();
                }
                item.Size = item.Target.Kind == CleanTargetKind.Task && f == 0 ? "" : ByteSize.Format(f);
                item.Status = status;
            }
            TotalText = ByteSize.Format(freed);
            TotalLabel = "freed";
            Summary = $"Cleaning complete. {ByteSize.Format(freed)} freed." +
                      (failed > 0 ? $" {failed:N0} items were in use or need administrator rights and were left alone." : "");
            ActivityLog.Write("Cleaner", $"Cleaned {toClean.Count} item(s), {ByteSize.Format(freed)} freed" +
                (failed > 0 ? $"; {failed:N0} in use or needing administrator rights were left alone." : "."));
        }
        catch (OperationCanceledException)
        {
            Summary = $"Cleaning cancelled after freeing {ByteSize.Format(freed)}.";
            ActivityLog.Write("Cleaner", Summary);
        }
        finally
        {
            IsBusy = false;
            Settings.Current.Save();
            if (Sort is CleanSort.LastCleaned or CleanSort.MostSpaceFreed) ApplyOrder();
        }
    }
}

/// <summary>One Sort By segment.</summary>
public sealed class SortOption : ObservableObject
{
    private readonly CleanerViewModel _owner;
    public SortOption(CleanerViewModel owner, CleanSort sort, string label)
    {
        _owner = owner;
        Sort = sort;
        Label = label;
        PickCommand = new RelayCommand(() => _owner.Resort(Sort));
    }
    /// <summary>Runs on every click, also on the option already selected.</summary>
    public System.Windows.Input.ICommand PickCommand { get; }
    public CleanSort Sort { get; }
    public string Label { get; }
    public bool IsSelected { get => _owner.Sort == Sort; set { if (value) _owner.Sort = Sort; } }
    public void Changed() => OnPropertyChanged(nameof(IsSelected));
}
