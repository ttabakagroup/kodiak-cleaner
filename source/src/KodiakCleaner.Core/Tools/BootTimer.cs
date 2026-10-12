using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;

namespace KodiakCleaner.Core.Tools;

/// <summary>How much a program held up startup, with the thresholds Windows' Task Manager uses for its Startup Impact.</summary>
public enum BootImpact { None, Low, Medium, High }

/// <summary>One program, service, driver or device in a Boot Timer report.</summary>
public sealed class BootItem
{
    public string Name { get; set; } = "";
    /// <summary>Startup Program, App, Service, Driver, Device or Windows (what Windows' own log calls it).</summary>
    public string Kind { get; set; } = "";
    /// <summary>The program file, when known (for its icon and for matching it on the Startup page).</summary>
    public string Path { get; set; } = "";
    /// <summary>The startup entry's name on the Startup page; empty when it isn't a startup entry.</summary>
    public string StartupName { get; set; } = "";
    /// <summary>Seconds after sign-in that the program started, or null when it wasn't running.</summary>
    public double? StartedAfterSignIn { get; set; }
    /// <summary>Processor time the program used while the PC was starting, in milliseconds.</summary>
    public long CpuMs { get; set; }
    /// <summary>Bytes the program read from and wrote to disk while the PC was starting.</summary>
    public long DiskBytes { get; set; }
    /// <summary>How long Windows measured this item holding up the start (its own slowdown events), in milliseconds.</summary>
    public long DelayMs { get; set; }
    public BootImpact Impact { get; set; }
}

/// <summary>What one timed restart measured. Times are UTC; a missing time means Windows didn't say.</summary>
public sealed class BootReport
{
    /// <summary>When Restart and Time was clicked.</summary>
    public DateTime RequestedUtc { get; set; }
    /// <summary>When Windows began starting (the PC's own startup screen comes before this).</summary>
    public DateTime BootUtc { get; set; }
    /// <summary>When this person signed in.</summary>
    public DateTime? SignInUtc { get; set; }
    /// <summary>When the PC settled down (the processor stayed quiet) after sign-in.</summary>
    public DateTime? ReadyUtc { get; set; }
    /// <summary>Windows' own measurement of this start (Diagnostics-Performance event 100), in milliseconds; 0 when not read.</summary>
    public long WindowsBootMs { get; set; }
    public long WindowsMainPathMs { get; set; }
    public long WindowsPostBootMs { get; set; }
    /// <summary>True when Windows' own log could be read (it needs administrator rights).</summary>
    public bool WindowsLogRead { get; set; }
    public List<BootItem> Items { get; set; } = new();

    public TimeSpan ShutdownAndRestart => Clamp(BootUtc - RequestedUtc);
    public TimeSpan? WindowsStarting => SignInUtc is { } s ? Clamp(s - BootUtc) : null;
    public TimeSpan? DesktopLoading => SignInUtc is { } s && ReadyUtc is { } r ? Clamp(r - s) : null;
    /// <summary>From the click to a settled desktop; to sign-in when the settle time wasn't measured.</summary>
    public TimeSpan Total => Clamp((ReadyUtc ?? SignInUtc ?? BootUtc) - RequestedUtc);

    private static TimeSpan Clamp(TimeSpan t) => t < TimeSpan.Zero ? TimeSpan.Zero : t;
}

/// <summary>One finished timing, kept for the Recent Restarts chart.</summary>
public sealed record BootHistoryEntry(DateTime RequestedUtc, double TotalSeconds);

/// <summary>Boot Timer's saved state: a restart waiting to happen, the latest report and recent totals.</summary>
public sealed class BootTimerState
{
    /// <summary>Set when Restart and Time was clicked; cleared once the report is made or the timing is cancelled.</summary>
    public DateTime? PendingSinceUtc { get; set; }
    /// <summary>When Windows had started before the click, so the same start isn't mistaken for the new one.</summary>
    public DateTime? PendingBootUtc { get; set; }
    public BootReport? Last { get; set; }
    public List<BootHistoryEntry> History { get; set; } = new();
}

