using KodiakCleaner.Core.Cleaning;

namespace KodiakCleaner.Core.Tests;

public sealed class FileCleanerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kodiakcleaner-tests-" + Guid.NewGuid().ToString("N"));

    public FileCleanerTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string Touch(string relative, int bytes = 10, DateTime? lastWriteUtc = null)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
        if (lastWriteUtc is { } t)
        {
            File.SetCreationTimeUtc(path, t);
            File.SetLastWriteTimeUtc(path, t);
        }
        return path;
    }

    [Fact]
    public void Analyze_finds_files_recursively_and_sums_sizes()
    {
        Touch("a.tmp", 100);
        Touch(@"sub/b.tmp", 50);

        var result = FileCleaner.Analyze(new[] { new CleanRule(_root) });

        Assert.Equal(2, result.Files.Count);
        Assert.Equal(150, result.TotalBytes);
    }

    [Fact]
    public void Analyze_respects_pattern_and_non_recursive()
    {
        Touch("thumbcache_1.db");
        Touch("other.db");
        Touch(@"sub/thumbcache_2.db");

        var result = FileCleaner.Analyze(new[] { new CleanRule(_root, "thumbcache_*.db", Recursive: false) });

        Assert.Single(result.Files);
        Assert.EndsWith("thumbcache_1.db", result.Files[0].Path);
    }

    [Fact]
    public void Analyze_skips_files_newer_than_min_age()
    {
        var now = DateTime.UtcNow;
        Touch("old.tmp", lastWriteUtc: now.AddDays(-3));
        Touch("new.tmp", lastWriteUtc: now.AddHours(-1));

        var result = FileCleaner.Analyze(new[] { new CleanRule(_root, MinAge: TimeSpan.FromDays(1)) }, nowUtc: now);

        Assert.Single(result.Files);
        Assert.EndsWith("old.tmp", result.Files[0].Path);
    }

    [Fact]
    public void Analyze_ignores_unsafe_roots()
    {
        var root = Path.GetPathRoot(_root)!;
        var result = FileCleaner.Analyze(new[]
        {
            new CleanRule(root),
            new CleanRule(""),
            new CleanRule("%TEMP%"),
        });
        Assert.Empty(result.Files);
    }

    [Fact]
    public void Analyze_does_not_follow_directory_symlinks()
    {
        var outside = Path.Combine(Path.GetTempPath(), "kodiakcleaner-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        try
        {
            File.WriteAllText(Path.Combine(outside, "precious.txt"), "keep me");
            Directory.CreateSymbolicLink(Path.Combine(_root, "link"), outside);

            var analysis = FileCleaner.Analyze(new[] { new CleanRule(_root) });
            FileCleaner.Clean(analysis);

            Assert.True(File.Exists(Path.Combine(outside, "precious.txt")));
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void Clean_deletes_files_and_empty_subdirectories_but_keeps_root()
    {
        Touch(@"sub/deeper/x.tmp", 20);
        Touch("y.tmp", 30);

        var analysis = FileCleaner.Analyze(new[] { new CleanRule(_root) });
        var result = FileCleaner.Clean(analysis);

        Assert.Equal(2, result.FilesDeleted);
        Assert.Equal(50, result.BytesFreed);
        Assert.Equal(2, result.DirectoriesRemoved);
        Assert.True(Directory.Exists(_root));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_root));
    }

    [Fact]
    public void Clean_keeps_directories_that_still_hold_recent_files()
    {
        var now = DateTime.UtcNow;
        Touch(@"sub/old.tmp", lastWriteUtc: now.AddDays(-5));
        Touch(@"sub/new.tmp", lastWriteUtc: now);

        var analysis = FileCleaner.Analyze(new[] { new CleanRule(_root, MinAge: TimeSpan.FromDays(1)) }, nowUtc: now);
        FileCleaner.Clean(analysis);

        Assert.True(File.Exists(Path.Combine(_root, "sub", "new.tmp")));
        Assert.False(File.Exists(Path.Combine(_root, "sub", "old.tmp")));
    }

    [Fact]
    public void Clean_removes_read_only_files()
    {
        var path = Touch("ro.tmp");
        File.SetAttributes(path, FileAttributes.ReadOnly);

        var result = FileCleaner.Clean(FileCleaner.Analyze(new[] { new CleanRule(_root) }));

        Assert.Equal(1, result.FilesDeleted);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Young_empty_folders_are_kept_when_a_min_age_is_set()
    {
        var now = DateTime.UtcNow;
        Directory.CreateDirectory(Path.Combine(_root, "justmade"));

        var aged = FileCleaner.Analyze(new[] { new CleanRule(_root, MinAge: TimeSpan.FromDays(1)) }, nowUtc: now);
        var any = FileCleaner.Analyze(new[] { new CleanRule(_root) }, nowUtc: now);

        Assert.Empty(aged.CandidateDirectories);
        Assert.Single(any.CandidateDirectories);
    }
}
