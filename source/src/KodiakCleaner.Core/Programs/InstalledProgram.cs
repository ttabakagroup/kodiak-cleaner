using KodiakCleaner.Core.RegistryTools;

namespace KodiakCleaner.Core.Programs;

public sealed class InstalledProgram
{
    public required string Name { get; init; }
    public string Publisher { get; init; } = "";
    public string Version { get; init; } = "";
    public DateTime? InstallDate { get; init; }
    public long SizeBytes { get; init; }
    public string UninstallString { get; init; } = "";
    public string QuietUninstallString { get; init; } = "";
    public string ModifyPath { get; init; } = "";
    /// <summary>The folder the program says it's installed in (often empty).</summary>
    public string InstallLocation { get; init; } = "";
    /// <summary>Where the program's icon is, as "path" or "path,index" (often empty).</summary>
    public string DisplayIcon { get; init; } = "";
    /// <summary>The uninstall entry's key name; for Windows Installer programs it is the product code.</summary>
    public string KeyName { get; init; } = "";
    public bool IsWindowsInstaller { get; init; }
    public bool NoRepair { get; init; }
    public bool NoModify { get; init; }
    public bool PerUser { get; init; }
    public bool Is32Bit { get; init; }
    public required RegistryLocation Location { get; init; }

    /// <summary>The folder holding the uninstaller, for programs that don't name their install folder; "" for Windows Installer.</summary>
    public string UninstallerFolder
    {
        get
        {
            if (IsWindowsInstaller) return "";
            var exe = Util.CommandLine.CheckablePath(Environment.ExpandEnvironmentVariables(UninstallString));
            if (exe.Length == 0 || !Path.IsPathFullyQualified(exe)) return "";
            return Path.GetDirectoryName(exe) ?? "";
        }
    }

    public bool CanUninstall => !string.IsNullOrWhiteSpace(UninstallString);

    /// <summary>Windows Installer programs repair themselves (msiexec /fa); others open their own change or repair screen.</summary>
    public bool CanRepair => RepairCommand() is not null;

    public bool IsProductCode => Guid.TryParse(KeyName, out _) && KeyName.StartsWith('{');

    /// <summary>The program and arguments that start a repair, or null when the program offers none.</summary>
    public (string File, string Arguments)? RepairCommand()
    {
        if (IsWindowsInstaller && IsProductCode && !NoRepair)
            return ("msiexec.exe", $"/fa {KeyName}");
        if (!NoModify && !string.IsNullOrWhiteSpace(ModifyPath))
            return Util.CommandLine.Split(Environment.ExpandEnvironmentVariables(ModifyPath!));
        return null;
    }
}
