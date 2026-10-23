using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using KodiakCleaner.App.ViewModels;
using KodiakCleaner.Core.Tools;

namespace KodiakCleaner.App;

/// <summary>A scheduled task backup with a tick box, and whether the task is in Task Scheduler now.</summary>
public sealed class TaskBackupRow : ObservableObject
{
    public TaskBackupRow(TaskBackup backup, ScheduledTaskItem? current)
    {
        Backup = backup;
        Current = current;
    }

    public TaskBackup Backup { get; }
    /// <summary>The task of the same path that is scheduled now, or null when it's gone.</summary>
    public ScheduledTaskItem? Current { get; }
    public string Name => Backup.Name;
    public string Folder => Backup.Folder;
    public string Program => Backup.Program;
    public DateTime SavedAt => Backup.SavedAt;
    public string Reason => Backup.Reason;
    public string Status => Current is null ? "Deleted" : "Still Scheduled";

    public bool Matches(string search) =>
        string.IsNullOrWhiteSpace(search) ||
        Name.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
        Folder.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
        Program.Contains(search, StringComparison.CurrentCultureIgnoreCase);

    private bool _isChecked;
    public bool IsChecked { get => _isChecked; set => Set(ref _isChecked, value); }
}

public partial class ScheduledTaskRestoreWindow : Window
{
    private readonly List<TaskBackupRow> _rows = new();
    private readonly ICollectionView _view;
    private readonly Dictionary<string, ScheduledTaskItem> _current;
    private bool _restored;

    private ScheduledTaskRestoreWindow(IEnumerable<ScheduledTaskItem> current)
    {
        _current = current.GroupBy(t => t.TaskPath, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        InitializeComponent();
        _view = CollectionViewSource.GetDefaultView(_rows);
        _view.Filter = o => o is TaskBackupRow r && r.Matches(Search.Text);
        DependencyPropertyDescriptor.FromProperty(Views.SearchBox.TextProperty, typeof(Views.SearchBox))
            .AddValueChanged(Search, (_, _) => _view.Refresh());
        Grid.ItemsSource = _view;
        Load();
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
    }

    /// <summary>Shows the backups; returns true when a task was restored (so the Scheduled Tasks list refreshes).</summary>
    public static bool Show(Window? owner, IEnumerable<ScheduledTaskItem> current)
    {
        var w = new ScheduledTaskRestoreWindow(current) { Owner = owner ?? Application.Current.MainWindow };
        w.ShowDialog();
        return w._restored;
    }

    private void Load()
    {
        _rows.Clear();
        _rows.AddRange(ScheduledTaskTools.Backups.List()
            .Select(b => new TaskBackupRow(b, _current.GetValueOrDefault(b.TaskPath))));
        _view.Refresh();
        AllBox.IsChecked = false;
        Empty.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Subheading.Text = $"Every scheduled task saved in Backups\\Scheduled Tasks: {TargetRun.Plural(_rows.Count, "backup")}, A to Z. " +
                          "Tasks are saved there before Kodiak Cleaner deletes them and whenever you choose Back Up. Restore puts a task back in its old folder; " +
                          "if a task of that name is scheduled now, it is backed up and then replaced. The files are the same XML Task Scheduler's Import Task reads.";
    }

    /// <summary>Ticked rows, or the highlighted row when none are ticked.</summary>
    private List<TaskBackupRow> Picked()
    {
        var ticked = _rows.Where(r => r.IsChecked).ToList();
        return ticked.Count > 0 || Grid.SelectedItem is not TaskBackupRow s ? ticked : new List<TaskBackupRow> { s };
    }

    private void All_Click(object sender, RoutedEventArgs e)
    {
        var on = AllBox.IsChecked == true;
        foreach (var r in _view.Cast<TaskBackupRow>()) r.IsChecked = on;
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        var picked = Picked();
        if (picked.Count == 0) return;
        var twice = picked.GroupBy(r => r.Backup.TaskPath, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (twice is not null)
        {
            Dialogs.Info($"Two backups of '{twice.First().Name}' are ticked. Tick just the one to put back.", "Restore Selected");
            return;
        }
        var what = picked.Count == 1 ? $"the task '{picked[0].Name}'" : $"{picked.Count} tasks";
        var scheduled = picked.Count(r => r.Current is not null);
        if (!Dialogs.Confirm($"Restore {what} from the backup?" +
                             (scheduled == 0 ? "" : $"\n\n{(scheduled == 1 && picked.Count == 1 ? "It is" : $"{scheduled} of them are")} still scheduled. " +
                                                     "The task there now is backed up first, then replaced with the backup."), "Restore Selected"))
            return;
        var problems = new List<string>();
        foreach (var r in picked)
        {
            if (ScheduledTaskTools.Restore(r.Backup, r.Current) is { } problem) problems.Add($"{r.Name}: {problem}");
            else _restored = true;
        }
        if (_restored) Close();
        if (problems.Count > 0) Dialogs.Error($"{problems.Count} of {picked.Count} task(s) weren't restored:\n\n" + string.Join("\n", problems));
        else Dialogs.Info(picked.Count == 1 ? "Restored." : $"Restored all {picked.Count}.", "Restore Selected");
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e) => Elevation.OpenFolder(ScheduledTaskTools.Backups.Folder);

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
