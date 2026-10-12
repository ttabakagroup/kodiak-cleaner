using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using KodiakCleaner.Core.Util;
using Microsoft.Win32;

namespace KodiakCleaner.Core.Tools;

/// <summary>What a check found.</summary>
public enum IntegrityResult
{
    NotChecked,
    /// <summary>Nothing wrong.</summary>
    Healthy,
    /// <summary>Problems were found and fixed.</summary>
    Repaired,
    /// <summary>Something worth looking at (a warning, not a failure).</summary>
    Attention,
    /// <summary>Problems were found and are still there.</summary>
    ProblemsFound,
    /// <summary>The fix waits for the next restart.</summary>
    RestartNeeded,
    /// <summary>Windows' tool finished but its message wasn't one Kodiak Cleaner knows (another language, say): see Details.</summary>
    Finished,
    CouldNotCheck,
    /// <summary>The check doesn't apply (a ReFS drive checks itself).</summary>
    NotSupported,
}

public static class IntegrityResultText
{
    public static string Name(IntegrityResult r) => r switch
    {
        IntegrityResult.Healthy => "No Problems",
        IntegrityResult.Repaired => "Repaired",
        IntegrityResult.Attention => "Worth a Look",
        IntegrityResult.ProblemsFound => "Problems Found",
        IntegrityResult.RestartNeeded => "Restart Needed",
        IntegrityResult.Finished => "Finished",
        IntegrityResult.CouldNotCheck => "Couldn't Check",
        IntegrityResult.NotSupported => "Not Needed",
        _ => "Not Checked",
    };

    /// <summary>good, warn, bad or plain: picks the chip's theme colors.</summary>
    public static string Tone(IntegrityResult r) => r switch
    {
        IntegrityResult.Healthy or IntegrityResult.Repaired => "good",
        IntegrityResult.Attention or IntegrityResult.RestartNeeded or IntegrityResult.CouldNotCheck => "warn",
        IntegrityResult.ProblemsFound => "bad",
        _ => "plain",
    };
}

/// <summary>What one run of a Windows console tool printed, and how it ended.</summary>
public sealed record ToolRun(int ExitCode, IReadOnlyList<string> Lines);

/// <summary>One physical disk's own health report (the drive's self-monitoring, as Windows' storage stack shares it).</summary>
public sealed record DiskHealth(string Id, string Name, string Type, long Size, string Health, int? TemperatureC, int? WearPercent,
    long? UncorrectedErrors, long? PowerOnHours, IntegrityResult Result, string Note)
{
    public string SizeText => Size > 0 ? ByteSize.Format(Size) : "";
    public string TemperatureText => TemperatureC is { } t ? $"{t} °C" : "";
    public string WearText => WearPercent is { } w ? $"{w}%" : "";
    public string ErrorsText => UncorrectedErrors?.ToString(CultureInfo.CurrentCulture) ?? "";
    public string PowerOnText => PowerOnHours is { } h ? $"{h:N0} h" : "";
    public string ResultName => IntegrityResultText.Name(Result);
    public string Tone => IntegrityResultText.Tone(Result);
}

/// <summary>Drive problems Windows logged, one row per kind of event.</summary>
public sealed record DriveEventGroup(string Source, int EventId, int Count, DateTime Last, string Meaning, string Message)
{
    public string LastText => Last.ToString("g", CultureInfo.CurrentCulture);
}

/// <summary>The last Windows Memory Diagnostic result.</summary>
public sealed record MemoryTestResult(DateTime When, bool ErrorsFound);

/// <summary>
/// Integrity Check (Ted, 2026-10-12): Windows' own repair tools (sfc, DISM, chkdsk, the Memory Diagnostic) and what Windows
/// already knows about its drives (their own health report, logged disk errors). Nothing is installed or sent anywhere;
/// the only thing that can go online is DISM's Component Store repair, which Windows may fetch from Windows Update.
/// </summary>
public static partial class IntegrityCheck
{
    static IntegrityCheck() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // chkdsk and DISM write in the OEM code page

    public static string Sys(string exe) => Path.Combine(Environment.SystemDirectory, exe);

    // ---------- Windows System Files (sfc) ----------

