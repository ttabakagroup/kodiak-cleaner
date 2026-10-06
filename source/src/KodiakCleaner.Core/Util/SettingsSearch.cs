namespace KodiakCleaner.Core.Util;

/// <summary>
/// The Settings search box (Ted, 0.8.45): a page matches when every word typed appears somewhere in its title, description or
/// the labels on the page, in any order and case ("dark mode" finds Appearance's "light or dark mode").
/// </summary>
public static class SettingsSearch
{
    public static bool Matches(string? query, IEnumerable<string> phrases)
    {
        var words = Words(query);
        if (words.Length == 0) return true;
        var all = string.Join("\n", phrases);
        return words.All(w => all.Contains(w, StringComparison.CurrentCultureIgnoreCase));
    }

    /// <summary>Up to <paramref name="max"/> of the page's own labels that contain a typed word, shown under the page name.</summary>
    public static IReadOnlyList<string> Hints(string? query, IEnumerable<string> labels, int max = 2)
    {
        var words = Words(query);
        if (words.Length == 0) return [];
        // Setting names first; the longer help sentences only when no name matches.
        return labels.Where(l => words.Any(w => l.Contains(w, StringComparison.CurrentCultureIgnoreCase)))
                     .Distinct(StringComparer.CurrentCultureIgnoreCase)
                     .OrderBy(l => l.Length > ShortLabel).Take(max).ToList();
    }

    private const int ShortLabel = 40;

    private static string[] Words(string? query) =>
        (query ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
