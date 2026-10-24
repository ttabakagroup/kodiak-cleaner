using System.Security.Cryptography;
using KodiakCleaner.Core.Tools;
using Xunit;

namespace KodiakCleaner.Core.Tests;

/// <summary>Drive Wiper's drive check (Ted, 0.8.114): the read-only surface scan, range merging, the verdict and the parsing.</summary>
public sealed class SurfaceScanTests : IDisposable
{
    private const int MB = 1024 * 1024;
    private readonly string _file = Path.Combine(Path.GetTempPath(), "kodiakcleaner-scan-" + Guid.NewGuid().ToString("N"));
    public void Dispose() => File.Delete(_file);

    [Fact]
    public void Scans_a_file_standing_in_for_a_disk_without_changing_it()
    {
        long length = 9L * MB + 4096; // more than one block, not a whole number of them
        var data = RandomNumberGenerator.GetBytes((int)length);
        File.WriteAllBytes(_file, data);
        var written = File.GetLastWriteTimeUtc(_file);
        var seen = new List<SurfaceProgress>();
        SurfaceScanResult r;
        using (var h = File.OpenHandle(_file, FileMode.Open, FileAccess.Read, FileShare.Read))
            r = SurfaceScan.Run(h, length, new Sync<SurfaceProgress>(seen.Add), default);
        Assert.True(r.Finished);
        Assert.False(r.GaveUp);
        Assert.Equal(length, r.Read);
        Assert.Empty(r.Unreadable);
        Assert.Contains(seen, p => p.Done == length && p.Total == length);
        Assert.Equal(data, File.ReadAllBytes(_file));
        Assert.Equal(written, File.GetLastWriteTimeUtc(_file));
    }

    [Fact]
    public void Unreadable_spots_are_found_to_within_64_KB_and_merged_into_areas()
    {
        // Bad: 3 MB + 64 KB to 3 MB + 192 KB (one area), and the last 64 KB of block 5 plus the first 64 KB of block 6 (one area).
        bool Bad(long at, int size) => Overlaps(at, size, 3L * MB + 64 * 1024, 128 * 1024) || Overlaps(at, size, 6L * MB - 64 * 1024, 128 * 1024);
        var r = SurfaceScan.Run(10L * MB, (at, b) => Bad(at, b.Length) ? throw new IOException("CRC") : b.Length, Clock(_ => 10), 1000, null, default);
        Assert.True(r.Finished);
        Assert.Equal(new[] { new DriveRange(3L * MB + 64 * 1024, 128 * 1024), new DriveRange(6L * MB - 64 * 1024, 128 * 1024) }, r.Unreadable);
        Assert.Empty(r.Slow);
        Assert.Equal(10L * MB, r.Read);
    }

    [Fact]
    public void A_short_read_counts_as_unreadable()
    {
        var r = SurfaceScan.Run(4L * MB, (at, b) => at >= 2L * MB ? 0 : b.Length, Clock(_ => 10), 1000, null, default);
        Assert.Equal(new[] { new DriveRange(2L * MB, 2L * MB) }, r.Unreadable);
    }

    [Fact]
    public void Unusually_slow_blocks_are_noted_and_merged()
    {
        // Normal blocks take 10 ms; blocks 12 and 13 take 400 ms (one area), block 15 takes 150 ms (not slow), block 17 takes 1.5 s.
        long Ms(long at) => (at / MB) switch { 12 or 13 => 400, 15 => 150, 17 => 1500, _ => 10 };
        var r = SurfaceScan.Run(20L * MB, (_, b) => b.Length, Clock(Ms), 1000, null, default);
        Assert.Equal(new[] { new DriveRange(12L * MB, 2L * MB), new DriveRange(17L * MB, MB) }, r.Slow);
        Assert.Empty(r.Unreadable);
        Assert.Equal(100, r.TypicalMegabytesPerSecond!.Value, 3);
    }

    [Fact]
    public void A_block_that_reads_on_the_second_try_is_slow_not_bad()
    {
        int failures = 0;
        var r = SurfaceScan.Run(4L * MB, (at, b) => at == 2L * MB && b.Length == SurfaceScan.Block && failures++ == 0 ? throw new IOException("retry") : b.Length,
            Clock(_ => 10), 1000, null, default);
        Assert.Empty(r.Unreadable);
        Assert.Equal(new[] { new DriveRange(2L * MB, MB) }, r.Slow);
    }

