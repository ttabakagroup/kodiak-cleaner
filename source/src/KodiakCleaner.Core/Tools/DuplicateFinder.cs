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
    /// <summary>
    /// Folders never searched, as a starting point or inside one (Windows, program folders). Every user's AppData,
    /// the Recycle Bin and System Volume Information are always skipped too, so a whole drive can be searched safely.
    /// </summary>
    public IReadOnlyList<string> Protected { get; init; } = Array.Empty<string>();
    /// <summary>In-depth search: also look for files that are alike without being identical (<see cref="SimilarFinder"/>).</summary>
    public bool FindSimilar { get; init; }
    /// <summary>Lowest likeness (0 to 1) a pair of files needs to be listed as similar.</summary>
    public double MinSimilarity { get; init; } = 0.6;
    /// <summary>In-depth: files whose names are alike count as similar on their names alone (contents may differ).</summary>
    public bool MatchNames { get; init; }
    /// <summary>Lowest name likeness (0 to 1) for <see cref="MatchNames"/>.</summary>
    public double MinNameSimilarity { get; init; } = 0.8;
    /// <summary>What to leave out when comparing names, and name patterns that gather files into sets.</summary>
    public NameRules Names { get; init; } = NameRules.Default;
    /// <summary>
    /// Lists a chosen root straight from its drive's file table, or returns null to have the folders walked. The default reads
    /// whole NTFS drives as an administrator (<see cref="NtfsFileTable.TryListDrive"/>); tests point it at a drive image.
    /// </summary>
    internal Func<string, Func<string, FileAttributes, bool>, CancellationToken, List<NtfsFileTable.TableFile>?> FileTable { get; init; } = NtfsFileTable.TryListDrive;
    /// <summary>
    /// Reads a picture's 64-bit difference hash (9x8 grayscale, each bit = left pixel brighter than its neighbour), or null
    /// when it can't be read. Supplied by the app, which has the Windows picture decoders; without it pictures are compared by bytes only.
    /// </summary>
    public Func<string, ulong?>? PictureHash { get; init; }
}

public sealed record DuplicateProgress(string Stage, int FilesScanned, int Done, int Total)
{
    /// <summary>The drive or folder being read, for the FileTable stage.</summary>
    public string Folder { get; init; } = "";
}

/// <summary>What one search found: identical sets, and (in-depth) sets of files that are alike.</summary>
/// <param name="FastDrives">Whole drives read straight from their file table.</param>
/// <param name="SlowDrives">Whole drives whose file table couldn't be read (no administrator rights, not NTFS), so their folders were walked.</param>
public sealed record DuplicateResults(IReadOnlyList<DuplicateGroup> Identical, IReadOnlyList<SimilarGroup> Similar, int FilesScanned, int SkippedRoots,
    int FastDrives = 0, int SlowDrives = 0);

/// <summary>
/// Finds identical files: group by size, then by a hash of the first 64 KB, then by a full SHA-256.
/// Never follows symlinks/junctions and never reports the same physical path twice.
/// </summary>
public static class DuplicateFinder
{
    private const int PartialBytes = 64 * 1024;

    public static List<DuplicateGroup> Find(DuplicateSearchOptions options, IProgress<DuplicateProgress>? progress = null, CancellationToken ct = default)
    {
        var files = Scan(options, progress, ct, out _, out _, out _);
        return FindIdentical(files, options, progress, ct);
    }

    /// <summary>Identical files, then (when <see cref="DuplicateSearchOptions.FindSimilar"/> is on) files that are alike.</summary>
    public static DuplicateResults Search(DuplicateSearchOptions options, IProgress<DuplicateProgress>? progress = null, CancellationToken ct = default)
    {
        var files = Scan(options, progress, ct, out var skipped, out var fast, out var slow);
        var identical = FindIdentical(files, options, progress, ct);
        var similar = options.FindSimilar
            ? SimilarFinder.Find(files, identical, options, progress, ct)
            : new List<SimilarGroup>();
        return new DuplicateResults(identical, similar, files.Count, skipped, fast, slow);
    }

