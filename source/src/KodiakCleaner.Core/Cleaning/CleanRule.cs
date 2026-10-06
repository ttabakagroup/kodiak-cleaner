namespace KodiakCleaner.Core.Cleaning;

/// <summary>A set of files to remove: files under <see cref="Directory"/> matching <see cref="Pattern"/>.</summary>
public sealed record CleanRule(
    string Directory,
    string Pattern = "*",
    bool Recursive = true,
    TimeSpan? MinAge = null,
    bool RemoveEmptySubdirectories = true);
