using KodiakCleaner.Core.Tools;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.Core.Tests;

/// <summary>Drive Wiper, Ted's 0.8.116 round: drive names, defaults, the several-drives warning, row clicks and Drive Details.</summary>
public sealed class DriveNameTests
{
    [Theory]
    [InlineData(MediaKind.FlashDrive, true, "Flash Drive")]
    [InlineData(MediaKind.HardDrive, false, "Hard Drive")]
    [InlineData(MediaKind.HardDrive, true, "USB Drive")]
    [InlineData(MediaKind.Ssd, false, "SSD")]
    [InlineData(MediaKind.NvmeSsd, false, "SSD")]
    [InlineData(MediaKind.Ssd, true, "USB SSD")]
    [InlineData(MediaKind.Unknown, true, "USB Drive")]
    [InlineData(MediaKind.Unknown, false, "Data Drive")]
    public void Each_drive_type_gets_a_name_that_fits_every_file_system(MediaKind kind, bool removable, string expected)
    {
        var name = WipeMethods.DefaultLabel(kind, removable);
        Assert.Equal(expected, name);
        foreach (var fs in DiskFormat.FileSystems)
            Assert.Equal(name, new DiskFormat(fs, true, name).SafeLabel, StringComparer.OrdinalIgnoreCase); // nothing cut off
        Assert.NotEqual("Wiped", name);
    }

    [Fact]
    public void Names_keep_to_each_file_systems_limit_and_fat32_uses_capitals()
    {
        Assert.Equal(32, DiskFormat.LabelLimit("NTFS"));
        Assert.Equal(11, DiskFormat.LabelLimit("exFAT"));
        Assert.Equal(11, DiskFormat.LabelLimit("FAT32"));
        Assert.Equal("Holiday Pho", new DiskFormat("exFAT", true, "Holiday Photos 2026").SafeLabel);
        Assert.Equal("HOLIDAY PHO", new DiskFormat("FAT32", false, "Holiday Photos 2026").SafeLabel);
        Assert.Equal("Holiday Photos 2026", new DiskFormat("NTFS", true, "Holiday Photos 2026").SafeLabel);
        Assert.Equal("FLASH DRIVE", new DiskFormat("FAT32", false, "Flash Drive").SafeLabel);
    }

    [Fact]
    public void An_unnamed_drive_is_formatted_without_a_label()
    {
        foreach (var fs in DiskFormat.FileSystems)
        {
            var script = WipeMethods.DiskpartScript(4, new DiskFormat(fs, true, ""));
            var format = Assert.Single(script, l => l.StartsWith("format ", StringComparison.Ordinal));
            Assert.Equal($"format fs={fs.ToLowerInvariant()} quick", format);
            Assert.DoesNotContain(script, l => l.Contains("label", StringComparison.OrdinalIgnoreCase));
        }
        // Only characters a name can't have: the same as no name.
        Assert.Equal("format fs=ntfs quick", WipeMethods.DiskpartScript(4, new DiskFormat("NTFS", true, "\"*?"))[7]);
        Assert.Equal("format fs=exfat quick label=\"USB SSD\"", WipeMethods.DiskpartScript(4, new DiskFormat("exFAT", true, "USB SSD"))[7]);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(1, null)]
    [InlineData(2, "2 Drives Selected: all 2 will be erased, one after another.")]
    [InlineData(5, "5 Drives Selected: all 5 will be erased, one after another.")]
    public void Ticking_several_drives_shows_a_warning(int ticked, string? expected) =>
        Assert.Equal(expected, WipeMethods.SelectedWarning(ticked));

    private static string Vm() => Repo.Read("src", "KodiakCleaner.App", "ViewModels", "ToolsViewModels.cs");
    private static string View() => Repo.Read("src", "KodiakCleaner.App", "Views", "WiperView.xaml");

    [Fact]
    public void Entire_drive_starts_quick_formatted_and_unchecked_with_a_named_drive()
    {
        var vm = Vm();
        Assert.Contains("private WipeLevel _level = WipeLevel.Quick;", vm);
        Assert.Contains("private bool _leaveUninitialized;", vm);
        Assert.Matches(@"private bool _checkAfterwards;", vm);
        Assert.Contains("private bool _nameDrive = true;", vm);
        Assert.DoesNotContain("_label = \"Wiped\"", vm);
        // Unticking Name the Drive formats without a name; the box shows blank.
        Assert.Contains("new DiskFormat(FileSystem, Gpt, NameDrive ? Label : \"\")", vm);
        Assert.Contains("get => NameDrive ? Label : \"\";", vm);
    }

