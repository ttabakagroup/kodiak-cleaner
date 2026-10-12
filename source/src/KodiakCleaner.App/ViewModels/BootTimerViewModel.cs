using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using KodiakCleaner.Core.Startup;
using KodiakCleaner.Core.Tools;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App.ViewModels;

/// <summary>One row of the Boot Timer report.</summary>
public sealed class BootItemViewModel : ObservableObject
{
    public BootItemViewModel(BootItem item) => Item = item;
    public BootItem Item { get; }
    public string Name => Item.Name;
    public string Kind => Item.Kind;
    public string Impact => Item.Impact == BootImpact.None ? "" : Item.Impact.ToString();
    public BootImpact ImpactLevel => Item.Impact;
    public bool IsStartupEntry => Item.StartupName.Length > 0;
    public string Started => Item.StartedAfterSignIn is { } s ? (s <= 0 ? "At Sign-In" : $"{BootTimer.Format(TimeSpan.FromSeconds(s))} after sign-in")
        : IsStartupEntry ? "Not Running" : "";
    public string Processor => Item.CpuMs > 0 ? BootTimer.Format(TimeSpan.FromMilliseconds(Item.CpuMs)) : "";
    public string Disk => Item.DiskBytes > 0 ? ByteSize.Format(Item.DiskBytes) : "";
    public string Delay => Item.DelayMs > 0 ? BootTimer.Format(TimeSpan.FromMilliseconds(Item.DelayMs)) : "";
    public string Details => DetailText.Lines(("Name", Name), ("Kind", Kind), ("Program", Item.Path), ("Startup Entry", Item.StartupName),
        ("Started", Started), ("Processor Time", Processor), ("Disk Read and Written", Disk), ("Windows Measured a Delay Of", Delay), ("Impact", Impact));

    private System.Windows.Media.ImageSource? _icon;
    public System.Windows.Media.ImageSource? Icon { get => _icon; set { if (Set(ref _icon, value)) OnPropertyChanged(nameof(NoIcon)); } }
    public bool NoIcon => _icon is null;

    private string _note = "";
    /// <summary>"Turned Off" once disabled from here.</summary>
    public string Note { get => _note; set => Set(ref _note, value); }
}

/// <summary>A bar in the Recent Restarts chart.</summary>
public sealed record BootHistoryBar(string Label, string Value, double Height, string Tip);

/// <summary>
/// Boot Timer (Ted, 2026-10-12): Restart and Time restarts the PC; Kodiak Cleaner reopens on this page after sign-in (a
/// one-time RunOnce entry), waits for the PC to settle and shows how long each part took and what slowed it down.
/// </summary>
public sealed class BootTimerViewModel : ObservableObject, ILoadOnFirstVisit
{
    private readonly StartupViewModel _startup;
    private readonly Action<string> _navigate;
    private BootTimerState _state = new();
    private CancellationTokenSource? _measuring;

    public BootTimerViewModel(StartupViewModel startup, Action<string> navigate)
    {
        _startup = startup;
        _navigate = navigate;
        RestartCommand = new RelayCommand(RestartAndTime, () => !IsMeasuring && !IsWaiting);
        CancelCommand = new RelayCommand(CancelTiming, () => IsWaiting || IsMeasuring);
        RefreshCommand = new RelayCommand(RefreshWindowsLog, () => HasReport && !IsMeasuring);
        ShowInStartupCommand = new RelayCommand(ShowInStartup, () => Selected is { IsStartupEntry: true });
        DisableCommand = new RelayCommand(DisableSelected, () => Selected is { IsStartupEntry: true, Note: "" });
        ClearHistoryCommand = new RelayCommand(ClearHistory, () => HasReport || _state.History.Count > 0);
    }

    public ICommand RestartCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ShowInStartupCommand { get; }
    public ICommand DisableCommand { get; }
    public ICommand ClearHistoryCommand { get; }

