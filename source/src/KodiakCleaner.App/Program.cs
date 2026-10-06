namespace KodiakCleaner.App;

/// <summary>
/// Starts Kodiak Cleaner. The uninstall cleanup that needs no window (Task Scheduler as administrator, or a quiet
/// uninstall) runs here before WPF starts, so it also works where no desktop is available to draw on.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (UninstallCleanup.TryRunWithoutWindow(args)) return;
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
