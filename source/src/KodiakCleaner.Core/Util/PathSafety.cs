namespace KodiakCleaner.Core.Util;

public static class PathSafety
{
    /// <summary>Number of directory levels below the drive/volume root.</summary>
    /// <summary>True when <paramref name="path"/> is inside <paramref name="folder"/> (not the folder itself).</summary>
    public static bool IsInside(string path, string folder)
    {
        var p = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var f = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        return p.Length > f.Length && p.StartsWith(f, StringComparison.OrdinalIgnoreCase) &&
               (p[f.Length] == Path.DirectorySeparatorChar || f.EndsWith(Path.DirectorySeparatorChar) || f.EndsWith(Path.VolumeSeparatorChar));
    }

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
    /// non-recursive rules need one level and a specific file pattern, or two levels and a filter that picks the files.
    /// </summary>
    public static bool IsSafeCleanRoot(string? directory, bool recursive, string pattern, bool filtered = false)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory)) return false;
        if (directory.Contains('%')) return false; // unexpanded environment variable
        var full = Path.GetFullPath(directory);
        int depth = Depth(full);
        if (recursive) return depth >= 2;
        if (filtered && depth >= 2) return true;
        return depth >= 1 && pattern != "*" && pattern != "*.*";
    }

    /// <summary>
    /// True only when a path clearly leads nowhere: a full path on a fixed drive that is ready, with neither a file nor a folder there.
    /// Empty paths, unexpanded variables, relative paths, network paths and removable or sleeping drives are never "missing".
    /// </summary>
    public static bool IsMissingOnFixedDrive(string path)
    {
        path = path.Trim().Trim('"');
        if (path.Length == 0 || path.Contains('%') || !Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal)) return false;
        var root = Path.GetPathRoot(path);
        if (string.IsNullOrEmpty(root)) return false;
        try
        {
            var drive = new DriveInfo(root);
            if (drive.DriveType != DriveType.Fixed || !drive.IsReady) return false;
        }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException) { return false; }
        return !File.Exists(path) && !Directory.Exists(path);
    }

    /// <summary>
    /// A PATH value without the entries <paramref name="isMissing"/> picks, and the entries removed. Empty entries go too, but only
    /// when something was removed; otherwise the value comes back exactly as it was.
    /// </summary>
    public static (string Value, IReadOnlyList<string> Removed) RemoveMissingPathEntries(string value, Func<string, bool> isMissing)
    {
        var entries = value.Split(';');
        var removed = entries.Where(e => e.Trim().Length > 0 && isMissing(e)).ToList();
        if (removed.Count == 0) return (value, removed);
        var kept = entries.Where(e => e.Trim().Length > 0 && !removed.Contains(e));
        return (string.Join(";", kept), removed);
    }
}