    [Fact]
    public void The_drive_list_scrolls_the_page_ticks_rows_and_opens_details()
    {
        var view = View();
        Assert.Contains("PreviewMouseWheel=\"DiskGrid_PreviewMouseWheel\"", view);
        Assert.Contains("PreviewMouseLeftButtonUp=\"DiskGrid_PreviewMouseLeftButtonUp\"", view);
        Assert.Contains("AutomationProperties.Name=\"Drive Details\"", view);
        Assert.Contains("{Binding SelectedWarning}", view);
        Assert.Contains("AutomationProperties.Name=\"Name the Drive\"", view);
        var code = Repo.Read("src", "KodiakCleaner.App", "Views", "WiperView.xaml.cs");
        Assert.Contains("SettingsArea.RaiseEvent", code);
        // The tick box and the details button keep their own clicks, so a row is never ticked twice.
        Assert.Contains("Up<ButtonBase>(source) is not null", code);
    }
}

public sealed class DriveDetailsTests
{
    private const long GB = 1024L * 1024 * 1024;

    private static WipeDisk Disk(string serial = "  WD-123456  ", string? reason = null) =>
        new(2, "WD My Passport", serial, 1000 * GB, MediaKind.HardDrive, true, ["E:\\"], reason);

    [Theory]
    [InlineData(0L, "None (Uninitialized)")]
    [InlineData(1L, "MBR")]
    [InlineData(2L, "GPT")]
    [InlineData(null, "Not Shared")]
    public void Partition_styles_are_named(long? style, string expected) => Assert.Equal(expected, DriveDetails.PartitionStyleName(style));

    [Theory]
    [InlineData(7L, "USB")]
    [InlineData(11L, "SATA")]
    [InlineData(17L, "NVMe")]
    [InlineData(12L, "SD Card")]
    [InlineData(99L, "Not Shared")]
    public void Connections_are_named(long bus, string expected) => Assert.Equal(expected, DriveDetails.BusName(bus));

    [Theory]
    [InlineData("{c12a7328-f81f-11d2-ba4b-00a0c93ec93b}", null, "EFI System")]
    [InlineData("{EBD0A0A2-B9E5-4433-87C0-68B6B72699C7}", null, "Basic Data")]
    [InlineData("{de94bba4-06d1-4d40-a16a-bfd50179d6ac}", null, "Recovery")]
    [InlineData("{00000000-1111-2222-3333-444444444444}", null, "Other")]
    [InlineData(null, 7L, "Basic Data")]
    [InlineData(null, 12L, "FAT32")]
    [InlineData(null, 0x27L, "Recovery")]
    public void Partition_kinds_come_from_their_type(string? gpt, long? mbr, string expected) =>
        Assert.Equal(expected, DriveDetails.PartitionKind(gpt, mbr));

    [Fact]
    public void Partitions_are_read_from_their_rows_with_or_without_a_letter()
    {
        var withLetter = DriveDetails.ReadPartition(new Dictionary<string, object?>
        {
            ["PartitionNumber"] = 2u, ["DriveLetter"] = 'e', ["Size"] = 500UL * (ulong)GB, ["GptType"] = "{ebd0a0a2-b9e5-4433-87c0-68b6b72699c7}", ["MbrType"] = null,
        });
        Assert.Equal(new DrivePartition(2, 500 * GB, "E:", "Basic Data"), withLetter);
        var noLetter = DriveDetails.ReadPartition(new Dictionary<string, object?>
        {
            ["PartitionNumber"] = (ushort)1, ["DriveLetter"] = '\0', ["Size"] = 104857600L, ["GptType"] = "", ["MbrType"] = (ushort)0x27,
        });
        Assert.Equal(new DrivePartition(1, 104857600, null, "Recovery"), noLetter);
        Assert.Equal("F:", DriveDetails.ReadPartition(new Dictionary<string, object?> { ["PartitionNumber"] = 1, ["DriveLetter"] = (ushort)'F' })?.Letter);
        Assert.Null(DriveDetails.ReadPartition(new Dictionary<string, object?> { ["DriveLetter"] = 'C' }));
    }

    [Fact]
    public void A_partition_reads_as_one_line()
    {
        var p = new DrivePartition(2, 500 * GB, "E:", "Basic Data") { FileSystem = "NTFS", Label = "Backups", Free = 120 * GB };
        Assert.Equal($"E: · Basic Data · NTFS · Named \"Backups\" · {ByteSize.Format(500 * GB)} ({ByteSize.Format(120 * GB)} free)", DriveDetails.PartitionText(p));
        Assert.Equal($"Recovery · {ByteSize.Format(100 * 1024 * 1024)}", DriveDetails.PartitionText(new DrivePartition(1, 100 * 1024 * 1024, null, "Recovery")));
    }

