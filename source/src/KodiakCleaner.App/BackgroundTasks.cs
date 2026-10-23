using System.Collections.ObjectModel;
using System.Windows;
using KodiakCleaner.App.ViewModels;

namespace KodiakCleaner.App;

/// <summary>
/// One piece of work a page is doing (a scan, a clean, a wipe). The corner pop-up shows it while another page is open,
/// and closing Kodiak Cleaner asks first while it runs (Ted, 2026-10-23).
/// </summary>
public sealed class RunningTask : ObservableObject, IDisposable
{
    internal RunningTask(object owner, string title)
    {
        Owner = owner;
        _title = title;
    }

    public DateTime Started { get; } = DateTime.UtcNow;

    /// <summary>The page's view model, so the pop-up hides on that page and a click opens it.</summary>
    public object Owner { get; }

    private string _title;
    /// <summary>What is happening, in Title Case: "Scanning for Files", "Optimizing Drives".</summary>
    public string Title { get => _title; set => OnUi(() => Set(ref _title, value, nameof(Title))); }

    private double? _percent;
    /// <summary>0 to 100, or null when there is no way to tell how far along it is (the bar just moves).</summary>
    public double? Percent
    {
        get => _percent;
        set => OnUi(() =>
        {
            var v = value is { } p ? Math.Clamp(p, 0, 100) : (double?)null;
            if (!Set(ref _percent, v, nameof(Percent))) return;
            OnPropertyChanged(nameof(HasPercent));
            OnPropertyChanged(nameof(NoPercent));
            OnPropertyChanged(nameof(PercentValue));
            OnPropertyChanged(nameof(PercentText));
        });
    }

    public bool HasPercent => _percent is not null;
    public bool NoPercent => _percent is null;
    public double PercentValue => _percent ?? 0;
    public string PercentText => _percent is { } p ? $"{Math.Floor(p):0}%" : "";

    /// <summary>Sets the percent from a count; a total of 0 means "can't tell".</summary>
    public void Step(long done, long total) => Percent = total > 0 ? 100.0 * done / total : null;

    /// <summary>The task is over (also when it was cancelled or failed).</summary>
    public void Dispose() => BackgroundTasks.End(this);

    private static void OnUi(Action a)
    {
        var d = Application.Current?.Dispatcher;
        if (d is null || d.CheckAccess()) a();
        else d.BeginInvoke(a);
    }
}

/// <summary>Every task running in any page. Pages add one with <c>using var task = BackgroundTasks.Start(this, "...")</c>.</summary>
public static class BackgroundTasks
{
    public static ObservableCollection<RunningTask> Running { get; } = new();

    /// <summary>Raised when a task starts or ends.</summary>
    public static event Action? Changed;

    public static bool Any => Running.Count > 0;

    public static RunningTask Start(object owner, string title)
    {
        var task = new RunningTask(owner, title);
        OnUi(() =>
        {
            Running.Add(task);
            Changed?.Invoke();
        });
        return task;
    }

    internal static void End(RunningTask task) => OnUi(() =>
    {
        if (Running.Remove(task)) Changed?.Invoke();
    });

    /// <summary>
    /// Asks before Kodiak Cleaner closes (or restarts) while something is still running. Keep Open is the default and what
    /// Esc or closing the question means, so nothing is stopped by accident.
    /// </summary>
    /// <returns>true to go ahead and close.</returns>
    public static bool ConfirmStop(string action = "Close")
    {
        if (!Any) return true;
        var titles = Running.Select(t => t.Title).Distinct().ToList();
        var list = titles.Count == 1 ? titles[0] : string.Join(", ", titles.Take(titles.Count - 1)) + " and " + titles[^1];
        var noun = titles.Count == 1 ? "A task is" : "Some tasks are";
        int choice = Dialogs.Choose(MessageKind.Warning, titles.Count == 1 ? "A Task Is Still Running" : "Tasks Are Still Running",
            $"{noun} still running: {list}. {action} {AppInfo.Name} anyway?",
            $"{action} Anyway stops {(titles.Count == 1 ? "it" : "them")} part way. Keep Open lets {(titles.Count == 1 ? "it" : "them")} finish first.",
            new[] { $"{action} Anyway", "Keep Open" }, defaultButton: 1, cancelButton: 1);
        return choice == 0;
    }

    private static void OnUi(Action a)
    {
        var d = Application.Current?.Dispatcher;
        if (d is null || d.CheckAccess()) a();
        else d.Invoke(a);
    }
}
