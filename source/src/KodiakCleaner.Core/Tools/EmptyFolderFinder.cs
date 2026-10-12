namespace KodiakCleaner.Core.Tools;

/// <summary>
/// Finds folders with no files anywhere inside them. Only the outermost empty folder is listed (its empty
/// subfolders go with it). Links, junctions and folders marked as system folders are never listed or entered,
/// and the chosen folder itself is never listed. Protected folders (AppData, Windows, program folders) found
/// inside the chosen folder are never entered, and a folder holding one is never called empty. Hidden folders are never
/// entered either: programs keep needed empty folders in them (a .git folder, for one). A whole drive can be searched
/// (Ted, 0.8.65); Windows' own folders at the top of a drive (Boot, Recovery, PerfLogs, $-folders, ...) are skipped.
/// </summary>
public static class EmptyFolderFinder
{
    /// <summary>Windows itself, drive roots and the app's own folders are refused as starting points.</summary>
    public static string? WhyNotAllowed(string root, IEnumerable<string> protectedFolders, bool allowDrive = false)
    {
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root)) return "Choose a full folder path.";
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (Util.PathSafety.Depth(full) == 0) return allowDrive ? null : "Choose a folder, not a whole drive.";
        if (full.Split(Path.DirectorySeparatorChar).Any(IsAppData))
            return "Programs keep their settings in AppData and can need empty folders there, so it isn't searched.";
        foreach (var p in protectedFolders.Where(p => !string.IsNullOrEmpty(p)))
        {
            var guarded = Path.TrimEndingDirectorySeparator(Path.GetFullPath(p));
            if (string.Equals(full, guarded, StringComparison.OrdinalIgnoreCase) ||
                full.StartsWith(guarded + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return $"Windows and program folders can need empty folders, so {guarded} isn't searched.";
        }
        return null;
    }

    public static IReadOnlyList<string> Find(string root, CancellationToken ct = default) =>
        Find(root, Array.Empty<string>(), ct);

    public static IReadOnlyList<string> Find(string root, IEnumerable<string> protectedFolders, CancellationToken ct = default) =>
        Find(root, protectedFolders, Array.Empty<string>(), ct);

    /// <param name="keepFolders">Folders searched inside but never listed themselves, such as Desktop and Documents.</param>
    public static IReadOnlyList<string> Find(string root, IEnumerable<string> protectedFolders, IEnumerable<string> keepFolders, CancellationToken ct = default)
    {
        var guarded = Guarded(protectedFolders);
        var keep = Guarded(keepFolders);
        var found = new List<string>();
        var top = new DirectoryInfo(Path.GetFullPath(root));
        var isDrive = top.Parent is null;
        foreach (var sub in SafeSubfolders(top))
        {
            if (isDrive && IsDriveSystemFolder(sub.Name)) continue;
            if (IsEmpty(sub, found, guarded, keep, ct)) found.Add(sub.FullName);
        }
        found.Sort(StringComparer.OrdinalIgnoreCase);
        return found;
    }

    /// <summary>Checks again just before removing: still there, still holding no files, and not a link, system or protected folder.</summary>
    public static bool IsStillEmpty(string path) => IsStillEmpty(path, Array.Empty<string>());

    public static bool IsStillEmpty(string path, IEnumerable<string> protectedFolders, IEnumerable<string>? keepFolders = null)
    {
        if (WhyNotAllowed(path, protectedFolders) is not null) return false;
        var dir = new DirectoryInfo(path);
        if (dir.Parent?.Parent is null && IsDriveSystemFolder(dir.Name)) return false;
        return dir.Exists && IsEmpty(dir, new List<string>(), Guarded(protectedFolders), Guarded(keepFolders ?? Array.Empty<string>()), CancellationToken.None);
    }

    private static HashSet<string> Guarded(IEnumerable<string> protectedFolders) =>
        protectedFolders.Where(p => !string.IsNullOrWhiteSpace(p) && Path.IsPathFullyQualified(p))
            .Select(p => Path.TrimEndingDirectorySeparator(Path.GetFullPath(p)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>True when the folder holds no files at any depth. Adds the outermost empty folders to <paramref name="found"/>.</summary>
    private static bool IsEmpty(DirectoryInfo dir, List<string> found, HashSet<string> guarded, HashSet<string> keep, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (dir.Attributes.HasFlag(FileAttributes.ReparsePoint) || dir.Attributes.HasFlag(FileAttributes.System) ||
            dir.Attributes.HasFlag(FileAttributes.Hidden)) return false;
        if (guarded.Contains(Path.TrimEndingDirectorySeparator(dir.FullName))) return false; // treat as holding files
        if (IsAppData(dir.Name)) return false; // every user's AppData, not only the one passed in (C:\Users holds them all)

        bool empty = true;
        var emptyChildren = new List<string>();
        try
        {
            foreach (var entry in dir.EnumerateFileSystemInfos())
            {
                if (entry is DirectoryInfo sub)
                {
                    var childFound = new List<string>();
                    if (IsEmpty(sub, childFound, guarded, keep, ct)) emptyChildren.Add(sub.FullName);
                    else
                    {
                        empty = false;
                        found.AddRange(childFound);
                    }
                }
                else empty = false;
            }
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            return false; // can't see inside, so never call it empty
        }

        // A kept folder (Desktop, Documents, ...) is never listed, but empty folders inside it still are.
        if (empty && !keep.Contains(Path.TrimEndingDirectorySeparator(dir.FullName))) return true;
        found.AddRange(emptyChildren);
        return false;
    }

    /// <summary>Folders at the top of a drive that Windows, its setup or drivers make and may need, even when empty.</summary>
    private static readonly HashSet<string> DriveSystemFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Boot", "EFI", "Recovery", "PerfLogs", "inetpub", "Windows.old", "MSOCache", "Config.Msi", "OneDriveTemp", "Intel", "AMD",
        "NVIDIA", "Drivers", "ESD", "System Volume Information", "Documents and Settings", "ProgramData", "Program Files",
        "Program Files (x86)", "Windows", "WinREAgent",
    };

    internal static bool IsDriveSystemFolder(string name) => name.StartsWith('$') || DriveSystemFolders.Contains(name);

    private static bool IsAppData(string name) => string.Equals(name, "AppData", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<DirectoryInfo> SafeSubfolders(DirectoryInfo dir)
    {
        try { return dir.EnumerateDirectories().ToList(); }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException or System.Security.SecurityException) { return Array.Empty<DirectoryInfo>(); }
    }
}
