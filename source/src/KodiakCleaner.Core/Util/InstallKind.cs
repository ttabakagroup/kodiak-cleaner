namespace KodiakCleaner.Core.Util;

/// <summary>How this copy of Kodiak Cleaner got onto the PC.</summary>
public enum InstallKind
{
    /// <summary>Unzipped anywhere; settings, backups and logs sit beside KodiakCleaner.exe.</summary>
    Portable,
    /// <summary>The MSI, in Program Files\Kodiak Cleaner.</summary>
    Installed,
    /// <summary>The Microsoft Store (MSIX) package, in the read-only C:\Program Files\WindowsApps\&lt;package&gt; folder.</summary>
    Store,
}

/// <summary>
/// The rules that differ between a portable copy, the MSI and the Microsoft Store package (Ted, 2026-10-26). Kept here,
/// without Windows calls, so tests cover them; the app's Integration class feeds in the real paths.
/// </summary>
public static class InstallKinds
{
    /// <summary>The name in the package's AppExecutionAlias (packaging/store/AppxManifest.xml; a test keeps them equal).</summary>
    public const string StoreAlias = "kodiakcleaner.exe";

    /// <summary>
    /// A packaged copy (it has package identity) is the Store copy, even though WindowsApps sits inside Program Files.
    /// Otherwise, anything under Program Files came from the MSI and anything else is portable.
    /// </summary>
    public static InstallKind Detect(string exePath, string programFiles, bool packaged)
    {
        if (packaged) return InstallKind.Store;
        if (programFiles.Length == 0) return InstallKind.Portable;
        var pf = programFiles.TrimEnd('\\', '/');
        return exePath.StartsWith(pf + '\\', StringComparison.OrdinalIgnoreCase) || exePath.StartsWith(pf + '/', StringComparison.OrdinalIgnoreCase)
            ? InstallKind.Installed : InstallKind.Portable;
    }

    /// <summary>
    /// The path Windows should start Kodiak Cleaner with (Run value, scheduled cleanings, RunOnce, the Recycle Bin menu,
    /// Restart as Administrator). The Store copy's folder name carries its version and changes with every Store update, and
    /// starting that exe directly loses the package identity, so the Store copy is always started through its app alias in
    /// %LOCALAPPDATA%\Microsoft\WindowsApps.
    /// </summary>
    public static string LaunchPath(InstallKind kind, string exePath, string localAppData) =>
        kind == InstallKind.Store && localAppData.Length > 0
            ? Path.Combine(localAppData, "Microsoft", "WindowsApps", StoreAlias)
            : exePath;

    /// <summary>
    /// Where KodiakCleaner.ini goes. The Store copy can't write beside its exe, so it uses the package's own LocalState folder,
    /// which Windows deletes when the app is uninstalled. Every other copy keeps it beside the exe.
    /// </summary>
    public static string SettingsFolder(InstallKind kind, string exeFolder, string localAppData, string? packageFamilyName) =>
        kind == InstallKind.Store && localAppData.Length > 0 && !string.IsNullOrEmpty(packageFamilyName)
            ? Path.Combine(localAppData, "Packages", packageFamilyName, "LocalState")
            : exeFolder;

    /// <summary>Backups, logs and reports go to Documents\Kodiak Cleaner for the MSI and the Store copy, beside the exe when portable.</summary>
    public static bool UsesDocuments(InstallKind kind) => kind != InstallKind.Portable;

    /// <summary>The website's update check and installer download: only the MSI. The Store updates its own copy, and a
    /// Store app must not download and run an installer (Store policy 10.2.2); a portable copy only checks.</summary>
    public static bool InstallsUpdates(InstallKind kind) => kind == InstallKind.Installed;

    /// <summary>Whether Kodiak Cleaner checks the website for a newer version at all. The Store copy never does.</summary>
    public static bool ChecksForUpdates(InstallKind kind) => kind != InstallKind.Store;

    /// <summary>
    /// Skip UAC and administrator scheduled cleanings start Kodiak Cleaner with full rights and no prompt, so they may only
    /// point at an exe that standard users can't replace: the MSI's Program Files copy. A portable exe and the Store's app
    /// alias (in the user's own AppData) could both be swapped by any program the user runs.
    /// </summary>
    public static bool MayStartElevatedWithoutPrompt(InstallKind kind) => kind == InstallKind.Installed;

    /// <summary>
    /// The Driver Cleaner's Safe Mode step. Windows doesn't start Store apps in Safe Mode, so the Store copy would never open
    /// there to finish the job and turn Safe Mode off again; it only offers the steps that run in a normal start.
    /// </summary>
    public static bool CanOpenInSafeMode(InstallKind kind) => kind != InstallKind.Store;

    /// <summary>Shown under Settings and in bug reports.</summary>
    public static string Describe(InstallKind kind) => kind switch
    {
        InstallKind.Installed => "Installed (MSI)",
        InstallKind.Store => "Microsoft Store",
        _ => "Portable",
    };
}
