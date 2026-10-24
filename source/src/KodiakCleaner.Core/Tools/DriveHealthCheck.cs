using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using KodiakCleaner.Core.Util;
using Microsoft.Win32.SafeHandles;

namespace KodiakCleaner.Core.Tools;

/// <summary>Drive Wiper's drive check verdict (Ted, 0.8.114).</summary>
public enum DriveVerdict
{
    /// <summary>Nothing could be read: no health report and no surface scan.</summary>
    NotChecked,
    Healthy,
    Warning,
    Failing,
}

public static class DriveVerdictText
{
    public static string Name(DriveVerdict v) => v switch
    {
        DriveVerdict.Healthy => "Healthy",
        DriveVerdict.Warning => "Warning",
        DriveVerdict.Failing => "Failing",
        _ => "Couldn't Check",
    };

    /// <summary>good, warn or bad: the same chip colors Integrity Check uses.</summary>
    public static string Tone(DriveVerdict v) => v switch
    {
        DriveVerdict.Healthy => "good",
        DriveVerdict.Failing => "bad",
        _ => "warn",
    };
}

/// <summary>A stretch of the disk, in bytes from its start.</summary>
public sealed record DriveRange(long Start, long Length)
{
    public long End => Start + Length;
}

/// <summary>Adding ranges as the scan finds them, joined when they touch or overlap, so neighbouring bad blocks count as one area.</summary>
public static class DriveRanges
{
    /// <summary>Adds [start, start + length) to a list kept in order, merging it with any range it touches.</summary>
    public static void Add(List<DriveRange> ranges, long start, long length)
    {
        if (length <= 0) return;
        long end = start + length;
        int i = 0;
        while (i < ranges.Count && ranges[i].End < start) i++;
        // Everything from i that touches [start, end) is folded into one range.
        int j = i;
        while (j < ranges.Count && ranges[j].Start <= end)
        {
            start = Math.Min(start, ranges[j].Start);
            end = Math.Max(end, ranges[j].End);
            j++;
        }
        ranges.RemoveRange(i, j - i);
        ranges.Insert(i, new DriveRange(start, end - start));
    }

    /// <summary>The ranges joined when they touch or overlap, in order.</summary>
    public static List<DriveRange> Merge(IEnumerable<DriveRange> ranges)
    {
        var list = new List<DriveRange>();
        foreach (var r in ranges.OrderBy(r => r.Start)) Add(list, r.Start, r.Length);
        return list;
    }

    public static long Bytes(IEnumerable<DriveRange> ranges) => ranges.Sum(r => r.Length);

    /// <summary>"at 12.3 GB, 40.0 GB and 3 more" for the report.</summary>
    public static string Where(IReadOnlyList<DriveRange> ranges, int show = 3)
    {
        var at = ranges.Take(show).Select(r => ByteSize.Format(r.Start)).ToList();
        var text = at.Count switch
        {
            0 => "",
            1 => at[0],
            _ => string.Join(", ", at.Take(at.Count - 1)) + " and " + at[^1],
        };
        return ranges.Count > show ? $"{string.Join(", ", at)} and {ranges.Count - show} more" : text;
    }
}

/// <summary>How far a surface scan has read, and what it found so far.</summary>
public sealed record SurfaceProgress(long Done, long Total, int UnreadableAreas, int SlowAreas);

/// <summary>What a surface scan found. <see cref="Finished"/> is false when it was stopped or gave up.</summary>
public sealed record SurfaceScanResult(long Total, long Read, IReadOnlyList<DriveRange> Unreadable, IReadOnlyList<DriveRange> Slow,
    bool Finished, bool GaveUp, TimeSpan Took, double? TypicalMegabytesPerSecond);

