using KodiakCleaner.Core.Cleaning;
using KodiakCleaner.Core.Util;
using Xunit;

namespace KodiakCleaner.Core.Tests;

public class CleanCatalogTests
{
    private static string Local(string rel) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), rel);
    private static string Roaming(string rel) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), rel);

    [Fact]
    public void AdvancedItemsAreNeverTickedByDefaultAndShareOneGroup()
    {
        var advanced = CleanCatalog.Build().Where(t => t.Advanced).ToList();
        Assert.NotEmpty(advanced);
        Assert.All(advanced, t => Assert.False(t.EnabledByDefault));
        Assert.All(advanced, t => Assert.Equal(CleanCatalog.AdvancedGroup, t.Group));
    }

    [Fact]
    public void RiskyItemsAreAdvancedWithAWarningAndTasksNameTheirTask()
    {
        var catalog = CleanCatalog.Build();
        var risky = catalog.Where(t => t.Warning.Length > 0).Select(t => t.Id).ToList();
        Assert.Contains("task.shadows", risky);
        Assert.Contains("task.hibernate", risky);
        Assert.Contains("task.compactos", risky);
        Assert.Contains("task.components", risky);
        Assert.Contains("task.eventlogs", risky);
        Assert.All(catalog.Where(t => t.Warning.Length > 0), t => Assert.True(t.Advanced && !t.EnabledByDefault));
        Assert.All(catalog.Where(t => t.Kind == CleanTargetKind.Task), t =>
        {
            Assert.Equal(t.Id, t.TaskId);
            Assert.Empty(t.Rules);
        });
    }

    [Fact]
    public void NewBrowsersAppearWithTheirOwnIds()
    {
        // The browsers' folders are created where the catalog looks, then removed again.
        var made = new[]
        {
            Path.Combine(Local(@"imput\Helium\User Data"), "Default"),
            Path.Combine(Local(@"Google\Chrome\User Data"), "Default"),
            Path.Combine(Roaming(@"Floorp\Profiles"), "abc.default-release"),
            Path.Combine(Roaming(@"Mullvad\MullvadBrowser\Profiles"), "xyz.default"),
            Path.Combine(Roaming(@"Opera Software\Opera Stable"), "Default"),
            Path.Combine(Roaming(@"Waterfox\Profiles"), "w.default"),
        };
        var created = made.Where(d => !Directory.Exists(d)).ToList();
        try
        {
            foreach (var d in created) Directory.CreateDirectory(d);
            var catalog = CleanCatalog.Build();
            var ids = catalog.Select(t => t.Id).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
            Assert.Contains("helium.cache", ids);
            Assert.Contains("chrome.cache", ids);
            Assert.Contains("floorp.cache", ids);
            Assert.Contains("mullvad.cookies", ids);
            Assert.Contains("opera.cache", ids);
            Assert.Contains("waterfox.cache", ids);
            Assert.Contains(catalog, t => t.Group == "Ablaze Floorp" && t.BlockingProcesses.Contains("floorp"));
            Assert.Contains(catalog, t => t.Group == "Helium" && t.Rules.Any(r => r.Directory.EndsWith(Path.Combine("Default", "Cache"))));
            Assert.All(catalog.Where(t => !t.Advanced), t => Assert.NotEmpty(t.AppFiles));
        }
        finally
        {
            foreach (var d in created)
            {
                // Remove only what this test made: the profile folder and the browser folder above it.
                Directory.Delete(d);
                var parent = Path.GetDirectoryName(d)!;
                if (!Directory.EnumerateFileSystemEntries(parent).Any()) Directory.Delete(parent);
            }
        }
    }

    [Theory]
    [InlineData(2L << 30, 500L << 30, 1000L << 30, true)]   // 2 GB found: always worth it
    [InlineData(300L << 20, 500L << 30, 1000L << 30, false)] // 300 MB, plenty free: not worth mentioning
    [InlineData(300L << 20, 5L << 30, 1000L << 30, true)]    // 300 MB, drive nearly full
    [InlineData(300L << 20, 50L << 30, 1000L << 30, true)]   // under 10% free
    [InlineData(50L << 20, 1L << 30, 1000L << 30, false)]    // too little to matter
    public void AdvancedCleaningIsSuggestedOnlyWhenItHelps(long found, long free, long total, bool expected) =>
        Assert.Equal(expected, AdvancedAdvice.ShouldRecommend(found, (free, total)));

    [Fact]
    public void UnknownDriveNeedsALargeAmount()
    {
        Assert.False(AdvancedAdvice.ShouldRecommend(500L << 20, null));
        Assert.True(AdvancedAdvice.ShouldRecommend(1L << 30, null));
    }

    [Fact]
    public void ListIsWindowsFirstThenProgramsAToZWithItemsAToZ()
    {
        CleanTarget T(string group, string name, bool advanced = false) => new() { Id = group + name, Group = group, Name = name, Advanced = advanced };
        var ordered = CleanCatalog.Order(new[]
        {
            T("Microsoft Edge", "Internet Cache"), T(CleanCatalog.AdvancedGroup, "Prefetch Data", true), T("Windows", "Thumbnail Cache"),
            T("Brave", "Cookies"), T(CleanCatalog.AdvancedGroup, "DNS Cache", true), T("Windows", "Recycle Bin"), T("Brave", "Browsing History"),
        });
        Assert.Equal(new[]
        {
            "Windows/Recycle Bin", "Windows/Thumbnail Cache", "Brave/Browsing History", "Brave/Cookies", "Microsoft Edge/Internet Cache",
            "Advanced Cleaning/DNS Cache", "Advanced Cleaning/Prefetch Data",
        }, ordered.Select(t => $"{t.Group}/{t.Name}"));

        // The real list follows the same rule.
        var built = CleanCatalog.Build();
        Assert.Equal(CleanCatalog.Order(built).Select(t => t.Id), built.Select(t => t.Id));
        Assert.Equal("Windows", built[0].Group);
    }
}

