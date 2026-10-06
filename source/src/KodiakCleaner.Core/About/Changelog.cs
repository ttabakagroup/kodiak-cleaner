using System.Text.RegularExpressions;

namespace KodiakCleaner.Core.About;

public sealed record ChangelogSection(string Title, IReadOnlyList<string> Items);

public sealed record ChangelogRelease(Version Version, string Date, IReadOnlyList<ChangelogSection> Sections, string Label = "")
{
    /// <summary>The heading as written in CHANGELOG.md, e.g. "0.8.3 Dev".</summary>
    public string VersionName => Label.Length == 0 ? Version.ToString(3) : $"{Version.ToString(3)} {Label}";
    public string Title => $"Version {VersionName}";
}

/// <summary>
/// Reads CHANGELOG.md in the "Keep a Changelog" format:
/// <c>## [1.1.0] - 2026-10-03</c> or <c>## [0.8.3 Dev] - 2026-10-04</c>, then <c>### Added</c> and <c>- item</c> lines.
/// An <c>## [Unreleased]</c> section is ignored.
/// </summary>
public static partial class Changelog
{
    // "## [0.8.3 Dev] - 2026-10-04", "## [1.0.2] — 2026-11-01", "## 2.0 - today"
    [GeneratedRegex(@"^##\s+\[?v?(?<ver>\d+\.\d+(?:\.\d+)?)(?:\s+(?<label>[^\]\-–—][^\]]*?))?\]?\s*(?:[-–—]\s*(?<date>.+))?$")]
    private static partial Regex ReleaseHeading();

    public static List<ChangelogRelease> Parse(string markdown)
    {
        var releases = new List<ChangelogRelease>();
        Version? version = null;
        string date = "", label = "";
        var sections = new List<ChangelogSection>();
        string? sectionTitle = null;
        var items = new List<string>();

        void FlushSection()
        {
            if (items.Count > 0) sections.Add(new ChangelogSection(sectionTitle ?? "Changes", items.ToList()));
            items.Clear();
            sectionTitle = null;
        }

        void FlushRelease()
        {
            FlushSection();
            if (version is not null && sections.Count > 0) releases.Add(new ChangelogRelease(version, date, sections.ToList(), label));
            sections.Clear();
            version = null;
        }

        foreach (var raw in markdown.Split('\n'))
        {
            var line = raw.TrimEnd('\r').Trim();
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                FlushRelease();
                var m = ReleaseHeading().Match(line);
                if (m.Success && Version.TryParse(Normalize(m.Groups["ver"].Value), out var v))
                {
                    version = v;
                    date = m.Groups["date"].Value.Trim();
                    label = m.Groups["label"].Value.Trim();
                }
            }
            else if (version is null)
            {
                continue; // preamble or [Unreleased]
            }
            else if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                FlushSection();
                sectionTitle = line[4..].Trim();
            }
            else if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
            {
                items.Add(line[2..].Trim());
            }
            else if (line.Length > 0 && items.Count > 0)
            {
                items[^1] += " " + line; // wrapped bullet
            }
        }
        FlushRelease();
        return releases.OrderByDescending(r => r.Version).ToList();
    }

    /// <summary>Releases newer than <paramref name="lastSeen"/> up to and including <paramref name="current"/>, newest first.</summary>
    public static List<ChangelogRelease> Since(IEnumerable<ChangelogRelease> releases, Version? lastSeen, Version current)
    {
        var cur = Normalize(current);
        var seen = lastSeen is null ? null : Normalize(lastSeen);
        return releases
            .Where(r => (seen is null || Normalize(r.Version) > seen) && Normalize(r.Version) <= cur)
            .OrderByDescending(r => r.Version)
            .ToList();
    }

    // Compare on major.minor.build only, so 1.1 == 1.1.0 and 1.1.0.0.
    public static Version Normalize(Version v) => new(v.Major, Math.Max(v.Minor, 0), Math.Max(v.Build, 0));

    private static string Normalize(string s) => s.Count(c => c == '.') == 1 ? s + ".0" : s;
}
