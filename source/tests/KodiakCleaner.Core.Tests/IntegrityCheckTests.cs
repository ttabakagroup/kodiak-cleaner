using System.Text;
using KodiakCleaner.Core.Tools;
using Xunit;

namespace KodiakCleaner.Core.Tests;

public class IntegrityCheckTests
{
    [Theory]
    [InlineData("Windows Resource Protection did not find any integrity violations.", false, IntegrityResult.Healthy)]
    [InlineData("Windows Resource Protection found corrupt files and successfully repaired them.", true, IntegrityResult.Repaired)]
    [InlineData("Windows Resource Protection found corrupt files and successfully repaired them. Some of the repairs will take effect after reboot.", true, IntegrityResult.RestartNeeded)]
    [InlineData("Windows Resource Protection found corrupt files but was unable to fix some of them.", true, IntegrityResult.ProblemsFound)]
    [InlineData("Windows Resource Protection found integrity violations.", false, IntegrityResult.ProblemsFound)]
    [InlineData("There is a system repair pending which requires reboot to complete.", true, IntegrityResult.RestartNeeded)]
    [InlineData("Windows Resource Protection could not perform the requested operation.", true, IntegrityResult.CouldNotCheck)]
    [InlineData("Der Windows-Ressourcenschutz hat keine Integritätsverletzungen gefunden.", false, IntegrityResult.Finished)]
    public void Reads_system_file_check_results(string text, bool repair, IntegrityResult expected) =>
        Assert.Equal(expected, IntegrityCheck.ReadSfc(new[] { "Beginning system scan.", "Verification 100% complete.", text }, repair));

    [Fact]
    public void Component_store_runs_in_english_and_reads_its_results()
    {
        Assert.Equal(new[] { "/Online", "/Cleanup-Image", "/ScanHealth", "/English" }, IntegrityCheck.DismArgs(false));
        Assert.Contains("/RestoreHealth", IntegrityCheck.DismArgs(true));
        Assert.Equal(IntegrityResult.Healthy, IntegrityCheck.ReadDism(0, new[] { "No component store corruption detected.", "The operation completed successfully." }, false));
        Assert.Equal(IntegrityResult.ProblemsFound, IntegrityCheck.ReadDism(0, new[] { "The component store is repairable.", "The operation completed successfully." }, false));
        Assert.Equal(IntegrityResult.Repaired, IntegrityCheck.ReadDism(0, new[] { "The restore operation completed successfully.", "The component store corruption was repaired." }, true));
        Assert.Equal(IntegrityResult.ProblemsFound, IntegrityCheck.ReadDism(-2146498529, new[] { "Error: 0x800f081f", "The source files could not be found." }, true));
        Assert.Equal(IntegrityResult.RestartNeeded, IntegrityCheck.ReadDism(3010, new[] { "The operation completed." }, true));
        Assert.Equal(IntegrityResult.CouldNotCheck, IntegrityCheck.ReadDism(0, Array.Empty<string>(), false));
        Assert.Equal("Error: 0x800f081f", IntegrityCheck.DismError(new[] { "x", "Error: 0x800f081f" }));
    }

    [Fact]
    public void Drive_check_arguments_never_lock_a_drive_unless_repairing()
    {
        Assert.Equal(new[] { "D:", "/scan" }, IntegrityCheck.ChkdskArgs("D", "NTFS", repair: false, badSectors: false, windowsDrive: false));
        Assert.Equal(new[] { "E:" }, IntegrityCheck.ChkdskArgs("E:", "exFAT", repair: false, badSectors: false, windowsDrive: false));
        Assert.Equal(new[] { "D:", "/f", "/x" }, IntegrityCheck.ChkdskArgs("D", "NTFS", repair: true, badSectors: false, windowsDrive: false));
        Assert.Equal(new[] { "D:", "/r", "/x" }, IntegrityCheck.ChkdskArgs("D", "NTFS", repair: true, badSectors: true, windowsDrive: false));
        Assert.Equal(new[] { "C:", "/f" }, IntegrityCheck.ChkdskArgs("C:\\", "NTFS", repair: true, badSectors: false, windowsDrive: true)); // scheduled, never dismounted
        Assert.True(IntegrityCheck.CanCheck("NTFS"));
        Assert.True(IntegrityCheck.CanCheck("exFAT"));
        Assert.False(IntegrityCheck.CanCheck("ReFS"));
    }

    [Fact]
    public void Drive_check_exit_codes_and_restart_schedule()
    {
        Assert.Equal(IntegrityResult.Healthy, IntegrityCheck.ReadChkdsk(0, false, false));
        Assert.Equal(IntegrityResult.Healthy, IntegrityCheck.ReadChkdsk(2, false, false));
        Assert.Equal(IntegrityResult.Repaired, IntegrityCheck.ReadChkdsk(1, true, false));
        Assert.Equal(IntegrityResult.ProblemsFound, IntegrityCheck.ReadChkdsk(3, false, false));
        Assert.Equal(IntegrityResult.RestartNeeded, IntegrityCheck.ReadChkdsk(0, true, true));
        Assert.True(IntegrityCheck.ScheduledIn(new[] { "autocheck autochk /p \\??\\C:", "autocheck autochk *" }, "c"));
        Assert.False(IntegrityCheck.ScheduledIn(new[] { "autocheck autochk *" }, "C:"));
    }

    [Theory]
    [InlineData("Verification 45% complete.", 45)]
    [InlineData("[==========================59.5%=====                     ]", 59)]
    [InlineData("Progress: 12345 of 50000 done; Stage:  80%; Total:  24%; ETA:   0:00:10 ..", 24)]
    [InlineData("Stage 1: Examining basic file system structure ...", null)]
    public void Reads_progress(string line, int? expected) => Assert.Equal(expected, IntegrityCheck.PercentOf(line));