/// <summary>
/// Reads every sector of a disk (or, in tests, a file standing in for one) through a read-only handle, in large aligned blocks,
/// and notes the areas that can't be read and the ones that read unusually slowly (a drive retrying a weak spot). Never writes.
/// </summary>
public static unsafe class SurfaceScan
{
    /// <summary>One read: 1 MB, a whole number of 512-byte and 4 KB sectors.</summary>
    public const int Block = 1024 * 1024;

    /// <summary>A block that fails is read again in pieces this size, so a bad spot is found to within 64 KB.</summary>
    public const int Piece = 64 * 1024;

    /// <summary>A block this slow is always slow (a healthy drive of any kind reads 1 MB in well under a second).</summary>
    public static readonly TimeSpan SlowAlways = TimeSpan.FromSeconds(1);

    /// <summary>Otherwise slow means at least this long and ten times the drive's typical block time.</summary>
    public static readonly TimeSpan SlowAtLeast = TimeSpan.FromMilliseconds(200);
    public const int SlowFactor = 10;

    /// <summary>A drive this broken isn't read any further: it would only take hours to say the same thing.</summary>
    public const int GiveUpAreas = 100;
    public const long GiveUpBytes = 64L * 1024 * 1024;

    /// <summary>Reads at an offset; returns the bytes read (fewer than asked = the device ended early), throws IOException when it can't.</summary>
    public delegate int ReadAt(long offset, Span<byte> buffer);

    /// <summary>Whether one block's read time is unusual. <paramref name="typical"/> is null until enough blocks were timed.</summary>
    public static bool IsSlow(TimeSpan took, TimeSpan? typical) =>
        took >= SlowAlways || (typical is { } t && took >= SlowAtLeast && took.Ticks >= t.Ticks * SlowFactor);

    /// <summary>Scans a whole disk opened read-only. <paramref name="length"/> is its size in bytes.</summary>
    public static SurfaceScanResult Run(SafeFileHandle handle, long length, IProgress<SurfaceProgress>? progress, CancellationToken ct) =>
        Run(length, (at, buffer) => RandomAccess.Read(handle, buffer, at), Stopwatch.GetTimestamp, Stopwatch.Frequency, progress, ct);

    /// <summary>The scan itself, with the reads and the clock passed in so tests can stand in for a failing drive.</summary>
    internal static SurfaceScanResult Run(long length, ReadAt read, Func<long> now, long ticksPerSecond,
        IProgress<SurfaceProgress>? progress, CancellationToken ct)
    {
        if (length <= 0) throw new IOException("The drive reported no size, so it couldn't be scanned.");
        var memory = (byte*)NativeMemory.AlignedAlloc(Block, 4096);
        var unreadable = new List<DriveRange>();
        var slow = new List<DriveRange>();
        var times = new Queue<long>();
        var sorted = new List<long>();
        long started = now(), done = 0;
        bool gaveUp = false;
        TimeSpan Between(long from, long to) => TimeSpan.FromSeconds((double)(to - from) / ticksPerSecond);
        try
        {
            var buffer = new Span<byte>(memory, Block);
            // Wake the drive first (a sleeping hard drive takes seconds to spin up), so the first block isn't counted as slow.
            try { read(0, buffer[..(int)Math.Min(4096, length)]); } catch (IOException) { }
            started = now();
            for (long at = 0; at < length; at += Block)
            {
                ct.ThrowIfCancellationRequested();
                int count = (int)Math.Min(Block, length - at);
                long t0 = now();
                bool ok;
                try { ok = read(at, buffer[..count]) == count; }
                catch (IOException) { ok = false; }
                long t1 = now();
                if (ok)
                {
                    var took = Between(t0, t1);
                    TimeSpan? typical = times.Count >= 8 ? TimeSpan.FromTicks(Median(sorted)) : null;
                    if (IsSlow(took, typical)) DriveRanges.Add(slow, at, count);
                    else Remember(times, sorted, took.Ticks);
                }
                else
                {
                    // Read it again in pieces to find just the bad spots; a block that reads on the second try was slow, not bad.
                    bool any = false;
                    for (int p = 0; p < count; p += Piece)
                    {
                        ct.ThrowIfCancellationRequested();
                        int size = Math.Min(Piece, count - p);
                        bool good;
                        try { good = read(at + p, buffer.Slice(p, size)) == size; }
                        catch (IOException) { good = false; }
                        if (!good) { DriveRanges.Add(unreadable, at + p, size); any = true; }
                    }
                    if (!any) DriveRanges.Add(slow, at, count);
                    if (unreadable.Count >= GiveUpAreas || DriveRanges.Bytes(unreadable) >= GiveUpBytes)
                    {
                        done = at + count;
                        gaveUp = true;
                        break;
                    }
                }
                done = at + count;
                if ((at / Block) % 32 == 0) progress?.Report(new SurfaceProgress(done, length, unreadable.Count, slow.Count));
            }
            progress?.Report(new SurfaceProgress(done, length, unreadable.Count, slow.Count));
            var elapsed = Between(started, now());
            double? speed = sorted.Count > 0 ? Block / 1048576.0 / TimeSpan.FromTicks(Median(sorted)).TotalSeconds : null;
            if (speed is { } s && (double.IsInfinity(s) || double.IsNaN(s))) speed = null;
            return new SurfaceScanResult(length, done, unreadable, slow, !gaveUp, gaveUp, elapsed, speed);
        }
        finally
        {
            NativeMemory.AlignedFree(memory);
        }
    }