    /// <summary>/verifyonly only reads; /scannow puts back clean copies from the component store.</summary>
    public static string[] SfcArgs(bool repair) => [repair ? "/scannow" : "/verifyonly"];

    /// <summary>Reads sfc's closing message (sfc has no exit codes worth trusting and no English switch).</summary>
    public static IntegrityResult ReadSfc(IEnumerable<string> lines, bool repair)
    {
        var all = string.Join(" ", lines);
        bool Has(string s) => all.Contains(s, StringComparison.OrdinalIgnoreCase);
        if (Has("repair pending") || Has("requires reboot") || Has("pending which requires")) return IntegrityResult.RestartNeeded;
        if (Has("did not find any integrity violations")) return IntegrityResult.Healthy;
        if (Has("unable to fix some")) return IntegrityResult.ProblemsFound;
        if (Has("successfully repaired")) return Has("reboot") || Has("restart") ? IntegrityResult.RestartNeeded : IntegrityResult.Repaired;
        if (Has("found integrity violations") || Has("found corrupt files")) return IntegrityResult.ProblemsFound;
        if (Has("could not perform") || Has("could not start") || Has("must be an administrator")) return IntegrityResult.CouldNotCheck;
        return lines.Any() ? IntegrityResult.Finished : IntegrityResult.CouldNotCheck;
    }

    // ---------- Windows Component Store (DISM) ----------

    /// <summary>/English keeps DISM's messages readable whatever Windows' language is.</summary>
    public static string[] DismArgs(bool repair) => ["/Online", "/Cleanup-Image", repair ? "/RestoreHealth" : "/ScanHealth", "/English"];

    public static IntegrityResult ReadDism(int exitCode, IEnumerable<string> lines, bool repair)
    {
        var all = string.Join(" ", lines);
        bool Has(string s) => all.Contains(s, StringComparison.OrdinalIgnoreCase);
        if (all.Trim().Length == 0) return IntegrityResult.CouldNotCheck; // DISM always says something
        if (exitCode == 3010 || Has("restart Windows to complete")) return IntegrityResult.RestartNeeded;
        if (exitCode != 0) return Has("source files could not be found") || Has("0x800f081f") ? IntegrityResult.ProblemsFound : IntegrityResult.CouldNotCheck;
        if (Has("cannot be repaired") || Has("is repairable")) return IntegrityResult.ProblemsFound;
        if (Has("corruption was repaired")) return IntegrityResult.Repaired;
        if (Has("No component store corruption detected") || Has("completed successfully")) return IntegrityResult.Healthy;
        return IntegrityResult.Finished;
    }

