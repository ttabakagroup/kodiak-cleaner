using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Data;
using System.Windows.Input;
using KodiakCleaner.Core.Startup;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App.ViewModels;

public sealed class StartupItemViewModel : ObservableObject
{
    public StartupItemViewModel(StartupItem item)
    {
        Item = item;
        Returned = StartupHistoryStore.Find(item) is { TimesReturned: > 0 } r ? r : null;
    }
    public StartupItem Item { get; }
    public string Name => Item.Name;
    public string Command => Item.Command;
    public string Location => Item.Location;
    public string Note => Item.FileMissing ? "Program not found" : Returned is not null ? "Came back after removal" : "";

    /// <summary>The history record when this entry came back after Kodiak Cleaner removed it (0.8.71), else null.</summary>
    public StartupRecord? Returned { get; }

    private string? _details;
    /// <summary>Everything about the entry, shown when the row is hovered (Ted, 0.8.63). The publisher is read from the program file once.</summary>
    public string Details => _details ??= DetailText.Lines(
        ("Name", Name), ("Command", Command), ("Starts", FileIcons.ProgramOf(Command)), ("Publisher", Publisher()),
        ("Location", Item.FullLocation), ("Note", Note), ("Came Back", StartupHistoryRow.ReturnedText(Returned)));

    private string Publisher()
    {
        try
        {
            var file = FileIcons.ProgramOf(Command);
            return File.Exists(file) ? FileVersionInfo.GetVersionInfo(file).CompanyName?.Trim() ?? "" : "";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return ""; }
    }

    private System.Windows.Media.ImageSource? _icon;
    /// <summary>The icon of the program the entry starts, loaded in the background; null shows a plain placeholder.</summary>
    public System.Windows.Media.ImageSource? Icon { get => _icon; set { if (Set(ref _icon, value)) OnPropertyChanged(nameof(NoIcon)); } }
    public bool NoIcon => _icon is null;

    public bool Matches(string search) =>
        string.IsNullOrWhiteSpace(search) ||
        Name.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
        Command.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
        Location.Contains(search, StringComparison.CurrentCultureIgnoreCase);

    /// <summary>Picked for a batch action (Enable Selected, Disable Selected, Remove).</summary>
    private bool _isChecked;
    public bool IsChecked { get => _isChecked; set => Set(ref _isChecked, value); }

    public bool Enabled
    {
        get => Item.Enabled;
        set
        {
            if (!TrySetEnabled(value))
                Elevation.OfferRestart("Changing this entry needs administrator rights.");
        }
    }

    /// <returns>false when the entry needs administrator rights to change.</returns>
    public bool TrySetEnabled(bool value)
    {
        if (value == Item.Enabled) return true;
        try
        {
            StartupManager.SetEnabled(Item, value);
            ActivityLog.Write("Startup", $"Turned {(value ? "on" : "off")} '{Item.Name}'");
            return true;
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
        finally { OnPropertyChanged(nameof(Enabled)); }
    }
}

public sealed class StartupViewModel : ObservableObject, ILoadOnFirstVisit
{
    public StartupViewModel()
    {
        View = CollectionViewSource.GetDefaultView(Items);
        View.Filter = o => o is StartupItemViewModel i && i.Matches(Filter);
        RefreshCommand = new RelayCommand(Refresh);
        DeleteCommand = new RelayCommand(Delete, () => Picked().Count > 0);
        EnableSelectedCommand = new RelayCommand(() => SetPicked(true), () => Picked().Any(i => !i.Enabled));
        EnableRowCommand = new RelayCommand(() => SetRows(true), () => Highlighted.Any(i => !i.Enabled));
        DisableRowCommand = new RelayCommand(() => SetRows(false), () => Highlighted.Any(i => i.Enabled));
        RemoveRowCommand = new RelayCommand(() => Remove(Highlighted.ToList()), () => Highlighted.Count > 0);
        DisableSelectedCommand = new RelayCommand(() => SetPicked(false), () => Picked().Any(i => i.Enabled));
        AddCommand = new RelayCommand(Add);
        HistoryCommand = new RelayCommand(() => { if (StartupHistoryWindow.Show(null)) Refresh(); });
        TaskOnCommand = new RelayCommand(() => SetTask(true), () => HighlightedTasks.Any(t => !t.Enabled));
        TaskOffCommand = new RelayCommand(() => SetTask(false), () => HighlightedTasks.Any(t => t.Enabled));
        TaskDeleteCommand = new RelayCommand(DeleteTask, () => HighlightedTasks.Count > 0);
        OpenTasksCommand = new RelayCommand(() => (System.Windows.Application.Current as App)?.ShowPage("tasks"));
    }

    // ------------------------------------------------ Scheduled tasks that start with Windows (Ted, 0.8.89)

    /// <summary>
    /// Scheduled tasks (not Windows' own) with a sign-in or startup trigger. They start programs with Windows too, but they
    /// aren't startup entries, so they get their own card below the list.
    /// </summary>
    public ObservableCollection<ScheduledTaskItem> StartupTasks { get; } = new();