    [Fact]
    public void A_drive_that_reads_nothing_is_given_up_on()
    {
        var r = SurfaceScan.Run(1024L * MB, (_, _) => throw new IOException("dead"), Clock(_ => 10), 1000, null, default);
        Assert.True(r.GaveUp);
        Assert.False(r.Finished);
        Assert.Equal(SurfaceScan.GiveUpBytes, DriveRanges.Bytes(r.Unreadable));
        Assert.True(r.Read < 1024L * MB);
    }

    [Fact]
    public void Stop_ends_the_scan()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => SurfaceScan.Run(4L * MB, (_, b) => b.Length, Clock(_ => 10), 1000, null, cts.Token));
    }

    [Theory]
    [InlineData(1000, null, true)]   // a second for 1 MB is always slow
    [InlineData(900, null, false)]   // before the typical time is known, only the fixed limit counts
    [InlineData(250, 20, true)]      // over 200 ms and over ten times typical
    [InlineData(150, 10, false)]     // ten times typical but under 200 ms
    [InlineData(300, 40, false)]     // over 200 ms but under ten times typical
    public void Slow_means_a_second_or_ten_times_typical(int ms, int? typicalMs, bool slow) =>
        Assert.Equal(slow, SurfaceScan.IsSlow(TimeSpan.FromMilliseconds(ms), typicalMs is { } t ? TimeSpan.FromMilliseconds(t) : null));

    [Fact]
    public void Ranges_merge_when_they_touch_or_overlap()
    {
        var list = new List<DriveRange>();
        DriveRanges.Add(list, 100, 10);
        DriveRanges.Add(list, 300, 10);
        DriveRanges.Add(list, 110, 5);   // touches the first
        DriveRanges.Add(list, 200, 10);  // on its own, kept in order
        DriveRanges.Add(list, 205, 100); // overlaps 200 and touches 300: all three become one
        DriveRanges.Add(list, 0, 0);     // nothing
        Assert.Equal(new[] { new DriveRange(100, 15), new DriveRange(200, 110) }, list);
        Assert.Equal(new[] { new DriveRange(0, 20) }, DriveRanges.Merge([new DriveRange(10, 10), new DriveRange(0, 10), new DriveRange(5, 2)]));
        Assert.Equal(125, DriveRanges.Bytes(list));
    }

    private static bool Overlaps(long at, int size, long start, long length) => at < start + length && start < at + size;

    /// <summary>A clock in milliseconds that moves on by the read's time each time a read finishes (the second call per read).</summary>
    private static Func<long> Clock(Func<long, long> msFor)
    {
        // The scan asks for the time before and after each block; the reader can't move it, so the step is worked out from
        // which block comes next: every second call after the start adds that block's time.
        long now = 0, calls = 0, block = 0;
        return () =>
        {
            calls++;
            if (calls > 2 && calls % 2 == 0) { now += msFor(block * MB); block++; }
            return now;
        };
    }

    private sealed class Sync<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}

public sealed class DriveVerdictTests
{
    private static readonly DriveHealthReport Good = new() { HealthStatus = 0, OperationalStatus = [2], ReadErrorsUncorrected = 0, WriteErrorsUncorrected = 0, WearPercent = 3, TemperatureC = 35, PowerOnHours = 1200 };
    private static readonly SurfaceScanResult Clean = new(100L << 30, 100L << 30, [], [], true, false, TimeSpan.FromMinutes(20), 150);
    private static readonly List<DriveEventGroup> NoEvents = new();

    [Fact]
    public void A_clean_drive_is_healthy()
    {
        var r = DriveHealthCheck.Judge(Good, NoEvents, Clean, null);
        Assert.Equal(DriveVerdict.Healthy, r.Verdict);
        Assert.Empty(r.Reasons);
        Assert.Contains("Every sector was read", r.Summary);
        Assert.Contains("Wear: 3%", r.Facts);
        Assert.Contains("Health: Healthy", r.Facts);
    }

    [Fact]
    public void Unreadable_areas_mean_failing_in_plain_words()
    {
        var scan = Clean with { Unreadable = [new DriveRange(1L << 30, 65536), new DriveRange(2L << 30, 65536), new DriveRange(3L << 30, 131072)] };
        var r = DriveHealthCheck.Judge(Good, NoEvents, scan, null);
        Assert.Equal(DriveVerdict.Failing, r.Verdict);
        Assert.StartsWith("3 unreadable areas", r.Reasons[0]);
        Assert.Contains("this drive is failing; don't trust it with data", r.Reasons[0]);
        Assert.Equal("Failing", r.VerdictName);
        Assert.Equal("bad", r.Tone);
    }