    [Fact]
    public void The_summary_lists_the_drive_its_layout_health_and_logged_errors()
    {
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        var info = new DriveDetailsInfo(Disk())
        {
            Firmware = "01.01A01", BusType = 7, PartitionStyle = 2, LogicalSectorSize = 512, PhysicalSectorSize = 4096, SpindleSpeed = 5400,
            IsOffline = false, IsReadOnly = false, Administrator = true,
            Partitions = [new DrivePartition(1, 1000 * GB, "E:", "Basic Data") { FileSystem = "exFAT", Free = 900 * GB }],
            Report = new DriveHealthReport
            {
                HealthStatus = 0, OperationalStatus = [2], TemperatureC = 38, TemperatureMaxC = 51, PowerOnHours = 12345,
                ReadErrorsUncorrected = 0, ReadErrorsCorrected = 12, WriteErrorsUncorrected = 0, Reallocated = 8, Pending = 0, PredictsFailure = false,
            },
            Events = [new DriveEventGroup("Disk", 153, 3, new DateTime(2026, 10, 20, 9, 30, 0), "Windows had to retry a read or write on a drive.", "")],
        };
        var sections = DriveDetails.Sections(info);
        Assert.Equal(new[] { "Summary", "Drive", "Layout", "Health", "Disk Errors Windows Logged (Last 30 Days)" }, sections.Select(s => s.Title));
        string Value(string section, string name) => sections.Single(s => s.Title == section).Lines.Single(l => l.Name == name).Value;

        Assert.Equal("Warning", Value("Summary", "Verdict")); // 8 replaced sectors and the logged retries
        Assert.Equal("WD-123456", Value("Drive", "Serial Number"));
        Assert.Equal("Hard Drive", Value("Drive", "Type"));
        Assert.Equal("USB", Value("Drive", "Connection"));
        Assert.Equal("01.01A01", Value("Drive", "Firmware Version"));
        Assert.EndsWith(" (1,073,741,824,000 bytes)", Value("Drive", "Size"));
        Assert.Equal("512 bytes (4,096 bytes physical)", Value("Drive", "Sector Size"));
        Assert.Equal("5,400 RPM", Value("Drive", "Spindle Speed"));
        Assert.Equal("GPT", Value("Layout", "Partition Style"));
        Assert.Equal("Yes", Value("Layout", "Can Be Wiped"));
        Assert.StartsWith("E: · Basic Data · exFAT", Value("Layout", "Partition 1"));
        Assert.Equal("Healthy", Value("Health", "Health"));
        Assert.Equal("OK", Value("Health", "Status"));
        Assert.Equal("No", Value("Health", "Predicts a Failure"));
        Assert.Equal("38 °C (highest 51 °C)", Value("Health", "Temperature"));
        Assert.Equal("0 uncorrected, 12 corrected", Value("Health", "Read Errors"));
        Assert.Equal("0 uncorrected", Value("Health", "Write Errors"));
        Assert.Equal("8", Value("Health", "Replaced Sectors"));
        Assert.StartsWith("3× (last ", Value("Disk Errors Windows Logged (Last 30 Days)", "Disk 153"));
        Assert.Null(sections.Single(s => s.Title == "Health").Note);

        var text = DriveDetails.Text(info, sections);
        Assert.StartsWith("Drive Details: Disk 2: WD My Passport\r\n", text);
        Assert.Contains("\r\nDrive\r\n  Model: WD My Passport\r\n  Serial Number: WD-123456\r\n", text);
        Assert.Contains("  Partition Style: GPT\r\n", text);
    }

    [Fact]
    public void Missing_values_say_so_instead_of_being_guessed()
    {
        var info = new DriveDetailsInfo(Disk(serial: " ", reason: "Windows is installed on this drive.")) { Events = null };
        var sections = DriveDetails.Sections(info);
        string Value(string section, string name) => sections.Single(s => s.Title == section).Lines.Single(l => l.Name == name).Value;
        Assert.Equal("Not Shared", Value("Drive", "Serial Number"));
        Assert.Equal("Not Shared", Value("Drive", "Firmware Version"));
        Assert.Equal("Not Shared", Value("Layout", "Partitions"));
        Assert.Equal("No: Windows is installed on this drive.", Value("Layout", "Can Be Wiped"));
        Assert.Equal("Couldn't Check", Value("Summary", "Verdict"));
        var health = sections.Single(s => s.Title == "Health");
        Assert.Equal(new[] { "Health" }, health.Lines.Select(l => l.Name));
        Assert.Contains("doesn't share its own health report", health.Note);
        var logged = sections.Single(s => s.Title.StartsWith("Disk Errors", StringComparison.Ordinal));
        Assert.Empty(logged.Lines);
        Assert.Equal("The System log couldn't be read.", logged.Note);

        // Shared, but without administrator rights the counts may be missing; an empty log says None.
        var limited = DriveDetails.Sections(new DriveDetailsInfo(Disk()) { Report = new DriveHealthReport { HealthStatus = 0 }, Events = [] });
        Assert.Contains("administrator", limited.Single(s => s.Title == "Health").Note);
        Assert.Equal("None", limited.Single(s => s.Title.StartsWith("Disk Errors", StringComparison.Ordinal)).Lines.Single().Value);
        Assert.Equal("Healthy", limited.Single(s => s.Title == "Summary").Lines.Single(l => l.Name == "Verdict").Value);
    }

    [Fact]
    public void Drive_details_only_ask_windows_never_write()
    {
        var source = Repo.Read("src", "KodiakCleaner.Core", "Tools", "DriveDetails.cs");
        Assert.DoesNotContain("GenericWrite", source);
        Assert.DoesNotContain("PhysicalDrive", source);
        Assert.DoesNotContain("diskpart", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SELECT ", source);
    }
}
