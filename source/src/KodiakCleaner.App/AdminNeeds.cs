using KodiakCleaner.App.ViewModels;

namespace KodiakCleaner.App;

/// <summary>
/// What each page can't do, or does slower, without administrator rights. Running as a standard user, the open page's entry
/// shows at the top of the page with a Restart as Administrator button (MainWindow, Ted 2026-10-23); pages without one show
/// nothing. Every page that checks <see cref="Elevation.IsAdministrator"/> or says it needs administrator rights must have an
/// entry here (AdminNoticeTests), and its actions offer the restart with <see cref="Elevation.OfferRestart"/>.
/// </summary>
public sealed record AdminNeed(string[] Needs, string[] Faster)
{
    public bool HasNeeds => Needs.Length > 0;
    public bool HasFaster => Faster.Length > 0;
    /// <summary>The banner's bullet lists, one line each.</summary>
    public string NeedsList => Bullets(Needs);
    public string FasterList => Bullets(Faster);

    private static string Bullets(string[] lines) => string.Join("\n", lines.Select(l => "•  " + l));

    private static AdminNeed Need(params string[] needs) => new(needs, []);

    /// <summary>Pages A to Z by their view model.</summary>
    private static readonly Dictionary<Type, AdminNeed> Pages = new()
    {
        [typeof(BootTimerViewModel)] = Need("Windows' own boot measurement and the slowdowns it noticed."),
        [typeof(CleanerViewModel)] = Need("Windows temporary files, memory dumps, the Windows Update and Delivery Optimization caches, and Advanced Cleaning's Windows tasks."),
        [typeof(DiskAnalyzerViewModel)] = new(
            ["Folders that belong to other users or to Windows, which are left out of the map."],
            ["Whole NTFS drives are read straight from the drive's file table in seconds, instead of folder by folder."]),
        [typeof(DriverCleanerViewModel)] = Need("Removing drivers, restarting in Safe Mode, BitLocker, and letting Windows Update install drivers."),
        [typeof(DuplicatesViewModel)] = new([],
            ["Whole NTFS drives are listed straight from the drive's file table, instead of folder by folder."]),
        [typeof(ExtensionsViewModel)] = Need("Turning off or removing Chrome, Edge and Brave extensions, which uses the browsers' policies."),
        [typeof(IntegrityCheckViewModel)] = Need(
            "Drive File System, Windows Component Store and Windows System Files.",
            "Drive Health's temperature, wear and error counts."),
        [typeof(OptimizeViewModel)] = Need("Defragmenting, trimming and analyzing drives (Windows' own optimizer)."),
        [typeof(ProgramsViewModel)] = Need("Renaming, deleting the entry of, and removing leftovers of programs installed for all users."),
        [typeof(RegistryViewModel)] = Need("Fixing entries saved for all users (HKEY_LOCAL_MACHINE)."),
        [typeof(RestoreViewModel)] = Need("Listing, creating and deleting restore points, and Point-in-Time Restore."),
        [typeof(ScheduledTasksViewModel)] = Need("Turning on, turning off, deleting and restoring Windows' tasks and tasks that run for all users."),
        [typeof(StartupViewModel)] = Need("Changing or removing startup entries and tasks for all users."),
        [typeof(SystemInfoViewModel)] = Need("A few details Windows shows only to administrators, such as the TPM and temperatures."),
        [typeof(WiperViewModel)] = Need("Wiping an entire drive, and checking a drive's health (reading every sector, and its error counts)."),
    };

    /// <summary>The page's entry, or null when it works the same either way.</summary>
    public static AdminNeed? For(object? page) => page is not null && Pages.TryGetValue(page.GetType(), out var need) ? need : null;
}