    public ObservableCollection<BootItemViewModel> Items { get; } = new();
    public ObservableCollection<BootHistoryBar> History { get; } = new();

    private BootItemViewModel? _selected;
    public BootItemViewModel? Selected { get => _selected; set => Set(ref _selected, value); }

    public bool IsAdministrator => Elevation.IsAdministrator;

    // Which panel shows: the intro, waiting for the restart, measuring, or the report.
    private bool _isWaiting, _isMeasuring;
    public bool IsWaiting { get => _isWaiting; private set { if (Set(ref _isWaiting, value)) Changed(); } }
    public bool IsMeasuring { get => _isMeasuring; private set { if (Set(ref _isMeasuring, value)) Changed(); } }
    public bool IsTiming => IsWaiting || IsMeasuring;
    public bool HasReport => _state.Last is not null;
    public bool ShowIntro => !HasReport && !IsWaiting && !IsMeasuring;
    public bool ShowReport => HasReport && !IsMeasuring;
    public bool HasHistory => History.Count > 1;

    private void Changed()
    {
        foreach (var p in new[] { nameof(IsTiming), nameof(HasReport), nameof(ShowIntro), nameof(ShowReport), nameof(HasHistory) }) OnPropertyChanged(p);
        CommandManager.InvalidateRequerySuggested();
    }

    private string _progress = "";
    public string Progress { get => _progress; private set => Set(ref _progress, value); }

    // Report summary.
    public string Total => _state.Last is { } r ? BootTimer.Format(r.Total) : "";
    public string Shutdown => _state.Last is { } r ? BootTimer.Format(r.ShutdownAndRestart) : "";
    public string WindowsStart => _state.Last?.WindowsStarting is { } t ? BootTimer.Format(t) : "Not Measured";
    public string DesktopLoad => _state.Last?.DesktopLoading is { } t ? BootTimer.Format(t) : "Not Measured";
    public string Measured => _state.Last is { } r ? $"Timed on {r.RequestedUtc.ToLocalTime():f}." : "";
    public GridLength ShutdownWidth => Star(_state.Last?.ShutdownAndRestart);
    public GridLength WindowsWidth => Star(_state.Last?.WindowsStarting);
    public GridLength DesktopWidth => Star(_state.Last?.DesktopLoading);
    private static GridLength Star(TimeSpan? t) => new(Math.Max(t?.TotalSeconds ?? 0, 0.01), GridUnitType.Star);

    private string _windowsLine = "";
    /// <summary>Windows' own measurement of the start, or why it isn't shown.</summary>
    public string WindowsLine { get => _windowsLine; private set => Set(ref _windowsLine, value); }

    private string _summary = "";
    public string Summary { get => _summary; private set => Set(ref _summary, value); }

