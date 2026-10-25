using KodiakCleaner.Core.About;
using System.Text;
using KodiakCleaner.Core.Cleaning;
using KodiakCleaner.Core.Tools;
using KodiakCleaner.Core.Updates;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.Core.Tests;

public sealed class DuplicateFinderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kodiakcleaner-dup-" + Guid.NewGuid().ToString("N"));
    public DuplicateFinderTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Write(string rel, byte[] content)
    {
        var p = Path.Combine(_root, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllBytes(p, content);
        return p;
    }

    [Fact]
    public void Uses_the_file_table_list_when_there_is_one_and_hashes_only_matching_sizes()
    {
        var a = Encoding.UTF8.GetBytes("hello world");
        var one = Write("one/a.txt", a);
        var two = Write("two/a.txt", a);
        Write("walk-only/a.txt", a); // not in the list, so it must not turn up
        var hidden = Write("one/hidden.txt", a);
        var protectedTried = false;

        var results = DuplicateFinder.Search(new DuplicateSearchOptions
        {
            Folders = new[] { _root },
            Protected = new[] { Path.Combine(_root, "guarded") },
            FileTable = (root, keep, _) =>
            {
                protectedTried = !keep(Path.Combine(root, "guarded"), FileAttributes.Directory);
                return new List<NtfsFileTable.TableFile>
                {
                    new(one, a.Length, DateTime.UtcNow, FileAttributes.Normal),
                    new(two, a.Length, DateTime.UtcNow, FileAttributes.Archive),
                    new(hidden, a.Length, DateTime.UtcNow, FileAttributes.Hidden),
                    new(one, a.Length, DateTime.UtcNow, FileAttributes.Normal),
                };
            },
        });

        Assert.True(protectedTried);
        Assert.Equal(1, results.FastDrives);
        Assert.Equal(0, results.SlowDrives);
        Assert.Equal(2, results.FilesScanned);
        var group = Assert.Single(results.Identical);
        Assert.Equal(new[] { one, two }, group.Files.Select(f => f.Path).OrderBy(p => p, StringComparer.Ordinal));
    }

    [Fact]
    public void Walks_the_folders_when_there_is_no_file_table()
    {
        var a = Encoding.UTF8.GetBytes("hello world");
        Write("one/a.txt", a);
        Write("two/a.txt", a);
        var results = DuplicateFinder.Search(new DuplicateSearchOptions { Folders = new[] { _root }, FileTable = (_, _, _) => null });
        Assert.Equal(0, results.FastDrives);
        Assert.Equal(0, results.SlowDrives); // a folder, not a whole drive
        Assert.Equal(2, results.FilesScanned);
        Assert.Single(results.Identical);
    }

    [Fact]
    public void Check_all_keeps_the_copy_without_a_copy_mark_outside_downloads_then_the_oldest()
    {
        var old = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newer = old.AddDays(3);
        DuplicateFile F(string path, DateTime when) => new(path, 10, when);

        Assert.Equal(@"C:\Docs\Report.pdf", DuplicateFinder.SuggestKeep(new[]
        {
            F(@"C:\Docs\Report - Copy.pdf", old), F(@"C:\Docs\Report.pdf", newer), F(@"C:\Docs\Copy of Report.pdf", old),
        }).Path);
        Assert.Equal(@"C:\Docs\Report.pdf", DuplicateFinder.SuggestKeep(new[]
        {
            F(@"C:\Users\Ted\Downloads\Report.pdf", old), F(@"C:\Docs\Report.pdf", newer),
        }).Path);
        Assert.Equal(@"D:\Old\Report.pdf", DuplicateFinder.SuggestKeep(new[]
        {
            F(@"C:\Docs\Report.pdf", newer), F(@"D:\Old\Report.pdf", old),
        }).Path);
        Assert.Equal(@"C:\A\Report.pdf", DuplicateFinder.SuggestKeep(new[]
        {
            F(@"C:\A\B\Report.pdf", old), F(@"C:\A\Report.pdf", old),
        }).Path);
    }

    [Fact]
    public void Moved_duplicates_keep_their_folders_get_a_report_and_can_be_put_back()
    {
        var a = Encoding.UTF8.GetBytes("hello world");
        var kept = Write("docs/a.txt", a);
        var copy = Write("downloads/a.txt", a);
        var gone = Path.Combine(_root, "missing.txt");
        var target = Path.Combine(_root, "Moved Duplicates");
        var when = new DateTime(2026, 10, 23, 21, 30, 5);

        var (batch, failed) = DuplicateMover.Move(target, new[]
        {
            (new DuplicateFile(copy, a.Length, DateTime.UtcNow), kept, "abc123"),
            (new DuplicateFile(gone, a.Length, DateTime.UtcNow), kept, "abc123"),
        }, when);

        Assert.Equal(Path.Combine(target, "Duplicates 2026-10-23 213005"), batch.Folder);
        var moved = Assert.Single(batch.Moves);
        Assert.Equal(gone, Assert.Single(failed).Path);
        Assert.False(File.Exists(copy));
        Assert.True(File.Exists(moved.To));
        Assert.Equal(DuplicateMover.PlaceFor(batch.Folder, copy), moved.To);
        Assert.EndsWith(Path.Combine("downloads", "a.txt"), moved.To);
        var report = File.ReadAllText(Path.Combine(batch.Folder, DuplicateMover.ReportName));
        Assert.Contains("Kept Copy (Original)", report);
        Assert.Contains($"Moved,{kept},{copy},{moved.To},11,abc123", report);

        // A second batch in the same second gets its own folder; the list reads both back, newest first.
        var again = DuplicateMover.Move(target, Array.Empty<(DuplicateFile, string, string)>(), when.AddSeconds(1)).Batch;
        var listed = DuplicateMover.List(target);
        Assert.Equal(new[] { again.Folder, batch.Folder }, listed.Select(b => b.Folder));

        var read = listed[1];
        Assert.Null(DuplicateMover.Restore(read, read.Moves[0], when.AddHours(1)));
        Assert.True(File.Exists(copy));
        Assert.Equal("It was already put back.", DuplicateMover.Restore(read, read.Moves[0], when.AddHours(2)));
        Assert.NotNull(DuplicateMover.List(target)[1].Moves[0].RestoredOn);
        Assert.StartsWith("Restored,", File.ReadAllLines(Path.Combine(batch.Folder, DuplicateMover.ReportName))[1]);
    }

    [Fact]
    public void Restore_never_overwrites_a_file_at_the_old_place()
    {
        var a = Encoding.UTF8.GetBytes("hello world");
        var copy = Write("two/a.txt", a);
        var (batch, _) = DuplicateMover.Move(Path.Combine(_root, "out"), new[] { (new DuplicateFile(copy, a.Length, DateTime.UtcNow), "kept", "h") }, DateTime.Now);
        Write("two/a.txt", Encoding.UTF8.GetBytes("new file"));
        Assert.Equal("Another file is already at its old place.", DuplicateMover.Restore(batch, batch.Moves[0], DateTime.Now));
        Assert.True(File.Exists(batch.Moves[0].To));
    }

    [Fact]
    public void Finds_identical_files_and_ignores_same_size_different_content()
    {
        var a = Encoding.UTF8.GetBytes("hello world");
        var b = Encoding.UTF8.GetBytes("hello WORLD"); // same length, different bytes
        Write("one/a.txt", a);
        Write("two/copy of a.txt", a);
        Write("three/a.txt", a);
        Write("b.txt", b);
        Write("unique.txt", Encoding.UTF8.GetBytes("x"));

        var groups = DuplicateFinder.Find(new DuplicateSearchOptions { Folders = new[] { _root } });

        var g = Assert.Single(groups);
        Assert.Equal(3, g.Files.Count);
        Assert.Equal(2 * a.Length, g.WastedBytes);
    }

    [Fact]
    public void Large_files_differing_after_first_64KB_are_not_duplicates()
    {
        var big1 = new byte[200_000];
        var big2 = new byte[200_000];
        big2[150_000] = 1;
        Write("x.bin", big1);
        Write("y.bin", big2);
        Write("z.bin", big1);

        var groups = DuplicateFinder.Find(new DuplicateSearchOptions { Folders = new[] { _root } });

        var g = Assert.Single(groups);
        Assert.Equal(new[] { "x.bin", "z.bin" }, g.Files.Select(f => Path.GetFileName(f.Path)).OrderBy(n => n));
    }

    [Fact]
    public void Respects_min_size_and_match_name()
    {
        var data = new byte[100];
        Write("a/same.dat", data);
        Write("b/same.dat", data);
        Write("c/other.dat", data);

        Assert.Empty(DuplicateFinder.Find(new DuplicateSearchOptions { Folders = new[] { _root }, MinSize = 101 }));

        var byName = DuplicateFinder.Find(new DuplicateSearchOptions { Folders = new[] { _root }, MatchName = true });
        Assert.Equal(2, Assert.Single(byName).Files.Count);
    }

    [Fact]
    public void Overlapping_folders_do_not_double_count()
    {
        var data = new byte[10];
        Write("sub/a", data);
        Write("sub/b", data);
        var groups = DuplicateFinder.Find(new DuplicateSearchOptions { Folders = new[] { _root, Path.Combine(_root, "sub") } });
        Assert.Equal(2, Assert.Single(groups).Files.Count);
    }

    [Fact]
    public void A_folder_also_chosen_through_a_link_is_not_its_own_duplicate()
    {
        // Same file reached as real/a.bin and link/a.bin: listing it would let "the copy" (the only copy) be removed.
        Write("real/a.bin", new byte[100]);
        var link = Path.Combine(_root, "link");
        Directory.CreateSymbolicLink(link, Path.Combine(_root, "real"));

        Assert.Empty(DuplicateFinder.Find(new DuplicateSearchOptions { Folders = new[] { Path.Combine(_root, "real"), link } }));
        Assert.Empty(DuplicateFinder.Find(new DuplicateSearchOptions { Folders = new[] { link, Path.Combine(_root, "real") } }));
        Assert.Equal(Path.Combine(_root, "real"), DuplicateFinder.RealFolder(link));
    }

    [Fact]
    public void Group_hash_is_the_full_content_hash()
    {
        var big = new byte[200_000];
        big[199_999] = 7;
        Write("p/big.bin", big);
        Write("q/big.bin", big);
        Write("p/small.bin", new byte[] { 1, 2, 3 });
        Write("q/small.bin", new byte[] { 1, 2, 3 });

        var groups = DuplicateFinder.Find(new DuplicateSearchOptions { Folders = new[] { _root } });

        Assert.Equal(2, groups.Count);
        foreach (var g in groups) Assert.Equal(DuplicateFinder.HashOf(g.Files[0].Path, long.MaxValue), g.Hash);
    }

    [Fact]
    public void Protected_folders_and_every_users_AppData_are_never_searched()
    {
        var data = Encoding.UTF8.GetBytes("same bytes everywhere");
        Write("Users/bob/Documents/a.txt", data);
        Write("Users/bob/AppData/Local/a.txt", data);
        Write("Program Files/Tool/a.txt", data);

        var options = new DuplicateSearchOptions { Folders = new[] { _root }, Protected = new[] { Path.Combine(_root, "Program Files") } };
        Assert.Empty(DuplicateFinder.Find(options));

        // A chosen folder inside a protected one is left out entirely, and counted so the page can say so.
        var inside = DuplicateFinder.Search(new DuplicateSearchOptions
        {
            Folders = new[] { Path.Combine(_root, "Program Files", "Tool") },
            Protected = new[] { Path.Combine(_root, "Program Files") },
        });
        Assert.Equal(1, inside.SkippedRoots);
        Assert.Equal(0, inside.FilesScanned);
    }

    [Fact]
    public void A_folder_on_a_chosen_drive_is_walked_once()
    {
        var data = new byte[50];
        Write("d/a", data);
        Write("d/b", data);
        var result = DuplicateFinder.Search(new DuplicateSearchOptions { Folders = new[] { _root, Path.Combine(_root, "d") } });
        Assert.Equal(2, result.FilesScanned);
        Assert.Equal(2, Assert.Single(result.Identical).Files.Count);
    }

    private static byte[] Text(int lines, int seed, string? insertAt100 = null)
    {
        var rng = new Random(seed);
        var sb = new StringBuilder();
        for (int i = 0; i < lines; i++)
        {
            if (i == 100 && insertAt100 is not null) sb.AppendLine(insertAt100);
            sb.Append("Line ").Append(i).Append(": ");
            for (int w = 0; w < 8; w++) sb.Append((char)('a' + rng.Next(26))).Append((char)('a' + rng.Next(26))).Append(' ');
            sb.AppendLine();
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    [Fact]
    public void In_depth_search_finds_an_edited_copy_but_not_an_unrelated_file_with_the_same_name()
    {
        Write("docs/Report.txt", Text(600, 1));
        Write("backup/Report (1).txt", Text(600, 1, insertAt100: "A new paragraph added later."));
        Write("other/Report.txt", Text(600, 2)); // same name, different contents
        Write("x/exact.txt", Text(300, 3));
        Write("y/exact - Copy.txt", Text(300, 3));

        var result = DuplicateFinder.Search(new DuplicateSearchOptions { Folders = new[] { _root }, FindSimilar = true });

        Assert.Single(result.Identical); // the exact pair stays identical, not similar
        var group = Assert.Single(result.Similar);
        Assert.Equal(new[] { "Report (1).txt", "Report.txt" }, group.Files.Select(f => Path.GetFileName(f.File.Path)).OrderBy(n => n));
        Assert.Contains(group.Files, f => f.File.Path.Contains("docs"));
        Assert.True(group.Score >= 0.8, $"score {group.Score}");
        Assert.Contains("ontents", group.Files[0].Why);
    }

    [Fact]
    public void Pictures_that_look_alike_are_grouped_whatever_their_names()
    {
        Write("a/IMG_0001.jpg", new byte[] { 1, 2, 3, 4 });
        Write("b/holiday.png", new byte[] { 9, 8, 7, 6, 5 });
        Write("c/IMG_0002.jpg", new byte[] { 5, 5, 5, 5 });
        var hashes = new Dictionary<string, ulong>
        {
            ["IMG_0001.jpg"] = 0x0F0F_F0F0_1234_5678,
            ["holiday.png"] = 0x0F0F_F0F0_1234_5679, // one bit away
            ["IMG_0002.jpg"] = 0xF0F0_0F0F_EDCB_A987, // a different picture
        };

        var result = DuplicateFinder.Search(new DuplicateSearchOptions
        {
            Folders = new[] { _root },
            FindSimilar = true,
            PictureHash = p => hashes[Path.GetFileName(p)],
        });

        var group = Assert.Single(result.Similar);
        Assert.Equal(new[] { "IMG_0001.jpg", "holiday.png" }, group.Files.Select(f => Path.GetFileName(f.File.Path)).OrderBy(n => n, StringComparer.Ordinal));
        Assert.StartsWith("Pictures look 98% alike", group.Files[0].Why);
    }

    [Theory]
    [InlineData("Report (1).docx", "report")]
    [InlineData("Copy of Report - Copy (2).docx", "report")]
    [InlineData("Budget_final_v2.xlsx", "budget")]
    [InlineData("my.holiday-photo.jpg", "my holiday photo")]
    [InlineData("(1).txt", "1")]
    public void Name_keys_drop_copy_marks(string name, string key) => Assert.Equal(key, SimilarFinder.NameKey(name));

    [Fact]
    public void Match_similar_names_finds_files_by_name_even_when_contents_differ()
    {
        Write("a/Quarterly Report 2024-03-31.txt", Text(300, 11));
        Write("b/quarterly_reprot.txt", Text(300, 12));
        Write("c/Shopping List.txt", Text(300, 13));

        var plain = DuplicateFinder.Search(new DuplicateSearchOptions { Folders = new[] { _root }, FindSimilar = true });
        Assert.Empty(plain.Similar); // contents differ, so names alone don't count unless asked

        var byName = DuplicateFinder.Search(new DuplicateSearchOptions
        {
            Folders = new[] { _root },
            FindSimilar = true,
            MatchNames = true,
            Names = new NameRules { IgnoreDates = true },
        });
        var group = Assert.Single(byName.Similar);
        Assert.Equal(new[] { "Quarterly Report 2024-03-31.txt", "quarterly_reprot.txt" },
            group.Files.Select(f => Path.GetFileName(f.File.Path)).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Contains("ames", group.Files[0].Why);
    }

    [Fact]
    public void Names_are_only_compared_across_file_types_when_asked()
    {
        Write("a/Resume John Smith.docx", Text(200, 21));
        Write("b/John Smith Resume.pdf", Text(200, 22));
        DuplicateSearchOptions Options(bool any) => new()
        {
            Folders = new[] { _root }, FindSimilar = true, MatchNames = true, Names = new NameRules { AnyFileType = any },
        };
        Assert.Empty(DuplicateFinder.Search(Options(false)).Similar);
        Assert.Equal(2, Assert.Single(DuplicateFinder.Search(Options(true)).Similar).Files.Count); // words in another order
    }

    [Fact]
    public void Name_patterns_gather_matching_files_into_a_set()
    {
        Write("x/Invoice 1001.pdf", Text(50, 31));
        Write("y/invoice 1002.PDF", Text(50, 32));
        Write("z/Invoice draft.pdf", Text(50, 33));
        Write("z/Receipt 1003.pdf", Text(50, 34));

        var result = DuplicateFinder.Search(new DuplicateSearchOptions
        {
            Folders = new[] { _root }, FindSimilar = true, Names = new NameRules { Patterns = new[] { "Invoice #.pdf", "Nothing*" } },
        });

        var group = Assert.Single(result.Similar);
        Assert.Equal("Invoice #.pdf", group.Pattern);
        Assert.Equal(new[] { "Invoice 1001.pdf", "invoice 1002.PDF" }, group.Files.Select(f => Path.GetFileName(f.File.Path)));
        Assert.Matches(SimilarFinder.PatternRegex("IMG_????"), "img_0041");
        Assert.DoesNotMatch(SimilarFinder.PatternRegex("IMG_????"), "IMG_00412");
    }

    [Theory]
    [InlineData("Scan 2024-05-01 14-30-05.pdf", "scan")]
    [InlineData("Scan_20240501.pdf", "scan")]
    [InlineData("Photo 01.05.2024.jpg", "photo")]
    public void Dates_can_be_left_out_of_names(string name, string key) =>
        Assert.Equal(key, SimilarFinder.NameKey(name, new NameRules { IgnoreDates = true }));

    [Fact]
    public void Numbers_and_own_words_can_be_left_out_of_names()
    {
        Assert.Equal("img", SimilarFinder.NameKey("IMG_0041.jpg", new NameRules { IgnoreNumbers = true }));
        Assert.Equal("budget", SimilarFinder.NameKey("Budget DRAFT scan.xlsx", new NameRules { ExtraWords = new[] { "draft", " Scan " } }));
        Assert.Equal("report final", SimilarFinder.NameKey("Report final.docx", new NameRules { IgnoreVersionWords = false }));
        Assert.Equal("report copy", SimilarFinder.NameKey("Report - Copy.docx", new NameRules { IgnoreCopyMarks = false, IgnoreVersionWords = false }));
        Assert.Equal("1", SimilarFinder.NameKey("1.txt", new NameRules { IgnoreNumbers = true })); // nothing left: plain name
    }

    [Fact]
    public void Numbered_series_count_for_little()
    {
        Assert.Equal(0.3, SimilarFinder.NameLikeness("img 0041", "img 0042"));
        Assert.Equal(1, SimilarFinder.NameLikeness("report", "report"));
        Assert.True(SimilarFinder.NameLikeness("annual report", "anual report") > 0.9);
        // The in-place digit check (2026-10-24) must agree with leaving every digit out.
        Assert.Equal(0.3, SimilarFinder.NameLikeness("trip 2024", "trip 22"));
        Assert.Equal(0.3, SimilarFinder.NameLikeness("123", "456"));
        Assert.NotEqual(0.3, SimilarFinder.NameLikeness("img 0041", "img 0041 b"));
        Assert.Equal(1, SimilarFinder.NameLikeness("smith john resume", "john smith resume"));
        Assert.Equal(0.5, SimilarFinder.NameLikeness("abcd", "abxy"));
        var longName = new string('a', 200);
        Assert.Equal(1, SimilarFinder.NameLikeness(longName, longName + "b"));  // compared on the first 120 letters
    }

}

public sealed class FreeSpaceWiperTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kodiakcleaner-wipe-" + Guid.NewGuid().ToString("N"));
    public FreeSpaceWiperTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Writes_requested_amount_per_pass_and_cleans_up_without_touching_files()
    {
        File.WriteAllText(Path.Combine(_root, "keep.txt"), "keep");
        long written = FreeSpaceWiper.Wipe(_root, passes: 2, freeBytes: () => 0, limitBytes: 10L * 1024 * 1024);

        Assert.Equal(20L * 1024 * 1024, written);
        Assert.False(Directory.Exists(Path.Combine(_root, FreeSpaceWiper.WorkFolderName)));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(_root, "keep.txt")));
    }

    [Fact]
    public void A_leftover_work_folder_from_a_crash_is_cleared_first()
    {
        var work = Directory.CreateDirectory(Path.Combine(_root, FreeSpaceWiper.WorkFolderName)).FullName;
        File.WriteAllBytes(Path.Combine(work, "fill0.tmp"), new byte[10]);

        long written = FreeSpaceWiper.Wipe(_root, 1, () => 0, limitBytes: 8L * 1024 * 1024);

        Assert.Equal(8L * 1024 * 1024, written);
        Assert.False(Directory.Exists(work));
    }

    [Fact]
    public void Cancelling_still_removes_the_filler_files()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() =>
            FreeSpaceWiper.Wipe(_root, 1, () => 0, ct: cts.Token, limitBytes: 1024 * 1024));
        Assert.False(Directory.Exists(Path.Combine(_root, FreeSpaceWiper.WorkFolderName)));
    }

    private sealed class Signal(ManualResetEventSlim started) : IProgress<WipeProgress>
    {
        public void Report(WipeProgress value) => started.Set(); // runs on the wiping thread, as it writes
    }

    [Fact]
    public void Closing_Kodiak_Cleaner_stops_a_wipe_and_removes_its_filler_files_first()
    {
        // On exit the process ends without running a background wipe's finally; the exit hook calls StopAll.
        using var started = new ManualResetEventSlim();
        var wipe = Task.Run(() => FreeSpaceWiper.Wipe(_root, 1, () => 0, new Signal(started), limitBytes: 10L * 1024 * 1024 * 1024));
        Assert.True(started.Wait(TimeSpan.FromSeconds(60)), "the wipe never started writing");
        Assert.True(Directory.Exists(Path.Combine(_root, FreeSpaceWiper.WorkFolderName)));

        FreeSpaceWiper.StopAll(TimeSpan.FromSeconds(30));

        Assert.False(Directory.Exists(Path.Combine(_root, FreeSpaceWiper.WorkFolderName))); // already gone when StopAll returns
        Assert.ThrowsAny<OperationCanceledException>(() => wipe.GetAwaiter().GetResult());
    }
}

