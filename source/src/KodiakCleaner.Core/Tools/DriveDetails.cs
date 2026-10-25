using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.Core.Tools;

/// <summary>One line of a drive's summary: what it is and its value.</summary>
public sealed record DriveDetailLine(string Name, string Value);

/// <summary>A titled part of a drive's summary, with an optional note under it.</summary>
public sealed record DriveDetailSection(string Title, IReadOnlyList<DriveDetailLine> Lines, string? Note = null);

/// <summary>A partition on the disk, with its letter's file system, name and free space when it has one.</summary>
public sealed record DrivePartition(int Number, long Size, string? Letter, string Kind)
{
    public string? FileSystem { get; init; }
    public string? Label { get; init; }
    public long? Free { get; init; }
}

/// <summary>Everything Drive Details shows about one disk. Null values weren't shared by Windows or the drive.</summary>
public sealed record DriveDetailsInfo(WipeDisk Disk)
{
    public string? Firmware { get; init; }
    public string? Manufacturer { get; init; }
    /// <summary>STORAGE_BUS_TYPE / MSFT_PhysicalDisk.BusType: 7 USB, 11 SATA, 17 NVMe and so on.</summary>
    public long? BusType { get; init; }
    /// <summary>MSFT_Disk.PartitionStyle: 0 none (uninitialized), 1 MBR, 2 GPT.</summary>
    public long? PartitionStyle { get; init; }
    public long? LogicalSectorSize { get; init; }
    public long? PhysicalSectorSize { get; init; }
    /// <summary>MSFT_PhysicalDisk.SpindleSpeed: revolutions a minute, 0 for flash, 4294967295 when unknown.</summary>
    public long? SpindleSpeed { get; init; }
    public bool? IsOffline { get; init; }
    public bool? IsReadOnly { get; init; }
    public IReadOnlyList<DrivePartition> Partitions { get; init; } = [];
    public DriveHealthReport Report { get; init; } = new();
    /// <summary>The disk errors Windows logged for this disk in the last <see cref="DriveHealthCheck.EventDays"/> days; null when the log couldn't be read.</summary>
    public IReadOnlyList<DriveEventGroup>? Events { get; init; }
    /// <summary>Kodiak Cleaner runs as administrator (most health counts need it).</summary>
    public bool Administrator { get; init; }
}

/// <summary>
/// Drive Details (Ted, 0.8.116): the summary behind the info button on each Drive Wiper row. Model, serial number, type, bus, size,
/// firmware, partition style and partitions, the drive's own health report (the same one Check Drive reads: Windows' storage
/// reliability counters and the ATA self-monitoring values) and the disk errors Windows logged. Read-only: only queries.
/// </summary>
public static class DriveDetails
{
    public const string NotShared = "Not Shared";

    public static string PartitionStyleName(long? style) => style switch
    {
        0 => "None (Uninitialized)",
        1 => "MBR",
        2 => "GPT",
        _ => NotShared,
    };

    /// <summary>How the drive is connected, in the names people know.</summary>
    public static string BusName(long? bus) => bus switch
    {
        1 => "SCSI",
        2 => "ATAPI",
        3 => "ATA",
        4 => "FireWire",
        5 => "SSA",
        6 => "Fibre Channel",
        7 => "USB",
        8 => "RAID",
        9 => "iSCSI",
        10 => "SAS",
        11 => "SATA",
        12 => "SD Card",
        13 => "MMC",
        15 => "Virtual Disk",
        16 => "Storage Spaces",
        17 => "NVMe",
        18 => "Storage Class Memory",
        _ => NotShared,
    };

