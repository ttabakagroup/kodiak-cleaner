namespace KodiakCleaner.Core.Tools;

/// <summary>One row in a Disk Analyzer report: a folder (with everything below it) or a single file.</summary>
public sealed record DiskItem(string Path, string Name, long Bytes, int Files, bool IsFolder);

public sealed record DiskReport(
    string Root,
    long TotalBytes,
    int Files,
    IReadOnlyList<DiskItem> Children,
    IReadOnlyList<DiskItem> LargestFiles,
    int Skipped);

/// <summary>
/// Shows what is taking up space: the size of each folder directly inside a chosen folder or drive, and the
/// largest files anywhere below it. Read-only. Links and junctions are never followed, and folders that
/// can't be read are counted as skipped.
/// </summary>
public static class DiskAnalyzer
{
    public static DiskReport Analyze(string root, int largestCount = 100, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        root = Path.GetFullPath(root);
        var largest = new PriorityQueue<DiskItem, long>();
        int skipped = 0, scanned = 0;
        var children = new List<DiskItem>();

        void Track(FileInfo f)
        {
            var item = new DiskItem(f.FullName, f.Name, f.Length, 1, false);
            if (largest.Count < largestCount) largest.Enqueue(item, f.Length);
            else if (largestCount > 0 && f.Length > largest.Peek().Bytes) largest.EnqueueDequeue(item, f.Length);
            if (++scanned % 2000 == 0) progress?.Report(scanned);
        }

        (long Bytes, int Files) Measure(DirectoryInfo dir)
        {
            long bytes = 0;
            int files = 0;
            var stack = new Stack<DirectoryInfo>();
            stack.Push(dir);
            while (stack.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var current = stack.Pop();
                try
                {
                    foreach (var entry in current.EnumerateFileSystemInfos())
                    {
                        if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                        if (entry is DirectoryInfo sub) stack.Push(sub);
                        else if (entry is FileInfo f)
                        {
                            bytes += f.Length;
                            files++;
                            Track(f);
                        }
                    }
                }
                catch (Exception e) when (e is UnauthorizedAccessException or IOException or System.Security.SecurityException)
                {
                    skipped++;
                }
            }
            return (bytes, files);
        }

        long looseBytes = 0;
        int looseFiles = 0;
        var top = new DirectoryInfo(root);
        foreach (var entry in top.EnumerateFileSystemInfos())
        {
            ct.ThrowIfCancellationRequested();
            if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
            if (entry is DirectoryInfo dir)
            {
                var (bytes, files) = Measure(dir);
                children.Add(new DiskItem(dir.FullName, dir.Name, bytes, files, true));
            }
            else if (entry is FileInfo f)
            {
                looseBytes += f.Length;
                looseFiles++;
                Track(f);
            }
        }
        if (looseFiles > 0) children.Add(new DiskItem(root, "Files in this folder", looseBytes, looseFiles, false));

        var biggest = new List<DiskItem>(largest.Count);
        while (largest.Count > 0) biggest.Add(largest.Dequeue());
        biggest.Reverse();

        return new DiskReport(root, children.Sum(c => c.Bytes), children.Sum(c => c.Files),
            children.OrderByDescending(c => c.Bytes).ToList(), biggest, skipped);
    }
}
