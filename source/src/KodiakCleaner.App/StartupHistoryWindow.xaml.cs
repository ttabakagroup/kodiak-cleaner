using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using KodiakCleaner.App.ViewModels;
using KodiakCleaner.Core.Startup;

namespace KodiakCleaner.App;

/// <summary>A history record with a tick box.</summary>
public sealed class StartupHistoryRow : ObservableObject
{
    public StartupHistoryRow(StartupRecord record) => Record = record;
    public StartupRecord Record { get; }
    public string Name => Record.Name;
    public string Command => Record.Command;
    public DateTime FirstSeen => Record.FirstSeen;
    public DateTime? RemovedOn => Record.RemovedOn;
    public string Location => new StartupItem { Name = "", Command = "", Source = Record.Source }.Location;
    public string Status => !Record.IsRemoved
        ? (Record.TimesReturned > 0 ? "Came Back" : "In Startup") + (Record.Enabled ? "" : " (Off)")
        : Record.RemovedByKodiakCleaner ? "Removed by Kodiak Cleaner" : "Removed Elsewhere";
    /// <summary>When and why it came back after Kodiak Cleaner removed it; shown when the Status is hovered.</summary>
    public string? WhyBack => ReturnedText(Record);

    /// <summary>"Came back twice after being removed, last on ... " and the likely reason; null when it never came back.</summary>
    public static string? ReturnedText(StartupRecord? r)
    {
        if (r is null || r.TimesReturned == 0) return null;
        var times = r.TimesReturned == 1 ? "once" : r.TimesReturned == 2 ? "twice" : $"{r.TimesReturned} times";
        var when = r.LastReturned is { } t ? $", last on {t:g}" : "";
        return $"Came back {times} after being removed{when}. {r.ReturnedBy}".TrimEnd();
    }

    public bool Matches(string search) =>
        string.IsNullOrWhiteSpace(search) ||
        Name.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
        Command.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
        Location.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
        Status.Contains(search, StringComparison.CurrentCultureIgnoreCase);

    private bool _isChecked;
    public bool IsChecked { get => _isChecked; set => Set(ref _isChecked, value); }
}

public partial class StartupHistoryWindow : Window
{
    private readonly List<StartupHistoryRow> _rows = new();
    private readonly ICollectionView _view;

    private StartupHistoryWindow()
    {
        InitializeComponent();
        _view = CollectionViewSource.GetDefaultView(_rows);
        _view.Filter = o => o is StartupHistoryRow r && r.Matches(Search.Text);
        DependencyPropertyDescriptor.FromProperty(Views.SearchBox.TextProperty, typeof(Views.SearchBox))
            .AddValueChanged(Search, (_, _) => _view.Refresh());
        Grid.ItemsSource = _view;
        Load();
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
    }

    /// <summary>Shows the history; returns true when something was restored (so the Startup list refreshes).</summary>
    public static bool Show(Window? owner)
    {
        var w = new StartupHistoryWindow { Owner = owner ?? Application.Current.MainWindow };
        w.ShowDialog();
        return w._restored;
    }

    private bool _restored;

    private void Load()
    {
        _rows.Clear();
        // Removed entries first (newest removal on top), then what is in startup now.
        var records = StartupHistoryStore.Current.Records
            .OrderBy(r => r.IsRemoved ? 0 : 1)
            .ThenByDescending(r => r.RemovedOn ?? DateTime.MinValue)
            .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase);
        _rows.AddRange(records.Select(r => new StartupHistoryRow(r)));
        _view.Refresh();
        AllBox.IsChecked = false;
        var removed = _rows.Count(r => r.Record.IsRemoved);
        Subheading.Text = $"Every startup entry Kodiak Cleaner has seen, and every scheduled task that starts with Windows: {_rows.Count} in all, {removed} removed. " +
                          "Entries are recorded each time the Startup page is opened or refreshed, and a removed entry or deleted task can be put back. " +
                          "Kodiak Cleaner backs up an entry before it removes one.";
    }

    /// <summary>Ticked rows, or the highlighted rows (Ctrl-click, Shift-click or drag) when none are ticked.</summary>
    private List<StartupHistoryRow> Picked()
    {
        var ticked = _rows.Where(r => r.IsChecked).ToList();
        return ticked.Count > 0 ? ticked : Grid.SelectedItems.OfType<StartupHistoryRow>().ToList();
    }

    private void All_Click(object sender, RoutedEventArgs e)
    {
        var on = AllBox.IsChecked == true;
        foreach (var r in _view.Cast<StartupHistoryRow>()) r.IsChecked = on;
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        var picked = Picked();
        if (picked.Count == 0) return;
        var restorable = picked.Where(r => StartupHistory.CanRestore(r.Record)).ToList();
        if (restorable.Count == 0)
        {
            Dialogs.Info(picked.All(r => !r.Record.IsRemoved)
                ? "Those entries are still in startup, so there is nothing to restore."
                : "Those entries can't be restored: Kodiak Cleaner has no saved copy of them.");
            return;
        }
        var what = restorable.Count == 1 ? $"'{restorable[0].Name}'" : $"{restorable.Count} entries";
        if (!Dialogs.Confirm($"Put {what} back in startup? Each comes back on or off, as it was.")) return;

        var problems = new List<string>();
        foreach (var r in restorable)
        {
            try
            {
                StartupHistoryStore.Restore(r.Record);
                _restored = true;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
            {
                problems.Add($"{r.Name}: needs administrator rights (use Restart as Administrator at the top).");
            }
            catch (Exception ex) when (ex is IOException)
            {
                problems.Add($"{r.Name}: {ex.Message}");
            }
        }
        // Record what is in startup now, so the restored entries show as In Startup again.
        StartupHistoryStore.Record(StartupManager.List());
        Load();
        if (problems.Count > 0) Dialogs.Error("Some entries couldn't be restored:\n\n" + string.Join("\n", problems));
    }

    private void Forget_Click(object sender, RoutedEventArgs e)
    {
        var picked = Picked();
        if (picked.Count == 0) return;
        var what = picked.Count == 1 ? $"'{picked[0].Name}'" : $"{picked.Count} records";
        if (!Dialogs.Confirm($"Forget {what}? They leave this history and can no longer be restored from it. Startup itself isn't changed.")) return;
        StartupHistoryStore.Forget(picked.Select(r => r.Record));
        Load();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
