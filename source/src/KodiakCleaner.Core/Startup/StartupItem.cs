namespace KodiakCleaner.Core.Startup;

public enum StartupSource
{
    UserRunKey,
    MachineRunKey,
    MachineRunKey32,
    UserStartupFolder,
    CommonStartupFolder,
}

public sealed class StartupItem
{
    public required string Name { get; init; }
    public required string Command { get; init; }
    public required StartupSource Source { get; init; }
    public bool Enabled { get; set; }
    /// <summary>Full path of the shortcut/file for startup-folder entries.</summary>
    public string? FilePath { get; init; }
    public bool FileMissing { get; init; }

    public bool IsMachineWide => Source is StartupSource.MachineRunKey or StartupSource.MachineRunKey32 or StartupSource.CommonStartupFolder;

    public string Location => Source switch
    {
        StartupSource.UserRunKey => @"HKCU\...\Run",
        StartupSource.MachineRunKey => @"HKLM\...\Run",
        StartupSource.MachineRunKey32 => @"HKLM\...\Run (32-bit)",
        StartupSource.UserStartupFolder => "Startup Folder (You)",
        StartupSource.CommonStartupFolder => "Startup Folder (All Users)",
        _ => Source.ToString(),
    };
}