public class RecycleBinItemsTests
{
    private static byte[] InfoV2(long size, DateTime deletedUtc, string path)
    {
        var name = Encoding.Unicode.GetBytes(path + "\0");
        var b = new byte[28 + name.Length];
        BitConverter.GetBytes(2L).CopyTo(b, 0);
        BitConverter.GetBytes(size).CopyTo(b, 8);
        BitConverter.GetBytes(deletedUtc.ToFileTimeUtc()).CopyTo(b, 16);
        BitConverter.GetBytes(path.Length + 1).CopyTo(b, 24);
        name.CopyTo(b, 28);
        return b;
    }

    [Fact]
    public void Parses_windows10_info_file()
    {
        var when = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var parsed = RecycleBinItems.ParseInfo(InfoV2(1234, when, @"C:\Users\ted\Desktop\old.txt"));
        Assert.NotNull(parsed);
        Assert.Equal(1234, parsed!.Value.Size);
        Assert.Equal(when, parsed.Value.DeletedUtc);
        Assert.Equal(@"C:\Users\ted\Desktop\old.txt", parsed.Value.OriginalPath);
    }

    [Fact]
    public void Rejects_garbage() => Assert.Null(RecycleBinItems.ParseInfo(new byte[] { 1, 2, 3 }));

    [Fact]
    public void Rejects_a_name_length_that_would_overflow()
    {
        var b = InfoV2(1, DateTime.UtcNow, "x");
        BitConverter.GetBytes(int.MaxValue).CopyTo(b, 24);
        Assert.Null(RecycleBinItems.ParseInfo(b));
        BitConverter.GetBytes(-5).CopyTo(b, 24);
        Assert.Null(RecycleBinItems.ParseInfo(b));
    }

