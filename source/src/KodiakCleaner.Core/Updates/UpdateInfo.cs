using System.Text.Json;
using KodiakCleaner.Core.About;

namespace KodiakCleaner.Core.Updates;

public sealed record UpdateAsset(string Name, string DownloadUrl, long Size, string? Sha256);

public sealed record UpdateInfo(Version Version, string Tag, string PageUrl, IReadOnlyList<UpdateAsset> Assets)
{
    /// <summary>
    /// The installer: releases ship it zipped (KodiakCleaner-Dev-Setup.zip since 0.8.31, KodiakCleaner-0.8.30-x64-msi-dev.zip before;
    /// Ted's rule); a bare .msi is accepted too.
    /// </summary>
    public UpdateAsset? Msi =>
        Assets.FirstOrDefault(a => a.Name.EndsWith("-Setup.zip", StringComparison.OrdinalIgnoreCase))
        ?? Assets.FirstOrDefault(a => a.Name.Contains("-msi", StringComparison.OrdinalIgnoreCase) && a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        ?? Assets.FirstOrDefault(a => a.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase));
}

/// <summary>Reads GitHub's release list API response (GET /repos/{owner}/{repo}/releases).</summary>
public static class UpdateParser
{
    /// <summary>
    /// The newest published release for <paramref name="channel"/>: stable tags look like v1.0.2,
    /// dev and beta tags end in -dev / -beta. Drafts are skipped. Each channel only ever updates to its own releases.
    /// </summary>
    public static UpdateInfo? PickLatest(string releasesJson, ReleaseChannel channel)
    {
        using var doc = JsonDocument.Parse(releasesJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;
        return doc.RootElement.EnumerateArray()
            .Select(r => Parse(r, channel))
            .Where(r => r is not null)
            .MaxBy(r => r!.Version);
    }

    private static UpdateInfo? Parse(JsonElement root, ReleaseChannel channel)
    {
        if (root.TryGetProperty("draft", out var d) && d.ValueKind == JsonValueKind.True) return null;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("tag_name", out var t) || t.ValueKind != JsonValueKind.String) return null;
        var tag = t.GetString() ?? "";
        if (ReleaseChannels.FromTag(tag) != channel) return null;
        // A stable channel never picks up something GitHub marks as a pre-release.
        if (channel == ReleaseChannel.Stable && root.TryGetProperty("prerelease", out var p) && p.ValueKind == JsonValueKind.True) return null;
        if (!TryParseVersion(tag, out var version)) return null;
        var page = root.TryGetProperty("html_url", out var h) && h.ValueKind == JsonValueKind.String ? h.GetString() ?? "" : "";

        var assets = new List<UpdateAsset>();
        if (root.TryGetProperty("assets", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in arr.EnumerateArray())
            {
                // Skip anything incomplete rather than failing the whole check.
                if (a.ValueKind != JsonValueKind.Object ||
                    !a.TryGetProperty("name", out var n) || n.ValueKind != JsonValueKind.String ||
                    !a.TryGetProperty("browser_download_url", out var u) || u.ValueKind != JsonValueKind.String) continue;
                string? sha = null;
                // GitHub reports "digest": "sha256:<hex>" for release assets.
                if (a.TryGetProperty("digest", out var dg) && dg.ValueKind == JsonValueKind.String && dg.GetString() is { } digest &&
                    digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                    sha = digest[7..].ToUpperInvariant();
                assets.Add(new UpdateAsset(
                    n.GetString() ?? "",
                    u.GetString() ?? "",
                    a.TryGetProperty("size", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt64() : 0,
                    sha));
            }
        }
        return new UpdateInfo(version, tag, page, assets);
    }

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