    // The GPT partition types Windows makes (lower case, as MSFT_Partition gives them inside braces).
    private static readonly Dictionary<string, string> GptKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["c12a7328-f81f-11d2-ba4b-00a0c93ec93b"] = "EFI System",
        ["e3c9e316-0b5c-4db8-817d-f92df00215ae"] = "Microsoft Reserved",
        ["ebd0a0a2-b9e5-4433-87c0-68b6b72699c7"] = "Basic Data",
        ["de94bba4-06d1-4d40-a16a-bfd50179d6ac"] = "Recovery",
        ["5808c8aa-7e8f-42e0-85d2-e1e90434cfb3"] = "Dynamic Disk Data",
        ["af9b60a0-1431-4f62-bc68-3311714a69ad"] = "Dynamic Disk Data",
        ["e75caf8f-f680-4cee-afa3-b001e56efc2d"] = "Storage Spaces",
    };

    /// <summary>What a partition is for, from its GPT type (a GUID) or its MBR type byte.</summary>
    public static string PartitionKind(string? gptType, long? mbrType)
    {
        if (!string.IsNullOrWhiteSpace(gptType))
            return GptKinds.TryGetValue(gptType.Trim().Trim('{', '}'), out var kind) ? kind : "Other";
        return mbrType switch
        {
            1 or 4 or 6 or 14 => "FAT",
            7 => "Basic Data",
            11 or 12 => "FAT32",
            0x27 => "Recovery",
            0x42 => "Dynamic Disk Data",
            0xEE => "GPT Protective",
            0xEF => "EFI System",
            _ => "Other",
        };
    }

    private static string YesNo(bool value) => value ? "Yes" : "No";
    private static string N(long value) => value.ToString("N0", CultureInfo.CurrentCulture);
    private static string Size(long bytes) => $"{ByteSize.Format(bytes)} ({N(bytes)} bytes)";

    /// <summary>A partition in one line: "C: · NTFS · Data · 465 GB (120 GB free)", or its kind and size when it has no letter.</summary>
    public static string PartitionText(DrivePartition p)
    {
        var parts = new List<string>();
        if (p.Letter is { Length: > 0 } letter) parts.Add(letter.TrimEnd('\\'));
        parts.Add(p.Kind);
        if (p.FileSystem is { Length: > 0 } fs) parts.Add(fs);
        if (p.Label is { Length: > 0 } label) parts.Add($"Named \"{label}\"");
        parts.Add(ByteSize.Format(p.Size) + (p.Free is { } free ? $" ({ByteSize.Format(free)} free)" : ""));
        return string.Join(" · ", parts);
    }

    /// <summary>The summary, top to bottom: Summary, Drive, Layout, Health, then the logged errors.</summary>
    public static List<DriveDetailSection> Sections(DriveDetailsInfo info)
    {
        var d = info.Disk;
        var r = info.Report;
        var events = info.Events ?? [];
        var sections = new List<DriveDetailSection>();

        // The same verdict as Check Drive, without reading the sectors.
        var verdict = DriveHealthCheck.Judge(r, events, null, null);
        var summary = new List<DriveDetailLine> { new("Verdict", verdict.VerdictName) };
        summary.AddRange(verdict.Reasons.Select((reason, i) => new DriveDetailLine(i == 0 ? "Why" : "", reason)));
        sections.Add(new DriveDetailSection("Summary", summary,
            verdict.Summary + " Check Drive in Step 4 also reads every sector, without writing anything."));

        var drive = new List<DriveDetailLine>
        {
            new("Model", d.Model),
            new("Serial Number", string.IsNullOrWhiteSpace(d.Serial) ? NotShared : d.Serial.Trim()),
        };
        if (SerialText.IsDriveId(d.Serial))
            drive.Add(new("", "This is the ID Windows gives the drive; the serial number on its label may differ."));
        if (info.Manufacturer is { Length: > 0 } maker && !d.Model.Contains(maker, StringComparison.OrdinalIgnoreCase)) drive.Add(new("Manufacturer", maker));
        drive.Add(new("Type", d.KindName));
        drive.Add(new("Connection", BusName(info.BusType)));
        drive.Add(new("Removable", YesNo(d.Removable)));
        drive.Add(new("Size", Size(d.Size)));
        drive.Add(new("Firmware Version", info.Firmware is { Length: > 0 } fw ? fw : NotShared));
        if (info.LogicalSectorSize is { } logical && logical > 0)
            drive.Add(new("Sector Size", info.PhysicalSectorSize is { } physical && physical > 0 && physical != logical
                ? $"{N(logical)} bytes ({N(physical)} bytes physical)" : $"{N(logical)} bytes"));
        if (info.SpindleSpeed is { } rpm && rpm > 0 && rpm < uint.MaxValue) drive.Add(new("Spindle Speed", $"{N(rpm)} RPM"));
        drive.Add(new("Disk Number", d.Number.ToString(CultureInfo.InvariantCulture)));
        sections.Add(new DriveDetailSection("Drive", drive));

        var layout = new List<DriveDetailLine>
        {
            new("Partition Style", PartitionStyleName(info.PartitionStyle)),
            new("Drive Letters", d.Drives),
        };
        if (info.IsOffline is { } offline) layout.Add(new("Online", YesNo(!offline)));
        if (info.IsReadOnly is { } readOnly) layout.Add(new("Read-Only", YesNo(readOnly)));
        layout.Add(new("Can Be Wiped", d.CanWipe ? "Yes" : "No: " + d.Reason));
        if (info.Partitions.Count == 0) layout.Add(new("Partitions", info.PartitionStyle == 0 ? "None" : NotShared));
        foreach (var p in info.Partitions.OrderBy(p => p.Number))
            layout.Add(new($"Partition {p.Number}", PartitionText(p)));
        sections.Add(new DriveDetailSection("Layout", layout));

        var health = new List<DriveDetailLine> { new("Health", r.HealthName) };
        if (r.OperationalStatus.Count > 0) health.Add(new("Status", string.Join(", ", r.OperationalStatus.Select(DriveHealthCheck.OperationalName))));
        if (r.PredictsFailure is { } predicts) health.Add(new("Predicts a Failure", YesNo(predicts)));
        if (r.TemperatureC is { } t) health.Add(new("Temperature", $"{t} °C" + (r.TemperatureMaxC is { } max && max > t ? $" (highest {max} °C)" : "")));
        if (r.WearPercent is { } wear) health.Add(new("Wear", $"{wear}% of its rated life used"));
        if (r.PowerOnHours is { } hours) health.Add(new("Powered On", $"{N(hours)} hours"));
        string Errors(long? uncorrected, long? corrected) =>
            string.Join(", ", new[] { uncorrected is { } u ? $"{N(u)} uncorrected" : null, corrected is { } c ? $"{N(c)} corrected" : null }.Where(s => s is not null));
        if (r.ReadErrorsUncorrected is not null || r.ReadErrorsCorrected is not null) health.Add(new("Read Errors", Errors(r.ReadErrorsUncorrected, r.ReadErrorsCorrected)));
        if (r.WriteErrorsUncorrected is not null || r.WriteErrorsCorrected is not null) health.Add(new("Write Errors", Errors(r.WriteErrorsUncorrected, r.WriteErrorsCorrected)));
        if (r.Reallocated is { } re) health.Add(new("Replaced Sectors", N(re)));
        if (r.Pending is { } pe) health.Add(new("Pending Sectors", N(pe)));
        if (r.OfflineUncorrectable is { } un) health.Add(new("Uncorrectable Sectors", N(un)));
        string? healthNote = !r.Shared
            ? "The drive doesn't share its own health report through Windows. This is common for USB drives and memory cards."
            : !info.Administrator
            ? "Error counts, wear, temperature and the self-monitoring (SMART) values need administrator rights, so some may be missing."
            : health.Count == 1 ? "The drive shares only its overall health, no counts." : null;
        sections.Add(new DriveDetailSection("Health", health, healthNote));

        var logged = new List<DriveDetailLine>();
        if (info.Events is { } list)
        {
            foreach (var e in list)
                logged.Add(new($"{e.Source} {e.EventId}", $"{e.Count}× (last {e.LastText}): {e.Meaning}"));
            if (list.Count == 0) logged.Add(new("Errors", "None"));
        }
        sections.Add(new DriveDetailSection($"Disk Errors Windows Logged (Last {DriveHealthCheck.EventDays} Days)", logged,
            info.Events is null ? "The System log couldn't be read." : null));
        return sections;
    }

    /// <summary>The summary as plain text, for Copy.</summary>
    public static string Text(DriveDetailsInfo info, IEnumerable<DriveDetailSection> sections)
    {
        var sb = new StringBuilder();
        sb.Append("Drive Details: ").Append(info.Disk.Name).Append("\r\n");
        foreach (var s in sections)
        {
            sb.Append("\r\n").Append(s.Title).Append("\r\n");
            foreach (var line in s.Lines)
                sb.Append(line.Name.Length > 0 ? $"  {line.Name}: {line.Value}" : $"    {line.Value}").Append("\r\n");
            if (s.Note is { Length: > 0 } note) sb.Append("  ").Append(note).Append("\r\n");
        }
        return sb.ToString();
    }

    /// <summary>A partition from its MSFT_Partition row; the letter comes as a char (or its number), 0 when there's none.</summary>
    internal static DrivePartition? ReadPartition(Dictionary<string, object?> row)
    {
        if (Number(row.GetValueOrDefault("PartitionNumber")) is not { } number) return null;
        var raw = row.GetValueOrDefault("DriveLetter");
        char? c = raw switch
        {
            null => null,
            char ch => ch,
            string s => s.Length > 0 ? s[0] : null,
            _ => Number(raw) is { } code and > 0 and < 128 ? (char)code : null,
        };
        string? letter = c is { } l && char.IsAsciiLetter(l) ? $"{char.ToUpperInvariant(l)}:" : null;
        var gpt = row.GetValueOrDefault("GptType")?.ToString();
        return new DrivePartition((int)number, Number(row.GetValueOrDefault("Size")) ?? 0, letter,
            PartitionKind(string.IsNullOrWhiteSpace(gpt) ? null : gpt, Number(row.GetValueOrDefault("MbrType"))));
    }

    private static long? Number(object? value)
    {
        try { return value is null or string { Length: 0 } ? null : Convert.ToInt64(value, CultureInfo.InvariantCulture); }
        catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException) { return null; }
    }

    private static bool? Flag(Dictionary<string, object?>? row, string name) => row?.GetValueOrDefault(name) is bool b ? b : null;

    /// <summary>
    /// Reads one disk's details from Windows: MSFT_PhysicalDisk, MSFT_Disk and MSFT_Partition (root\Microsoft\Windows\Storage), the
    /// health report Check Drive uses, each letter's file system, and the System log. Queries only; nothing is written to the disk.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static DriveDetailsInfo Read(WipeDisk disk, bool administrator)
    {
        const string scope = @"root\Microsoft\Windows\Storage";
        var id = disk.Number.ToString(CultureInfo.InvariantCulture);
        var physical = SystemDetails.Query("SELECT DeviceId, Manufacturer, BusType, FirmwareVersion, LogicalSectorSize, PhysicalSectorSize, SpindleSpeed " +
                                           $"FROM MSFT_PhysicalDisk WHERE DeviceId = '{id}'", scope).FirstOrDefault();
        var msftDisk = SystemDetails.Query($"SELECT Number, PartitionStyle, IsOffline, IsReadOnly, FirmwareVersion FROM MSFT_Disk WHERE Number = {id}", scope).FirstOrDefault();
        var partitions = SystemDetails.Query($"SELECT DiskNumber, PartitionNumber, DriveLetter, Size, GptType, MbrType FROM MSFT_Partition WHERE DiskNumber = {id}", scope)
            .Select(ReadPartition).OfType<DrivePartition>().Select(WithVolume).ToList();

        DriveHealthReport report;
        try { report = DriveHealthCheck.ReadReport(disk.Number); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException) { report = new DriveHealthReport(); }
        var xml = IntegrityCheck.QueryEvents(IntegrityCheck.DriveEventQueryArgs(DriveHealthCheck.EventDays));

        string? firmware = (physical is null ? null : SystemDetails.Clean(SystemDetails.Text(physical, "FirmwareVersion")))
                           ?? (msftDisk is null ? null : SystemDetails.Clean(SystemDetails.Text(msftDisk, "FirmwareVersion")));
        return new DriveDetailsInfo(disk)
        {
            Firmware = firmware,
            Manufacturer = physical is null ? null : SystemDetails.Clean(SystemDetails.Text(physical, "Manufacturer")),
            BusType = physical is null ? null : SystemDetails.Number(physical, "BusType"),
            PartitionStyle = msftDisk is null ? null : SystemDetails.Number(msftDisk, "PartitionStyle"),
            LogicalSectorSize = physical is null ? null : SystemDetails.Number(physical, "LogicalSectorSize"),
            PhysicalSectorSize = physical is null ? null : SystemDetails.Number(physical, "PhysicalSectorSize"),
            SpindleSpeed = physical is null ? null : SystemDetails.Number(physical, "SpindleSpeed"),
            IsOffline = Flag(msftDisk, "IsOffline"),
            IsReadOnly = Flag(msftDisk, "IsReadOnly"),
            Partitions = partitions,
            Report = report,
            Events = xml is null ? null : IntegrityCheck.ParseDriveEvents(xml, disk.Number),
            Administrator = administrator,
        };
    }

    /// <summary>A lettered partition's file system, name and free space (left out when the drive isn't ready).</summary>
    private static DrivePartition WithVolume(DrivePartition p)
    {
        if (p.Letter is null) return p;
        try
        {
            var v = new DriveInfo(p.Letter + "\\");
            return v.IsReady ? p with { FileSystem = v.DriveFormat, Label = v.VolumeLabel, Free = v.AvailableFreeSpace } : p;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return p; }
    }
}