    /// <summary>Keeps the last 64 normal block times (as a sorted copy too) for the median.</summary>
    private static void Remember(Queue<long> times, List<long> sorted, long ticks)
    {
        times.Enqueue(ticks);
        int at = sorted.BinarySearch(ticks);
        sorted.Insert(at < 0 ? ~at : at, ticks);
        if (times.Count <= 64) return;
        long old = times.Dequeue();
        sorted.RemoveAt(sorted.BinarySearch(old) is var i and >= 0 ? i : 0);
    }

    private static long Median(List<long> sorted) => sorted.Count == 0 ? 0 : sorted[sorted.Count / 2];
}

/// <summary>
/// What a drive says about itself through Windows: MSFT_PhysicalDisk's health and operational status, MSFT_StorageReliabilityCounter's
/// counts, and, for ATA drives that share them, the self-monitoring attributes for reallocated, pending and uncorrectable sectors.
/// Every value is null when Windows doesn't share it (most counts need administrator rights; USB enclosures often share none).
/// </summary>
public sealed record DriveHealthReport
{
    /// <summary>MSFT_PhysicalDisk.HealthStatus: 0 Healthy, 1 Warning, 2 Unhealthy.</summary>
    public long? HealthStatus { get; init; }
    public IReadOnlyList<long> OperationalStatus { get; init; } = [];
    public long? ReadErrorsUncorrected { get; init; }
    public long? WriteErrorsUncorrected { get; init; }
    public long? ReadErrorsCorrected { get; init; }
    public long? WriteErrorsCorrected { get; init; }
    /// <summary>Percent of the rated life used (SSDs and NVMe drives).</summary>
    public int? WearPercent { get; init; }
    public int? TemperatureC { get; init; }
    public int? TemperatureMaxC { get; init; }
    public long? PowerOnHours { get; init; }
    /// <summary>Self-monitoring attribute 5: sectors the drive moved to spares.</summary>
    public long? Reallocated { get; init; }
    /// <summary>Attribute 197: sectors waiting to be moved because they couldn't be read.</summary>
    public long? Pending { get; init; }
    /// <summary>Attribute 198: sectors the drive's own offline scan couldn't read.</summary>
    public long? OfflineUncorrectable { get; init; }
    /// <summary>The drive's own failure prediction (MSStorageDriver_FailurePredictStatus).</summary>
    public bool? PredictsFailure { get; init; }

