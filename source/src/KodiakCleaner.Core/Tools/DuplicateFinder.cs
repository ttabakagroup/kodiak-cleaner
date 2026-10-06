using System.Security.Cryptography;

namespace KodiakCleaner.Core.Tools;

public sealed record DuplicateFile(string Path, long Size, DateTime LastWriteUtc);

public sealed record DuplicateGroup(string Hash, long Size, IReadOnlyList<DuplicateFile> Files)
{
    /// <summary>Space recovered by keeping one copy.</summary>
    public long WastedBytes => Size * (Files.Count - 1);
}

public sealed class DuplicateSearchOptions
{
    public required IReadOnlyList<string> Folders { get; init; }
    public long MinSize { get; init; } = 1;
    public long MaxSize { get; init; } = long.MaxValue;
    public bool IncludeHidden { get; init; }
    public bool IncludeSystem { get; init; }
    /// <summary>When true, files must also have the same name to count as duplicates.</summary>
    public bool MatchName { get; init; }
}

public sealed record DuplicateProgress(string Stage, int FilesScanned, int Done, int Total);

/// <summary>
/// Finds identical files: group by size, then by a hash of the first 64 KB, then by a full SHA-256.
/// Never follows symlinks/junctions and never reports the same physical path twice.
/// </summary>
public static class DuplicateFinder
{
    private const int PartialBytes = 64 * 1024;

    public static List<DuplicateGroup> Find(DuplicateSearchOptions options, IProgress<DuplicateProgress>? progress = null, CancellationToken ct = default)
    {
        var skip = FileAttributes.ReparsePoint;
        if (!options.IncludeHidden) skip |= FileAttributes.Hidden;
        if (!options.IncludeSystem) skip |= FileAttributes.System;
        var enumeration = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = skip,
        };

        // 1. Collect candidate files grouped by size (and name when requested).
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var bySize = new Dictionary<(long, string), List<DuplicateFile>>();
        int scanned = 0;
        foreach (var folder in options.Folders.Where(Directory.Exists))
        {
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(folder, "*", enumeration); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { continue; }

            foreach (var path in Safe(files))
            {
                ct.ThrowIfCancellationRequested();
                FileInfo info;
                try { info = new FileInfo(path); if (!info.Exists) continue; }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { continue; }
                if (info.Length < options.MinSize || info.Length > options.MaxSize) continue;
                if (!seen.Add(info.FullName)) continue;
                var key = (info.Length, options.MatchName ? info.Name.ToLowerInvariant() : "");
                if (!bySize.TryGetValue(key, out var list)) bySize[key] = list = new List<DuplicateFile>();
                list.Add(new DuplicateFile(info.FullName, info.Length, info.LastWriteTimeUtc));
                if (++scanned % 500 == 0) progress?.Report(new DuplicateProgress("Scanning", scanned, 0, 0));
            }
        }

        var candidates = bySize.Values.Where(l => l.Count > 1).ToList();
        int total = candidates.Sum(l => l.Count), done = 0;
        var groups = new List<DuplicateGroup>();

        // 2. Narrow by partial hash, 3. confirm by full hash.
        foreach (var sameSize in candidates)
        {
            ct.ThrowIfCancellationRequested();
            foreach (var partial in GroupByHash(sameSize, PartialBytes, ct).Where(g => g.Count > 1))
            {
                var needFull = partial[0].Size > PartialBytes;
                var finals = needFull ? GroupByHash(partial, long.MaxValue, ct) : new List<List<DuplicateFile>> { partial };
                foreach (var g in finals.Where(g => g.Count > 1))
                    groups.Add(new DuplicateGroup(HashOf(g[0].Path, long.MaxValue) ?? "", g[0].Size,
                        g.OrderBy(f => f.LastWriteUtc).ToList()));
            }
            done += sameSize.Count;
            progress?.Report(new DuplicateProgress("Comparing", scanned, done, total));
        }

        return groups.OrderByDescending(g => g.WastedBytes).ToList();
    }

    private static List<List<DuplicateFile>> GroupByHash(List<DuplicateFile> files, long bytes, CancellationToken ct)
    {
        var map = new Dictionary<string, List<DuplicateFile>>();
        foreach (var f in files)
        {
            ct.ThrowIfCancellationRequested();
            var h = HashOf(f.Path, bytes);
            if (h is null) continue; // unreadable (locked, permission)
            if (!map.TryGetValue(h, out var list)) map[h] = list = new List<DuplicateFile>();
            list.Add(f);
        }
        return map.Values.ToList();
    }

    internal static string? HashOf(string path, long maxBytes)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16, FileOptions.SequentialScan);
            using var sha = SHA256.Create();
            if (maxBytes == long.MaxValue) return Convert.ToHexString(sha.ComputeHash(stream));
            var buffer = new byte[(int)Math.Min(maxBytes, stream.Length)];
            int read = 0, n;
            while (read < buffer.Length && (n = stream.Read(buffer, read, buffer.Length - read)) > 0) read += n;
            return Convert.ToHexString(sha.ComputeHash(buffer, 0, read));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static IEnumerable<string> Safe(IEnumerable<string> source)
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
