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

public class UpdateParserTests
{
    private const string Json = """
        [
         {"tag_name":"v1.3.0","html_url":"https://github.com/o/r/releases/tag/v1.3.0","draft":false,"prerelease":false,
          "assets":[{"name":"KodiakCleaner-1.3.0-x64.msi","browser_download_url":"https://x/KodiakCleaner-1.3.0-x64.msi","size":100,"digest":"sha256:abcdef"},
                    {"name":"KodiakCleaner-1.3.0-x64-portable.zip","browser_download_url":"https://x/p.zip","size":50}]},
         {"tag_name":"v1.4.0","draft":true,"prerelease":false,"assets":[]},
         {"tag_name":"v2.0.0","draft":false,"prerelease":true,"assets":[]},
         {"tag_name":"v0.8.4-dev","draft":false,"prerelease":true,"assets":[]},
         {"tag_name":"v0.8.12-dev","draft":false,"prerelease":true,"assets":[]},
         {"tag_name":"v0.9.1-beta","draft":false,"prerelease":true,"assets":[]},
         {"tag_name":"nightly","draft":false,"prerelease":false,"assets":[]}
        ]
        """;

    [Fact]
    public void Stable_picks_newest_published_stable_release_and_finds_msi_with_digest()
    {
        var info = UpdateParser.PickLatest(Json, ReleaseChannel.Stable)!;
        Assert.Equal(new Version(1, 3, 0), info.Version); // drafts and pre-releases skipped
        Assert.Equal("ABCDEF", info.Msi!.Sha256);
        Assert.Null(info.Assets[1].Sha256);
        Assert.True(UpdateParser.IsNewer(info, new Version(1, 2, 0, 0)));
        Assert.False(UpdateParser.IsNewer(info, new Version(1, 3, 0)));
    }

    [Fact]
    public void Each_channel_only_sees_its_own_releases()
    {
        Assert.Equal(new Version(0, 8, 12), UpdateParser.PickLatest(Json, ReleaseChannel.Dev)!.Version);
        Assert.Equal(new Version(0, 9, 1), UpdateParser.PickLatest(Json, ReleaseChannel.Beta)!.Version);
        Assert.Null(UpdateParser.PickLatest("[]", ReleaseChannel.Dev));
    }

    [Fact]
    public void Finds_the_installer_by_its_fixed_name()
    {
        const string json = """
            [{"tag_name":"v0.8.31-dev","assets":[
              {"name":"KodiakCleaner-Dev-Portable.zip","browser_download_url":"https://x/p.zip","size":50},
              {"name":"KodiakCleaner-Dev-Setup.zip","browser_download_url":"https://x/s.zip","size":40}]},
             {"tag_name":"v0.8.37-dev","assets":[
              {"name":"KodiakCleaner-Dev-Portable.zip","browser_download_url":"https://x/p2.zip","size":50},
              {"name":"KodiakCleaner-Dev-Setup.msi","browser_download_url":"https://x/s2.msi","size":40}]},
             {"tag_name":"latest-dev","assets":[]}]
            """;
        var info = UpdateParser.PickLatest(json, ReleaseChannel.Dev)!;
        Assert.Equal("v0.8.37-dev", info.Tag);
        Assert.Equal("https://x/s2.msi", info.Msi!.DownloadUrl); // the bare MSI since 0.8.37
        const string before = """
            [{"tag_name":"v0.8.31-dev","assets":[
              {"name":"KodiakCleaner-Dev-Portable.zip","browser_download_url":"https://x/p.zip","size":50},
              {"name":"KodiakCleaner-Dev-Setup.zip","browser_download_url":"https://x/s.zip","size":40}]}]
            """;
        Assert.Equal("https://x/s.zip", UpdateParser.PickLatest(before, ReleaseChannel.Dev)!.Msi!.DownloadUrl); // 0.8.31 to 0.8.36
    }

    [Fact]
    public void Prefers_the_zipped_msi_and_skips_incomplete_assets()
    {
        const string json = """
            [{"tag_name":"v0.8.13-dev","draft":false,"prerelease":true,"assets":[
              {"name":"broken.msi","size":"big"},
              {"name":"KodiakCleaner-0.8.13-x64-portable-dev.zip","browser_download_url":"https://x/p.zip","size":50},
              {"name":"KodiakCleaner-0.8.13-x64-msi-dev.zip","browser_download_url":"https://x/m.zip","size":40,"digest":"sha256:0a"}]},
             {"tag_name":5,"assets":null}]
            """;
        var info = UpdateParser.PickLatest(json, ReleaseChannel.Dev)!;
        Assert.Equal(2, info.Assets.Count);
        Assert.Equal("https://x/m.zip", info.Msi!.DownloadUrl);
        Assert.Equal("0A", info.Msi.Sha256);
    }

    [Fact]
    public void Reads_two_part_versions()
    {
        Assert.True(UpdateParser.TryParseVersion("v1.4", out var v));
        Assert.Equal(new Version(1, 4, 0), v);
        Assert.False(UpdateParser.TryParseVersion("nightly", out _));
    }
}
