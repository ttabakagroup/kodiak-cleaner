using System.Collections.Concurrent;

namespace KodiakCleaner.Core.Tools;

/// <summary>What a file is, by its extension, for the Disk Analyzer's colors (A to Z; Other last is only a display choice).</summary>
public enum FileKind { Archives, Documents, Music, Other, Pictures, Programs, Videos }

public static class FileKinds
{
    public static readonly IReadOnlyList<FileKind> All = Enum.GetValues<FileKind>();

    private static readonly Dictionary<string, FileKind> ByExtension = Build();

    private static Dictionary<string, FileKind> Build()
    {
        var map = new Dictionary<string, FileKind>(StringComparer.OrdinalIgnoreCase);
        void Add(FileKind kind, string list)
        {
            foreach (var e in list.Split(' ', StringSplitOptions.RemoveEmptyEntries)) map["." + e] = kind;
        }
        Add(FileKind.Archives, "7z ace arj bak bz2 cab gz img iso lz lzh lzma rar tar tbz2 tgz vhd vhdx vmdk wim xz z zip zst");
        Add(FileKind.Documents, "csv doc docm docx epub htm html json md mobi odp ods odt one pdf ppt pptm pptx pst ost rtf tex txt xls xlsm xlsx xml xps");
        Add(FileKind.Music, "aac aif aiff alac ape flac m4a mid midi mp3 ogg opus wav wma");
        Add(FileKind.Pictures, "arw avif bmp cr2 cr3 dng gif heic heif ico jfif jpeg jpg nef orf png psd raf raw svg tga tif tiff webp");
        Add(FileKind.Programs, "appx appxbundle bat cmd com cpl dll drv efi exe jar lib mui msi msix msixbundle msp msu ocx pdb ps1 pyd so sys");
        Add(FileKind.Videos, "3gp avi flv m2ts m4v mkv mov mp4 mpeg mpg mts ts vob webm wmv");
        return map;
    }

    public static FileKind Of(string fileName) => ByExtension.TryGetValue(Path.GetExtension(fileName), out var kind) ? kind : FileKind.Other;
}

/// <summary>One file in a Disk Analyzer list.</summary>
public sealed record DiskFile(string Path, string Name, long Bytes, DateTime Modified)
{
    public FileKind Kind => FileKinds.Of(Name);
}

/// <summary>
/// A folder (or drive, or the All Drives group) with everything below it. Only folders are kept, so even a full drive stays small in
/// memory; the files directly inside one folder are read again when that folder is opened (<see cref="DiskAnalyzer.FilesIn"/>).
/// </summary>
public sealed class DiskNode
{
    /// <summary>A scanned top (drive, folder or the All Drives group), which keeps its own path and name.</summary>
    public DiskNode(string name, string path, DiskNode? parent)
    {
        Name = name;
        _path = path;
        Parent = parent;
    }

    /// <summary>A folder found inside another: its path is worked out from the folders above, which saves a lot of memory on a full drive.</summary>
    internal DiskNode(string name, DiskNode parent)
    {
        Name = name;
        Parent = parent;
    }

    private readonly string? _path;

    public string Name { get; }
    public string Path => _path ?? System.IO.Path.Combine(Parent!.Path, Name);
    public DiskNode? Parent { get; }
    /// <summary>The All Drives group: not a real folder.</summary>
    public bool IsGroup { get; internal init; }
    public bool IsDrive { get; internal init; }
    /// <summary>This drive was read through its NTFS file table (the fast scan) rather than folder by folder.</summary>
    public bool FileTable { get; internal set; }
    /// <summary>Subfolders, biggest first.</summary>
    public List<DiskNode> Children { get; } = new();

    /// <summary>Size and count of the files directly in this folder.</summary>
    public long OwnBytes { get; internal set; }
    public int OwnFiles { get; internal set; }
    /// <summary>Everything below, this folder included.</summary>
    public long Bytes { get; internal set; }
    public long Files { get; internal set; }
    public long Folders { get; internal set; }
    /// <summary>Folders that couldn't be read, at or below this one.</summary>
    public int Skipped { get; internal set; }
    /// <summary>Bytes per <see cref="FileKind"/> at or below this folder.</summary>
    public long[] KindBytes { get; } = new long[FileKinds.All.Count];
    /// <summary>The same split for the files directly in this folder.</summary>
    public long[] OwnKindBytes { get; } = new long[FileKinds.All.Count];

