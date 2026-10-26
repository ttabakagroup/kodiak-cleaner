using System.IO;
using System.Windows;
using System.Windows.Threading;
using KodiakCleaner.Core.Startup;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App;

/// <summary>
/// Notices programs that added themselves to startup (Ted, 0.8.53). When Kodiak Cleaner starts, and every 10 minutes while
/// it's open or in the tray, the startup list is compared with the Startup History; anything new pops up in
/// <see cref="NewStartupWindow"/>. The very first look only records what's there, so existing entries never pop up.
/// </summary>
public static class NewStartupWatch
{
    private static readonly TimeSpan Every = TimeSpan.FromMinutes(10);
    private static DispatcherTimer? _timer;
    private static bool _busy;

    public static void Start()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = Every };
        _timer.Tick += (_, _) => _ = CheckAsync();
        _timer.Start();
        _ = CheckAsync();
    }

    /// <summary>True for Kodiak Cleaner's own entries (Clean on Startup and the like), which never pop up.</summary>
    private static bool IsOurs(StartupItem i) =>
        i.Name.StartsWith(AppInfo.ShortName, StringComparison.OrdinalIgnoreCase) || // every channel's ("Kodiak Cleaner Dev AutoClean")
        (i.Command.Contains(Integration.ExePath, StringComparison.OrdinalIgnoreCase) ||
        i.Command.Contains(Integration.LaunchPath, StringComparison.OrdinalIgnoreCase));

    private static async Task CheckAsync()
    {
        if (_busy || !Settings.Current.NotifyNewStartup) return;
        _busy = true;
        try
        {
            List<StartupItem> items;
            try { items = await Task.Run(StartupManager.List); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { return; }

            var history = StartupHistoryStore.Current;
            var firstLook = history.Records.Count == 0;
            var fresh = firstLook ? new List<StartupItem>() : history.NotYetSeen(items).Where(i => !IsOurs(i)).ToList();
            StartupHistoryStore.Record(items); // seen now, so they pop up only once
            if (fresh.Count == 0) return;

            ActivityLog.Write("Startup", $"New in startup: {string.Join(", ", fresh.Select(f => $"{f.Name} ({f.Location})"))}");
            NewStartupWindow.Ask(fresh);
        }
        finally { _busy = false; }
    }
}