    private ScheduledTaskItem? _selectedTask;
    public ScheduledTaskItem? SelectedTask { get => _selectedTask; set => Set(ref _selectedTask, value); }

    /// <summary>Task rows highlighted with Ctrl-click, Shift-click or a drag (0.8.90); the right-click menu acts on all of them.</summary>
    public IReadOnlyList<ScheduledTaskItem> HighlightedTasks { get; set; } = Array.Empty<ScheduledTaskItem>();

    private string _tasksSummary = "Reading scheduled tasks...";
    public string TasksSummary { get => _tasksSummary; private set => Set(ref _tasksSummary, value); }

    public ICommand TaskOnCommand { get; }
    public ICommand TaskOffCommand { get; }
    public ICommand TaskDeleteCommand { get; }
    public ICommand OpenTasksCommand { get; }

    private void LoadTasks()
    {
        StartupTasks.Clear();
        List<ScheduledTaskItem> all;
        try { all = ScheduledTaskTools.ReadAll(); }
        catch (Exception e) when (ScheduledTasksViewModel.IsSchedulerError(e))
        {
            TasksSummary = "Scheduled tasks couldn't be read on this PC.";
            return;
        }
        foreach (var t in all.Where(StartupHistoryStore.IsStartupTask)) StartupTasks.Add(t);
        StartupHistoryStore.RecordTasks(StartupTasks);
        TasksSummary = StartupTasks.Count == 0
            ? "No scheduled tasks start a program when you sign in or when Windows starts."
            : $"{StartupTasks.Count} scheduled task{(StartupTasks.Count == 1 ? "" : "s")} start{(StartupTasks.Count == 1 ? "s" : "")} a program when you sign in " +
              "or when Windows starts. These come from Windows Task Scheduler, not from a startup entry, so removing a program's startup " +
              "entry doesn't stop them. Turning a task off is reversible; Delete backs it up first.";
        FileIcons.LoadInto(StartupTasks.Where(i => i.NoIcon && i.HasProgram).ToList(), i => i.ProgramPath, (i, icon) => i.Icon = icon);
    }

    private void SetTask(bool enabled)
    {
        var failed = HighlightedTasks.Where(t => t.Enabled != enabled).Count(t => !t.TrySetEnabled(enabled));
        if (failed > 0)
            Elevation.OfferRestart("Changing this task needs administrator rights.");
    }

    private void DeleteTask()
    {
        var tasks = HighlightedTasks.ToList();
        if (tasks.Count == 0) return;
        var what = tasks.Count == 1 ? $"the scheduled task '{tasks[0].Name}'" : $"these {tasks.Count} scheduled tasks";
        if (!Dialogs.Warn($"Delete {what}?\n\nKodiak Cleaner backs them up first, and you can put them back from " +
                          "Tools > Scheduled Tasks > Restore. Turning them off instead is reversible.", "Delete Scheduled Task",
                          tasks.Any(t => t.UsesPassword) ? ScheduledTaskTools.PasswordCaution : null)) return;
        var problems = tasks.Select(ScheduledTaskTools.Delete).OfType<string>().ToList();
        if (problems.Count > 0) Dialogs.Error(string.Join("\n\n", problems));
        LoadTasks();
    }

    private bool _loaded;
    /// <summary>Reads the list the first time the page is shown, so starting Kodiak Cleaner stays quick.</summary>
    public void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        LoadFirstTime();
    }

    /// <summary>The first visit reads the entries in the background, so the page opens straight away (Ted, 2026-10-23).</summary>
    private async void LoadFirstTime() => Fill(await Task.Run(StartupManager.List));

    public ICommand EnableSelectedCommand { get; }
    public ICommand AddCommand { get; }
    public ICommand HistoryCommand { get; }

    private void Add()
    {
        if (AddStartupWindow.Ask() is not { } added) return;
        Refresh();
        Selected = Items.FirstOrDefault(i => string.Equals(i.Item.FilePath, added, StringComparison.OrdinalIgnoreCase));
    }
    public ICommand DisableSelectedCommand { get; }
    /// <summary>Right-click menu: act on the highlighted rows (the right-clicked row, or a Ctrl/Shift-click selection), whatever is ticked.</summary>
    public ICommand EnableRowCommand { get; }
    public ICommand DisableRowCommand { get; }
    public ICommand RemoveRowCommand { get; }

    private void SetRows(bool enabled) => SetAll(Highlighted, enabled);

    /// <summary>The highlighted rows (Ctrl-click and Shift-click pick several; Ted, 0.8.49). Set by the view.</summary>
    public IReadOnlyList<StartupItemViewModel> Highlighted { get; set; } = Array.Empty<StartupItemViewModel>();

    /// <summary>The header checkbox: true when every entry shown is ticked, false when none, null when some. It ticks only what is shown.</summary>
    public bool? AllChecked
    {
        get
        {
            var shown = View.Cast<StartupItemViewModel>().ToList();
            return shown.Count > 0 && shown.All(i => i.IsChecked) ? true : shown.Any(i => i.IsChecked) ? null : false;
        }
        set
        {
            var on = value != false;
            foreach (var i in View.Cast<StartupItemViewModel>().ToList()) i.IsChecked = on;
        }
    }