    /// <summary>The kind of file that takes the most space here (Other when empty).</summary>
    public FileKind MainKind => MainOf(KindBytes);
    public FileKind OwnMainKind => MainOf(OwnKindBytes);

    internal static FileKind MainOf(long[] bytes)
    {
        int best = (int)FileKind.Other;
        for (int i = 0; i < bytes.Length; i++)
            if (bytes[i] > bytes[best]) best = i;
        return (FileKind)best;
    }

    /// <summary>From the top (All Drives or the scanned folder) down to this one.</summary>
    public IReadOnlyList<DiskNode> Trail()
    {
        var list = new List<DiskNode>();
        for (var n = this; n is not null; n = n.Parent) list.Add(n);
        list.Reverse();
        return list;
    }

    /// <summary>Takes a removed file off this folder and every folder above it.</summary>
    public void FileRemoved(DiskFile file)
    {
        OwnBytes -= file.Bytes;
        OwnFiles--;
        OwnKindBytes[(int)file.Kind] -= file.Bytes;
        for (var n = this; n is not null; n = n.Parent)
        {
            n.Bytes -= file.Bytes;
            n.Files--;
            n.KindBytes[(int)file.Kind] -= file.Bytes;
        }
        for (var n = this; n.Parent is { } p; n = p) p.Children.Sort(BySize);
    }

    internal static int BySize(DiskNode a, DiskNode b) => b.Bytes.CompareTo(a.Bytes) is var c && c != 0 ? c : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
}

/// <summary>A finished scan: the folder tree, the largest files found anywhere, and how long it took.</summary>
public sealed record DiskScan(DiskNode Root, IReadOnlyList<DiskFile> LargestFiles, TimeSpan Took);

/// <summary>What a running scan has done so far (read from any thread).</summary>
public sealed class DiskScanProgress
{
    private long _files, _bytes;
    public long Files => Interlocked.Read(ref _files);
    public long Bytes => Interlocked.Read(ref _bytes);
    public volatile string Current = "";

    internal void Add(long files, long bytes)
    {
        Interlocked.Add(ref _files, files);
        Interlocked.Add(ref _bytes, bytes);
    }
}

/// <summary>
/// Shows what is taking up space on one or more drives or in a folder: a tree of folder sizes with the kinds of files in each,
/// plus the largest files. Read-only. Links and junctions are never followed, and folders that can't be read are counted as skipped.
/// </summary>
public static class DiskAnalyzer
{
    public const string AllDrivesName = "All Drives";

    /// <summary>
    /// Scans one folder or drive, or several (then grouped under All Drives). Drives, and the folders right inside a single
    /// root, are read side by side, which is much faster on SSDs and costs nothing on a single hard drive.
    /// </summary>
    public static DiskScan Scan(IReadOnlyList<(string Path, string Name)> roots, int largestCount = 1000,
        DiskScanProgress? progress = null, CancellationToken ct = default)
    {
        if (roots.Count == 0) throw new ArgumentException("Nothing to scan.", nameof(roots));
        var started = DateTime.UtcNow;
        progress ??= new DiskScanProgress();
        try
        {
            return ScanAll(roots, largestCount, progress, started, ct);
        }
        catch (AggregateException e) when (ct.IsCancellationRequested && e.Flatten().InnerExceptions.All(x => x is OperationCanceledException))
        {
            throw new OperationCanceledException(ct);
        }
    }

    private static DiskScan ScanAll(IReadOnlyList<(string Path, string Name)> roots, int largestCount, DiskScanProgress progress, DateTime started, CancellationToken ct)
    {
        var largest = new ConcurrentBag<List<DiskFile>>();
        DiskNode root;
        if (roots.Count == 1)
        {
            root = NewRoot(roots[0].Path, roots[0].Name, null);
            ScanTop(root, largestCount, largest, progress, ct);
        }
        else
        {
            root = new DiskNode(AllDrivesName, "", null) { IsGroup = true };
            var drives = roots.Select(r => NewRoot(r.Path, r.Name, root)).ToList();
            Parallel.ForEach(drives, new ParallelOptions { CancellationToken = ct }, d => ScanTop(d, largestCount, largest, progress, ct));
            root.Children.AddRange(drives);
            Total(root);
        }

        var biggest = largest.SelectMany(l => l).OrderByDescending(f => f.Bytes).Take(largestCount).ToList();
        return new DiskScan(root, biggest, DateTime.UtcNow - started);
    }

