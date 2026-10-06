using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using KodiakCleaner.Core.Cleaning;

namespace KodiakCleaner.App;

/// <summary>One row of the schedule list.</summary>
public sealed class ScheduleRow
{
    public ScheduleRow(CleaningSchedule s) => Schedule = s;
    public CleaningSchedule Schedule { get; }
    public string Name => Schedule.Name;
    public string When => Schedule.Describe() + (Schedule.AsAdmin ? " (as administrator)" : "");
    public int Items => Schedule.Targets.Count;
    public string NextRun => Schedule.NextRun(DateTime.Now)?.ToString("ddd d MMM, h:mm tt") ?? "";
    public string LastRun => Schedule.LastRunUtc is { } last ? $"{last.ToLocalTime():d MMM, h:mm tt}: {Schedule.LastResult}" : "Not yet";
}

public partial class ScheduleWindow : Window
{
    private readonly IReadOnlyList<CleanTarget> _catalog;

    /// <param name="catalog">The Cleaner's items.</param>
    public ScheduleWindow(IReadOnlyList<CleanTarget> catalog)
    {
        _catalog = catalog;
        InitializeComponent();
        Owner = Application.Current.MainWindow;
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
        Load();
    }

    private void Load(CleaningSchedule? select = null)
    {
        var rows = Settings.Current.Schedules
            .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(s => new ScheduleRow(s)).ToList();
        Grid.ItemsSource = rows;
        Grid.SelectedItem = rows.FirstOrDefault(r => r.Schedule == select) ?? rows.FirstOrDefault();
        Empty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private CleaningSchedule? Selected => (Grid.SelectedItem as ScheduleRow)?.Schedule;

    private void New_Click(object sender, RoutedEventArgs e)
    {
        if (ScheduleEditWindow.Edit(this, _catalog, null) is { } made) Load(made);
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is { } s && ScheduleEditWindow.Edit(this, _catalog, s) is { } saved) Load(saved);
    }

    private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Edit_Click(sender, e);

    private async void RunNow_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } s) return;
        if (!Dialogs.Confirm($"Run '{s.Name}' now? It cleans its {TargetRun.Plural(s.Targets.Count, "item")} without asking again.", "Run Now")) return;
        IsEnabled = false;
        Cursor = Cursors.Wait;
        try
        {
            var result = await Task.Run(() => ScheduledCleaning.Run(s.Id));
            Load(s);
            Dialogs.Info($"'{s.Name}' finished: {result}.", "Run Now");
        }
        finally
        {
            IsEnabled = true;
            Cursor = null;
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } s) return;
        if (!Dialogs.Confirm($"Delete the schedule '{s.Name}'? Its Windows task is deleted too. Nothing that was cleaned is affected.", "Delete Schedule"))
            return;
        try { ScheduledCleaning.Unregister(s); }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception) { }
        Settings.Current.Schedules.Remove(s);
        Settings.Current.Save();
        Load();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
