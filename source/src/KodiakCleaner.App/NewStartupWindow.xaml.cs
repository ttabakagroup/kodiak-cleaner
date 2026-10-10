using System.IO;
using System.Windows;
using KodiakCleaner.App.ViewModels;
using KodiakCleaner.Core.Startup;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App;

/// <summary>One new startup entry and what to do with it.</summary>
public sealed class NewStartupChoice : ObservableObject
{
    public const string Keep = "Keep", Disable = "Disable", Delete = "Delete";
    public NewStartupChoice(StartupItem item) => Item = item;
    public StartupItem Item { get; }
    public string Name => Item.Name;
    public string Detail => $"{Item.Location} · {Item.Command}";
    public string[] Actions { get; } = { Keep, Disable, Delete };
    private string _action = Keep;
    public string Action { get => _action; set => Set(ref _action, value); }
}

public partial class NewStartupWindow : Window
{
    private static NewStartupWindow? _open;
    private readonly List<NewStartupChoice> _choices;

    private NewStartupWindow(IEnumerable<StartupItem> items)
    {
        InitializeComponent();
        _choices = items.Select(i => new NewStartupChoice(i)).ToList();
        Heading.Text = _choices.Count == 1 ? "A New Startup Program" : $"{_choices.Count} New Startup Programs";
        List.ItemsSource = _choices;
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
    }

    /// <summary>Shows the pop-up (not modal, so Kodiak Cleaner keeps working); a second batch joins an open one.</summary>
    public static void Ask(IReadOnlyList<StartupItem> items)
    {
        if (_open is { } w)
        {
            foreach (var i in items.Where(i => w._choices.All(c => StartupRecord.KeyOf(c.Item) != StartupRecord.KeyOf(i))))
                w._choices.Add(new NewStartupChoice(i));
            w.List.ItemsSource = null;
            w.List.ItemsSource = w._choices;
            w.Heading.Text = $"{w._choices.Count} New Startup Programs";
            w.Activate();
            return;
        }
        _open = new NewStartupWindow(items);
        _open.Closed += (_, _) => _open = null;
        _open.Show();
        _open.Activate();
    }

    private void KeepAll_Click(object sender, RoutedEventArgs e) => Close();

    private void OpenStartup_Click(object sender, RoutedEventArgs e)
    {
        Close();
        (Application.Current as App)?.ShowPage("startup");
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        var failed = new List<string>();
        int disabled = 0, deleted = 0;
        foreach (var c in _choices.Where(c => c.Action != NewStartupChoice.Keep))
        {
            try
            {
                if (c.Action == NewStartupChoice.Disable)
                {
                    StartupManager.SetEnabled(c.Item, false);
                    disabled++;
                }
                else
                {
                    StartupHistoryStore.Removed(c.Item, StartupManager.Delete(c.Item));
                    deleted++;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or OperationCanceledException)
            {
                failed.Add(c.Name);
            }
        }
        if (disabled + deleted > 0)
            ActivityLog.Write("Startup", $"From the new startup pop-up: {disabled} disabled, {deleted} deleted.");
        Close();
        (Application.Current as App)?.StartupChanged();
        if (failed.Count > 0)
            Dialogs.Error($"These couldn't be changed{(Elevation.IsAdministrator ? "" : ". Entries for all users need administrator rights (use Restart as Administrator)")}:\n\n" +
                          string.Join("\n", failed));
    }
}