    private static DiskNode NewRoot(string path, string name, DiskNode? parent)
    {
        path = System.IO.Path.GetFullPath(path);
        bool isDrive = System.IO.Path.GetPathRoot(path) == path;
        return new DiskNode(string.IsNullOrEmpty(name) ? NameOf(path) : name, path, parent) { IsDrive = isDrive };
    }

    private static string NameOf(string path) => System.IO.Path.GetFileName(path.TrimEnd('\\', '/')) is { Length: > 0 } n ? n : path;

    /// <summary>Reads the files right in the root, then each folder inside it on its own thread.</summary>
    private static void ScanTop(DiskNode top, int largestCount, ConcurrentBag<List<DiskFile>> largest, DiskScanProgress progress, CancellationToken ct)
    {
        if (NtfsFileTable.TryScanDrive(top, largestCount, largest, progress, ct)) return;
        var tops = new List<DiskNode>();
        var mine = new TopFiles(largestCount);
        ReadFolder(top, tops, mine, progress, ct);
        largest.Add(mine.List());
        Parallel.ForEach(tops, new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = 4 }, sub =>
        {
            var found = new TopFiles(largestCount);
            ScanTree(sub, found, progress, ct);
            largest.Add(found.List());
        });
        top.Children.AddRange(tops);
        Total(top);
    }

    private static void ScanTree(DiskNode start, TopFiles found, DiskScanProgress progress, CancellationToken ct)
    {
        var stack = new Stack<DiskNode>();
        stack.Push(start);
        var subs = new List<DiskNode>();
        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var node = stack.Pop();
            subs.Clear();
            ReadFolder(node, subs, found, progress, ct);
            node.Children.AddRange(subs);
            foreach (var s in subs) stack.Push(s);
        }
        Total(start);
    }

    /// <summary>Adds the files directly in one folder to it and lists its subfolders (not yet read).</summary>
    private static void ReadFolder(DiskNode node, List<DiskNode> subs, TopFiles found, DiskScanProgress progress, CancellationToken ct)
    {
        var path = node.Path;
        progress.Current = path;
        long bytes = 0;
        int files = 0;
        try
        {
            foreach (var entry in new DirectoryInfo(path).EnumerateFileSystemInfos())
            {
                if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                if (entry is DirectoryInfo dir) subs.Add(new DiskNode(dir.Name, node));
                else if (entry is FileInfo f)
                {
                    long len = f.Length;
                    bytes += len;
                    files++;
                    node.OwnKindBytes[(int)FileKinds.Of(f.Name)] += len;
                    found.Offer(f, len);
                    if (files % 500 == 0)
                    {
                        progress.Add(500, 0);
                        ct.ThrowIfCancellationRequested();
                    }
                }
            }
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            node.Skipped = 1;
        }
        node.OwnBytes = bytes;
        node.OwnFiles = files;
        progress.Add(files % 500, bytes);
    }

    /// <summary>Adds up sizes from the bottom of the tree and sorts every folder's subfolders, biggest first.</summary>
    internal static void Total(DiskNode top)
    {
        // Post-order without recursion: deep folder trees would otherwise overflow the stack.
        var order = new List<DiskNode>();
        var stack = new Stack<DiskNode>();
        stack.Push(top);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            order.Add(n);
            foreach (var c in n.Children) stack.Push(c);
        }
        for (int i = order.Count - 1; i >= 0; i--)
        {
            var n = order[i];
            long bytes = n.OwnBytes, files = n.OwnFiles, folders = 0;
            int skipped = n.IsGroup ? 0 : n.Skipped > 0 && n.Children.Count == 0 && n.OwnFiles == 0 ? 1 : 0;
            Array.Copy(n.OwnKindBytes, n.KindBytes, n.KindBytes.Length);
            foreach (var c in n.Children)
            {
                bytes += c.Bytes;
                files += c.Files;
                folders += c.Folders + 1;
                skipped += c.Skipped;
                for (int k = 0; k < n.KindBytes.Length; k++) n.KindBytes[k] += c.KindBytes[k];
            }
            n.Bytes = bytes;
            n.Files = files;
            n.Folders = folders;
            n.Skipped = skipped;
            n.Children.Sort(DiskNode.BySize);
        }
    }

    /// <summary>The files directly in a folder, biggest first, read now (empty for the All Drives group or a folder that's gone).</summary>
    public static IReadOnlyList<DiskFile> FilesIn(DiskNode folder)
    {
        if (folder.IsGroup) return Array.Empty<DiskFile>();
        var list = new List<DiskFile>();
        try
        {
            foreach (var f in new DirectoryInfo(folder.Path).EnumerateFiles())
                if (!f.Attributes.HasFlag(FileAttributes.ReparsePoint)) list.Add(new DiskFile(f.FullName, f.Name, f.Length, f.LastWriteTime));
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException or System.Security.SecurityException) { }
        list.Sort((a, b) => b.Bytes.CompareTo(a.Bytes));
        return list;
    }

    /// <summary>Whether a path is the folder itself or inside it.</summary>
    public static bool IsUnder(string path, DiskNode folder)
    {
        if (folder.IsGroup) return true;
        var root = folder.Path.EndsWith('\\') || folder.Path.EndsWith('/') ? folder.Path : folder.Path + System.IO.Path.DirectorySeparatorChar;
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Keeps the N largest files seen, cheaply (a min-heap).</summary>
    private sealed class TopFiles
    {
        private readonly int _count;
        private readonly PriorityQueue<FileInfo, long> _heap = new();
        public TopFiles(int count) => _count = count;

        public void Offer(FileInfo f, long len)
        {
            if (_count <= 0) return;
            if (_heap.Count < _count) _heap.Enqueue(f, len);
            else if (_heap.TryPeek(out _, out var smallest) && len > smallest) _heap.EnqueueDequeue(f, len);
        }

        public List<DiskFile> List()
        {
            var list = new List<DiskFile>(_heap.Count);
            while (_heap.TryDequeue(out var f, out var len))
            {
                DateTime modified;
                try { modified = f.LastWriteTime; } catch (IOException) { modified = DateTime.MinValue; }
                list.Add(new DiskFile(f.FullName, f.Name, len, modified));
            }
            return list;
        }
    }
}

