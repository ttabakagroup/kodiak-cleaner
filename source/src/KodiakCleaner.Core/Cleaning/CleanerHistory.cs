using System.Globalization;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.Core.Cleaning;

/// <summary>How the Cleaner list is ordered (Ted, 0.8.21). Default (0.8.23) is the standing order: Windows first, then programs A to Z.</summary>
public enum CleanSort
{
    Default,
    /// <summary>Every group A to Z, Windows included (not pinned first).</summary>
    AToZ,
    LastCleaned,
    MostSpaceFreed,
    LargestFound,
}

/// <summary>What Kodiak Cleaner remembers about one item: when it was last cleaned, what that freed, and the total so far.</summary>
public sealed record CleanStat(DateTime LastCleanedUtc, long LastFreed, long TotalFreed)
{
    public CleanStat Add(DateTime nowUtc, long freed) => new(nowUtc, freed, TotalFreed + freed);
}

/// <summary>Every item's <see cref="CleanStat"/>, kept in KodiakCleaner.ini under [Cleaner History] as id=time;last;total.</summary>
public sealed class CleanerHistory
{
    public const string Section = "Cleaner History";

    private readonly Dictionary<string, CleanStat> _stats = new(StringComparer.OrdinalIgnoreCase);

    public CleanStat? this[string id] => _stats.TryGetValue(id, out var s) ? s : null;
    public int Count => _stats.Count;

    public void Record(string id, DateTime nowUtc, long freed) =>
        _stats[id] = (this[id] ?? new CleanStat(nowUtc, 0, 0)).Add(nowUtc, Math.Max(0, freed));

    public static CleanerHistory Read(IniFile ini)
    {
        var h = new CleanerHistory();
        foreach (var (id, value) in ini.Section(Section))
        {
            var parts = value.Split(';');
            if (parts.Length == 3 &&
                DateTime.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var when) &&
                long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var last) &&
                long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var total))
                h._stats[id] = new CleanStat(when, last, total);
        }
        return h;
    }

    public void Write(IniFile ini)
    {
        foreach (var (id, s) in _stats.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            ini.Set(Section, id, string.Create(CultureInfo.InvariantCulture, $"{s.LastCleanedUtc:yyyy-MM-ddTHH:mm:ssZ};{s.LastFreed};{s.TotalFreed}"));
    }

    /// <summary>"Today", "Yesterday", "5 days ago", or the date; "Never" when it hasn't been cleaned.</summary>
    public static string Describe(CleanStat? s, DateTime nowLocal)
    {
        if (s is null) return "Never";
        var days = (nowLocal.Date - s.LastCleanedUtc.ToLocalTime().Date).Days;
        return days switch
        {
            <= 0 => "Today",
            1 => "Yesterday",
            < 31 => $"{days} days ago",
            _ => s.LastCleanedUtc.ToLocalTime().ToString("d MMM yyyy", CultureInfo.CurrentCulture),
        };
    }
}