    [Fact]
    public void A_wipe_read_back_counts_as_the_surface_test()
    {
        var ok = DriveHealthCheck.Judge(Good, NoEvents, null, new WipeReadBack(0));
        Assert.Equal(DriveVerdict.Healthy, ok.Verdict);
        Assert.Contains("Surface: Written and Read Back by the Wipe", ok.Facts);
        var bad = DriveHealthCheck.Judge(Good, NoEvents, null, new WipeReadBack(12));
        Assert.Equal(DriveVerdict.Failing, bad.Verdict);
        Assert.Contains("12 bytes didn't read back", bad.Reasons[0]);
    }

    [Theory]
    [InlineData(2, DriveVerdict.Failing)]
    [InlineData(1, DriveVerdict.Warning)]
    [InlineData(0, DriveVerdict.Healthy)]
    public void Windows_health_status_counts(long status, DriveVerdict expected) =>
        Assert.Equal(expected, DriveHealthCheck.Judge(Good with { HealthStatus = status }, NoEvents, Clean, null).Verdict);

    [Fact]
    public void Things_worth_watching_are_warnings()
    {
        DriveVerdict V(DriveHealthReport report, SurfaceScanResult? scan = null, List<DriveEventGroup>? events = null) =>
            DriveHealthCheck.Judge(report, events ?? NoEvents, scan ?? Clean, null).Verdict;
        Assert.Equal(DriveVerdict.Warning, V(Good with { WearPercent = 92 }));
        Assert.Equal(DriveVerdict.Warning, V(Good with { TemperatureC = 72 }));
        Assert.Equal(DriveVerdict.Warning, V(Good with { ReadErrorsUncorrected = 4 }));
        Assert.Equal(DriveVerdict.Warning, V(Good with { Reallocated = 8 }));
        Assert.Equal(DriveVerdict.Warning, V(Good with { OperationalStatus = [3] }));
        Assert.Equal(DriveVerdict.Warning, V(Good, Clean with { Slow = [new DriveRange(0, 1 << 20)] }));
        Assert.Equal(DriveVerdict.Warning, V(Good, Clean with { Finished = false, Read = 10L << 30 }));
        Assert.Equal(DriveVerdict.Warning, V(Good, events: [new DriveEventGroup("Disk", 7, 2, DateTime.Now, "", "")]));
        Assert.Equal(DriveVerdict.Healthy, V(Good with { ReadErrorsCorrected = 500 })); // corrected errors are the drive doing its job
    }

    [Fact]
    public void Sectors_that_cannot_be_read_or_a_predicted_failure_mean_failing()
    {
        Assert.Equal(DriveVerdict.Failing, DriveHealthCheck.Judge(Good with { Pending = 1 }, NoEvents, Clean, null).Verdict);
        Assert.Equal(DriveVerdict.Failing, DriveHealthCheck.Judge(Good with { OfflineUncorrectable = 2 }, NoEvents, Clean, null).Verdict);
        Assert.Equal(DriveVerdict.Failing, DriveHealthCheck.Judge(Good with { PredictsFailure = true }, NoEvents, Clean, null).Verdict);
        Assert.Equal(DriveVerdict.Failing, DriveHealthCheck.Judge(Good with { OperationalStatus = [2, 5] }, NoEvents, Clean, null).Verdict);
    }

    [Fact]
    public void Nothing_read_is_not_checked_and_a_quiet_drive_says_so()
    {
        var none = DriveHealthCheck.Judge(new DriveHealthReport(), NoEvents, null, null);
        Assert.Equal(DriveVerdict.NotChecked, none.Verdict);
        Assert.Equal("Couldn't Check", none.VerdictName);
        var usb = DriveHealthCheck.Judge(new DriveHealthReport(), NoEvents, Clean, null);
        Assert.Equal(DriveVerdict.Healthy, usb.Verdict);
        Assert.Contains(usb.Reasons, r => r.Contains("didn't share its own health report"));
        Assert.Contains("Health: Not Shared", usb.Facts);
    }