    [Fact]
    public async Task Decodes_utf16_and_oem_output_into_lines()
    {
        var got = new List<string>();
        var sink = new Sink(got.Add);
        await IntegrityCheck.Pump(new MemoryStream(Encoding.Unicode.GetBytes("Beginning\r\nVerification 5% complete.\rVerification 9% complete.\r\nDone\r\n")), sink);
        Assert.Equal(new[] { "Beginning", "Verification 5% complete.", "Verification 9% complete.", "Done" }, got);
        got.Clear();
        await IntegrityCheck.Pump(new MemoryStream(Encoding.ASCII.GetBytes("The type of the file system is NTFS.\r\n\r\nNo problems.\n")), sink);
        Assert.Equal(new[] { "The type of the file system is NTFS.", "No problems." }, got);
        Assert.True(IntegrityCheck.LooksLikeUtf16(new byte[] { 0xFF, 0xFE, 0x41, 0 }));
        Assert.False(IntegrityCheck.LooksLikeUtf16(Encoding.ASCII.GetBytes("abcd")));
    }

    private sealed class Sink(Action<string> add) : IProgress<string>
    {
        public void Report(string value) => add(value);
    }

    [Theory]
    [InlineData(2L, null, null, null, IntegrityResult.ProblemsFound)]
    [InlineData(1L, null, null, null, IntegrityResult.Attention)]
    [InlineData(0L, 35, 3, 0L, IntegrityResult.Healthy)]
    [InlineData(0L, 35, 95, 0L, IntegrityResult.Attention)]
    [InlineData(0L, 72, 3, 0L, IntegrityResult.Attention)]
    [InlineData(0L, 35, 3, 4L, IntegrityResult.Attention)]
    [InlineData(null, null, null, null, IntegrityResult.CouldNotCheck)]
    public void Judges_drive_health(long? health, int? temp, int? wear, long? errors, IntegrityResult expected) =>
        Assert.Equal(expected, IntegrityCheck.Judge(health, temp, wear, errors).Result);

    [Fact]
    public void Drive_types_match_the_other_tools()
    {
        Assert.Equal("NVMe SSD", IntegrityCheck.TypeName(4, 17));
        Assert.Equal("SSD", IntegrityCheck.TypeName(4, 11));
        Assert.Equal("Hard Drive", IntegrityCheck.TypeName(3, 11));
        Assert.Equal("Flash Drive", IntegrityCheck.TypeName(0, 7));
    }

    private const string Ns = "xmlns='http://schemas.microsoft.com/win/2004/08/events/event'";

    private static string Event(string provider, int id, string time, string message = "") =>
        $"<Event {Ns}><System><Provider Name='{provider}'/><EventID>{id}</EventID><Level>2</Level><TimeCreated SystemTime='{time}'/></System>" +
        $"<RenderingInfo Culture='en-US'><Message>{message}</Message></RenderingInfo></Event>";

    [Fact]
    public void Groups_logged_drive_problems_newest_first()
    {
        var xml = Event("disk", 7, "2026-10-01T10:00:00.000Z", "The device, \\Device\\Harddisk1\\DR1, has a bad block.")
                  + Event("disk", 7, "2026-10-05T10:00:00.000Z", "The device, \\Device\\Harddisk1\\DR1, has a bad block.  Again.")
                  + Event("Microsoft-Windows-Ntfs", 55, "2026-10-03T10:00:00.000Z", "A corruption was discovered.");
        var groups = IntegrityCheck.ParseDriveEvents(xml);
        Assert.Equal(2, groups.Count);
        Assert.Equal("Disk", groups[0].Source);
        Assert.Equal(2, groups[0].Count);
        Assert.Contains("bad block", groups[0].Meaning);
        Assert.EndsWith("Again.", groups[0].Message); // newest message, whitespace tidied
        Assert.Equal("NTFS", groups[1].Source);
        Assert.Empty(IntegrityCheck.ParseDriveEvents(""));
        Assert.Contains("Provider[@Name='disk']", IntegrityCheck.DriveEventQueryArgs(30)[2]);
        Assert.Contains("2592000000", IntegrityCheck.DriveEventQueryArgs(30)[2]);
    }

    [Fact]
    public void Reads_the_last_memory_test()
    {
        var ok = IntegrityCheck.ParseMemoryResult(Event("Microsoft-Windows-MemoryDiagnostics-Results", 1201, "2026-09-01T08:00:00.000Z"));
        Assert.NotNull(ok);
        Assert.False(ok!.ErrorsFound);
        Assert.True(IntegrityCheck.ParseMemoryResult(Event("Microsoft-Windows-MemoryDiagnostics-Results", 1202, "2026-09-01T08:00:00.000Z"))!.ErrorsFound);
        Assert.Null(IntegrityCheck.ParseMemoryResult(""));
    }

    [Fact]
    public void Saves_a_report_in_its_own_folder()
    {
        var dir = Path.Combine(Path.GetTempPath(), "kc-integrity-" + Guid.NewGuid().ToString("N"));
        try
        {
            var file = IntegrityCheck.SaveReport(dir, "Windows System Files", "Check", new[] { "line one" }, IntegrityResult.Healthy, new DateTime(2026, 10, 12, 9, 5, 0));
            Assert.Equal(Path.Combine(dir, "Integrity Check", "2026-10-12 09-05-00 Windows System Files.txt"), file);
            var text = File.ReadAllText(file);
            Assert.Contains("Result: No Problems", text);
            Assert.Contains("line one", text);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