    [Fact]
    public void Reads_folder_and_deletes_file_and_folder_items()
    {
        var dir = Path.Combine(Path.GetTempPath(), "kodiakcleaner-rb-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllBytes(Path.Combine(dir, "$IABC123.txt"), InfoV2(4, DateTime.UtcNow.AddDays(-3), @"C:\a.txt"));
            File.WriteAllText(Path.Combine(dir, "$RABC123.txt"), "data");
            File.WriteAllBytes(Path.Combine(dir, "$IDEF456"), InfoV2(0, DateTime.UtcNow, @"C:\folder"));
            Directory.CreateDirectory(Path.Combine(dir, "$RDEF456", "inner"));
            File.WriteAllText(Path.Combine(dir, "$RDEF456", "inner", "f.txt"), "x");

            var items = RecycleBinItems.ReadFolder(dir);
            Assert.Equal(2, items.Count);
            foreach (var i in items) Assert.True(RecycleBinItems.Delete(i));
            Assert.Empty(Directory.EnumerateFileSystemEntries(dir));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}

public class IniFileTests
{
    [Fact]
    public void Round_trips_sections_and_typed_values()
    {
        var ini = new IniFile();
        ini.Set("General", "Theme", "Dark");
        ini.Set("General", "Confirm", true);
        ini.Set("Cleaner", "Age", 24);
        var text = ini.Serialize("Kodiak Cleaner settings");

        Assert.StartsWith("; Kodiak Cleaner settings\r\n", text);
        var back = IniFile.Parse(text);
        Assert.Equal("Dark", back.GetString("general", "theme", ""));
        Assert.True(back.GetBool("General", "Confirm", false));
        Assert.Equal(24, back.GetInt("Cleaner", "Age", 0));
        Assert.Equal(7, back.GetInt("Cleaner", "Missing", 7));
    }

    [Fact]
    public void Tolerates_comments_blank_lines_and_bad_values()
    {
        var ini = IniFile.Parse("# c\n\n[A]\nflag = yes\nnum=abc\nnoequals\n=novalue\n");
        Assert.True(ini.GetBool("A", "flag", false));
        Assert.Equal(5, ini.GetInt("A", "num", 5));
    }

    [Fact]
    public void Newlines_in_values_cannot_inject_keys()
    {
        var ini = new IniFile();
        ini.Set("A", "k", "x\r\n[B]\r\nevil=1");
        Assert.Null(IniFile.Parse(ini.Serialize()).Get("B", "evil"));
    }
}

public class UpdateManifestTests
{
    private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static string File(string channel = "dev", string version = "0.8.40",
        string url = "https://www.kodiakcleaner.app/download/KodiakCleaner-Installer.msi", string sha = Sha) =>
        $$$"""{"channel":"{{{channel}}}","version":"{{{version}}}","msi":{"url":"{{{url}}}","sha256":"{{{sha}}}"}}""";

    [Fact]
    public void Reads_the_websites_update_file()
    {
        Assert.Equal("https://www.kodiakcleaner.app/updates/dev.json", UpdateManifest.Url(ReleaseChannel.Dev));
        Assert.Equal("https://www.kodiakcleaner.app/updates/stable.json", UpdateManifest.Url(ReleaseChannel.Stable));
        Assert.Equal("https://www.kodiakcleaner.app/updates/beta.json", UpdateManifest.Url(ReleaseChannel.Beta));
        var info = UpdateManifest.Parse(File(), ReleaseChannel.Dev)!;
        Assert.Equal(new Version(0, 8, 40), info.Version);
        Assert.Equal("https://www.kodiakcleaner.app/download/KodiakCleaner-Installer.msi", info.MsiUrl);
        Assert.Equal(Sha.ToUpperInvariant(), info.Sha256);
        Assert.True(UpdateManifest.IsNewer(info, new Version(0, 8, 39, 0)));
        Assert.False(UpdateManifest.IsNewer(info, new Version(0, 8, 40)));
    }

    [Fact]
    public void Each_channel_only_takes_its_own_file()
    {
        Assert.Null(UpdateManifest.Parse(File(channel: "beta"), ReleaseChannel.Dev));
        Assert.NotNull(UpdateManifest.Parse(File(channel: "Beta"), ReleaseChannel.Beta));
    }

    [Theory]
    [InlineData("http://www.kodiakcleaner.app/download/KodiakCleaner-Installer.msi")] // not HTTPS
    [InlineData("https://evil.example/KodiakCleaner-Installer.msi")]                 // another host
    [InlineData("https://www.kodiakcleaner.app.evil.example/x.msi")]
    [InlineData("https://www.kodiakcleaner.app:8443/download/x.msi")]                 // another port
    [InlineData("https://www.kodiakcleaner.app/download/KodiakCleaner-Portable.zip")] // not an installer
    [InlineData("download/KodiakCleaner-Installer.msi")]
    public void Only_accepts_an_installer_on_the_website(string url) =>
        Assert.Null(UpdateManifest.Parse(File(url: url), ReleaseChannel.Dev));

    [Fact]
    public void Needs_a_full_checksum_and_a_version()
    {
        Assert.Null(UpdateManifest.Parse(File(sha: "abcdef"), ReleaseChannel.Dev));
        Assert.Null(UpdateManifest.Parse(File(sha: Sha[..63] + "g"), ReleaseChannel.Dev));
        Assert.Null(UpdateManifest.Parse(File(version: "nightly"), ReleaseChannel.Dev));
        Assert.Null(UpdateManifest.Parse("""{"channel":"dev","version":"0.8.40"}""", ReleaseChannel.Dev));
        Assert.Null(UpdateManifest.Parse("[]", ReleaseChannel.Dev));
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => UpdateManifest.Parse("<html>Sign in</html>", ReleaseChannel.Dev));
    }

    [Fact]
    public void Reads_two_part_versions()
    {
        Assert.True(UpdateManifest.TryParseVersion("v1.4", out var v));
        Assert.Equal(new Version(1, 4, 0), v);
        Assert.False(UpdateManifest.TryParseVersion("nightly", out _));
    }
}

public class UpdateScheduleTests
{
    private static readonly DateTime Last = new(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void On_startup_only_checks_when_kodiak_cleaner_starts()
    {
        Assert.True(UpdateSchedule.IsDue(UpdateInterval.OnStartup, Last, Last.AddMinutes(1), atStartup: true));
        Assert.False(UpdateSchedule.IsDue(UpdateInterval.OnStartup, Last, Last.AddDays(40), atStartup: false));
    }

    [Theory]
    [InlineData(UpdateInterval.EveryHour, 1)]
    [InlineData(UpdateInterval.Every4Hours, 4)]
    [InlineData(UpdateInterval.Daily, 24)]
    [InlineData(UpdateInterval.Every3Days, 72)]
    [InlineData(UpdateInterval.Weekly, 168)]
    [InlineData(UpdateInterval.TwiceAMonth, 360)]
    public void Intervals_wait_their_time_from_the_last_check(UpdateInterval interval, int hours)
    {
        foreach (var start in new[] { true, false })
        {
            Assert.False(UpdateSchedule.IsDue(interval, Last, Last.AddHours(hours).AddMinutes(-1), start));
            Assert.True(UpdateSchedule.IsDue(interval, Last, Last.AddHours(hours), start));
            Assert.True(UpdateSchedule.IsDue(interval, null, Last, start)); // never checked: check now
        }
    }

    [Fact]
    public void Monthly_means_a_calendar_month()
    {
        Assert.False(UpdateSchedule.IsDue(UpdateInterval.Monthly, Last, Last.AddDays(30), atStartup: true)); // Oct 6 to Nov 5
        Assert.True(UpdateSchedule.IsDue(UpdateInterval.Monthly, Last, Last.AddMonths(1), atStartup: true));
    }

    [Fact]
    public void Options_list_every_interval_shortest_first()
    {
        Assert.Equal(Enum.GetValues<UpdateInterval>(), UpdateSchedule.Options.Select(o => o.Interval));
        Assert.Equal("On Startup", UpdateSchedule.Options[0].Label);
        Assert.Equal("Monthly", UpdateSchedule.Options[^1].Label);
    }
}