    public ICollectionView View { get; }

    private string _filter = "";
    public string Filter
    {
        get => _filter;
        set { if (Set(ref _filter, value)) { View.Refresh(); OnPropertyChanged(nameof(AllChecked)); } }
    }

    /// <summary>Ticked entries, or the highlighted rows when nothing is ticked.</summary>
    private List<StartupItemViewModel> Picked()
    {
        var ticked = Items.Where(i => i.IsChecked).ToList();
        return ticked.Count > 0 ? ticked : Highlighted.ToList();
    }

    private void SetPicked(bool enabled) => SetAll(Picked(), enabled);

    private static void SetAll(IEnumerable<StartupItemViewModel> items, bool enabled)
    {
        var failed = items.ToList().Count(i => !i.TrySetEnabled(enabled));
        if (failed > 0)
            Elevation.OfferRestart($"{failed} entr{(failed == 1 ? "y needs" : "ies need")} administrator rights to change.");
    }

    public ObservableCollection<StartupItemViewModel> Items { get; } = new();
    public ICommand RefreshCommand { get; }
    public ICommand DeleteCommand { get; }

    private StartupItemViewModel? _selected;
    public StartupItemViewModel? Selected { get => _selected; set => Set(ref _selected, value); }

    private void Refresh() => Fill(StartupManager.List());

    private void Fill(List<StartupItem> list)
    {
        Items.Clear();
        StartupHistoryStore.Record(list);
        foreach (var i in list)
        {
            var vm = new StartupItemViewModel(i);
            vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(StartupItemViewModel.IsChecked)) OnPropertyChanged(nameof(AllChecked)); };
            Items.Add(vm);
        }
        OnPropertyChanged(nameof(AllChecked));
        FileIcons.LoadInto(Items.ToList(), i => FileIcons.ProgramOf(i.Command), (i, icon) => i.Icon = icon);
        // Reading every task takes a moment, so the startup list shows first.
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(LoadTasks, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void Delete() => Remove(Picked());

    private void Remove(List<StartupItemViewModel> picked)
    {
        if (picked.Count == 0) return;
        var what = picked.Count == 1 ? $"'{picked[0].Name}'" : $"{picked.Count} entries";
        var returning = picked.Where(p => p.Returned is not null).ToList();
        if (returning.Count > 0)
        {
            // Removing these didn't stick before; turning them off does, because programs put their entry back but leave the switch alone.
            var names = string.Join("\n", returning.Select(r => $"• {r.Name} (came back {Times(r.Returned!.TimesReturned)})"));
            var choice = Dialogs.Choose(MessageKind.Question, "This Came Back Before",
                $"These came back after they were removed:\n\n{names}\n\nThe program most likely puts its entry back when it starts or updates, " +
                "so removing it again probably won't last.\n\nTo stop it for good, open the program and turn off its own startup setting " +
                "(often called \"Start with Windows\", \"Launch at startup\" or \"Open at login\"). " +
                "Or turn the entry off here: that keeps the entry but stops it starting, and programs leave that switch alone.",
                null, new[] { "Cancel", "Remove Anyway", "Turn Off Instead" }, 2, 0);
            if (choice == 0) return;
            if (choice == 2)
            {
                SetAll(returning, false);
                picked = picked.Except(returning).ToList();
                if (picked.Count == 0) return;
                what = picked.Count == 1 ? $"'{picked[0].Name}'" : $"{picked.Count} entries";
            }
        }
        if (!Dialogs.Confirm($"Remove {what} from startup?\n\nTip: turning an entry off is reversible; removing deletes it. " +
                             "Kodiak Cleaner backs it up first (registry entries to a .reg file, shortcuts to the Recycle Bin), " +
                             "and you can put it back from History.\n\nIf the program has its own \"Start with Windows\" setting, turn that off " +
                             "in the program too, or it may put the entry back the next time it starts or updates.")) return;
        int failed = 0, failedOther = 0;
        foreach (var s in picked)
        {
            try
            {
                var backup = StartupManager.Delete(s.Item);
                StartupHistoryStore.Removed(s.Item, backup);
                ActivityLog.Write("Startup", $"Removed '{s.Name}'. Backup: {backup}");
                Items.Remove(s);
            }
            catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException)
            {
                failed++;
            }
            // A shortcut the Recycle Bin wouldn't take (in use, or the deletion was cancelled) stays where it is.
            catch (Exception e) when (e is IOException or OperationCanceledException)
            {
                ActivityLog.Write("Startup", $"Couldn't remove '{s.Name}': {e.Message}");
                failedOther++;
            }
        }
        OnPropertyChanged(nameof(AllChecked));
        if (failed > 0)
            Elevation.OfferRestart($"{failed} entr{(failed == 1 ? "y needs" : "ies need")} administrator rights to remove.");
        if (failedOther > 0)
            Dialogs.Error($"{failedOther} entr{(failedOther == 1 ? "y" : "ies")} couldn't be removed. The shortcut may be in use; try again in a moment.");
    }

    private static string Times(int n) => n == 1 ? "once" : n == 2 ? "twice" : $"{n} times";
}