    public long? UncorrectedErrors => ReadErrorsUncorrected is null && WriteErrorsUncorrected is null ? null : (ReadErrorsUncorrected ?? 0) + (WriteErrorsUncorrected ?? 0);
    public long? CorrectedErrors => ReadErrorsCorrected is null && WriteErrorsCorrected is null ? null : (ReadErrorsCorrected ?? 0) + (WriteErrorsCorrected ?? 0);

    /// <summary>Windows shared anything at all.</summary>
    public bool Shared => HealthStatus is not null || OperationalStatus.Count > 0 || UncorrectedErrors is not null || WearPercent is not null
                          || TemperatureC is not null || PowerOnHours is not null || Reallocated is not null || PredictsFailure is not null;

    public string HealthName => DriveHealthCheck.HealthName(HealthStatus);
}

/// <summary>The write-and-read-back test the wipe already did (Verify the Last Pass): every byte written, then read and compared.</summary>
public sealed record WipeReadBack(long Mismatches);

/// <summary>A drive check's verdict, the reasons in plain words (worst first) and the facts it was based on.</summary>
public sealed record DriveCheckResult(DriveVerdict Verdict, string Summary, IReadOnlyList<string> Reasons, IReadOnlyList<string> Facts)
{
    public string VerdictName => DriveVerdictText.Name(Verdict);
    public string Tone => DriveVerdictText.Tone(Verdict);
    public string ReasonsText => string.Join("\n", Reasons.Select(r => "•  " + r));
    public string FactsText => string.Join(" · ", Facts);
}

/// <summary>
/// Drive Wiper's drive check (Ted, 0.8.114): the drive's own health report through Windows, the disk errors Windows logged for it,
/// and a read-only surface scan of every sector (or, after a wipe with Verify the Last Pass, that write-and-read-back instead).
/// Read-only: the disk is opened for reading only, and nothing is ever written to it.
/// </summary>
public static class DriveHealthCheck
{
    /// <summary>Days of the System log looked at for the disk's logged errors.</summary>
    public const int EventDays = 30;

    public static string HealthName(long? status) => status switch
    {
        0 => "Healthy",
        1 => "Warning",
        2 => "Unhealthy",
        _ => "Not Shared",
    };

    /// <summary>MSFT_PhysicalDisk.OperationalStatus values (CIM's standard list).</summary>
    public static string OperationalName(long code) => code switch
    {
        0 => "Unknown",
        1 => "Other",
        2 => "OK",
        3 => "Degraded",
        4 => "Stressed",
        5 => "Predictive Failure",
        6 => "Error",
        7 => "Non-Recoverable Error",
        8 => "Starting",
        9 => "Stopping",
        10 => "Stopped",
        11 => "In Service",
        12 => "No Contact",
        13 => "Lost Communication",
        14 => "Aborted",
        15 => "Dormant",
        16 => "Supporting Entity in Error",
        17 => "Completed",
        _ => $"Status {code}",
    };

    /// <summary>Reads a disk's report out of the MSFT_PhysicalDisk and MSFT_StorageReliabilityCounter rows (null = not shared).</summary>
    internal static DriveHealthReport ReadReport(Dictionary<string, object?>? disk, Dictionary<string, object?>? counters)
    {
        long? N(Dictionary<string, object?>? row, string name) => Number(row?.GetValueOrDefault(name));
        int? Temp(string name) => N(counters, name) is { } t and > 0 and < 150 ? (int)t : null;
        return new DriveHealthReport
        {
            HealthStatus = N(disk, "HealthStatus") is { } h and >= 0 and <= 2 ? h : null,
            OperationalStatus = Numbers(disk, "OperationalStatus"),
            ReadErrorsUncorrected = N(counters, "ReadErrorsUncorrected"),
            WriteErrorsUncorrected = N(counters, "WriteErrorsUncorrected"),
            ReadErrorsCorrected = N(counters, "ReadErrorsCorrected"),
            WriteErrorsCorrected = N(counters, "WriteErrorsCorrected"),
            WearPercent = N(counters, "Wear") is { } w and >= 0 and <= 255 ? (int)w : null,
            TemperatureC = Temp("Temperature"),
            TemperatureMaxC = Temp("TemperatureMax"),
            PowerOnHours = N(counters, "PowerOnHours") is { } p and > 0 ? p : null,
        };
    }

