namespace KodiakCleaner.App;

public sealed record Credit(string Name, string Use, string License, string Url);

/// <summary>Every third-party font, library and tool Kodiak Cleaner uses. Add new credits here; About lists them from this one place.</summary>
public static class Credits
{
    public static IReadOnlyList<Credit> All { get; } = new[]
    {
        new Credit("Fredoka", "The font for all text, Regular and SemiBold, built into the app with its license", "SIL Open Font License 1.1", "https://github.com/hafontia/Fredoka-One"),
        new Credit(".NET and WPF", "The runtime and user interface framework", "MIT License", "https://github.com/dotnet/wpf"),
        new Credit("WiX Toolset", "Builds the MSI installer (not part of the app itself)", "Microsoft Reciprocal License", "https://wixtoolset.org"),
        new Credit("Segoe Fluent Icons", "Icons, from the font that comes with Windows (not bundled)", "Part of Windows", "https://learn.microsoft.com/windows/apps/design/style/segoe-fluent-icons-font"),
    };
}
