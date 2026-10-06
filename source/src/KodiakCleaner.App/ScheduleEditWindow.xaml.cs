using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using KodiakCleaner.App.ViewModels;
using KodiakCleaner.Core.Cleaning;

namespace KodiakCleaner.App;

public sealed class ScheduleItem : ObservableObject
{
    private readonly Action _changed;
    public ScheduleItem(CleanTarget target, bool on, Action changed) { Target = target; _isChecked = on; _changed = changed; }
    public CleanTarget Target { get; }
    public string Name => Target.Name + (Target.RequiresAdmin ? " (Needs Administrator)" : "");
    public string Description => Target.Description;
    private bool _isChecked;
    public bool IsChecked { get => _isChecked; set { if (Set(ref _isChecked, value)) _changed(); } }
}

/// <summary>A Cleaner group in the schedule editor; its heading box ticks the whole group.</summary>
public sealed class ScheduleGroup : ObservableObject
{
    public ScheduleGroup(string name, IEnumerable<CleanTarget> targets, ISet<string> on)
    {
        Name = name;
        Items = targets.Select(t => new ScheduleItem(t, on.Contains(t.Id), () => OnPropertyChanged(nameof(AllChecked)))).ToList();
    }
    public string Name { get; }
    public List<ScheduleItem> Items { get; }
    public bool? AllChecked
    {
        get => Items.All(i => i.IsChecked) ? true : Items.Any(i => i.IsChecked) ? null : false;
        set { var on = value != false; foreach (var i in Items) i.IsChecked = on; }
    }
}

public partial class ScheduleEditWindow : Window
{
    private readonly CleaningSchedule? _original;
    private readonly List<ScheduleGroup> _groups;
    private CleaningSchedule? _saved;

    private ScheduleEditWindow(IReadOnlyList<CleanTarget> catalog, CleaningSchedule? original)
    {
        _original = original;
        InitializeComponent();
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
        var s = original ?? new CleaningSchedule { Days = { DayOfWeek.Sunday } };
        Heading.Text = original is null ? "New Schedule" : "Edit Schedule";
        NameBox.Text = s.Name;

        foreach (var day in CleaningSchedule.Week)
            DayButtons.Children.Add(new ToggleButton
            {
                Content = day.ToString()[..3], Tag = day, IsChecked = s.Days.Contains(day),
                Style = (Style)FindResource("SegmentToggle"), ToolTip = day.ToString(),
            });
        for (int d = 1; d <= 28; d++) DayOfMonthBox.Items.Add(new ComboBoxItem { Content = CleaningSchedule.DayOfMonthText(d), Tag = d });
        DayOfMonthBox.Items.Add(new ComboBoxItem { Content = CleaningSchedule.DayOfMonthText(CleaningSchedule.LastDay), Tag = CleaningSchedule.LastDay });
        DayOfMonthBox.SelectedItem = DayOfMonthBox.Items.Cast<ComboBoxItem>().First(i => (int)i.Tag == s.DayOfMonth);
        for (var t = TimeSpan.Zero; t < TimeSpan.FromDays(1); t += TimeSpan.FromMinutes(30))
            TimeBox.Items.Add(new ComboBoxItem { Content = CleaningSchedule.TimeText(t), Tag = t });
        TimeBox.SelectedItem = TimeBox.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (TimeSpan)i.Tag == s.Time) ?? TimeBox.Items[18];
        (s.Repeat == ScheduleRepeat.Monthly ? MonthlyBox : WeeklyBox).IsChecked = true;

        AdminSwitch.IsEnabled = ScheduledCleaning.CanRunAsAdmin;
        AdminSwitch.IsChecked = s.AsAdmin && ScheduledCleaning.CanRunAsAdmin;
        AdminCaption.Text = ScheduledCleaning.CanRunAsAdmin
            ? "Also cleans the items that need administrator rights. Off, those items are skipped."
            : Integration.IsInstalled
                ? "Items that need administrator rights are skipped. To include them, open Kodiak Cleaner as administrator first."
                : "Items that need administrator rights are skipped. Only the installed version can run schedules as administrator.";