/// <summary>
/// Boot Timer (Ted, 2026-10-12): Kodiak Cleaner restarts the PC, then times the shutdown, Windows' start, sign-in and the
/// desktop settling, and lists which startup programs and Windows parts slowed it down. Only Windows' own sources are used:
/// process start times and counters, the sign-in time, and the Diagnostics-Performance event log.
/// </summary>
public static class BootTimer
{
    public const int HistoryKept = 10;
    /// <summary>A start more than this long ago wasn't seen straight after the restart, so its settle time isn't measured.</summary>
    public static readonly TimeSpan LateAfterSignIn = TimeSpan.FromMinutes(10);
    /// <summary>Windows' clock and the boot time worked out from the tick count can differ by a moment.</summary>
    public static readonly TimeSpan BootSlack = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Task Manager's Startup Impact (Microsoft's published thresholds): High above 1 s of processor time or 3 MB of disk,
    /// Medium above 300 ms or 300 KB, otherwise Low. A delay Windows itself reported counts as High from 1 s, else Medium.
    /// </summary>
    public static BootImpact ImpactOf(long cpuMs, long diskBytes, long delayMs = 0)
    {
        if (delayMs >= 1000 || cpuMs > 1000 || diskBytes > 3L * 1024 * 1024) return BootImpact.High;
        if (delayMs > 0 || cpuMs > 300 || diskBytes > 300L * 1024) return BootImpact.Medium;
        return BootImpact.Low;
    }

    /// <summary>True when Windows started again after Restart and Time was clicked, so the report can be made.</summary>
    public static bool RestartHappened(BootTimerState state, DateTime bootUtc) =>
        state.PendingSinceUtc is { } since && bootUtc > since - BootSlack &&
        (state.PendingBootUtc is not { } before || bootUtc > before + BootSlack);

    /// <summary>Slowest first: Windows' own delay, then impact, then processor time and disk, then A to Z.</summary>
    public static List<BootItem> Rank(IEnumerable<BootItem> items) =>
        items.OrderByDescending(i => i.DelayMs)
             .ThenByDescending(i => i.Impact)
             .ThenByDescending(i => i.CpuMs)
             .ThenByDescending(i => i.DiskBytes)
             .ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
             .ToList();

    /// <summary>Adds a finished report to the history (newest last, <see cref="HistoryKept"/> kept) and makes it the latest.</summary>
    public static void Finish(BootTimerState state, BootReport report)
    {
        state.PendingSinceUtc = null;
        state.PendingBootUtc = null;
        state.Last = report;
        state.History.Add(new BootHistoryEntry(report.RequestedUtc, Math.Round(report.Total.TotalSeconds, 1)));
        if (state.History.Count > HistoryKept) state.History.RemoveRange(0, state.History.Count - HistoryKept);
    }

    // --- Windows' own measurements: wevtutil output from Microsoft-Windows-Diagnostics-Performance/Operational ---

    public const string PerformanceLog = "Microsoft-Windows-Diagnostics-Performance/Operational";

    /// <summary>wevtutil arguments for the newest boot events (100 = the boot, 101 to 110 = what slowed it).</summary>
    public static string[] QueryArgs => new[]
    {
        "qe", PerformanceLog, "/q:*[System[(EventID>=100 and EventID<=110)]]", "/c:200", "/rd:true", "/f:xml",
    };

    /// <summary>Windows' boot measurement and slowdown events (wevtutil /f:xml prints events one after another without a root).</summary>
    public sealed record WindowsBootData(long BootMs, long MainPathMs, long PostBootMs, List<BootItem> Items);

    /// <summary>
    /// Reads the events that belong to the start beginning at <paramref name="bootUtc"/>: event 100 whose BootStartTime is
    /// that start, and the 101 to 110 slowdown events written after it. Returns null when event 100 for this start isn't there yet.
    /// </summary>
    public static WindowsBootData? ParseWindowsEvents(string xml, DateTime bootUtc)
    {
        XElement root;
        try { root = XElement.Parse("<Events>" + xml + "</Events>"); }
        catch (System.Xml.XmlException) { return null; }
        XNamespace ns = "http://schemas.microsoft.com/win/2004/08/events/event";
        long boot = 0, main = 0, post = 0;
        bool found = false;
        var items = new List<BootItem>();
        foreach (var ev in root.Elements(ns + "Event"))
        {
            var system = ev.Element(ns + "System");
            if (!int.TryParse(system?.Element(ns + "EventID")?.Value, out var id)) continue;
            var created = Utc(system?.Element(ns + "TimeCreated")?.Attribute("SystemTime")?.Value);
            var data = (ev.Element(ns + "EventData")?.Elements(ns + "Data") ?? Enumerable.Empty<XElement>())
                .Where(d => d.Attribute("Name") is not null)
                .GroupBy(d => d.Attribute("Name")!.Value)
                .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.OrdinalIgnoreCase);
            if (id == 100)
            {
                var start = Utc(data.GetValueOrDefault("BootStartTime"));
                if (start is null || (start.Value - bootUtc).Duration() > BootSlack) continue;
                found = true;
                boot = Long(data, "BootTime");
                main = Long(data, "MainPathBootTime");
                post = Long(data, "BootPostBootTime");
            }
            else if (created is { } at && at >= bootUtc - BootSlack)
            {
                var name = First(data, "FriendlyName", "Name");
                if (name.Length == 0) continue;
                var delay = Long(data, "DegradationTime");
                if (delay <= 0) delay = Long(data, "TotalTime");
                items.Add(new BootItem
                {
                    Name = name,
                    Kind = KindOf(id),
                    Path = First(data, "Path", "Name") is var p && p.Contains('\\') ? p : "",
                    DelayMs = delay,
                    Impact = ImpactOf(0, 0, delay),
                });
            }
        }
        return found ? new WindowsBootData(boot, main, post, items) : null;
    }

    /// <summary>What each Diagnostics-Performance boot event is about.</summary>
    public static string KindOf(int eventId) => eventId switch
    {
        101 => "App",
        102 => "Driver",
        103 => "Service",
        104 or 105 or 107 => "Windows",
        106 => "Background Task",
        108 => "Windows",
        109 => "Device",
        110 => "Session",
        _ => "Windows",
    };

    private static long Long(Dictionary<string, string> d, string key) =>
        long.TryParse(d.GetValueOrDefault(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static string First(Dictionary<string, string> d, params string[] keys) =>
        keys.Select(k => d.GetValueOrDefault(k)?.Trim() ?? "").FirstOrDefault(v => v.Length > 0) ?? "";

    private static DateTime? Utc(string? text) =>
        DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t) ? t : null;

    // --- Saving ---

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static BootTimerState Load(string file)
    {
        try
        {
            return File.Exists(file) ? JsonSerializer.Deserialize<BootTimerState>(File.ReadAllText(file), Json) ?? new() : new();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }

    public static void Save(string file, BootTimerState state)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
        File.WriteAllText(file, JsonSerializer.Serialize(state, Json));
    }

    /// <summary>"8.4 s", "1 min 12 s", "2 h 5 min".</summary>
    public static string Format(TimeSpan t) =>
        t.TotalSeconds < 60 ? $"{t.TotalSeconds.ToString("0.0", CultureInfo.CurrentCulture)} s"
        : t.TotalMinutes < 60 ? $"{(int)t.TotalMinutes} min {t.Seconds} s"
        : $"{(int)t.TotalHours} h {t.Minutes} min";
}