/// <summary>A tile in a <see cref="Treemap"/> layout.</summary>
public readonly record struct TreemapTile(int Index, double X, double Y, double Width, double Height);

/// <summary>
/// Squarified treemap layout (Bruls, Huizing and van Wijk): sizes become rectangles whose areas match their share, kept as close
/// to square as possible so they're easy to compare and click.
/// </summary>
public static class Treemap
{
    /// <summary>Lays out positive sizes (any order) in the given box. Zero or negative sizes get no tile.</summary>
    public static List<TreemapTile> Layout(IReadOnlyList<double> sizes, double x, double y, double width, double height)
    {
        var tiles = new List<TreemapTile>();
        var items = Enumerable.Range(0, sizes.Count).Where(i => sizes[i] > 0).OrderByDescending(i => sizes[i]).ToList();
        double total = items.Sum(i => sizes[i]);
        if (items.Count == 0 || width <= 0 || height <= 0 || total <= 0) return tiles;
        double scale = width * height / total;
        var areas = items.Select(i => sizes[i] * scale).ToList();

        int start = 0;
        while (start < items.Count)
        {
            double side = Math.Min(width, height);
            int end = start + 1;
            double rowSum = areas[start];
            double worst = Worst(areas, start, end, rowSum, side);
            while (end < items.Count)
            {
                double nextSum = rowSum + areas[end];
                double next = Worst(areas, start, end + 1, nextSum, side);
                if (next > worst) break;
                worst = next;
                rowSum = nextSum;
                end++;
            }

            // Lay the row along the shorter side.
            double thickness = rowSum / side;
            double offset = 0;
            bool horizontal = width >= height; // the row is a column on the left when the box is wide
            for (int k = start; k < end; k++)
            {
                double length = areas[k] / thickness;
                tiles.Add(horizontal
                    ? new TreemapTile(items[k], x, y + offset, thickness, length)
                    : new TreemapTile(items[k], x + offset, y, length, thickness));
                offset += length;
            }
            if (horizontal) { x += thickness; width -= thickness; }
            else { y += thickness; height -= thickness; }
            start = end;
        }
        return tiles;
    }

    private static double Worst(List<double> areas, int start, int end, double sum, double side)
    {
        double max = 0, min = double.MaxValue;
        for (int i = start; i < end; i++) { max = Math.Max(max, areas[i]); min = Math.Min(min, areas[i]); }
        double s2 = side * side, sum2 = sum * sum;
        return Math.Max(s2 * max / sum2, sum2 / (s2 * min));
    }
}
