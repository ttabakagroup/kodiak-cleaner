namespace KodiakCleaner.Core.Util;

public static class PathSafety
{
    /// <summary>Number of directory levels below the drive/volume root.</summary>
    public static int Depth(string fullPath)
    {
        var root = Path.GetPathRoot(fullPath) ?? "";
        var rest = fullPath[root.Length..];
        return rest.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries).Length;
    }

    /// <summary>
    /// Guards against a mis-expanded rule (empty env var, drive root, ...) wiping a large tree.
    /// Recursive deletes need at least two levels below the root (e.g. C:\Windows\Temp);
    /// non-recursive rules need one level and a specific file pattern.
    /// </summary>
    public static bool IsSafeCleanRoot(string? directory, bool recursive, string pattern)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory)) return false;
        if (directory.Contains('%')) return false; // unexpanded environment variable
        var full = Path.GetFullPath(directory);
        int depth = Depth(full);
        if (recursive) return depth >= 2;
        return depth >= 1 && pattern != "*" && pattern != "*.*";
    }
}
