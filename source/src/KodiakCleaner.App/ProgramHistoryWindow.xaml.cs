using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using KodiakCleaner.App.ViewModels;
using KodiakCleaner.Core.Programs;

namespace KodiakCleaner.App;

/// <summary>A Programs history record with a tick box.</summary>
public sealed class ProgramHistoryRow : ObservableObject
{
    public ProgramHistoryRow(ProgramRecord record) => Record = record;
    public ProgramRecord Record { get; }
    public string Program => Record.Program;
    public string What => Record.What;
    public string Detail => Record.Detail;
    public DateTime When => Record.When;
    public string Status => Record.RestoredOn is { } on ? $"Restored {on:d MMM}" : Record.Restorable ? "Can Be Restored" : "Record Only";

    public bool Matches(string search) =>
        string.IsNullOrWhiteSpace(search) ||
        Program.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
        Record.Publisher.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
        Detail.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
        What.Contains(search, StringComparison.CurrentCultureIgnoreCase);

    private bool _isChecked;
    public bool IsChecked { get => _isChecked; set => Set(ref _isChecked, value); }
}

public partial class ProgramHistoryWindow : Window
{
    private readonly List<ProgramHistoryRow> _rows = new();
    private readonly ICollectionView _view;
    private bool _restored;

    private ProgramHistoryWindow()
    {
        InitializeComponent();
        _view = CollectionViewSource.GetDefaultView(_rows);
        _view.Filter = o => o is ProgramHistoryRow r && r.Matches(Search.Text);
        DependencyPropertyDescriptor.FromProperty(Views.SearchBox.TextProperty, typeof(Views.SearchBox))
            .AddValueChanged(Search, (_, _) => _view.Refresh());
        Grid.ItemsSource = _view;
        Load();
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
    }

    /// <summary>Shows the history; returns true when something was restored (so the Programs list refreshes).</summary>
    public static bool Show(Window? owner)
    {
        var w = new ProgramHistoryWindow { Owner = owner ?? Application.Current.MainWindow };
        w.ShowDialog();
        return w._restored;
    }

    private void Load()
    {
        _rows.Clear();
        _rows.AddRange(ProgramHistoryStore.Current.Records.OrderByDescending(r => r.When).Select(r => new ProgramHistoryRow(r)));
        _view.Refresh();
        AllBox.IsChecked = false;
        Empty.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Subheading.Text = $"Everything Kodiak Cleaner removed or changed on the Programs page: {TargetRun.Plural(_rows.Count, "record")}. " +
                          "Deleted entries, renames and removed registry keys come back from their backups, and removed files and folders " +
                          "come back from the Recycle Bin while they are still in it. An uninstalled program is kept as a record only: " +
                          "it can't be put back from here, so install it again to get it back.";
    }

    /// <summary>Ticked rows, or the highlighted row when none are ticked.</summary>
    private List<ProgramHistoryRow> Picked()
    {
        var ticked = _rows.Where(r => r.IsChecked).ToList();
        return ticked.Count > 0 || Grid.SelectedItem is not ProgramHistoryRow s ? ticked : new List<ProgramHistoryRow> { s };
    }

    private void All_Click(object sender, RoutedEventArgs e)
    {
        var on = AllBox.IsChecked == true;
        foreach (var r in _view.Cast<ProgramHistoryRow>()) r.IsChecked = on;
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        var picked = Picked();
        if (picked.Count == 0) return;
        var restorable = picked.Where(r => r.Record.Restorable && !r.Record.IsRestored).ToList();
        if (restorable.Count == 0)
        {
            Dialogs.Info(picked.All(r => r.Record.IsRestored)
                ? "Those were already restored."
                : "Those are records only. An uninstalled program can't be put back from here; install it again to get it back.");
            return;
        }
        var what = restorable.Count == 1 ? $"'{restorable[0].Program}: {restorable[0].What}'" : $"{restorable.Count} records";
        if (!Dialogs.Confirm($"Put {what} back?" +
                             (restorable.Count < picked.Count ? $"\n\n{picked.Count - restorable.Count} of the ticked records can't be restored and are skipped." : "")))
            return;
        var problems = new List<string>();
        foreach (var r in restorable)
        {
            if (ProgramHistoryStore.Restore(r.Record) is { } problem) problems.Add($"{r.Program} ({r.What}): {problem}");
            else _restored = true;
        }
        Load();
        if (problems.Count > 0) Dialogs.Error("Some records couldn't be restored:\n\n" + string.Join("\n", problems));
        else Dialogs.Info(restorable.Count == 1 ? "Restored." : $"Restored all {restorable.Count}.", "Restore Selected");
    }

    private void Forget_Click(object sender, RoutedEventArgs e)
    {
        var picked = Picked();
        if (picked.Count == 0) return;
        var what = picked.Count == 1 ? $"this record of '{picked[0].Program}'" : $"{picked.Count} records";
        if (!Dialogs.Confirm($"Forget {what}? They leave this history and can no longer be restored from it. Their backup files stay in the Backups folder.")) return;
        ProgramHistoryStore.Forget(picked.Select(r => r.Record));
        Load();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