public class CleanerSortAndScheduleTests
{
    private static CleanTarget T(string group, string name, bool advanced = false) => new() { Id = group + name, Group = group, Name = name, Advanced = advanced };

    [Fact]
    public void CustomOrderMovesGroupsAndItemsButKeepsAdvancedLast()
    {
        var values = new Dictionary<string, long> { ["BraveCookies"] = 5, ["WindowsRecycle Bin"] = 9, ["Advanced CleaningDNS Cache"] = 100 };
        var ordered = CleanCatalog.Order(new[]
        {
            T("Windows", "Recycle Bin"), T("Windows", "Thumbnail Cache"), T("Brave", "Cookies"), T("Brave", "Browsing History"),
            T("Microsoft Edge", "Internet Cache"), T("Advanced Cleaning", "DNS Cache", true),
        }, t => values.GetValueOrDefault(t.Id));
        Assert.Equal(new[]
        {
            "Windows/Recycle Bin", "Windows/Thumbnail Cache", "Brave/Cookies", "Brave/Browsing History", "Microsoft Edge/Internet Cache",
            "Advanced Cleaning/DNS Cache",
        }, ordered.Select(t => $"{t.Group}/{t.Name}"));
    }

    [Fact]
    public void HistoryAddsUpAndSurvivesTheIniFile()
    {
        var h = new CleanerHistory();
        var when = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
        h.Record("win.usertemp", when, 100);
        h.Record("win.usertemp", when.AddDays(1), 50);
        var ini = new IniFile();
        h.Write(ini);
        var back = CleanerHistory.Read(IniFile.Parse(ini.Serialize()))["win.usertemp"];
        Assert.Equal(new CleanStat(when.AddDays(1), 50, 150), back);
        Assert.Equal("Never", CleanerHistory.Describe(null, DateTime.Now));
    }

