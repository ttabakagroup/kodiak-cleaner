using KodiakCleaner.Core.Util;

namespace KodiakCleaner.Core.Cleaning;

public sealed record FoundFile(string Path, long Size);

public sealed class AnalysisResult
{
    public List<FoundFile> Files { get; } = new();
    /// <summary>Directories that may be removed once empty, deepest first.</summary>
    public List<string> CandidateDirectories { get; } = new();
    public long TotalBytes => Files.Sum(f => f.Size);
}

public sealed record CleanResult(int FilesDeleted, long BytesFreed, int FilesFailed, int DirectoriesRemoved);

/// <summary>Finds and deletes files described by <see cref="CleanRule"/>s. Never follows symlinks or junctions.</summary>
public static class FileCleaner
{
    public static AnalysisResult Analyze(IEnumerable<CleanRule> rules, CancellationToken ct = default, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var result = new AnalysisResult();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in rules)
        {
            ct.ThrowIfCancellationRequested();
            if (!PathSafety.IsSafeCleanRoot(rule.Directory, rule.Recursive, rule.Pattern)) continue;
            if (!Directory.Exists(rule.Directory)) continue;

            var options = new EnumerationOptions
            {
                RecurseSubdirectories = rule.Recursive,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint, // include hidden/system files, skip links
                ReturnSpecialDirectories = false,
            };

            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(rule.Directory, rule.Pattern, options); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { continue; }

            foreach (var path in SafeEnumerate(files))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var info = new FileInfo(path);
                    if (rule.MinAge is { } minAge)
                    {
                        var newest = info.LastWriteTimeUtc > info.CreationTimeUtc ? info.LastWriteTimeUtc : info.CreationTimeUtc;
                        if (now - newest < minAge) continue;
                    }
                    if (seen.Add(path)) result.Files.Add(new FoundFile(path, info.Length));
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            }

            if (rule.Recursive && rule.RemoveEmptySubdirectories)
            {
                try
                {
                    var dirs = Directory.EnumerateDirectories(rule.Directory, "*", options)
                        .OrderByDescending(d => d.Length);
                    foreach (var d in SafeEnumerate(dirs))
                    {
                        // A program may have just made this folder to use it; respect the same age limit as files.
                        if (rule.MinAge is { } minAge && now - SafeCreationTime(d) < minAge) continue;
                        if (seen.Add(d)) result.CandidateDirectories.Add(d);
                    }
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            }
        }

        result.CandidateDirectories.Sort((a, b) => b.Length.CompareTo(a.Length));
        return result;
    }

    public static CleanResult Clean(AnalysisResult analysis, CancellationToken ct = default)
    {
        int deleted = 0, failed = 0, dirsRemoved = 0;
        long freed = 0;

        foreach (var file in analysis.Files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var info = new FileInfo(file.Path);
                if (!info.Exists) continue;
                if (info.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                if (info.IsReadOnly) info.IsReadOnly = false;
                long size = info.Length;
                info.Delete();
                deleted++;
                freed += size;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                failed++; // typically locked by a running program
            }
        }

        foreach (var dir in analysis.CandidateDirectories)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var info = new DirectoryInfo(dir);
                if (!info.Exists || info.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                if (info.EnumerateFileSystemInfos().Any()) continue;
                info.Delete();
                dirsRemoved++;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }

        return new CleanResult(deleted, freed, failed, dirsRemoved);
    }

    private static DateTime SafeCreationTime(string dir)
    {
        try { return Directory.GetCreationTimeUtc(dir); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return DateTime.MaxValue; } // unknown: keep it
    }

    // Directory enumeration can still throw mid-iteration (e.g. a folder vanishes); stop that rule quietly.
    private static IEnumerable<string> SafeEnumerate(IEnumerable<string> source)
    {
        using var e = source.GetEnumerator();
        while (true)
        {
            string current;
            try
            {
                if (!e.MoveNext()) yield break;
                current = e.Current;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { yield break; }
            yield return current;
        }
    }
}
