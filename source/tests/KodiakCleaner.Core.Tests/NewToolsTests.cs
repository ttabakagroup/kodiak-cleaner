using KodiakCleaner.Core.Tools;
using Xunit;

namespace KodiakCleaner.Core.Tests;

public class NewToolsTests
{
    [Fact]
    public void Large_files_are_listed_biggest_first_and_protected_folders_are_skipped()
    {
        var root = Path.Combine(Path.GetTempPath(), "kc-large-" + Guid.NewGuid().ToString("N"));
        var guarded = Path.Combine(root, "Windows");
        Directory.CreateDirectory(Path.Combine(root, "a"));
        Directory.CreateDirectory(guarded);
        try
        {
            File.WriteAllBytes(Path.Combine(root, "small.bin"), new byte[10]);
            File.WriteAllBytes(Path.Combine(root, "a", "big.bin"), new byte[5000]);
            File.WriteAllBytes(Path.Combine(root, "medium.bin"), new byte[2000]);
            File.WriteAllBytes(Path.Combine(guarded, "skip.bin"), new byte[9000]);

            var found = LargeFileFinder.Find(root, 1000, 10, new[] { guarded });

            Assert.Equal(new[] { "big.bin", "medium.bin" }, found.Select(f => Path.GetFileName(f.Path)));
            Assert.Single(LargeFileFinder.Find(root, 1000, 1, new[] { guarded }));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void System_report_groups_facts_as_plain_text()
    {
        var text = SystemReport.ToText(new[]
        {
            new SystemFact("Windows", "Edition", "Windows 11 Pro"),
            new SystemFact("Windows", "Build", "26100"),
            new SystemFact("Memory", "Installed", "16 GB"),
        }, "Kodiak Cleaner 0.8.11 Dev");
        var lines = text.Split(Environment.NewLine);
        Assert.Equal(new[] { "Kodiak Cleaner 0.8.11 Dev", "", "Windows", "  Edition: Windows 11 Pro", "  Build: 26100", "", "Memory", "  Installed: 16 GB" }, lines);
    }

    [Fact]
    public void Uptime_reads_naturally()
    {
        Assert.Equal("3 days, 4 hours", SystemReport.Uptime(new TimeSpan(3, 4, 10, 0)));
        Assert.Equal("1 hour, 1 minute", SystemReport.Uptime(new TimeSpan(1, 1, 0)));
        Assert.Equal("5 minutes", SystemReport.Uptime(TimeSpan.FromMinutes(5)));
    }
}
