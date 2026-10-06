namespace KodiakCleaner.Core.About;

public enum ReleaseChannel
{
    Dev,
    Beta,
    Stable,
}

/// <summary>
/// Names and version labels for the three release channels. Each channel has its own number range
/// (Dev 0.8.x, Beta 0.9.x, Stable 1.x and up), its own name and its own install, so all three can sit side by side.
/// </summary>
public static class ReleaseChannels
{
    public static ReleaseChannel Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "beta" => ReleaseChannel.Beta,
        "stable" => ReleaseChannel.Stable,
        _ => ReleaseChannel.Dev,
    };

    /// <summary>"Dev", "Beta" or "Stable".</summary>
    public static string Label(ReleaseChannel channel) => channel.ToString();

    /// <summary>"Kodiak Cleaner Dev", "Kodiak Cleaner Beta" or "Kodiak Cleaner".</summary>
    public static string AppName(string baseName, ReleaseChannel channel) =>
        channel == ReleaseChannel.Stable ? baseName : $"{baseName} {Label(channel)}";

    /// <summary>
    /// The version people see: "0.8.3 Dev", "0.9.4 Beta", "1.0.2". Once a stable release exists
    /// (the target is past 1.0.0), dev and beta builds also say which release they lead to: "0.9.12 Beta (for 1.2.0)".
    /// </summary>
    public static string VersionName(Version version, ReleaseChannel channel, Version? targetStable)
    {
        var v = version.ToString(3);
        if (channel == ReleaseChannel.Stable) return v;
        var name = $"{v} {Label(channel)}";
        return targetStable is not null && targetStable > new Version(1, 0, 0) ? $"{name} (for {targetStable.ToString(3)})" : name;
    }

    /// <summary>Git tag for a release: v0.8.4-dev, v0.9.0-beta, v1.0.2.</summary>
    public static string Tag(Version version, ReleaseChannel channel) =>
        channel == ReleaseChannel.Stable ? $"v{version.ToString(3)}" : $"v{version.ToString(3)}-{channel.ToString().ToLowerInvariant()}";

    /// <summary>Which channel a git tag belongs to (v1.0.2 = Stable, v0.8.4-dev = Dev).</summary>
    public static ReleaseChannel? FromTag(string tag)
    {
        var dash = tag.IndexOf('-');
        if (dash < 0) return ReleaseChannel.Stable;
        return tag[(dash + 1)..].ToLowerInvariant() switch
        {
            "dev" => ReleaseChannel.Dev,
            "beta" => ReleaseChannel.Beta,
            _ => null,
        };
    }
}