    /// <summary>The error line DISM ends with ("Error: 0x800f081f"), for the summary.</summary>
    public static string? DismError(IEnumerable<string> lines) =>
        lines.LastOrDefault(l => l.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) || l.Contains("0x8", StringComparison.Ordinal));

    // ---------- Drive File System (chkdsk) ----------

    /// <summary>chkdsk can check NTFS while the drive stays in use (/scan) and FAT/exFAT read-only; ReFS checks and repairs itself.</summary>
    public static bool CanCheck(string fileSystem) =>
        fileSystem.ToUpperInvariant() is "NTFS" or "FAT" or "FAT32" or "EXFAT";

    /// <summary>
    /// Check: NTFS gets an online /scan (nothing locked), FAT and exFAT a read-only pass. Repair: /f fixes, /x dismounts first,
    /// /r also looks for bad sectors (slow). The Windows drive can't be locked, so its repair is scheduled for the next restart.
    /// </summary>
    public static string[] ChkdskArgs(string letter, string fileSystem, bool repair, bool badSectors, bool windowsDrive)
    {
        var args = new List<string> { letter.TrimEnd(':', '\\') + ":" };
        bool ntfs = fileSystem.Equals("NTFS", StringComparison.OrdinalIgnoreCase);
        if (!repair)
        {
            if (ntfs) args.Add("/scan");
            return args.ToArray();
        }
        args.Add(badSectors ? "/r" : "/f");
        if (!windowsDrive) args.Add("/x");
        return args.ToArray();
    }

    /// <summary>
    /// chkdsk's exit codes: 0 no errors, 1 errors fixed, 2 cleanup done (or not done without /f), 3 errors found and not fixed
    /// (or the check couldn't run). <paramref name="scheduled"/> = the repair waits for the next restart.
    /// </summary>
    public static IntegrityResult ReadChkdsk(int exitCode, bool repair, bool scheduled)
    {
        if (scheduled) return IntegrityResult.RestartNeeded;
        return exitCode switch
        {
            0 or 2 => IntegrityResult.Healthy,
            1 => repair ? IntegrityResult.Repaired : IntegrityResult.ProblemsFound,
            3 => IntegrityResult.ProblemsFound,
            _ => IntegrityResult.CouldNotCheck,
        };
    }

    /// <summary>Whether Windows will check this drive at the next start (chkdsk adds it to Session Manager's BootExecute).</summary>
    public static bool IsScheduledAtRestart(string letter)
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager");
            return k?.GetValue("BootExecute") is string[] lines && ScheduledIn(lines, letter);
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { return false; }
    }

    /// <summary>"autocheck autochk /p \??\C:" (a chkdsk /f scheduled for drive C).</summary>
    public static bool ScheduledIn(IEnumerable<string> bootExecute, string letter)
    {
        var target = @"\??\" + letter.TrimEnd(':', '\\').ToUpperInvariant() + ":";
        return bootExecute.Any(l => l.Contains("autochk", StringComparison.OrdinalIgnoreCase) && l.ToUpperInvariant().Contains(target));
    }

    // ---------- Progress and running Windows' tools ----------

    /// <summary>
    /// The percent in a progress line: sfc "Verification 45% complete.", DISM "[=====  27.1%  ]", chkdsk "Stage: 40%; Total: 12%"
    /// (the last percent on the line, which is chkdsk's total).
    /// </summary>
    public static int? PercentOf(string line)
    {
        var ms = PercentRx().Matches(line);
        if (ms.Count == 0) return null;
        return double.TryParse(ms[^1].Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v is >= 0 and <= 100 ? (int)v : null;
    }

    [GeneratedRegex(@"(\d{1,3}(?:[.,]\d)?)\s*%")]
    private static partial Regex PercentRx();

    /// <summary>
    /// Runs one of Windows' own console tools from System32 and reports each line (sfc writes UTF-16 when its output is
    /// redirected, chkdsk and DISM the OEM code page; the first bytes tell which). <paramref name="input"/> answers its prompts.
    /// </summary>
    public static async Task<ToolRun> RunAsync(string exe, IEnumerable<string> args, IProgress<string> output, IProgress<int> percent,
        CancellationToken ct, string? input = null)
    {
        var psi = new ProcessStartInfo(Sys(exe))
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = input is not null,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Windows' {exe} could not be started.");
        if (input is not null)
        {
            try
            {
                await p.StandardInput.WriteAsync(input);
                p.StandardInput.Close();
            }
            catch (IOException) { } // the tool didn't wait for an answer
        }
        var lines = new List<string>();
        var sink = new LineSink(line =>
        {
            lock (lines) lines.Add(line);
            output.Report(line);
            if (PercentOf(line) is { } v) percent.Report(v);
        });
        var readOut = Pump(p.StandardOutput.BaseStream, sink);
        var readErr = Pump(p.StandardError.BaseStream, sink);
        using (ct.Register(() => { try { p.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } }))
        {
            await p.WaitForExitAsync(CancellationToken.None);
        }
        await Task.WhenAll(readOut, readErr);
        ct.ThrowIfCancellationRequested();
        lock (lines) return new ToolRun(p.ExitCode, lines.ToList());
    }

    /// <summary>Decodes a tool's bytes into lines, splitting on '\r' too (progress lines rewrite themselves with '\r').</summary>
    public static async Task Pump(Stream stream, IProgress<string> output)
    {
        var bytes = new byte[4096];
        Decoder? decoder = null;
        var chars = new char[8192];
        var sb = new StringBuilder();
        int n;
        while ((n = await stream.ReadAsync(bytes)) > 0)
        {
            int start = 0;
            if (decoder is null)
            {
                bool unicode = LooksLikeUtf16(bytes.AsSpan(0, n));
                decoder = (unicode ? Encoding.Unicode : Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage)).GetDecoder();
                if (unicode && n >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) start = 2;
            }
            int count = decoder.GetChars(bytes, start, n - start, chars, 0);
            for (int i = 0; i < count; i++)
            {
                if (chars[i] is '\r' or '\n')
                {
                    Flush();
                }
                else if (chars[i] != '\0') sb.Append(chars[i]);
            }
        }
        Flush();

        void Flush()
        {
            if (sb.ToString().Trim() is { Length: > 0 } line) output.Report(line);
            sb.Clear();
        }
    }

    /// <summary>UTF-16 text has a byte-order mark, or a zero in most odd positions (plain Latin letters).</summary>
    public static bool LooksLikeUtf16(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) return true;
        int pairs = b.Length / 2, zeros = 0;
        if (pairs == 0) return false;
        for (int i = 1; i < b.Length; i += 2) if (b[i] == 0) zeros++;
        return zeros * 2 > pairs;
    }

    /// <summary>Reports on the calling thread (Progress&lt;T&gt; would post each line later and out of order).</summary>
    private sealed class LineSink(Action<string> report) : IProgress<string>
    {
        public void Report(string value) => report(value);
    }

    /// <summary>Saves a run's output as Reports\Integrity Check\&lt;date time&gt; &lt;check&gt;.txt and returns the file.</summary>
    public static string SaveReport(string reportsFolder, string check, string what, IEnumerable<string> lines, IntegrityResult result, DateTime when)
    {
        var folder = Path.Combine(reportsFolder, "Integrity Check");
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, $"{when:yyyy-MM-dd HH-mm-ss} {check}.txt");
        var text = new StringBuilder()
            .AppendLine($"{check}: {what}")
            .AppendLine(when.ToString("F", CultureInfo.CurrentCulture))
            .AppendLine($"Result: {IntegrityResultText.Name(result)}")
            .AppendLine();
        foreach (var l in lines) text.AppendLine(l);
        File.WriteAllText(file, text.ToString());
        return file;
    }

    // ---------- Drive Health (the drives' own report) ----------

    /// <summary>
    /// Every physical disk's health as Windows' storage stack reports it, plus temperature, wear and uncorrected errors when
    /// Windows shares them (those need administrator rights). Empty when the storage classes aren't there.
    /// </summary>
    public static List<DiskHealth> ReadDiskHealth()
    {
        const string scope = @"root\Microsoft\Windows\Storage";
        var disks = SystemDetails.Query("SELECT DeviceId, FriendlyName, MediaType, BusType, Size, HealthStatus FROM MSFT_PhysicalDisk", scope);
        var counters = new Dictionary<string, Dictionary<string, object?>>();
        foreach (var r in SystemDetails.Query(
                     "SELECT DeviceId, Temperature, Wear, ReadErrorsUncorrected, WriteErrorsUncorrected, PowerOnHours FROM MSFT_StorageReliabilityCounter", scope))
            if (SystemDetails.Text(r, "DeviceId") is { } id) counters[id] = r;

        var list = new List<DiskHealth>();
        foreach (var d in disks.OrderBy(d => int.TryParse(SystemDetails.Text(d, "DeviceId"), out var n) ? n : int.MaxValue))
        {
            var id = SystemDetails.Text(d, "DeviceId") ?? "?";
            counters.TryGetValue(id, out var c);
            int? temp = c is null ? null : SystemDetails.Number(c, "Temperature") is { } t and > 0 and < 150 ? (int)t : null;
            int? wear = c is null ? null : SystemDetails.Number(c, "Wear") is { } w and >= 0 and <= 100 ? (int)w : null;
            long? errors = c is null ? null
                : (SystemDetails.Number(c, "ReadErrorsUncorrected"), SystemDetails.Number(c, "WriteErrorsUncorrected")) switch
                {
                    (null, null) => null,
                    var (r, wr) => (r ?? 0) + (wr ?? 0),
                };
            long? hours = c is null ? null : SystemDetails.Number(c, "PowerOnHours") is { } h and > 0 ? h : null;
            var health = SystemDetails.Number(d, "HealthStatus");
            var (result, note) = Judge(health, temp, wear, errors);
            var type = TypeName(SystemDetails.Number(d, "MediaType"), SystemDetails.Number(d, "BusType"));
            list.Add(new DiskHealth(id, $"Disk {id}: {SystemDetails.Text(d, "FriendlyName") ?? "Disk"}", type, SystemDetails.Number(d, "Size") ?? 0,
                SystemDetails.Health(health) ?? "Unknown", temp, wear, errors, hours, result, note));
        }
        return list;
    }

    /// <summary>The same names Defrag and TRIM and Drive Wiper use: Hard Drive, SSD, NVMe SSD, Flash Drive.</summary>
    public static string TypeName(long? mediaType, long? busType) => (mediaType, busType) switch
    {
        (_, 17) => "NVMe SSD",
        (_, 7 or 12 or 13) => "Flash Drive",
        (3, _) => "Hard Drive",
        (4, _) => "SSD",
        _ => "Unknown",
    };

    /// <summary>
    /// The verdict for one disk: Windows' own health status first (Warning or Unhealthy means the drive itself predicts a
    /// failure), then wear at 90% or more, uncorrected errors, and a temperature of 70 °C or more.
    /// </summary>
    public static (IntegrityResult Result, string Note) Judge(long? healthStatus, int? temperatureC, int? wearPercent, long? uncorrectedErrors)
    {
        if (healthStatus == 2) return (IntegrityResult.ProblemsFound, "The drive reports it is failing. Back up its files now and replace it.");
        if (healthStatus == 1) return (IntegrityResult.Attention, "The drive warns it may fail. Back up its files soon.");
        var notes = new List<string>();
        if (wearPercent >= 90) notes.Add($"{wearPercent}% of its rated life is used up");
        if (uncorrectedErrors > 0) notes.Add($"{uncorrectedErrors} read or write errors it couldn't correct");
        if (temperatureC >= 70) notes.Add($"it's running hot ({temperatureC} °C)");
        if (notes.Count > 0) return (IntegrityResult.Attention, "Worth watching: " + string.Join(", ", notes) + ".");
        return healthStatus == 0 ? (IntegrityResult.Healthy, "Healthy.") : (IntegrityResult.CouldNotCheck, "The drive didn't share its health.");
    }

    // ---------- Drive Error History (System event log) ----------

    /// <summary>Providers that log drive and file system trouble in the System log.</summary>
    public static readonly string[] DriveProviders =
        ["disk", "Ntfs", "Microsoft-Windows-Ntfs", "stornvme", "storahci", "iaStorAC", "iaStorAVC", "iaStorVD", "volmgr", "partmgr"];

    /// <summary>wevtutil arguments: critical, error and warning events from <see cref="DriveProviders"/> in the last <paramref name="days"/> days.</summary>
    public static string[] DriveEventQueryArgs(int days)
    {
        var providers = string.Join(" or ", DriveProviders.Select(p => $"Provider[@Name='{p}']"));
        long ms = days * 24L * 3600 * 1000;
        return ["qe", "System", $"/q:*[System[({providers}) and (Level=1 or Level=2 or Level=3) and TimeCreated[timediff(@SystemTime) <= {ms}]]]",
            "/c:1000", "/rd:true", "/f:RenderedXml"];
    }

    /// <summary>What a known event means, in plain words (null for events without a known meaning).</summary>
    public static string? Meaning(string provider, int id) => (provider.ToLowerInvariant(), id) switch
    {
        ("disk", 7) => "A bad block was found on a drive. Copy off anything important and check the drive's health.",
        ("disk", 11) => "A drive or its cable reported an error.",
        ("disk", 15) => "A drive wasn't ready when Windows asked for it.",
        ("disk", 51) => "A read or write failed while Windows was paging memory to a drive.",
        ("disk", 52) => "A drive warned that it may fail soon. Back it up.",
        ("disk", 153) => "Windows had to retry a read or write on a drive.",
        ("disk", 154) => "A drive reported a hardware error.",
        ("disk", 157) => "A drive was removed without being ejected first.",
        ("ntfs" or "microsoft-windows-ntfs", 55) => "A drive's file system is damaged. Repair it under Drive File System.",
        ("ntfs" or "microsoft-windows-ntfs", 98) => "Windows found a drive that needs its file system checked. Repair it under Drive File System.",
        ("ntfs" or "microsoft-windows-ntfs", 137 or 140) => "Windows couldn't finish writing file system changes to a drive.",
        ("stornvme" or "storahci" or "iastorac" or "iastoravc" or "iastorvd", 11) => "A drive's controller reported an error.",
        ("stornvme" or "storahci" or "iastorac" or "iastoravc" or "iastorvd", 129) => "A drive stopped answering and Windows had to reset it.",
        ("volmgr", 46) => "Windows couldn't set up a crash dump on the start-up drive.",
        _ => null,
    };

    /// <summary>Groups wevtutil's events (printed one after another, without a root) by source and event number, newest first.</summary>
    public static List<DriveEventGroup> ParseDriveEvents(string xml)
    {
        var groups = new Dictionary<(string, int), (int Count, DateTime Last, string Message)>();
        foreach (var (provider, id, when, message) in Events(xml))
        {
            var key = (provider, id);
            if (groups.TryGetValue(key, out var g))
                groups[key] = (g.Count + 1, when > g.Last ? when : g.Last, when > g.Last ? message : g.Message);
            else groups[key] = (1, when, message);
        }
        return groups.Select(kv => new DriveEventGroup(SourceName(kv.Key.Item1), kv.Key.Item2, kv.Value.Count, kv.Value.Last,
                Meaning(kv.Key.Item1, kv.Key.Item2) ?? "Windows logged a drive problem. See its message.", kv.Value.Message))
            .OrderByDescending(g => g.Last).ToList();
    }

    /// <summary>The event's source as people know it.</summary>
    public static string SourceName(string provider) => provider.ToLowerInvariant() switch
    {
        "disk" => "Disk",
        "ntfs" or "microsoft-windows-ntfs" => "NTFS",
        "stornvme" => "NVMe Controller",
        "storahci" => "SATA Controller",
        "iastorac" or "iastoravc" or "iastorvd" => "Intel Storage Controller",
        "volmgr" => "Volume Manager",
        "partmgr" => "Partition Manager",
        _ => provider,
    };

    /// <summary>wevtutil arguments for the newest Windows Memory Diagnostic result.</summary>
    public static readonly string[] MemoryQueryArgs =
        ["qe", "System", "/q:*[System[Provider[@Name='Microsoft-Windows-MemoryDiagnostics-Results']]]", "/c:1", "/rd:true", "/f:xml"];

    /// <summary>Event 1201 = no errors, 1202 = errors found (other results are ignored).</summary>
    public static MemoryTestResult? ParseMemoryResult(string xml)
    {
        foreach (var (_, id, when, _) in Events(xml))
            if (id is 1201 or 1202) return new MemoryTestResult(when, id == 1202);
        return null;
    }

    private static IEnumerable<(string Provider, int Id, DateTime When, string Message)> Events(string xml)
    {
        XElement root;
        try { root = XElement.Parse("<Events>" + xml + "</Events>"); }
        catch (System.Xml.XmlException) { yield break; }
        XNamespace ns = "http://schemas.microsoft.com/win/2004/08/events/event";
        foreach (var ev in root.Elements(ns + "Event"))
        {
            var system = ev.Element(ns + "System");
            var provider = (string?)system?.Element(ns + "Provider")?.Attribute("Name") ?? "";
            if (!int.TryParse(system?.Element(ns + "EventID")?.Value, out var id)) continue;
            var time = (string?)system?.Element(ns + "TimeCreated")?.Attribute("SystemTime");
            if (!DateTime.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var utc)) continue;
            var message = ev.Element(ns + "RenderingInfo")?.Element(ns + "Message")?.Value ?? "";
            message = Regex.Replace(message, @"\s+", " ").Trim();
            if (message.Length > 400) message = message[..400] + "…";
            yield return (provider, id, DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime(), message);
        }
    }

    /// <summary>Runs wevtutil with <paramref name="args"/> and returns its XML, or null when the log can't be read.</summary>
    public static string? QueryEvents(string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo(Sys("wevtutil.exe"))
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
            };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            if (p is null) return null;
            var errors = p.StandardError.ReadToEndAsync();
            var xml = p.StandardOutput.ReadToEnd();
            if (!p.WaitForExit(30_000)) { try { p.Kill(); } catch (InvalidOperationException) { } return null; }
            _ = errors.Result;
            return p.ExitCode == 0 ? xml : null;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException or InvalidOperationException) { return null; }
    }
}
