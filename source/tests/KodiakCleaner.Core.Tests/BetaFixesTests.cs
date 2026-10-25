using KodiakCleaner.Core.Tools;

namespace KodiakCleaner.Core.Tests;

/// <summary>
/// Drive protection fails closed (pre-Beta review, 2026-10-25): every lookup is handed in, so no real disk is ever opened here.
/// </summary>
public sealed class DiskGuardTests
{
    private static DiskMedia Disk(int n, string model = "Test Disk", bool usb = false) =>
        new(n, model, $"SN{n}", 64L * 1024 * 1024 * 1024, usb ? DriveMedia.BusUsb : DriveMedia.BusSata, false, true, usb);

    private static VolumeOnDisk Volume(string letter, params int[] disks) => new($@"\\?\Volume{{{letter}}}\", [$@"{letter}:\"], disks);

    private static readonly KeptDrive Windows = new(@"C:\", "Windows", "Windows is installed on this drive.");
    private static readonly KeptDrive App = new(@"D:\", "Kodiak Cleaner", "Kodiak Cleaner is running from this drive.");
    private static readonly KeptDrive PageFile = new(@"D:\", "Windows' page file", "Windows uses this drive for its page file.");

    private static IReadOnlyList<int> NoAnswer(string root) => [];
    private static string? Nothing(string root) => null;

    [Fact]
    public void A_disk_holding_nothing_kept_is_ready_and_the_windows_disk_is_refused()
    {
        var list = DiskGuard.Judge([Disk(0), Disk(1, usb: true)], [Volume("C", 0), Volume("E", 1)], 0, [Windows], NoAnswer, Nothing);
        Assert.Equal("Windows starts from this drive.", list[0].Reason);
        Assert.True(list[1].CanWipe);
        Assert.Equal(["E:\\"], list[1].Letters);
    }

    [Fact]
    public void A_windows_drive_that_cant_be_placed_refuses_every_disk()
    {
        // Under Wine C: was on no disk and Disk 0 was listed as Ready: now nothing can be erased.
        var list = DiskGuard.Judge([Disk(0), Disk(1, usb: true)], [Volume("C"), Volume("E", 1)], 0, [Windows], NoAnswer, Nothing);
        Assert.All(list, d => Assert.False(d.CanWipe));
        Assert.Equal("Kodiak Cleaner couldn't tell which disk holds Windows, so no drive can be erased.", list[1].Reason);
    }

    [Fact]
    public void An_unknown_startup_disk_refuses_every_disk()
    {
        var list = DiskGuard.Judge([Disk(0), Disk(1, usb: true)], [Volume("C", 0), Volume("E", 1)], null, [Windows], NoAnswer, Nothing);
        Assert.All(list, d => Assert.False(d.CanWipe));
        Assert.Equal(DiskGuard.StartupUnknown, list[1].Reason);
    }

    [Theory]
    [InlineData("Kodiak Cleaner")]
    [InlineData("Windows' page file")]
    [InlineData("your user folder")]
    public void Any_kept_drive_that_cant_be_placed_refuses_every_disk(string holds)
    {
        var kept = new[] { Windows, new KeptDrive(@"D:\", holds, "Kept.") };
        var list = DiskGuard.Judge([Disk(0), Disk(1, usb: true)], [Volume("C", 0), Volume("E", 1)], 0, kept, NoAnswer, Nothing);
        Assert.All(list, d => Assert.False(d.CanWipe));
        Assert.Equal(DiskGuard.CouldNotPlace(holds), list[1].Reason);
    }

    [Fact]
    public void A_folder_windows_doesnt_place_at_all_refuses_every_disk()
    {
        var list = DiskGuard.Judge([Disk(0), Disk(1, usb: true)], [Volume("C", 0), Volume("E", 1)], 0, [Windows, new KeptDrive("", "your user folder", "Kept.")], NoAnswer, Nothing);
        Assert.Equal(DiskGuard.CouldNotPlace("your user folder"), list[1].Reason);
    }

    [Fact]
    public void A_kept_drive_found_by_asking_windows_directly_refuses_only_its_disk()
    {
        // D: (Kodiak Cleaner and the page file) isn't in the volume list, but Windows places it on disk 2 when asked.
        IReadOnlyList<int> Ask(string root) => root == @"D:\" ? [2] : [];
        var list = DiskGuard.Judge([Disk(0), Disk(1, usb: true), Disk(2)], [Volume("C", 0), Volume("E", 1)], 0, [Windows, App, PageFile], Ask, Nothing);
        Assert.False(list[0].CanWipe);
        Assert.True(list[1].CanWipe);
        Assert.Equal("Kodiak Cleaner is running from this drive.", list[2].Reason);
    }

    [Fact]
    public void A_lookup_that_throws_counts_as_not_placed()
    {
        IReadOnlyList<int> Broken(string root) => throw new IOException("The device is not ready.");
        var list = DiskGuard.Judge([Disk(1, usb: true)], [Volume("C"), Volume("E", 1)], 0, [Windows], Broken, Nothing);
        Assert.Equal(DiskGuard.CouldNotPlace("Windows"), Assert.Single(list).Reason);
    }

    [Fact]
    public void Letter_reasons_spanning_volumes_and_unknown_sizes_still_refuse()
    {
        string? Protect(string root) => root == @"F:\" ? "Programs are installed on this drive." : null;
        var disks = new[] { Disk(0), Disk(1), Disk(2), Disk(3) with { Size = 0 } };
        var list = DiskGuard.Judge(disks, [Volume("C", 0), Volume("F", 1), Volume("G", 2, 4)], 0, [Windows], NoAnswer, Protect);
        Assert.Equal("Programs are installed on this drive.", list[1].Reason);
        Assert.Equal("A drive on this disk spans another disk.", list[2].Reason);
        Assert.Equal("Windows doesn't report this disk's size.", list[3].Reason);
    }

    [Fact]
    public void A_disk_without_a_model_is_called_unnamed()
    {
        Assert.Equal("Unnamed Drive", DriveMedia.ModelText(""));
        Assert.Equal("Unnamed Drive", DriveMedia.ModelText("   "));
        Assert.Equal("Samsung SSD 990", DriveMedia.ModelText("Samsung SSD 990"));
        var disk = new WipeDisk(0, DriveMedia.ModelText(""), "", 1, MediaKind.Unknown, false, [], null);
        Assert.Equal("Disk 0: Unnamed Drive", disk.Name);
    }
}

public sealed class PartialWipeTextTests
{
    [Fact]
    public void A_wipe_that_stopped_partway_never_says_not_erased()
    {
        foreach (var wrote in new[] { true, false })
            foreach (var there in new[] { true, false })
                foreach (var setUp in new[] { true, false })
                {
                    var text = WipeMethods.PartialWipeText(wrote, there, setUp);
                    Assert.DoesNotContain("wasn't erased", text);
                    Assert.Contains("Wipe it again to finish", text, StringComparison.OrdinalIgnoreCase);
                }
        Assert.StartsWith("The wipe stopped partway: part of the drive was overwritten and its partitions are gone, so its old files may be partly unreadable.",
            WipeMethods.PartialWipeText(true, true, true));
        Assert.Contains("unplugged", WipeMethods.PartialWipeText(true, false, false));
        Assert.Contains("Disk Management", WipeMethods.PartialWipeText(true, true, false));
    }
}

public sealed class DriveLabelTests
{
    [Theory]
    [InlineData("Café Photos", "NTFS", "Caf Photos")]
    [InlineData("Über-Backup_2", "NTFS", "ber-Backup_2")]
    [InlineData("Daten Ölfeld", "exFAT", "Daten lfeld")]
    [InlineData("диск", "NTFS", "")]
    [InlineData("Ｆｕｌｌ", "NTFS", "")] // full-width letters are letters, but not A to Z
    [InlineData("Backups 2026", "FAT32", "BACKUPS 202")]
    public void Drive_names_keep_to_characters_diskpart_reads_the_same_in_every_code_page(string typed, string fs, string expected)
    {
        var label = new DiskFormat(fs, true, typed).SafeLabel;
        Assert.Equal(expected, label);
        Assert.All(label, c => Assert.True(c < 128 && DiskFormat.IsLabelChar(c)));
    }

    [Fact]
    public void The_diskpart_script_is_plain_ascii()
    {
        foreach (var line in WipeMethods.DiskpartScript(1, new DiskFormat("NTFS", true, "Grüße Ärger")))
            Assert.All(line, c => Assert.True(c < 128));
    }
}