    /// <summary>A WMI array value (uint16[] and the like) as numbers; a single value counts as a list of one.</summary>
    internal static IReadOnlyList<long> Numbers(Dictionary<string, object?>? row, string name)
    {
        if (row?.GetValueOrDefault(name) is not { } v) return [];
        if (v is System.Collections.IEnumerable list and not string)
        {
            var result = new List<long>();
            foreach (var item in list)
                if (Number(item) is { } n) result.Add(n);
            return result;
        }
        return Number(v) is { } one ? [one] : [];
    }

    /// <summary>A WMI number of any width (byte to UInt64) as a long; null when it's missing or isn't one.</summary>
    private static long? Number(object? value)
    {
        try { return value is null ? null : Convert.ToInt64(value, CultureInfo.InvariantCulture); }
        catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException) { return null; }
    }

    /// <summary>
    /// The raw value of one self-monitoring attribute from MSStorageDriver_FailurePredictData.VendorSpecific: a 2-byte version,
    /// then 30 entries of 12 bytes (id, 2 flag bytes, current, worst, 6 raw bytes, 1 reserved). Only the low 32 bits of the raw
    /// value are used: some makers keep other numbers in the top bytes. Null when the attribute isn't there.
    /// </summary>
    public static long? SmartRaw(byte[]? data, int id)
    {
        if (data is null || id is <= 0 or > 255) return null;
        for (int at = 2; at + 12 <= data.Length && at < 2 + 30 * 12; at += 12)
            if (data[at] == id) return BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at + 5, 4));
        return null;
    }

    /// <summary>Adds the self-monitoring attributes that matter here (5, 197, 198) to a report.</summary>
    public static DriveHealthReport WithSmart(DriveHealthReport report, byte[]? vendorSpecific, bool? predictsFailure) => report with
    {
        Reallocated = SmartRaw(vendorSpecific, 5),
        Pending = SmartRaw(vendorSpecific, 197),
        OfflineUncorrectable = SmartRaw(vendorSpecific, 198),
        PredictsFailure = predictsFailure,
    };

    /// <summary>
    /// The verdict. Failing: the drive or Windows says it's failing, areas couldn't be read, the wipe's read-back didn't match, or
    /// sectors are waiting to be replaced because they can't be read. Warning: anything worth watching (warning status, wear,
    /// heat, errors, replaced sectors, slow areas, errors Windows logged for the disk, a scan that didn't finish). Healthy: none of
    /// these and at least something was checked.
    /// </summary>
    public static DriveCheckResult Judge(DriveHealthReport report, IReadOnlyList<DriveEventGroup> events, SurfaceScanResult? scan, WipeReadBack? readBack)
    {
        var failing = new List<string>();
        var warning = new List<string>();
        string Plural(long n, string one, string many) => n == 1 ? one : many;

        // Windows' and the drive's own view.
        if (report.HealthStatus == 2) failing.Add("Windows reports this drive as Unhealthy.");
        else if (report.HealthStatus == 1) warning.Add("Windows reports a warning for this drive.");
        if (report.PredictsFailure == true) failing.Add("The drive's own self-monitoring predicts it will fail.");
        if (report.OperationalStatus.Any(c => c is 5 or 7))
            failing.Add($"Windows lists the drive as {string.Join(", ", report.OperationalStatus.Where(c => c is 5 or 7).Select(OperationalName))}.");
        else if (report.OperationalStatus.Any(c => c is 3 or 4 or 6 or 12 or 13 or 16))
            warning.Add($"Windows lists the drive as {string.Join(", ", report.OperationalStatus.Where(c => c is 3 or 4 or 6 or 12 or 13 or 16).Select(OperationalName))}.");
        if (report.Pending > 0) failing.Add($"{report.Pending:N0} {Plural(report.Pending.Value, "sector is", "sectors are")} waiting to be replaced because {Plural(report.Pending.Value, "it", "they")} couldn't be read.");
        if (report.OfflineUncorrectable > 0) failing.Add($"The drive's own scan found {report.OfflineUncorrectable:N0} {Plural(report.OfflineUncorrectable.Value, "sector", "sectors")} it couldn't read.");
        if (report.Reallocated > 0) warning.Add($"{report.Reallocated:N0} worn {Plural(report.Reallocated.Value, "sector was", "sectors were")} already replaced with spares.");
        if (report.UncorrectedErrors > 0) warning.Add($"The drive counted {report.UncorrectedErrors:N0} read or write {Plural(report.UncorrectedErrors.Value, "error", "errors")} it couldn't correct.");
        if (report.WearPercent >= 100) warning.Add($"It has used up its rated life ({report.WearPercent}% worn).");
        else if (report.WearPercent >= 90) warning.Add($"{report.WearPercent}% of its rated life is used up.");
        if (report.TemperatureC >= 70) warning.Add($"It's running hot ({report.TemperatureC} °C).");

        // What Windows logged for this disk.
        int serious = events.Where(e => e.Source == "Disk" && e.EventId is 7 or 51 or 52 or 154).Sum(e => e.Count);
        int other = events.Sum(e => e.Count) - serious;
        if (serious > 0) warning.Add($"Windows logged {serious} serious disk {Plural(serious, "error", "errors")} for it in the last {EventDays} days (bad blocks or failed reads or writes).");
        else if (other > 0) warning.Add($"Windows logged {other} disk {Plural(other, "problem", "problems")} for it in the last {EventDays} days. Unplugging a drive without ejecting it shows up here too.");

        // The surface: the wipe's read-back, or the scan.
        if (readBack is { Mismatches: > 0 } rb)
            failing.Add($"{rb.Mismatches:N0} {Plural(rb.Mismatches, "byte", "bytes")} didn't read back as written during the wipe. This drive is failing; don't trust it with data.");
        if (scan is not null)
        {
            int bad = scan.Unreadable.Count, slowAreas = scan.Slow.Count;
            if (bad > 0)
                failing.Add($"{bad} unreadable {Plural(bad, "area", "areas")} ({ByteSize.Format(DriveRanges.Bytes(scan.Unreadable))}, at {DriveRanges.Where(scan.Unreadable)}): this drive is failing; don't trust it with data."
                            + (scan.GaveUp ? " The scan stopped early because so much of it couldn't be read." : ""));
            if (slowAreas > 0)
                warning.Add($"{slowAreas} {Plural(slowAreas, "area", "areas")} read unusually slowly (at {DriveRanges.Where(scan.Slow)}). The drive had to retry, which can mean it's starting to wear out, or another program was using it.");
            if (!scan.Finished && !scan.GaveUp)
                warning.Add($"The surface scan was stopped after {ByteSize.Format(scan.Read)} of {ByteSize.Format(scan.Total)}, so only part of the drive was read.");
        }

        bool checkedSomething = report.Shared || readBack is not null || scan is { Read: > 0 };
        var verdict = failing.Count > 0 ? DriveVerdict.Failing : warning.Count > 0 ? DriveVerdict.Warning
            : checkedSomething ? DriveVerdict.Healthy : DriveVerdict.NotChecked;
        var reasons = failing.Concat(warning).ToList();
        string summary = verdict switch
        {
            DriveVerdict.Failing => "This drive is failing. Don't trust it with data; replace it, or keep only copies of files on it.",
            DriveVerdict.Warning => "This drive works, but something is worth watching. Keep a backup of anything you put on it.",
            DriveVerdict.Healthy => readBack is not null
                ? "No problems found. The wipe wrote every sector and read it back without a mismatch, so that counts as the surface test."
                : scan is { Finished: true }
                ? "No problems found. Every sector was read without errors."
                : "No problems found in what the drive shared.",
            _ => "Windows didn't share this drive's health, and its sectors weren't read.",
        };
        if (verdict == DriveVerdict.Healthy && !report.Shared)
            reasons.Add("The drive didn't share its own health report through Windows (common for USB drives), so only its sectors were checked.");
        return new DriveCheckResult(verdict, summary, reasons, Facts(report, events, scan, readBack));
    }

    /// <summary>The values the verdict was based on, for the results and the report.</summary>
    public static List<string> Facts(DriveHealthReport report, IReadOnlyList<DriveEventGroup> events, SurfaceScanResult? scan, WipeReadBack? readBack)
    {
        var facts = new List<string> { $"Health: {report.HealthName}" };
        if (report.OperationalStatus.Count > 0) facts.Add("Status: " + string.Join(", ", report.OperationalStatus.Select(OperationalName)));
        if (report.UncorrectedErrors is { } u) facts.Add($"Uncorrected Errors: {u:N0}");
        if (report.CorrectedErrors is { } c) facts.Add($"Corrected Errors: {c:N0}");
        if (report.WearPercent is { } w) facts.Add($"Wear: {w}%");
        if (report.TemperatureC is { } t) facts.Add($"Temperature: {t} °C" + (report.TemperatureMaxC is { } m && m > t ? $" (highest {m} °C)" : ""));
        if (report.PowerOnHours is { } h) facts.Add($"Powered On: {h:N0} h");
        if (report.Reallocated is { } r) facts.Add($"Replaced Sectors: {r:N0}");
        if (report.Pending is { } p) facts.Add($"Pending Sectors: {p:N0}");
        if (report.OfflineUncorrectable is { } o) facts.Add($"Uncorrectable Sectors: {o:N0}");
        facts.Add($"Logged Errors ({EventDays} Days): {events.Sum(e => e.Count)}");
        if (readBack is not null) facts.Add("Surface: Written and Read Back by the Wipe");
        else if (scan is not null)
            facts.Add($"Surface: {ByteSize.Format(scan.Read)} Read in {ProgressClock.FormatElapsed(scan.Took)}" +
                      (scan.TypicalMegabytesPerSecond is { } s ? string.Create(CultureInfo.CurrentCulture, $", about {s:0} MB/s") : ""));
        return facts;
    }

    /// <summary>The check as plain text lines, for the activity log's report file.</summary>
    public static List<string> ReportLines(string drive, DriveCheckResult result)
    {
        var lines = new List<string> { drive, $"Verdict: {result.VerdictName}", result.Summary, "" };
        lines.AddRange(result.Reasons.Select(r => "- " + r));
        if (result.Reasons.Count > 0) lines.Add("");
        lines.AddRange(result.Facts);
        return lines;
    }

    // ---------- Reading from Windows ----------

    /// <summary>
    /// Checks physical disk <paramref name="number"/>: its health report, the errors Windows logged for it and, unless the wipe
    /// already wrote and read back every sector (<paramref name="readBack"/>), a read-only scan of every sector. Needs
    /// administrator rights for the scan and most of the report. Throws OperationCanceledException when stopped.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static Task<DriveCheckResult> CheckAsync(int number, WipeReadBack? readBack, IProgress<string> status,
        IProgress<SurfaceProgress>? progress, CancellationToken ct) =>
        Task.Run(() => Check(number, readBack, status, progress, ct), CancellationToken.None);

    [SupportedOSPlatform("windows")]
    private static DriveCheckResult Check(int number, WipeReadBack? readBack, IProgress<string> status, IProgress<SurfaceProgress>? progress, CancellationToken ct)
    {
        status.Report("Reading the drive's own health report…");
        var report = ReadReport(number);
        ct.ThrowIfCancellationRequested();
        status.Report($"Reading the disk errors Windows logged in the last {EventDays} days…");
        var xml = IntegrityCheck.QueryEvents(IntegrityCheck.DriveEventQueryArgs(EventDays));
        var events = xml is null ? new List<DriveEventGroup>() : IntegrityCheck.ParseDriveEvents(xml, number);
        ct.ThrowIfCancellationRequested();
        SurfaceScanResult? scan = null;
        if (readBack is null)
        {
            status.Report("Reading every sector…");
            try { scan = ScanDisk(number, progress, ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                var r = Judge(report, events, null, null);
                return r with { Reasons = r.Reasons.Append("The surface scan couldn't run: " + e.Message).ToList() };
            }
        }
        return Judge(report, events, scan, readBack);
    }

    /// <summary>The disk opened for reading only (no write access is ever asked for) and scanned.</summary>
    [SupportedOSPlatform("windows")]
    private static SurfaceScanResult ScanDisk(int number, IProgress<SurfaceProgress>? progress, CancellationToken ct)
    {
        using var h = DriveMedia.Native.Open($@"\\.\PhysicalDrive{number}", DriveMedia.Native.GenericRead, 0x20000000 /* FILE_FLAG_NO_BUFFERING */);
        if (h.IsInvalid) throw new UnauthorizedAccessException("Windows wouldn't open the disk for reading. Kodiak Cleaner needs administrator rights.");
        long length = DriveMedia.Native.Length(h);
        if (length <= 0) length = DriveMedia.Native.Size(h);
        return SurfaceScan.Run(h, length, progress, ct);
    }

    /// <summary>One disk's report: MSFT_PhysicalDisk (DeviceId = the disk number), its reliability counters and the ATA attributes.</summary>
    [SupportedOSPlatform("windows")]
    public static DriveHealthReport ReadReport(int number)
    {
        var id = number.ToString(CultureInfo.InvariantCulture);
        var (disks, counters) = IntegrityCheck.ReadStorageRows();
        var disk = disks.FirstOrDefault(d => SystemDetails.Text(d, "DeviceId") == id);
        counters.TryGetValue(id, out var c);
        var report = ReadReport(disk, c);
        var (data, predicts) = ReadSmart(number);
        return WithSmart(report, data, predicts);
    }

    /// <summary>
    /// The ATA self-monitoring data Windows' disk driver shares in root\wmi (administrator only; NVMe and most USB drives share
    /// none). Matched to the disk by Win32_DiskDrive's PNPDeviceID, which the instance name starts with.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static (byte[]? Data, bool? Predicts) ReadSmart(int number)
    {
        var pnp = SystemDetails.Query($"SELECT PNPDeviceID FROM Win32_DiskDrive WHERE Index = {number}")
            .Select(r => SystemDetails.Text(r, "PNPDeviceID")).FirstOrDefault(p => p is not null);
        if (pnp is null) return (null, null);
        bool Mine(Dictionary<string, object?> r) => SystemDetails.Text(r, "InstanceName")?.StartsWith(pnp, StringComparison.OrdinalIgnoreCase) == true;
        var data = SystemDetails.Query("SELECT InstanceName, VendorSpecific FROM MSStorageDriver_FailurePredictData", @"root\wmi")
            .FirstOrDefault(Mine)?.GetValueOrDefault("VendorSpecific") as byte[];
        var status = SystemDetails.Query("SELECT InstanceName, PredictFailure FROM MSStorageDriver_FailurePredictStatus", @"root\wmi").FirstOrDefault(Mine);
        bool? predicts = status?.GetValueOrDefault("PredictFailure") is bool b ? b : null;
        return (data, predicts);
    }
}
