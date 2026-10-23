namespace KodiakCleaner.Core.Startup;

public enum StartupSource
{
    UserRunKey,
    MachineRunKey,
    MachineRunKey32,
    UserStartupFolder,
    CommonStartupFolder,
    /// <summary>A Task Scheduler task that starts a program at sign-in or when Windows starts (Startup History only; Ted, 0.8.92).</summary>
    ScheduledTask,
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
    /// <summary>The task's full path in Task Scheduler ("\OneDrive Startup Task"), for <see cref="StartupSource.ScheduledTask"/>.</summary>
    public string? TaskPath { get; init; }

    public bool IsMachineWide => Source is StartupSource.MachineRunKey or StartupSource.MachineRunKey32 or StartupSource.CommonStartupFolder;

    public string Location => Source switch
    {
        StartupSource.UserRunKey => @"HKCU\...\Run",
        StartupSource.MachineRunKey => @"HKLM\...\Run",
        StartupSource.MachineRunKey32 => @"HKLM\...\Run (32-bit)",
        StartupSource.UserStartupFolder => "Startup Folder (You)",
        StartupSource.CommonStartupFolder => "Startup Folder (All Users)",
        StartupSource.ScheduledTask => "Scheduled Task",
        _ => Source.ToString(),
    };

    /// <summary>Exactly where the entry lives: the full Run key and value name, or the startup folder file.</summary>
    public string FullLocation => Source switch
    {
        StartupSource.UserRunKey => $@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run : {Name}",
        StartupSource.MachineRunKey => $@"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Run : {Name}",
        StartupSource.MachineRunKey32 => $@"HKEY_LOCAL_MACHINE\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run : {Name}",
        StartupSource.ScheduledTask => $"Task Scheduler: {TaskPath}",
        _ => FilePath ?? Location,
    };
}
