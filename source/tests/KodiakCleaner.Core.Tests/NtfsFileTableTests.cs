using System.Collections.Concurrent;
using System.IO.Compression;
using KodiakCleaner.Core.Tools;

namespace KodiakCleaner.Core.Tests;

/// <summary>
/// The Disk Analyzer's fast scan, read from two small NTFS images (made with mkntfs and filled through ntfs-3g; one with 512-byte clusters,
/// so each 1 KB file record spans two clusters). Each holds the same tree: Alpha (4 files, one of them hard linked from Beta),
/// Alpha\Deep\Deeper\Deepest\bottom.zip, Beta (a sparse file, a fragmented file, and a link to Alpha that ntfs-3g stores as a
/// 24-byte system file, which Windows also sees as a plain file), Many (300 files),
/// Long Names (one file with 9 hard links, so its names spill into extension records), Empty, and root-file.txt.
/// </summary>
public class NtfsFileTableTests
{
    private const long TotalBytes = 305_100 + 77_777 + 316_050 + 1_000_000 + 163_840 + 4_000 + 2_222 + 24;

    private static string Image(string name)
    {
        var gz = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
        var img = Path.Combine(Path.GetTempPath(), "kc-ntfs-" + Guid.NewGuid().ToString("N") + ".img");
        using (var input = new GZipStream(File.OpenRead(gz), CompressionMode.Decompress))
        using (var output = File.Create(img))
            input.CopyTo(output);
        return img;
    }

    private static (DiskNode Top, List<DiskFile> Largest, DiskScanProgress Progress) Scan(string fixture, int largestCount = 1000)
    {
        var img = Image(fixture);
        try
        {
            var top = new DiskNode("Fixture (F:)", Path.Combine(Path.GetTempPath(), "F"), null) { IsDrive = true };
            var largest = new ConcurrentBag<List<DiskFile>>();
            var progress = new DiskScanProgress();
            Assert.True(NtfsFileTable.TryScan(img, top, largestCount, largest, progress, CancellationToken.None));
            return (top, largest.SelectMany(l => l).OrderByDescending(f => f.Bytes).ToList(), progress);
        }
        finally { File.Delete(img); }
    }

    private static DiskNode Child(DiskNode n, string name) => Assert.Single(n.Children, c => c.Name == name);