    /// <summary>Every file in the chosen folders and drives, each physical path once, protected folders left out.</summary>
    internal static List<DuplicateFile> Scan(DuplicateSearchOptions options, IProgress<DuplicateProgress>? progress, CancellationToken ct,
        out int skippedRoots, out int fastDrives, out int slowDrives)
    {
        fastDrives = slowDrives = 0;
        var skip = FileAttributes.ReparsePoint;
        if (!options.IncludeHidden) skip |= FileAttributes.Hidden;
        if (!options.IncludeSystem) skip |= FileAttributes.System;
        var enumeration = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = skip };
        var guarded = options.Protected.Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => Path.TrimEndingDirectorySeparator(Path.GetFullPath(p))).ToList();

        // Roots are followed to where they really are, so a folder reached through a link and directly is one folder:
        // otherwise each file there would be listed as its own duplicate and removing "the copy" would remove the only one.
        // A root inside another chosen root (a folder on a chosen drive) is already covered, so it isn't walked twice.
        var roots = options.Folders.Where(Directory.Exists).Select(RealFolder).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        roots = roots.Where(r => !roots.Any(o => !string.Equals(o, r, StringComparison.OrdinalIgnoreCase) && IsInside(r, o))).ToList();
        skippedRoots = roots.Count(r => IsGuarded(r, guarded));
        roots = roots.Where(r => !IsGuarded(r, guarded)).ToList();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var found = new List<DuplicateFile>();
        foreach (var root in roots)
        {
            // A whole drive is listed from its file table when it can be (Ted, 2026-10-23): one long read instead of opening every folder.
            // The same folders are left out as in the walk below: links, hidden and system folders as asked, protected and Windows' own.
            progress?.Report(new DuplicateProgress("FileTable", found.Count, 0, 0) { Folder = root });
            var listed = options.FileTable(root, (path, attributes) =>
                (attributes & skip) == 0 && !IsGuarded(path, guarded) && !AlwaysSkipped(new DirectoryInfo(path)), ct);
            if (listed is not null)
            {
                fastDrives++;
                foreach (var f in listed)
                {
                    if ((f.Attributes & skip) != 0 || f.Size < options.MinSize || f.Size > options.MaxSize) continue;
                    if (!seen.Add(f.Path)) continue;
                    found.Add(new DuplicateFile(f.Path, f.Size, f.LastWriteUtc));
                }
                progress?.Report(new DuplicateProgress("Scanning", found.Count, 0, 0));
                continue;
            }
            if (IsDriveRoot(root)) slowDrives++;

            var pending = new Stack<DirectoryInfo>();
            pending.Push(new DirectoryInfo(root));
            while (pending.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var dir = pending.Pop();
                IEnumerable<FileSystemInfo> entries;
                try { entries = dir.EnumerateFileSystemInfos("*", enumeration); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { continue; }
                foreach (var entry in Safe(entries))
                {
                    if (entry is DirectoryInfo sub)
                    {
                        if (!IsGuarded(sub.FullName, guarded) && !AlwaysSkipped(sub)) pending.Push(sub);
                        continue;
                    }
                    if (entry is not FileInfo info) continue;
                    long length;
                    try { length = info.Length; }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException) { continue; }
                    if (length < options.MinSize || length > options.MaxSize) continue;
                    if (!seen.Add(info.FullName)) continue;
                    found.Add(new DuplicateFile(info.FullName, length, info.LastWriteTimeUtc));
                    if (found.Count % 500 == 0) progress?.Report(new DuplicateProgress("Scanning", found.Count, 0, 0));
                }
            }
        }
        progress?.Report(new DuplicateProgress("Scanning", found.Count, 0, 0));
        return found;
    }

    /// <summary>Folders Windows keeps for itself or for programs, skipped wherever they turn up (any drive, any user).</summary>
    internal static bool AlwaysSkipped(DirectoryInfo dir)
    {
        var name = dir.Name;
        if (dir.Parent is { } parent && parent.Parent is null &&
            (name.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase) ||
             name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase) ||
             name.Equals("Recovery", StringComparison.OrdinalIgnoreCase) ||
             name.Equals("Config.Msi", StringComparison.OrdinalIgnoreCase) ||
             name.StartsWith("$WINDOWS.~", StringComparison.OrdinalIgnoreCase) ||
             name.Equals("$WinREAgent", StringComparison.OrdinalIgnoreCase)))
            return true;
        // Programs keep their own copies of files in AppData; removing one can break the program.
        return name.Equals("AppData", StringComparison.OrdinalIgnoreCase) &&
               dir.Parent?.Parent?.Name.Equals("Users", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// Which copy of an identical set Keep Original keeps (Ted, 2026-10-23): one without a copy mark in its name (Copy of …, … (1)),
    /// then one outside Downloads and temporary folders, then the oldest (most likely the original), then the shortest path.
    /// </summary>
    public static DuplicateFile SuggestKeep(IReadOnlyList<DuplicateFile> files) => files
        .OrderBy(f => SimilarFinder.HasCopyMark(f.Path[(f.Path.LastIndexOfAny(new[] { '\\', '/' }) + 1)..]))
        .ThenBy(f => InPassingFolder(f.Path))
        .ThenBy(f => f.LastWriteUtc)
        .ThenBy(f => f.Path.Length)
        .ThenBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
        .First();

    /// <summary>Folders files usually only pass through: Downloads and temporary folders.</summary>
    private static bool InPassingFolder(string path) =>
        path.Split('\\', '/').SkipLast(1).Any(part =>
            part.Equals("Downloads", StringComparison.OrdinalIgnoreCase) ||
            part.Equals("Temp", StringComparison.OrdinalIgnoreCase) ||
            part.Equals("Tmp", StringComparison.OrdinalIgnoreCase));

    private static bool IsDriveRoot(string path) => path.Length == 3 && path[1] == ':' && path[2] == '\\' && char.IsAsciiLetter(path[0]);

    private static bool IsInside(string path, string folder) =>
        string.Equals(path, folder, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(Path.TrimEndingDirectorySeparator(folder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static bool IsGuarded(string path, List<string> guarded)
    {
        var full = Path.TrimEndingDirectorySeparator(path);
        return guarded.Any(g => IsInside(full, g));
    }

    private static List<DuplicateGroup> FindIdentical(List<DuplicateFile> files, DuplicateSearchOptions options, IProgress<DuplicateProgress>? progress, CancellationToken ct)
    {
        // 1. Group candidates by size (and name when requested).
        var candidates = files
            .GroupBy(f => (f.Size, options.MatchName ? Path.GetFileName(f.Path).ToLowerInvariant() : ""))
            .Select(g => g.ToList()).Where(l => l.Count > 1).ToList();
        int total = candidates.Sum(l => l.Count), done = 0;
        var groups = new List<DuplicateGroup>();

        // 2. Narrow by partial hash, 3. confirm by full hash.
        foreach (var sameSize in candidates)
        {
            ct.ThrowIfCancellationRequested();
            foreach (var (partialHash, partial) in GroupByHash(sameSize, PartialBytes, ct).Where(g => g.Files.Count > 1))
            {
                // Files up to 64 KB were read whole, so the partial hash is already the full one; big files are read once more, not twice.
                var needFull = partial[0].Size > PartialBytes;
                var finals = needFull ? GroupByHash(partial, long.MaxValue, ct) : new List<(string Hash, List<DuplicateFile> Files)> { (partialHash, partial) };
                foreach (var (hash, g) in finals.Where(g => g.Files.Count > 1))
                    groups.Add(new DuplicateGroup(hash, g[0].Size, g.OrderBy(f => f.LastWriteUtc).ToList()));
            }
            done += sameSize.Count;
            progress?.Report(new DuplicateProgress("Comparing", files.Count, done, total));
        }

        return groups.OrderByDescending(g => g.WastedBytes).ToList();
    }

    private static List<(string Hash, List<DuplicateFile> Files)> GroupByHash(List<DuplicateFile> files, long bytes, CancellationToken ct)
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
        return map.Select(kv => (kv.Key, kv.Value)).ToList();
    }

    /// <summary>The folder with every link or junction on its path followed, so the same folder always has the same path.</summary>
    internal static string RealFolder(string folder)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        var root = Path.GetPathRoot(full) ?? "";
        var real = root;
        foreach (var part in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            real = Path.Combine(real, part);
            try
            {
                if (!new DirectoryInfo(real).Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                // A volume mount point or cloud placeholder has no plain path to follow, so it stays as it is.
                if (Directory.ResolveLinkTarget(real, returnFinalTarget: true) is { } target &&
                    Path.IsPathFullyQualified(target.FullName) && !target.FullName.StartsWith(@"\\?\", StringComparison.Ordinal))
                    real = Path.TrimEndingDirectorySeparator(target.FullName);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return real;
    }

    /// <summary>The SHA-256 of the whole file (hex), or null when it can't be read. Used to check results again on request.</summary>
    public static string? FullHash(string path) => HashOf(path, long.MaxValue);

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

    private static IEnumerable<T> Safe<T>(IEnumerable<T> source)
    {
        using var e = source.GetEnumerator();
        while (true)
        {
            T current;
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