    [Fact]
    public void Reliability_values_are_read_from_Windows_rows()
    {
        var disk = new Dictionary<string, object?> { ["DeviceId"] = "1", ["HealthStatus"] = (ushort)0, ["OperationalStatus"] = new ushort[] { 2, 3 } };
        var counters = new Dictionary<string, object?>
        {
            ["Temperature"] = (byte)41, ["TemperatureMax"] = (byte)200, ["Wear"] = (byte)7, ["ReadErrorsUncorrected"] = 2UL, ["WriteErrorsUncorrected"] = null,
            ["ReadErrorsCorrected"] = 10UL, ["WriteErrorsCorrected"] = 5UL, ["PowerOnHours"] = 31234u,
        };
        var r = DriveHealthCheck.ReadReport(disk, counters);
        Assert.Equal(0, r.HealthStatus);
        Assert.Equal(new long[] { 2, 3 }, r.OperationalStatus);
        Assert.Equal(41, r.TemperatureC);
        Assert.Null(r.TemperatureMaxC); // out of range
        Assert.Equal(7, r.WearPercent);
        Assert.Equal(2, r.UncorrectedErrors);
        Assert.Equal(15, r.CorrectedErrors);
        Assert.Equal(31234, r.PowerOnHours);
        Assert.True(r.Shared);
        var none = DriveHealthCheck.ReadReport(null, null);
        Assert.False(none.Shared);
        Assert.Null(none.UncorrectedErrors);
        Assert.Equal("Degraded", DriveHealthCheck.OperationalName(3));
    }

    [Fact]
    public void Self_monitoring_attributes_are_read_from_the_raw_table()
    {
        var data = new byte[512];
        void Put(int slot, byte id, uint raw, byte high = 0)
        {
            int at = 2 + slot * 12;
            data[at] = id;
            BitConverter.TryWriteBytes(data.AsSpan(at + 5, 4), raw);
            data[at + 9] = high; // upper raw bytes some makers fill with other numbers
        }
        Put(0, 9, 4000);   // power-on hours
        Put(1, 5, 12, 0x7F);
        Put(2, 197, 3);
        Put(3, 198, 0);
        Assert.Equal(12, DriveHealthCheck.SmartRaw(data, 5));
        Assert.Equal(3, DriveHealthCheck.SmartRaw(data, 197));
        Assert.Equal(0, DriveHealthCheck.SmartRaw(data, 198));
        Assert.Null(DriveHealthCheck.SmartRaw(data, 187));
        Assert.Null(DriveHealthCheck.SmartRaw(null, 5));
        Assert.Null(DriveHealthCheck.SmartRaw(new byte[10], 5));
        var r = DriveHealthCheck.WithSmart(new DriveHealthReport(), data, false);
        Assert.Equal((12L, 3L, 0L, false), (r.Reallocated!.Value, r.Pending!.Value, r.OfflineUncorrectable!.Value, r.PredictsFailure!.Value));
        Assert.Equal(DriveVerdict.Failing, DriveHealthCheck.Judge(r, [], null, null).Verdict);
    }

    private const string Ns = "xmlns='http://schemas.microsoft.com/win/2004/08/events/event'";

    private static string Event(string provider, int id, string message, string data = "") =>
        $"<Event {Ns}><System><Provider Name='{provider}'/><EventID>{id}</EventID><Level>2</Level><TimeCreated SystemTime='2026-10-20T10:00:00.000Z'/></System>" +
        $"<EventData><Data>{data}</Data></EventData><RenderingInfo Culture='en-US'><Message>{message}</Message></RenderingInfo></Event>";

    [Fact]
    public void Only_the_events_that_name_the_disk_count()
    {
        var xml = Event("disk", 7, "The device, \\Device\\Harddisk1\\DR1, has a bad block.")
                  + Event("disk", 7, "The device, \\Device\\Harddisk10\\DR10, has a bad block.")
                  + Event("disk", 153, "The IO operation was retried.", "\\Device\\Harddisk1\\DR1")
                  + Event("stornvme", 129, "Reset to device, \\Device\\RaidPort0, was issued.");
        var mine = IntegrityCheck.ParseDriveEvents(xml, 1);
        Assert.Equal(new[] { 153, 7 }.OrderBy(i => i), mine.Select(g => g.EventId).OrderBy(i => i));
        Assert.Equal(4, IntegrityCheck.ParseDriveEvents(xml).Sum(g => g.Count));
        Assert.True(IntegrityCheck.NamesDisk("\\\\.\\PhysicalDrive2", 2));
        Assert.False(IntegrityCheck.NamesDisk("\\Device\\Harddisk12\\DR12", 1));
    }

    [Fact]
    public void The_check_never_asks_to_write()
    {
        var source = File.ReadAllText(Repo.File("src", "KodiakCleaner.Core", "Tools", "DriveHealthCheck.cs"));
        Assert.DoesNotContain("GenericWrite", source);
        Assert.DoesNotContain("RandomAccess.Write", source);
        Assert.DoesNotContain("FileAccess.Write", source);
    }
}