    [Fact]
    public void ScheduleRoundTripsAndDescribesItself()
    {
        var s = new CleaningSchedule
        {
            Name = "Browsers", Targets = { "chrome.cache", "msedge.cache" }, Days = { DayOfWeek.Friday, DayOfWeek.Monday },
            Time = new TimeSpan(21, 30, 0),
        };
        Assert.Null(s.Problem());
        Assert.Equal("Every Monday and Friday at 9:30 PM", s.Describe());
        var ini = new IniFile();
        s.Write(ini);
        var back = Assert.Single(CleaningSchedule.ReadAll(IniFile.Parse(ini.Serialize())));
        Assert.Equal(s.Id, back.Id);
        Assert.Equal(s.Targets, back.Targets);
        Assert.Equal(new[] { DayOfWeek.Monday, DayOfWeek.Friday }, back.Days);
        Assert.Equal(s.Time, back.Time);

        var monthly = new CleaningSchedule { Name = "General", Targets = { "win.usertemp" }, Repeat = ScheduleRepeat.Monthly, DayOfMonth = CleaningSchedule.LastDay };
        Assert.Equal("Monthly on the last day at 9:00 AM", monthly.Describe());
        Assert.Equal(new DateTime(2026, 10, 31, 9, 0, 0), monthly.NextRun(new DateTime(2026, 10, 4, 15, 0, 0)));
        Assert.Equal(new DateTime(2026, 10, 5, 21, 30, 0), s.NextRun(new DateTime(2026, 10, 4, 15, 0, 0))); // a Sunday
        Assert.Equal("Pick at least one day of the week.", new CleaningSchedule { Name = "x", Targets = { "a" } }.Problem());
    }

    [Fact]
    public void TaskDefinitionIsValidXmlWithTheRightTrigger()
    {
        var s = new CleaningSchedule { Name = "A & B", Targets = { "a" }, Repeat = ScheduleRepeat.Monthly, DayOfMonth = 15 };
        var xml = s.TaskXml(@"C:\Program Files\Kodiak Cleaner\KodiakCleaner.exe", "/schedule:" + s.Id, @"PC\Ted", "A & B: monthly", new DateTime(2026, 10, 4));
        var doc = System.Xml.Linq.XDocument.Parse(xml.Replace("encoding=\"UTF-16\"", ""));
        System.Xml.Linq.XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        Assert.Equal("15", doc.Descendants(ns + "Day").Single().Value);
        Assert.Equal(12, doc.Descendants(ns + "Months").Single().Elements().Count());
        Assert.Equal("LeastPrivilege", doc.Descendants(ns + "RunLevel").Single().Value);
        Assert.Equal("true", doc.Descendants(ns + "StartWhenAvailable").Single().Value);
        Assert.Equal("2026-10-04T09:00:00", doc.Descendants(ns + "StartBoundary").Single().Value);
    }
}

public class DefaultSortTests
{
    [Fact]
    public void DefaultKeepsWindowsFirstAndAToZPutsItInItsPlace()
    {
        CleanTarget T(string group, string name, bool advanced = false) => new() { Id = group + name, Group = group, Name = name, Advanced = advanced };
        var list = new[] { T("Windows", "Recycle Bin"), T("Brave", "Cookies"), T("Waterfox", "Cookies"), T("Advanced Cleaning", "DNS Cache", true) };
        Assert.Equal(new[] { "Windows", "Brave", "Waterfox", "Advanced Cleaning" },
            CleanCatalog.Order(list, _ => 0).Select(t => t.Group));
        Assert.Equal(new[] { "Brave", "Waterfox", "Windows", "Advanced Cleaning" },
            CleanCatalog.Order(list, _ => 0, windowsFirst: false).Select(t => t.Group));
        Assert.Equal(CleanSort.Default, default(CleanSort));
    }
}

public class TickedFirstTests
{
    [Fact]
    public void DefaultPutsTickedItemsFirstInEachGroupAToZ()
    {
        CleanTarget T(string group, string name) => new() { Id = group + "/" + name, Group = group, Name = name };
        var list = new[]
        {
            T("Windows", "Thumbnail Cache"), T("Windows", "Recycle Bin"), T("Windows", "Memory Dumps"), T("Windows", "DirectX Shader Cache"),
            T("Brave", "Cookies"), T("Brave", "Internet Cache"), T("Brave", "Browsing History"),
        };
        var ticked = new HashSet<string> { "Windows/Thumbnail Cache", "Windows/Memory Dumps", "Brave/Internet Cache" };
        Assert.Equal(new[]
        {
            "Windows/Memory Dumps", "Windows/Thumbnail Cache", "Windows/DirectX Shader Cache", "Windows/Recycle Bin",
            "Brave/Internet Cache", "Brave/Browsing History", "Brave/Cookies",
        }, CleanCatalog.Order(list, _ => 0, ticked: t => ticked.Contains(t.Id)).Select(t => t.Id));
    }
}