    private bool _loaded;
    public void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _state = BootTimer.Load(BootProbe.StateFile);
        if (_state.PendingSinceUtc is not null && BootTimer.RestartHappened(_state, BootProbe.BootUtc)) _ = MeasureAsync();
        else IsWaiting = _state.PendingSinceUtc is not null;
        ShowState();
    }

    private void RestartAndTime()
    {
        if (!Dialogs.Confirm("Kodiak Cleaner will restart this PC now and time how long it takes to be ready again.\n\n" +
                             "Save your work and close your programs first. After you sign in, Kodiak Cleaner opens by itself, " +
                             "waits for the PC to settle and shows the report. Restart now?", "Restart and Time")) return;
        _state.PendingSinceUtc = DateTime.UtcNow;
        _state.PendingBootUtc = BootProbe.BootUtc;
        try
        {
            BootTimer.Save(BootProbe.StateFile, _state);
            BootProbe.SetReopen(true);
            ActivityLog.Write("Boot Timer", "Restarted the PC to time the start");
            BootProbe.Restart();
            IsWaiting = true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or Win32Exception or System.Security.SecurityException)
        {
            _state.PendingSinceUtc = null;
            _state.PendingBootUtc = null;
            Try(() => BootProbe.SetReopen(false));
            Try(() => BootTimer.Save(BootProbe.StateFile, _state));
            Dialogs.Error("The PC couldn't be restarted:\n\n" + e.Message);
        }
    }

    /// <summary>The restart didn't happen (a program stopped it) or isn't wanted any more.</summary>
    private void CancelTiming()
    {
        _measuring?.Cancel();
        _state.PendingSinceUtc = null;
        _state.PendingBootUtc = null;
        Try(() => BootProbe.SetReopen(false));
        Save();
        IsWaiting = false;
        IsMeasuring = false;
        ShowState();
    }

    private async Task MeasureAsync()
    {
        IsMeasuring = true;
        _measuring = new CancellationTokenSource();
        var token = _measuring.Token;
        var progress = new Progress<string>(s => Progress = s);
        try
        {
            var since = _state.PendingSinceUtc ?? DateTime.UtcNow;
            var report = await Task.Run(() => Measure(since, progress, token), token);
            BootTimer.Finish(_state, report);
            Try(() => BootProbe.SetReopen(false));
            Save();
            ActivityLog.Write("Boot Timer", $"Timed a restart: {BootTimer.Format(report.Total)} from restart to ready");
        }
        catch (OperationCanceledException) { }
        finally
        {
            IsMeasuring = false;
            IsWaiting = _state.PendingSinceUtc is not null;
            ShowState();
        }
    }

    /// <summary>
    /// Waits for the PC to settle (the processor under 15% busy for 5 seconds in a row, at most 4 minutes after sign-in),
    /// then reads what every startup program used and Windows' own boot log.
    /// </summary>
    private static BootReport Measure(DateTime requestedUtc, IProgress<string> progress, CancellationToken token)
    {
        var report = new BootReport { RequestedUtc = requestedUtc, BootUtc = BootProbe.BootUtc, SignInUtc = BootProbe.SignInUtc() };
        var signIn = report.SignInUtc;
        bool late = signIn is null || DateTime.UtcNow - signIn > BootTimer.LateAfterSignIn;
        if (!late)
        {
            var limit = signIn!.Value + TimeSpan.FromMinutes(4);
            DateTime? quietSince = null;
            BootProbe.ProcessorBusy();
            while (DateTime.UtcNow < limit)
            {
                token.WaitHandle.WaitOne(1000);
                token.ThrowIfCancellationRequested();
                var busy = BootProbe.ProcessorBusy() ?? 1;
                if (busy < 0.15) quietSince ??= DateTime.UtcNow.AddSeconds(-1);
                else quietSince = null;
                if (quietSince is { } q && DateTime.UtcNow - q >= TimeSpan.FromSeconds(5)) break;
                progress.Report($"Waiting for the PC to settle. The processor is {busy:P0} busy, " +
                                $"{BootTimer.Format(DateTime.UtcNow - signIn.Value)} after sign-in.");
            }
            report.ReadyUtc = quietSince ?? DateTime.UtcNow;
        }
        progress.Report("Reading what each startup program used.");
        report.Items = Collect(report, BootProbe.StartupPrograms());
        return report;
    }

    /// <summary>The startup programs (running or not), busy programs that started with the session, and Windows' own slowdown events.</summary>
    private static List<BootItem> Collect(BootReport report, Dictionary<string, StartupItem> startup)
    {
        var items = new Dictionary<string, BootItem>(StringComparer.OrdinalIgnoreCase);
        var signIn = report.SignInUtc ?? report.BootUtc;
        var until = (report.ReadyUtc ?? DateTime.UtcNow) + TimeSpan.FromSeconds(5);
        var session = System.Diagnostics.Process.GetCurrentProcess().SessionId;
        var self = Environment.ProcessPath ?? "";
        foreach (var p in BootProbe.Processes())
        {
            if (p.Path.Length == 0 || p.StartUtc < report.BootUtc || p.Path.Equals(self, StringComparison.OrdinalIgnoreCase)) continue;
            startup.TryGetValue(p.Path, out var entry);
            // Other programs: only this person's, started from sign-in until the PC settled.
            if (entry is null && (p.SessionId != session || p.StartUtc < signIn - TimeSpan.FromSeconds(5) || p.StartUtc > until)) continue;
            if (!items.TryGetValue(p.Path, out var item))
            {
                item = new BootItem
                {
                    Name = entry?.Name ?? ProgramName(p.Path),
                    Kind = entry is not null ? "Startup Program" : "Other Program",
                    Path = p.Path,
                    StartupName = entry?.Name ?? "",
                    StartedAfterSignIn = Math.Round(Math.Max(0, (p.StartUtc - signIn).TotalSeconds), 1),
                };
                items[p.Path] = item;
            }
            // Several copies of one program (browsers, helpers) count together; the first start counts.
            item.CpuMs += p.CpuMs;
            item.DiskBytes += p.DiskBytes;
            var started = Math.Round(Math.Max(0, (p.StartUtc - signIn).TotalSeconds), 1);
            if (started < item.StartedAfterSignIn) item.StartedAfterSignIn = started;
        }
        foreach (var i in items.Values) i.Impact = BootTimer.ImpactOf(i.CpuMs, i.DiskBytes);
        // Programs that weren't startup entries are listed only when they did real work.
        var list = items.Values.Where(i => i.Kind == "Startup Program" || i.Impact >= BootImpact.Medium).ToList();
        foreach (var (file, entry) in startup)
            if (!items.ContainsKey(file))
                list.Add(new BootItem { Name = entry.Name, Kind = "Startup Program", Path = file, StartupName = entry.Name, Impact = BootImpact.None });
        return BootTimer.Rank(list);
    }

    private static string ProgramName(string path)
    {
        try
        {
            var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(path);
            var name = info.FileDescription?.Trim();
            if (!string.IsNullOrEmpty(name)) return name;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { }
        return Path.GetFileNameWithoutExtension(path);
    }

    /// <summary>Windows writes its own boot measurement a few minutes after the start; Refresh adds it to the latest report.</summary>
    private void RefreshWindowsLog()
    {
        if (_state.Last is not { } r) return;
        MergeWindowsLog(r);
        Save();
        ShowState();
    }

    private static void MergeWindowsLog(BootReport r)
    {
        // Only the start that is still running can be looked up (older starts have scrolled on in the log or been cleared).
        if ((r.BootUtc - BootProbe.BootUtc).Duration() > BootTimer.BootSlack) return;
        var data = BootProbe.ReadWindowsLog(r.BootUtc, out var readable);
        r.WindowsLogRead |= readable;
        if (data is null) return;
        r.WindowsBootMs = data.BootMs;
        r.WindowsMainPathMs = data.MainPathMs;
        r.WindowsPostBootMs = data.PostBootMs;
        r.Items.RemoveAll(i => i.Kind is not ("Startup Program" or "Other Program"));
        foreach (var w in data.Items)
        {
            var match = w.Path.Length > 0 ? r.Items.FirstOrDefault(i => i.Path.Equals(w.Path, StringComparison.OrdinalIgnoreCase)) : null;
            if (match is not null)
            {
                match.DelayMs = Math.Max(match.DelayMs, w.DelayMs);
                match.Impact = BootTimer.ImpactOf(match.CpuMs, match.DiskBytes, match.DelayMs);
            }
            else r.Items.Add(w);
        }
        r.Items = BootTimer.Rank(r.Items);
    }

    private void ShowState()
    {
        Changed();
        foreach (var p in new[] { nameof(Total), nameof(Shutdown), nameof(WindowsStart), nameof(DesktopLoad), nameof(Measured),
                                  nameof(ShutdownWidth), nameof(WindowsWidth), nameof(DesktopWidth) })
            OnPropertyChanged(p);
        Items.Clear();
        History.Clear();
        if (IsWaiting)
            Progress = $"Waiting for the restart you started on {_state.PendingSinceUtc!.Value.ToLocalTime():f}. " +
                       "If a program stopped Windows from restarting, close it and use Restart and Time again, or cancel.";
        if (_state.Last is not { } r) return;

        foreach (var i in r.Items) Items.Add(new BootItemViewModel(i));
        FileIcons.LoadInto(Items.ToList(), i => i.Item.Path, (i, icon) => i.Icon = icon);
        int slow = r.Items.Count(i => i.Impact == BootImpact.High);
        Summary = r.Items.Count == 0 ? "Nothing slowed this start down that Windows lets Kodiak Cleaner see."
            : $"{r.Items.Count} item{(r.Items.Count == 1 ? "" : "s")}, slowest first. {slow} had a high impact. " +
              "Turning off startup programs you don't need right away makes the next start quicker.";
        WindowsLine = r.WindowsBootMs > 0
            ? $"Windows' own measurement: {BootTimer.Format(TimeSpan.FromMilliseconds(r.WindowsBootMs))} to start, " +
              $"{BootTimer.Format(TimeSpan.FromMilliseconds(r.WindowsMainPathMs))} until the desktop showed and " +
              $"{BootTimer.Format(TimeSpan.FromMilliseconds(r.WindowsPostBootMs))} more until it settled."
            : !IsAdministrator ? "Restart as Administrator to add Windows' own boot measurement and the slowdowns it noticed."
            : r.WindowsLogRead ? "Windows writes its own boot measurement a few minutes after startup. Press Refresh to add it."
            : (r.BootUtc - BootProbe.BootUtc).Duration() > BootTimer.BootSlack ? "Windows' own measurement can only be added before the next restart."
            : "Press Refresh to add Windows' own boot measurement.";
        if (r.SignInUtc is null || r.ReadyUtc is null)
            WindowsLine = "Kodiak Cleaner opened too long after sign-in to time the desktop settling. " + WindowsLine;

        var bars = _state.History.TakeLast(BootTimer.HistoryKept).ToList();
        double max = Math.Max(1, bars.Select(b => b.TotalSeconds).DefaultIfEmpty(1).Max());
        foreach (var b in bars)
        {
            var when = b.RequestedUtc.ToLocalTime();
            var value = BootTimer.Format(TimeSpan.FromSeconds(b.TotalSeconds));
            History.Add(new BootHistoryBar(when.ToString("MMM d"), value, Math.Max(4, 90 * b.TotalSeconds / max), $"{when:f}: {value}"));
        }
        OnPropertyChanged(nameof(HasHistory));
    }

    private void ShowInStartup()
    {
        if (Selected is not { IsStartupEntry: true } row) return;
        _navigate("startup");
        _startup.Filter = row.Item.StartupName;
    }

    private void DisableSelected()
    {
        if (Selected is not { IsStartupEntry: true } row) return;
        var entry = StartupManager.List().FirstOrDefault(i => i.Enabled && i.Name == row.Item.StartupName);
        if (entry is null) { row.Note = "Turned Off"; return; }
        try
        {
            StartupManager.SetEnabled(entry, false);
            ActivityLog.Write("Startup", $"Turned off '{entry.Name}' (Boot Timer)");
            row.Note = "Turned Off";
            _startup.RefreshCommand.Execute(null);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException)
        {
            Dialogs.Error("Changing this entry needs administrator rights. Use Restart as Administrator at the top.");
        }
    }

    private void ClearHistory()
    {
        if (!Dialogs.Confirm("Clear the latest report and the Recent Restarts chart?", "Clear Results")) return;
        _state.Last = null;
        _state.History.Clear();
        Save();
        ShowState();
    }

    private void Save() => Try(() => BootTimer.Save(BootProbe.StateFile, _state));

    private static void Try(Action a)
    {
        try { a(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
    }
}
