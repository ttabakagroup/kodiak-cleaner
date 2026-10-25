namespace KodiakCleaner.Core.Util;

/// <summary>
/// The Favorites at the top of the sidebar (Ted, 2026-10-23): page keys in the order they show. Any page from Favorites or Tools
/// can be pinned or unpinned and moved; pinned pages leave the Tools list. The default is Cleaner, Registry, Startup, Programs.
/// </summary>
public static class SidebarFavorites
{
    /// <summary>The default favorites, always in this order (Ted).</summary>
    public static readonly IReadOnlyList<string> Default = new[] { "cleaner", "registry", "startup", "programs" };

    /// <summary>Saved keys made safe: only pages that exist, each once, in the saved order. Null (never saved) gives the default.</summary>
    public static List<string> Normalize(IEnumerable<string>? saved, ICollection<string> known)
    {
        if (saved is null) return Default.Where(known.Contains).ToList();
        var list = new List<string>();
        foreach (var raw in saved)
        {
            var key = raw.Trim().ToLowerInvariant();
            if (key.Length > 0 && known.Contains(key) && !list.Contains(key)) list.Add(key);
        }
        return list;
    }

    /// <summary>Adds a page at the end, or at <paramref name="index"/> (a drop between two favorites). Already pinned: unchanged.</summary>
    public static bool Pin(List<string> favorites, string key, int index = -1)
    {
        if (favorites.Contains(key)) return false;
        favorites.Insert(index < 0 || index > favorites.Count ? favorites.Count : index, key);
        return true;
    }

    public static bool Unpin(List<string> favorites, string key) => favorites.Remove(key);

    /// <summary>Moves a favorite to a new position (clamped to the list).</summary>
    public static bool Move(List<string> favorites, string key, int index)
    {
        var from = favorites.IndexOf(key);
        if (from < 0) return false;
        index = Math.Clamp(index, 0, favorites.Count - 1);
        if (index == from) return false;
        favorites.RemoveAt(from);
        favorites.Insert(index, key);
        return true;
    }

    /// <summary>
    /// Restore Default Order: Cleaner, Registry, Startup and Programs come back first in that order (pinned again if they were
    /// unpinned), and anything else that was pinned stays after them in the order it had.
    /// </summary>
    public static List<string> RestoreDefaultOrder(IEnumerable<string> favorites, ICollection<string> known)
    {
        var list = Default.Where(known.Contains).ToList();
        foreach (var key in favorites)
            if (!list.Contains(key)) list.Add(key);
        return list;
    }

    /// <summary>What the sidebar shows: the saved order, or A to Z by name when that's chosen in Settings.</summary>
    public static List<T> Arrange<T>(IEnumerable<string> favorites, IReadOnlyDictionary<string, T> pages, Func<T, string> title, bool aToZ)
    {
        var shown = favorites.Where(pages.ContainsKey).Select(k => pages[k]);
        return (aToZ ? shown.OrderBy(title, StringComparer.CurrentCultureIgnoreCase) : shown).ToList();
    }

    /// <summary>
    /// The Tools list: every page not in Favorites, in the order set by dragging (Ted, 2026-10-23), or A to Z when none was set
    /// (<paramref name="toolsOrder"/> null). Pages missing from a saved order (new tools, ones just unpinned) go at the end, A to Z.
    /// </summary>
    public static List<string> ArrangeTools(IEnumerable<string> all, ICollection<string> favorites, IList<string>? toolsOrder, Func<string, string> title)
    {
        var tools = all.Where(k => !favorites.Contains(k)).OrderBy(title, StringComparer.CurrentCultureIgnoreCase).ToList();
        if (toolsOrder is null) return tools;
        var listed = toolsOrder.Select(k => k.Trim().ToLowerInvariant()).Where(tools.Contains).Distinct().ToList();
        return listed.Concat(tools.Where(k => !listed.Contains(k))).ToList();
    }

    // ---- Hidden tools (Ted, 0.9.1 Beta: Settings › Tools turns a tool off, which only takes it off the sidebar) ----

    /// <summary>Saved hidden tools made safe: only pages that exist, each once, lower case.</summary>
    public static HashSet<string> NormalizeHidden(IEnumerable<string>? saved, ICollection<string> known)
    {
        var hidden = new HashSet<string>();
        foreach (var raw in saved ?? Enumerable.Empty<string>())
        {
            var key = raw.Trim().ToLowerInvariant();
            if (key.Length > 0 && known.Contains(key)) hidden.Add(key);
        }
        return hidden;
    }

    /// <summary>
    /// After the shown entries were changed (pinned, unpinned or moved), puts the hidden ones back where they were in
    /// <paramref name="before"/>: each right after the entry it followed (or first), so turning a tool back on returns it to its
    /// old place in Favorites or Tools.
    /// </summary>
    public static List<string> MergeHidden(IEnumerable<string> shown, IEnumerable<string> before, ICollection<string> hidden)
    {
        var result = shown.Where(k => !hidden.Contains(k)).ToList();
        var at = 0;
        foreach (var key in before)
        {
            if (hidden.Contains(key))
            {
                if (result.Contains(key)) continue;
                result.Insert(at, key);
                at++;
            }
            else
            {
                var i = result.IndexOf(key);
                if (i >= 0) at = i + 1;
            }
        }
        return result;
    }
}
