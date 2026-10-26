using KodiakCleaner.Core.Util;
using Xunit;

namespace KodiakCleaner.Core.Tests;

/// <summary>The Microsoft Store (MSIX) copy: how it's told apart and what it does differently (2026-10-26).</summary>
public class StoreInstallTests
{
    private const string ProgramFiles = @"C:\Program Files";
    private const string LocalAppData = @"C:\Users\Ann\AppData\Local";
    private const string StoreExe = @"C:\Program Files\WindowsApps\TedTabaka.KodiakCleaner_0.9.3.0_x64__abc123\KodiakCleaner.exe";

    [Fact]
    public void A_packaged_copy_is_the_Store_copy_even_inside_Program_Files()
    {
        Assert.Equal(InstallKind.Store, InstallKinds.Detect(StoreExe, ProgramFiles, packaged: true));
        Assert.Equal(InstallKind.Installed, InstallKinds.Detect(@"C:\Program Files\Kodiak Cleaner\KodiakCleaner.exe", ProgramFiles, packaged: false));
        Assert.Equal(InstallKind.Portable, InstallKinds.Detect(@"D:\Tools\KodiakCleaner.exe", ProgramFiles, packaged: false));
        Assert.Equal(InstallKind.Portable, InstallKinds.Detect(@"C:\Program Files Extra\KodiakCleaner.exe", ProgramFiles, packaged: false));
        Assert.Equal(InstallKind.Installed, InstallKinds.Detect(@"C:\Program Files\Kodiak Cleaner\KodiakCleaner.exe", ProgramFiles + @"\", packaged: false));
    }

    [Fact]
    public void Only_the_Store_copy_starts_through_its_app_alias()
    {
        Assert.Equal(Path.Combine(LocalAppData, "Microsoft", "WindowsApps", "kodiakcleaner.exe"),
            InstallKinds.LaunchPath(InstallKind.Store, StoreExe, LocalAppData));
        Assert.Equal(@"D:\Tools\KodiakCleaner.exe", InstallKinds.LaunchPath(InstallKind.Portable, @"D:\Tools\KodiakCleaner.exe", LocalAppData));
        Assert.Equal(StoreExe, InstallKinds.LaunchPath(InstallKind.Store, StoreExe, "")); // no AppData: never a made-up path
    }

    [Fact]
    public void The_Store_copy_keeps_its_settings_in_the_package_LocalState_folder()
    {
        Assert.Equal(Path.Combine(LocalAppData, "Packages", "TedTabaka.KodiakCleaner_abc123", "LocalState"),
            InstallKinds.SettingsFolder(InstallKind.Store, @"C:\Program Files\WindowsApps\x", LocalAppData, "TedTabaka.KodiakCleaner_abc123"));
        Assert.Equal(@"C:\Program Files\Kodiak Cleaner", InstallKinds.SettingsFolder(InstallKind.Installed, @"C:\Program Files\Kodiak Cleaner", LocalAppData, null));
        Assert.Equal(@"D:\Tools", InstallKinds.SettingsFolder(InstallKind.Portable, @"D:\Tools", LocalAppData, null));
    }

    [Fact]
    public void The_Store_copy_never_updates_itself_or_starts_elevated_without_a_prompt()
    {
        Assert.False(InstallKinds.ChecksForUpdates(InstallKind.Store));
        Assert.False(InstallKinds.InstallsUpdates(InstallKind.Store));
        Assert.True(InstallKinds.InstallsUpdates(InstallKind.Installed));
        Assert.False(InstallKinds.MayStartElevatedWithoutPrompt(InstallKind.Store));
        Assert.False(InstallKinds.MayStartElevatedWithoutPrompt(InstallKind.Portable));
        Assert.True(InstallKinds.MayStartElevatedWithoutPrompt(InstallKind.Installed));
        Assert.False(InstallKinds.CanOpenInSafeMode(InstallKind.Store));
        Assert.True(InstallKinds.UsesDocuments(InstallKind.Store));
        Assert.Equal("Microsoft Store", InstallKinds.Describe(InstallKind.Store));
    }

    /// <summary>The package manifest lives in the repository's packaging/store folder, beside source/.</summary>
    private static string? Manifest()
    {
        var file = Path.Combine(Repo.Root, "..", "packaging", "store", "AppxManifest.xml");
        return File.Exists(file) ? File.ReadAllText(file) : null; // a copy of source/ on its own has no packaging folder
    }

    [Fact]
    public void The_package_manifest_matches_the_app()
    {
        if (Manifest() is not { } xml) return;
        Assert.Contains($"Alias=\"{InstallKinds.StoreAlias}\"", xml);
        // build-msix.ps1 fills these in; a real identity or version must never be committed by hand.
        Assert.Contains("Name=\"PLACEHOLDER-PartnerCenter.KodiakCleaner\"", xml);
        Assert.Contains("Version=\"0.0.0.0\"", xml);
        Assert.Contains("<PublisherDisplayName>Ted Tabaka</PublisherDisplayName>", xml);
        Assert.Contains("<DisplayName>Kodiak Cleaner</DisplayName>", xml);
        Assert.Contains("Executable=\"KodiakCleaner.exe\"", xml);
        // Kodiak Cleaner changes the real registry and AppData, so virtualization is off (needs unvirtualizedResources).
        Assert.Contains("<desktop6:RegistryWriteVirtualization>disabled</desktop6:RegistryWriteVirtualization>", xml);
        Assert.Contains("<rescap:Capability Name=\"unvirtualizedResources\" />", xml);
        Assert.Contains("<rescap:Capability Name=\"runFullTrust\" />", xml);
        // Restart as Administrator is a normal UAC prompt; allowElevation (auto-elevation at launch) is not needed.
        Assert.DoesNotContain("<rescap:Capability Name=\"allowElevation\"", xml);
    }

    [Fact]
    public void The_menu_command_has_one_class_id_everywhere()
    {
        var id = System.Text.RegularExpressions.Regex.Match(Repo.Read("src", "KodiakCleaner.App", "Integration.cs"),
            @"ShellCommandClsid = ""\{([0-9A-F-]{36})\}""").Groups[1].Value;
        Assert.NotEmpty(id);
        var g = Guid.Parse(id).ToByteArray();
        // The C++ form: { 0x04c283a9, 0x1a6f, 0x4898, { 0xbe, ... } }
        var cpp = $"{{ 0x{BitConverter.ToUInt32(g, 0):x8}, 0x{BitConverter.ToUInt16(g, 4):x4}, 0x{BitConverter.ToUInt16(g, 6):x4}, {{ " +
                  string.Join(", ", g[8..].Select(b => $"0x{b:x2}")) + " } }";
        Assert.Contains(cpp, Repo.Read("shell", "KodiakCleanerShell.cpp"));
        if (Manifest() is not { } xml) return;
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(xml, id).Count); // the COM class and the menu verb
        Assert.Contains("Path=\"KodiakCleanerShell.dll\"", xml);
    }
}
