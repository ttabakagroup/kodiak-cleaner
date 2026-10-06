using System.Text.Json;
using System.Text.RegularExpressions;
using KodiakCleaner.Core.About;

namespace KodiakCleaner.Core.Updates;

/// <summary>The newest release for a channel, as the website's update file describes it.</summary>
public sealed record UpdateInfo(Version Version, string MsiUrl, string Sha256);

/// <summary>
/// Reads the update file Kodiak Cleaner's website publishes for each channel, for example
/// https://www.kodiakcleaner.app/updates/dev.json. Since 0.8.40 updates come from the website rather than GitHub,
/// so they work while the GitHub repository is private (Ted). The file looks like:
/// <code>
/// { "channel": "dev", "version": "0.8.40",
///   "msi": { "url": "https://www.kodiakcleaner.app/download/KodiakCleaner-Installer.msi", "sha256": "64 hex digits" } }
/// </code>
/// </summary>
public static class UpdateManifest
{
    /// <summary>The only host an update file or installer is accepted from.</summary>
    public const string Host = "www.kodiakcleaner.app";

    public static string Url(ReleaseChannel channel) => $"https://{Host}/updates/{channel.ToString().ToLowerInvariant()}.json";

    /// <summary>
    /// The release the file describes, or null when it is for another channel, is incomplete, has no SHA-256, or points
    /// anywhere but an .msi on <see cref="Host"/> over HTTPS. Malformed JSON throws <see cref="JsonException"/>.
    /// </summary>
    public static UpdateInfo? Parse(string json, ReleaseChannel channel)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return null;
        if (Text(root, "channel") is not { } ch || !string.Equals(ch, channel.ToString(), StringComparison.OrdinalIgnoreCase)) return null;
        if (Text(root, "version") is not { } v || !TryParseVersion(v, out var version)) return null;
        if (!root.TryGetProperty("msi", out var msi) || msi.ValueKind != JsonValueKind.Object) return null;
        if (Text(msi, "url") is not { } url || !IsOurs(url, ".msi")) return null;
        if (Text(msi, "sha256") is not { } sha || !Regex.IsMatch(sha, "^[0-9A-Fa-f]{64}$")) return null;
        return new UpdateInfo(version, url, sha.ToUpperInvariant());
    }

    private static string? Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static bool IsOurs(string url, string extension) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps &&
        string.Equals(u.Host, Host, StringComparison.OrdinalIgnoreCase) && u.IsDefaultPort &&
        u.AbsolutePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase);

    public static bool TryParseVersion(string tag, out Version version)
    {
        var t = tag.Trim().TrimStart('v', 'V');
        int dash = t.IndexOf('-');
        if (dash >= 0) t = t[..dash];
        if (Version.TryParse(t.Count(c => c == '.') == 1 ? t + ".0" : t, out var v))
        {
            version = new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
            return true;
        }
        version = new Version(0, 0, 0);
        return false;
    }

    public static bool IsNewer(UpdateInfo info, Version current) =>
        info.Version > new Version(current.Major, current.Minor, Math.Max(current.Build, 0));
}
