namespace KodiakCleaner.Core.Cleaning;

/// <summary>A set of files to remove: files under <see cref="Directory"/> matching <see cref="Pattern"/>.</summary>
/// <param name="Filter">When set, only files it returns true for are removed (the extension lists of the Downloads items, broken shortcuts).</param>
public sealed record CleanRule(
    string Directory,
    string Pattern = "*",
    bool Recursive = true,
    TimeSpan? MinAge = null,
    bool RemoveEmptySubdirectories = true,
    Func<string, bool>? Filter = null);