    [Theory]
    [InlineData("ntfs-512-byte-clusters.img.gz")]
    [InlineData("ntfs-4k-clusters.img.gz")]
    public void Fast_scan_reads_the_whole_tree_from_the_file_table(string fixture)
    {
        var (top, largest, progress) = Scan(fixture);
        Assert.True(top.FileTable);
        Assert.Equal(new[] { "Alpha", "Beta", "Empty", "Long Names", "Many" }, top.Children.Select(c => c.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(TotalBytes, top.Bytes);
        Assert.Equal(310, top.Files);
        Assert.Equal(8, top.Folders);
        Assert.Equal(0, top.Skipped);
        Assert.Equal(2_222, top.OwnBytes);
        Assert.Equal(1, top.OwnFiles);
        Assert.Equal(TotalBytes, progress.Bytes);

        // Biggest first, as the folder walk leaves them.
        Assert.Equal(top.Children.Select(c => c.Bytes).OrderByDescending(b => b), top.Children.Select(c => c.Bytes));

        var many = Child(top, "Many");
        Assert.Equal(300, many.OwnFiles);
        Assert.Equal(316_050, many.Bytes);
        Assert.Equal(316_050, many.KindBytes[(int)FileKind.Programs]);

        var deepest = Child(Child(Child(Child(top, "Alpha"), "Deep"), "Deeper"), "Deepest");
        Assert.Equal(77_777, deepest.OwnBytes);
        Assert.Equal(Path.Combine(top.Path, "Alpha", "Deep", "Deeper", "Deepest"), deepest.Path);
        Assert.Equal(77_777, Child(top, "Alpha").KindBytes[(int)FileKind.Archives]);

        // The link isn't followed; the sparse and fragmented files count at their full size.
        var beta = Child(top, "Beta");
        Assert.Empty(beta.Children);
        Assert.Contains(largest, f => f.Name == "LinkToAlpha" && f.Bytes == 24);
        Assert.Contains(largest, f => f.Name == "sparse.vhd" && f.Bytes == 1_000_000 && f.Path == Path.Combine(beta.Path, "sparse.vhd"));
        Assert.Contains(largest, f => f.Name == "frag.mkv" && f.Bytes == 163_840);

        // Hard links count once: big.mp4 is in Alpha or Beta, never both; the file with 9 names counts once.
        Assert.Single(largest, f => f.Bytes == 300_000);
        Assert.Equal(300_000, Child(top, "Alpha").KindBytes[(int)FileKind.Videos] + beta.KindBytes[(int)FileKind.Videos] - 163_840);
        Assert.Equal(4_000, Child(top, "Long Names").Bytes);
        Assert.Equal(1, Child(top, "Long Names").Files);

        Assert.Equal(0, Child(top, "Empty").Bytes);
        Assert.Equal(310, largest.Count);
        Assert.All(largest, f => Assert.True(f.Modified > new DateTime(2020, 1, 1)));
    }

    [Fact]
    public void Fast_scan_keeps_only_the_largest_files_asked_for()
    {
        var (_, largest, _) = Scan("ntfs-4k-clusters.img.gz", largestCount: 3);
        Assert.Equal(new long[] { 1_000_000, 300_000, 163_840 }, largest.Select(f => f.Bytes));
    }

    [Fact]
    public void Anything_that_is_not_NTFS_falls_back_to_the_folder_walk()
    {
        var file = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(file, new byte[64 * 1024]);
            var top = new DiskNode("Not NTFS", file, null) { IsDrive = true };
            Assert.False(NtfsFileTable.TryScan(file, top, 10, new(), new(), CancellationToken.None));
            Assert.False(NtfsFileTable.TryScan(file + ".missing", top, 10, new(), new(), CancellationToken.None));
            Assert.Empty(top.Children);
            Assert.False(top.FileTable);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void A_damaged_file_table_falls_back_without_changing_anything()
    {
        var img = Image("ntfs-4k-clusters.img.gz");
        try
        {
            var bytes = File.ReadAllBytes(img);
            long mft = BitConverter.ToInt64(bytes, 0x30) * 4096;
            bytes[mft + 0x1FE] ^= 0xFF; // record 0's sector check no longer matches
            File.WriteAllBytes(img, bytes);
            var top = new DiskNode("Damaged", img, null) { IsDrive = true };
            Assert.False(NtfsFileTable.TryScan(img, top, 10, new(), new(), CancellationToken.None));
            Assert.Empty(top.Children);
            Assert.Equal(0, top.Bytes);
        }
        finally { File.Delete(img); }
    }

    [Fact]
    public void Folder_scans_and_other_systems_never_try_the_file_table()
    {
        var dir = Directory.CreateTempSubdirectory("kc-ntfs").FullName;
        try
        {
            File.WriteAllBytes(Path.Combine(dir, "a.txt"), new byte[10]);
            var scan = DiskAnalyzer.Scan(new[] { (dir, "") });
            Assert.False(scan.Root.FileTable);
            Assert.Equal(10, scan.Root.Bytes);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Runs_decode_with_negative_offsets_and_sparse_pieces()
    {
        // Header 0x40 attribute layout: runs start at 0x40. Runs: 0x21 len 0x10 at +0x100; 0x11 len 4 at -0x10 (0xF0); 0x01 sparse len 8; end.
        var a = new byte[0x50];
        a[0x20] = 0x40;
        byte[] runs = { 0x21, 0x10, 0x00, 0x01, 0x11, 0x04, 0xF0, 0x01, 0x08, 0x00 };
        runs.CopyTo(a, 0x40);
        var list = new List<(long Vcn, long Lcn, long Count)>();
        Assert.True(NtfsFileTable.DecodeRuns(a, 0, list));
        Assert.Equal(new (long, long, long)[] { (0, 0x100, 0x10), (0x10, 0xF0, 4), (0x14, -1, 8) }, list);
    }
    [Theory]
    [InlineData("ntfs-512-byte-clusters.img.gz")]
    [InlineData("ntfs-4k-clusters.img.gz")]
    public void Duplicate_finder_list_has_every_file_with_its_full_path(string fixture)
    {
        var img = Image(fixture);
        try
        {
            var files = NtfsFileTable.TryList(img, @"F:\", (_, _) => true, CancellationToken.None);
            Assert.NotNull(files);
            Assert.Equal(310, files!.Count);
            Assert.Equal(TotalBytes, files.Sum(f => f.Size));
            Assert.Equal(files.Count, files.Select(f => f.Path).Distinct().Count());
            Assert.Contains(files, f => f.Path == Path.Combine(@"F:\", "root-file.txt") && f.Size == 2_222);
            Assert.Contains(files, f => f.Path == Path.Combine(@"F:\", "Alpha", "Deep", "Deeper", "Deepest", "bottom.zip"));
            Assert.Equal(300, files.Count(f => f.Path.StartsWith(Path.Combine(@"F:\", "Many"))));
            Assert.All(files, f => Assert.True(f.LastWriteUtc > new DateTime(2020, 1, 1)));
        }
        finally { File.Delete(img); }
    }

    [Fact]
    public void Duplicate_finder_list_leaves_out_folders_it_is_told_to()
    {
        var img = Image("ntfs-4k-clusters.img.gz");
        try
        {
            var asked = new List<string>();
            var files = NtfsFileTable.TryList(img, @"F:\", (path, _) =>
            {
                asked.Add(path);
                return Path.GetFileName(path) is not ("Alpha" or "Many");
            }, CancellationToken.None);
            Assert.NotNull(files);
            Assert.DoesNotContain(files!, f => f.Path.StartsWith(Path.Combine(@"F:\", "Alpha")) || f.Path.StartsWith(Path.Combine(@"F:\", "Many")));
            Assert.Contains(files!, f => f.Path.EndsWith("root-file.txt"));
            // Nothing below a folder left out is looked at.
            Assert.DoesNotContain(asked, p => p.Contains("Deep"));
        }
        finally { File.Delete(img); }
    }

    [Fact]
    public void Duplicate_finder_list_is_null_for_something_that_is_not_ntfs()
    {
        var junk = Path.Combine(Path.GetTempPath(), "kc-ntfs-" + Guid.NewGuid().ToString("N") + ".img");
        File.WriteAllBytes(junk, new byte[64 * 1024]);
        try { Assert.Null(NtfsFileTable.TryList(junk, @"F:\", (_, _) => true, CancellationToken.None)); }
        finally { File.Delete(junk); }
    }
}
