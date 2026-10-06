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
    public void System_report_text_lists_items_under_their_section()
    {
        var text = SystemReport.ToText(new[]
        {
            new SystemFact("Storage", "Serial Number", "S1", "Disk 0: Fast SSD"),
            new SystemFact("Storage", "Size", "1 TB", "Disk 0: Fast SSD"),
            new SystemFact("Storage", "Serial Number", "S2", "Disk 1: Big HDD"),
        }, "Report", new DateTime(2026, 10, 6, 15, 30, 0));
        Assert.Equal(new[] { "Report", "Created 2026-10-06 15:30", "", "Storage", "  Disk 0: Fast SSD", "    Serial Number: S1", "    Size: 1 TB",
                             "  Disk 1: Big HDD", "    Serial Number: S2" }, text.Split(Environment.NewLine));
    }

    [Fact]
    public void System_report_csv_quotes_cells_and_defuses_formulas()
    {
        var csv = SystemReport.ToCsv(new[]
        {
            new SystemFact("Network", "DNS Servers", "1.1.1.1, 8.8.8.8", "Wi-Fi"),
            new SystemFact("Memory", "Part Number", "=HYPERLINK(\"x\")"),
            new SystemFact("Windows", "Edition", "Windows 11 Pro"),
        });
        var rows = csv.Split("\r\n");
        Assert.Equal("Section,Item,Name,Value", rows[0]);
        Assert.Equal("Network,Wi-Fi,DNS Servers,\"1.1.1.1, 8.8.8.8\"", rows[1]);
        Assert.Equal("Memory,,Part Number,\"'=HYPERLINK(\"\"x\"\")\"", rows[2]);
        Assert.Equal("Windows,,Edition,Windows 11 Pro", rows[3]);
        Assert.Equal("'-5", SystemReport.Cell("-5"));
        Assert.Equal("'@sum", SystemReport.Cell("@sum"));
    }

    [Fact]
    public void System_report_html_is_escaped_self_contained_and_themed()
    {
        var style = new ReportStyle("#101010", "#202020", "#F0F0F0", "#A0A0A0", "#303030", "#E0A030", "#F0C060", "Segoe UI\"</style><script>");
        var html = SystemReport.ToHtml(new[]
        {
            new SystemFact("Graphics", "Driver Version", "<b>31.0</b>", "Card & Co"),
            new SystemFact("Windows", "Edition", "Windows 11 Pro"),
        }, "Kodiak Cleaner <Report>", new DateTime(2026, 10, 6, 15, 30, 0), style);
        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("&lt;b&gt;31.0&lt;/b&gt;", html);
        Assert.Contains("<h3>Card &amp; Co</h3>", html);
        Assert.Contains("<title>Kodiak Cleaner &lt;Report&gt;</title>", html);
        Assert.Contains("--bg:#101010", html);
        Assert.Contains("--accent:#E0A030", html);
        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("http://", html);
        Assert.DoesNotContain("https://", html);
        Assert.Contains("href=\"#s0\">Graphics</a>", html);
        // A color that isn't #RRGGBB never reaches the page.
        var odd = SystemReport.ToHtml(Array.Empty<SystemFact>(), "R", DateTime.Now, style with { Background = "red;}body{x" });
        Assert.Contains("--bg:inherit", odd);
    }

    [Fact]
    public void System_report_files_are_named_by_date_and_format()
    {
        var when = new DateTime(2026, 10, 6, 15, 30, 12);
        Assert.Equal("System Report 2026-10-06 153012.html", SystemReport.FileName(when, ReportFormat.Html));
        Assert.Equal("System Report 2026-10-06 153012.csv", SystemReport.FileName(when, ReportFormat.Csv));
        Assert.Equal("System Report 2026-10-06 153012.txt", SystemReport.FileName(when, ReportFormat.Text));
        var labels = SystemReport.Formats.Select(f => f.Label).ToList();
        Assert.Equal(labels.OrderBy(l => l, StringComparer.OrdinalIgnoreCase), labels); // A to Z
        Assert.Equal(Enum.GetValues<ReportFormat>().Length, labels.Count);
    }

    [Fact]
    public void Advanced_details_name_hardware_plainly()
    {
#pragma warning disable CA1416 // pure helpers, no Windows calls
        Assert.Equal("DDR4", SystemDetails.MemoryType(26, 0));
        Assert.Equal("DDR5", SystemDetails.MemoryType(34, 0));
        Assert.Equal("DDR3", SystemDetails.MemoryType(0, 24));
        Assert.Null(SystemDetails.MemoryType(0, 0));
        Assert.Equal("SO-DIMM", SystemDetails.FormFactor(12));
        Assert.Equal("NVMe", SystemDetails.BusType(17));
        Assert.Equal("SATA", SystemDetails.BusType(11));
        Assert.Equal("Solid-state drive (SSD)", SystemDetails.MediaType(4));
        Assert.Equal("Healthy", SystemDetails.Health(0));
        Assert.Equal("00-1A-2B-3C-4D-5E", SystemDetails.Mac(new byte[] { 0x00, 0x1A, 0x2B, 0x3C, 0x4D, 0x5E }));
        Assert.Null(SystemDetails.Mac(Array.Empty<byte>()));
        Assert.Null(SystemDetails.Mac(new byte[6]));
        Assert.Equal("1 Gbps", SystemDetails.LinkSpeed(1_000_000_000));
        Assert.Equal("866.7 Mbps", SystemDetails.LinkSpeed(866_700_000));
        Assert.Equal("3.70 GHz", SystemDetails.Ghz(3700));
        Assert.Equal("45 °C", SystemDetails.Celsius(3181));
        Assert.Null(SystemDetails.Clean("To be filled by O.E.M."));
        Assert.Null(SystemDetails.Clean("  Default string "));
        Assert.Equal("S4EWNX0R123456", SystemDetails.Clean(" S4EWNX0R123456 "));
#pragma warning restore CA1416
    }

    [Fact]
    public void Uptime_reads_naturally()
    {
        Assert.Equal("3 days, 4 hours", SystemReport.Uptime(new TimeSpan(3, 4, 10, 0)));
        Assert.Equal("1 hour, 1 minute", SystemReport.Uptime(new TimeSpan(1, 1, 0)));
        Assert.Equal("5 minutes", SystemReport.Uptime(TimeSpan.FromMinutes(5)));
    }
}
