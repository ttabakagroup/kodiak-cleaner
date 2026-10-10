using System.Windows;
using System.Windows.Threading;
using KodiakCleaner.Core.RegistryTools;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App;

public partial class App : Application
{
    private TrayIcon? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnUnhandledException;
        base.OnStartup(e);
        try
        {
            Start(e.Args);
        }
        catch (Exception ex) when (MainWindow is null)
        {
            // Nothing is on screen yet, so report it and exit rather than leave a hidden process behind.
            // Never during an uninstall: an error box there would hold up Windows Installer.
            if (!UninstallCleanup.IsCleanupArg(e.Args)) Dialogs.Error($"{AppInfo.Name} couldn't start:\n\n" + ex.Message);
            Shutdown();
        }
    }

    private void Start(string[] startArgs)
    {

        Settings.Load();
        Supporter.Refresh();
        // Run by the MSI while Kodiak Cleaner is being removed: clean up, no window.
        if (UninstallCleanup.TryRun(startArgs))
        {
            Shutdown();
            return;
        }
        Settings.PrepareDataFolders();
        RegistryBackup.BackupFolder = Settings.Data.Registry;
        var args = startArgs;

        // Startup / jump-list clean: no window at all.
        if (args.Any(a => a.Equals(Integration.AutoCleanArg, StringComparison.OrdinalIgnoreCase)))
        {
            try { AutoClean.Run(); }
            finally { Shutdown(); }
            return;
        }

        // Command-line switches (/clean, /registry, /uninstall, /help): no window; the exit code says how it went.
        if (Cli.IsCommand(args))
        {
            int code = Cli.BadArgs;
            try { code = Cli.Run(args); }
            finally { Shutdown(code); }
            return;
        }

        // A scheduled cleaning started by Windows Task Scheduler: no window either.
        // When Kodiak Cleaner is already open, that copy runs it, so both don't write the settings file.
        if (args.FirstOrDefault(a => a.StartsWith(ScheduledCleaning.Arg, StringComparison.OrdinalIgnoreCase)) is { } scheduled)
        {
            try
            {
                if (SingleInstance.IsRunning) SingleInstance.SendToFirst(new[] { scheduled });
                else ScheduledCleaning.Run(scheduled[ScheduledCleaning.Arg.Length..]);
            }
            finally { Shutdown(); }
            return;
        }

        // "Skip UAC": hand over to the elevated scheduled task.
        if (!Elevation.IsAdministrator && Settings.Current.SkipUac && Integration.CanSkipUac && args.Length == 0 &&
            Integration.TryLaunchViaTask())
        {
            Shutdown();
            return;
        }

        // "Always Start as Administrator": ask Windows for an elevated copy. If the prompt is declined, keep starting normally.
        if (!Elevation.IsAdministrator && Settings.Current.AlwaysAdmin && !args.Contains(Elevation.ElevatedArg) &&
            !SingleInstance.IsRunning && Elevation.TryStartElevated(args))
        {
            Shutdown();
            return;
        }
        bool restarted = args.Contains(Elevation.RestartedArg);
        bool updated = args.Contains(UpdateService.UpdatedArg);
        args = args.Where(a => a != Elevation.ElevatedArg && a != Elevation.RestartedArg && a != UpdateService.UpdatedArg).ToArray();

        if (!SingleInstance.Claim(waitForPrevious: restarted))
        {
            SingleInstance.SendToFirst(args.Length > 0 ? args : new[] { "--activate" });
            Shutdown();
            return;
        }

        ActivityLog.Write("Start", $"{AppInfo.Name} {AppInfo.VersionName} started" +
            $" ({(Elevation.IsAdministrator ? "as administrator" : "as a standard user")}, {(Integration.IsInstalled ? "installed" : "portable")})");
        ThemeManager.Apply();
        Donation.CountLaunch();
        var window = new MainWindow { BringToFront = updated };
        MainWindow = window;
        _tray = new TrayIcon(window);
        window.Show();

        // Windows registrations (startup entry, Recycle Bin menu, jump list, scheduled cleanings) are checked once the
        // window is drawn, so they never hold up the first paint (0.8.47).
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, Integration.SyncAll);
        SingleInstance.Listen(a =>
        {
            // A scheduled cleaning runs quietly; anything else brings the window back.
            if (!a.All(x => x.StartsWith(ScheduledCleaning.Arg, StringComparison.OrdinalIgnoreCase))) _tray.Restore();
            window.HandleArgs(a);
        });
        window.HandleArgs(args);
    }

    /// <summary>Brings the window back (from the tray too) on a page such as "startup".</summary>
    public void ShowPage(string key)
    {
        _tray?.Restore();
        (MainWindow as MainWindow)?.OpenSettings(key);
    }

    /// <summary>Startup entries changed outside the Startup page; it reads the list again.</summary>
    public void StartupChanged() => (MainWindow as MainWindow)?.RefreshStartup();

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        UpdateService.InstallPendingOnExit();
        base.OnExit(e);
    }

    // Show unexpected errors instead of closing the app; nothing is sent anywhere.
    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ActivityLog.Error("Error", e.Exception);
        Dialogs.Error("Something went wrong:\n\n" + e.Exception.Message);
        e.Handled = true;
    }
}
