namespace KodiakCleaner.Core.Tools;

public sealed record LargeFile(string Path, long Size, DateTime Modified);

/// <summary>
/// Lists the biggest files under a folder, largest first. Hidden and system files, links and junctions are
/// skipped (Windows needs files like pagefile.sys), and so are the folders passed as protected (Windows, Program Files).
/// </summary>
public static class LargeFileFinder
{
    public static IReadOnlyList<LargeFile> Find(string root, long minBytes, int limit, IEnumerable<string> protectedFolders, CancellationToken ct = default)
    {
        var guarded = protectedFolders.Where(p => !string.IsNullOrEmpty(p))
            .Select(p => Path.TrimEndingDirectorySeparator(Path.GetFullPath(p))).ToList();
        bool IsGuarded(string path) => guarded.Any(g =>
            string.Equals(path, g, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(g + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

        var found = new List<LargeFile>();
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.Hidden | FileAttributes.System,
        };
        var pending = new Stack<DirectoryInfo>();
        pending.Push(new DirectoryInfo(Path.GetFullPath(root)));
        while (pending.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var dir = pending.Pop();
            try
            {
                foreach (var entry in dir.EnumerateFileSystemInfos("*", options))
                {
                    if (entry is DirectoryInfo sub)
                    {
                        if (!IsGuarded(sub.FullName)) pending.Push(sub);
                    }
                    else if (entry is FileInfo file && file.Length >= minBytes)
                    {
                        found.Add(new LargeFile(file.FullName, file.Length, file.LastWriteTime));
                    }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        }
        return found.OrderByDescending(f => f.Size).ThenBy(f => f.Path, StringComparer.OrdinalIgnoreCase).Take(limit).ToList();
    }
}