        var on = s.Targets.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _groups = ScheduledCleaning.Schedulable(CleanCatalog.Order(catalog)).GroupBy(t => t.Group).Select(g => new ScheduleGroup(g.Key, g, on)).ToList();
        Groups.ItemsSource = _groups;
    }

    /// <summary>Edits <paramref name="original"/> (or makes a new schedule when null). Returns the saved schedule, or null when cancelled.</summary>
    public static CleaningSchedule? Edit(Window owner, IReadOnlyList<CleanTarget> catalog, CleaningSchedule? original)
    {
        var w = new ScheduleEditWindow(catalog, original) { Owner = owner };
        w.ShowDialog();
        return w._saved;
    }

    private IEnumerable<ScheduleItem> AllItems => _groups.SelectMany(g => g.Items);

    private void Repeat_Checked(object sender, RoutedEventArgs e)
    {
        if (WeeklyPanel is null) return;
        var weekly = WeeklyBox.IsChecked == true;
        WeeklyPanel.Visibility = weekly ? Visibility.Visible : Visibility.Collapsed;
        MonthlyPanel.Visibility = weekly ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Pick(Func<CleanTarget, bool> rule) { foreach (var i in AllItems) i.IsChecked = rule(i.Target); }
    private void Basic_Click(object sender, RoutedEventArgs e) => Pick(t => t.EnabledByDefault && !t.Advanced);
    private void Browsers_Click(object sender, RoutedEventArgs e) => Pick(t => !t.Advanced && t.Group != "Windows");
    private void Cleaner_Click(object sender, RoutedEventArgs e)
    {
        var ticked = Settings.Current.CheckedTargets?.ToHashSet() ?? new HashSet<string>();
        Pick(t => ticked.Contains(t.Id));
    }
    private void Clear_Click(object sender, RoutedEventArgs e) => Pick(_ => false);

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var s = new CleaningSchedule
        {
            Id = _original?.Id ?? new CleaningSchedule().Id,
            Name = NameBox.Text.Trim(),
            Targets = AllItems.Where(i => i.IsChecked).Select(i => i.Target.Id).ToList(),
            Repeat = MonthlyBox.IsChecked == true ? ScheduleRepeat.Monthly : ScheduleRepeat.Weekly,
            Days = DayButtons.Children.OfType<ToggleButton>().Where(b => b.IsChecked == true).Select(b => (DayOfWeek)b.Tag).ToList(),
            DayOfMonth = (int)((ComboBoxItem)DayOfMonthBox.SelectedItem).Tag,
            Time = (TimeSpan)((ComboBoxItem)TimeBox.SelectedItem).Tag,
            AsAdmin = AdminSwitch.IsChecked == true,
            LastRunUtc = _original?.LastRunUtc,
            LastResult = _original?.LastResult ?? "",
        };
        if (s.Problem() is { } problem)
        {
            Dialogs.Info(problem, "Almost There");
            return;
        }
        if (Settings.Current.Schedules.Any(x => x.Id != s.Id && x.Name.Equals(s.Name, StringComparison.CurrentCultureIgnoreCase)))
        {
            Dialogs.Info($"There is already a schedule called '{s.Name}'. Pick another name.", "Almost There");
            return;
        }
        try
        {
            ScheduledCleaning.Register(s);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            Dialogs.Error($"Windows Task Scheduler couldn't save this schedule, so nothing was changed.\n\n{ex.Message}");
            return;
        }
        var list = Settings.Current.Schedules;
        var at = _original is null ? -1 : list.IndexOf(_original);
        if (at >= 0) list[at] = s; else list.Add(s);
        Settings.Current.Save();
        _saved = s;
        Close();
    }
}
