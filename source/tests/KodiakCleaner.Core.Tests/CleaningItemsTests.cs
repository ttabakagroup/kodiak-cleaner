using KodiakCleaner.Core.Cleaning;
using KodiakCleaner.Core.Util;
using Xunit;

namespace KodiakCleaner.Core.Tests;

/// <summary>The cleaning items added from Ted's two lists (2026-10-12): browsers, programs, Windows, Advanced Cleaning and Downloads.</summary>
public sealed class CleaningItemsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kodiakcleaner-items-" + Guid.NewGuid().ToString("N"));

    public CleaningItemsTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static string Roaming(string rel) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), rel);
    private static string Local(string rel) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), rel);

    [Fact]
    public void FilteredRuleRemovesOnlyPickedFilesAndNeverRecurses()
    {
        var folder = Path.Combine(_root, "Downloads");
        Directory.CreateDirectory(Path.Combine(folder, "unpacked"));
        foreach (var f in new[] { "setup.exe", "photo.JPG", "notes.pdf", "song.mp3", "data.zip", "thing.xyz", "half.crdownload", "desktop.ini", @"unpacked/tool.exe" })
            File.WriteAllBytes(Path.Combine(folder, f), new byte[5]);

        string[] Picked(string kind) => FileCleaner.Analyze(new[] { new CleanRule(folder, "*", Recursive: false, Filter: f => CleanCatalog.DownloadKind(f) == kind) })
            .Files.Select(f => Path.GetFileName(f.Path)).OrderBy(n => n).ToArray();

        Assert.Equal(new[] { "setup.exe" }, Picked("Programs"));
        Assert.Equal(new[] { "photo.JPG" }, Picked("Pictures"));
        Assert.Equal(new[] { "notes.pdf" }, Picked("Documents"));
        Assert.Equal(new[] { "song.mp3" }, Picked("Audio and Video"));
        Assert.Equal(new[] { "data.zip" }, Picked("Compressed Files"));
        Assert.Equal(new[] { "thing.xyz" }, Picked("Other Files")); // never unfinished downloads or desktop.ini
    }

    [Fact]
    public void FilteredRuleNeedsTwoLevelsAndAFilterThatThrowsKeepsTheFile()
    {
        var root = Path.GetPathRoot(_root)!;
        Assert.False(PathSafety.IsSafeCleanRoot(Path.Combine(root, "One"), recursive: false, "*", filtered: true));
        Assert.True(PathSafety.IsSafeCleanRoot(Path.Combine(root, "One", "Two"), recursive: false, "*", filtered: true));

        File.WriteAllBytes(Path.Combine(_root, "a.lnk"), new byte[5]);
        var found = FileCleaner.Analyze(new[] { new CleanRule(_root, "*.lnk", Recursive: false, Filter: _ => throw new IOException("unreadable")) });
        Assert.Empty(found.Files);
    }

    [Fact]
    public void MissingPathEntriesAreRemovedAndNothingElseChanges()
    {
        var gone = new HashSet<string> { @"C:\Old Tool\bin" };
        var (value, removed) = PathSafety.RemoveMissingPathEntries(@"%SystemRoot%\system32;C:\Old Tool\bin;;C:\Tools", gone.Contains);
        Assert.Equal(@"%SystemRoot%\system32;C:\Tools", value);
        Assert.Equal(new[] { @"C:\Old Tool\bin" }, removed);

        const string clean = @"C:\A;;C:\B;";
        Assert.Equal((clean, 0), (PathSafety.RemoveMissingPathEntries(clean, _ => false).Value, PathSafety.RemoveMissingPathEntries(clean, _ => false).Removed.Count));
    }

    [Fact]
    public void UncertainPathsAreNeverMissing()
    {
        Assert.False(PathSafety.IsMissingOnFixedDrive(""));
        Assert.False(PathSafety.IsMissingOnFixedDrive(@"%NOT_SET%\bin"));
        Assert.False(PathSafety.IsMissingOnFixedDrive("relative\\bin"));
        Assert.False(PathSafety.IsMissingOnFixedDrive(@"\\server\share\tool.exe"));
        Assert.False(PathSafety.IsMissingOnFixedDrive(_root)); // it exists
    }

    [Fact]
    public void NewRiskyItemsAskFirstAndSafeOnesDont()
    {
        var catalog = CleanCatalog.Build().ToDictionary(t => t.Id);
        foreach (var id in new[] { "adv.jumplists", "task.shellbags", "task.envpath", "task.wipefree" })
            Assert.True(catalog[id].Advanced && catalog[id].Warning.Length > 0, id);
        foreach (var id in new[] { "task.explorermru", "task.typedurls", "task.userassist", "task.menuorder", "task.regedit", "task.7zip",
                     "adv.etl", "adv.driverlogs", "adv.iislogs", "adv.inetcookies" })
            Assert.True(catalog[id].Advanced && catalog[id].Warning.Length == 0, id);
        Assert.True(catalog["task.wipefree"].RequiresAdmin);
        var downloads = catalog.Values.Where(t => t.Id.StartsWith("downloads.", StringComparison.Ordinal)).ToList();
        Assert.Equal(6, downloads.Count);
        Assert.All(downloads, t => Assert.True(t.Advanced && t.Warning.Length > 0 && t.Rules.All(r => !r.Recursive && r.Filter is not null)));
        foreach (var id in new[] { "win.startmenulinks", "win.desktoplinks", "win.widgets", "win.notifications", "win.gameexplorer" })
            Assert.Equal("Windows", catalog[id].Group);
    }

    [Fact]
    public void BrowserAndProgramItemsAppearWhenInstalled()
    {
        var made = new[]
        {
            Path.Combine(Local(@"Google\Chrome\User Data"), "Default"),
            Path.Combine(Roaming(@"Mozilla\Firefox\Profiles"), "t.default-release"),
            Roaming("discord"), Roaming("Zoom"), Roaming(@"Telegram Desktop\tdata"),
        };
        var created = made.Where(d => !Directory.Exists(d)).ToList();
        try
        {
            foreach (var d in created) Directory.CreateDirectory(d);
            var catalog = CleanCatalog.Build();
            var ids = catalog.Select(t => t.Id).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
            foreach (var id in new[] { "chrome.metrics", "chrome.session", "chrome.bookmarksbackup", "firefox.session", "firefox.crashreports",
                         "firefox.siteprefs", "firefox.formdata", "firefox.bookmarksbackup", "discord.cache", "zoom.logs", "telegram.cache" })
                Assert.Contains(id, ids);
            var byId = catalog.ToDictionary(t => t.Id);
            // Passwords and Chromium's Web Data (which also holds payment cards) only under Advanced Cleaning, asking first.
            foreach (var id in new[] { "chrome.passwords", "chrome.formdata", "firefox.passwords" })
                Assert.True(byId[id].Advanced && byId[id].Warning.Length > 0 && byId[id].Name.Contains('('), id);
            Assert.True(byId["chrome.metrics"].EnabledByDefault);
            Assert.Contains(byId["discord.cache"].BlockingProcesses, p => p == "Discord");
            Assert.All(catalog.Where(t => !t.Advanced), t => Assert.NotEmpty(t.AppFiles));
            Assert.Equal(CleanCatalog.Order(catalog).Select(t => t.Id), catalog.Select(t => t.Id));
        }
        finally
        {
            foreach (var d in created.OrderByDescending(d => d.Length))
            {
                Directory.Delete(d);
                var parent = Path.GetDirectoryName(d)!;
                while (parent.Length > 3 && Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any()
                       && !parent.Equals(Roaming(""), StringComparison.Ordinal) && !parent.Equals(Local(""), StringComparison.Ordinal)
                       && !Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar).Equals(Path.GetFullPath(Roaming("")).TrimEnd(Path.DirectorySeparatorChar))
                       && !Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar).Equals(Path.GetFullPath(Local("")).TrimEnd(Path.DirectorySeparatorChar)))
                {
                    Directory.Delete(parent);
                    parent = Path.GetDirectoryName(parent)!;
                }
            }
        }
    }
}
