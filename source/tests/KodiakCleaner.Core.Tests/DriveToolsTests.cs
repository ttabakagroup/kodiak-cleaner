using KodiakCleaner.Core.Tools;

namespace KodiakCleaner.Core.Tests;

public sealed class DriveMediaTests
{
    [Theory]
    [InlineData(DriveMedia.BusNvme, null, false, MediaKind.NvmeSsd)]
    [InlineData(DriveMedia.BusNvme, true, false, MediaKind.NvmeSsd)] // NVMe is always flash, whatever else it says
    [InlineData(DriveMedia.BusSata, true, false, MediaKind.HardDrive)]
    [InlineData(DriveMedia.BusSata, false, false, MediaKind.Ssd)]
    [InlineData(DriveMedia.BusUsb, true, true, MediaKind.HardDrive)]   // a USB hard drive spins
    [InlineData(DriveMedia.BusUsb, false, false, MediaKind.Ssd)]       // a USB SSD enclosure
    [InlineData(DriveMedia.BusUsb, false, true, MediaKind.FlashDrive)]
    [InlineData(DriveMedia.BusSd, null, true, MediaKind.FlashDrive)]
    [InlineData(DriveMedia.BusSata, null, false, MediaKind.Unknown)]
    public void Classifies_drives_by_bus_and_seek_penalty(int bus, bool? seek, bool removable, MediaKind expected) =>
        Assert.Equal(expected, DriveMedia.Classify(bus, seek, removable));

    [Fact]
    public void Kind_names_are_title_case() =>
        Assert.Equal(new[] { "Unknown", "Hard Drive", "SSD", "NVMe SSD", "Flash Drive" },
            Enum.GetValues<MediaKind>().Select(MediaKindText.Name));
}

public sealed class WipeMethodTests
{
    [Fact]
    public void Dod_patterns_follow_the_standard()
    {
        Assert.Equal(new byte?[] { 0x00 }, WipeMethods.Passes(WipeMethod.Zeros).Select(p => p.Fill));
        Assert.Equal(new byte?[] { 0x00, 0xFF, null }, WipeMethods.Passes(WipeMethod.Dod3).Select(p => p.Fill));
        Assert.Equal(new byte?[] { 0x00, 0xFF, null, null, 0x00, 0xFF, null }, WipeMethods.Passes(WipeMethod.Dod7).Select(p => p.Fill));
        Assert.Empty(WipeMethods.Passes(WipeMethod.Firmware));
        Assert.Equal(new byte?[] { 0x00, null }, WipeMethods.ForCount(2).Select(p => p.Fill));
    }

    [Theory]
    [InlineData(MediaKind.HardDrive, WipeMethod.Dod3, WipeMethod.Dod3)]
    [InlineData(MediaKind.FlashDrive, WipeMethod.Dod7, WipeMethod.Dod7)]
    [InlineData(MediaKind.Unknown, WipeMethod.Zeros, WipeMethod.Zeros)]
    [InlineData(MediaKind.Ssd, WipeMethod.Dod3, WipeMethod.Firmware)]
    [InlineData(MediaKind.NvmeSsd, WipeMethod.Dod7, WipeMethod.Firmware)]
    public void Flash_gets_a_firmware_erase_and_everything_else_the_chosen_passes(MediaKind kind, WipeMethod chosen, WipeMethod expected) =>
        Assert.Equal(expected, WipeMethods.Recommended(kind, chosen));

    /// <summary>
    /// Ted (0.8.115): "Firmware Erase" made people think the drive's firmware is erased and the drive stops working. Users see
    /// "Built-In Secure Erase" instead, and no text Drive Wiper shows calls it a firmware erase.
    /// </summary>
    [Fact]
    public void The_drives_own_erase_is_called_built_in_secure_erase_never_firmware_erase()
    {
        Assert.Equal("Built-In Secure Erase", WipeMethods.Name(WipeMethod.Firmware));
        var files = new[]
        {
            Repo.File("src", "KodiakCleaner.App", "Views", "WiperView.xaml"),
            Repo.File("src", "KodiakCleaner.App", "ViewModels", "ToolsViewModels.cs"),
            Repo.File("src", "KodiakCleaner.App", "WelcomeWindow.xaml.cs"),
            Repo.File("src", "KodiakCleaner.Core", "Tools", "DriveWiper.cs"),
            Repo.File("src", "KodiakCleaner.Core", "Tools", "WipeMethods.cs"),
        };
        foreach (var file in files)
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(file), "\"[^\"\n]*\""))
                Assert.False(System.Text.RegularExpressions.Regex.IsMatch(m.Value, @"firmware (secure )?erase|erased by its firmware|drive's firmware is erasing",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase), $"{Path.GetFileName(file)}: {m.Value}");
    }
}

public sealed class OverwriterTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "kodiakcleaner-overwrite-" + Guid.NewGuid().ToString("N"));
    public void Dispose() => File.Delete(_file);

    private const long Length = 9L * 1024 * 1024 + 4096; // more than two chunks, not a whole number of them

    private Microsoft.Win32.SafeHandles.SafeFileHandle Target()
    {
        File.WriteAllBytes(_file, Enumerable.Repeat((byte)0x5A, (int)Length).ToArray()); // "old data"
        return File.OpenHandle(_file, FileMode.Open, FileAccess.ReadWrite);
    }

    [Fact]
    public void Zeros_overwrite_every_byte()
    {
        using (var h = Target())
            Assert.Equal(0, Overwriter.Run(h, Length, WipeMethods.Passes(WipeMethod.Zeros), verifyLast: true, null, default));
        Assert.All(File.ReadAllBytes(_file), b => Assert.Equal(0, b));
    }

    [Fact]
    public void Dod_passes_end_with_verified_random_data()
    {
        var seen = new List<OverwriteProgress>();
        using (var h = Target())
            Assert.Equal(0, Overwriter.Run(h, Length, WipeMethods.Passes(WipeMethod.Dod3), verifyLast: true, new Sync(seen.Add), default));
        var bytes = File.ReadAllBytes(_file);
        Assert.Equal(Length, bytes.Length);
        Assert.DoesNotContain(bytes.Chunk(4096), c => c.All(b => b == 0x5A) || c.All(b => b == 0) || c.All(b => b == 0xFF));
        Assert.Equal(new[] { 1, 2, 3 }, seen.Where(p => !p.Verifying).Select(p => p.Pass).Distinct());
        Assert.Contains(seen, p => p.Verifying && p.Done == Length);
    }

    [Fact]
    public void Verification_finds_bytes_that_did_not_land()
    {
        // A second handle changes a byte after the pass is written, as a failing drive would.
        var corrupt = new Sync(p =>
        {
            if (p is { Verifying: true, Done: 0 })
                using (var other = File.OpenHandle(_file, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
                    RandomAccess.Write(other, new byte[] { 0x01 }, 5 * 1024 * 1024 + 7);
        });
        using var h = File.OpenHandle(_file, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
        RandomAccess.Write(h, new byte[Length], 0);
        Assert.Equal(1, Overwriter.Run(h, Length, WipeMethods.Passes(WipeMethod.Zeros), verifyLast: true, corrupt, default));
    }

    [Fact]
    public void Random_data_depends_only_on_key_and_offset()
    {
        var a = new byte[1000];
        var b = new byte[1000];
        Overwriter.FillRandom(a, 42, 4096);
        Overwriter.FillRandom(b, 42, 4096);
        Assert.Equal(a, b);
        Overwriter.FillRandom(b, 42, 8192);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Stop_ends_the_wipe()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        using var h = Target();
        Assert.Throws<OperationCanceledException>(() => Overwriter.Run(h, Length, WipeMethods.Passes(WipeMethod.Dod3), true, null, cts.Token));
    }

    private sealed class Sync(Action<OverwriteProgress> report) : IProgress<OverwriteProgress>
    {
        public void Report(OverwriteProgress value) => report(value);
    }
}

public sealed class DriveOptimizerTests
{
    [Theory]
    [InlineData(MediaKind.HardDrive, "NTFS", OptimizeAction.Defragment)]
    [InlineData(MediaKind.HardDrive, "FAT32", OptimizeAction.Defragment)]
    [InlineData(MediaKind.HardDrive, "ReFS", OptimizeAction.WindowsDecides)]
    [InlineData(MediaKind.Ssd, "NTFS", OptimizeAction.Trim)]
    [InlineData(MediaKind.NvmeSsd, "NTFS", OptimizeAction.Trim)]
    [InlineData(MediaKind.FlashDrive, "exFAT", OptimizeAction.NotNeeded)]
    [InlineData(MediaKind.Unknown, "NTFS", OptimizeAction.WindowsDecides)]
    public void Picks_defrag_or_trim_from_the_drive_type(MediaKind kind, string fs, OptimizeAction expected) =>
        Assert.Equal(expected, DriveOptimizer.Decide(kind, fs));

    [Fact]
    public void Uses_windows_optimizer_switches()
    {
        Assert.Equal("D: /D /U /V /H", DriveOptimizer.Arguments("D", OptimizeAction.Defragment));
        Assert.Equal("C: /L /U /V /H", DriveOptimizer.Arguments("C", OptimizeAction.Trim));
        Assert.Equal("E: /O /U /V /H", DriveOptimizer.Arguments("E", OptimizeAction.WindowsDecides));
    }

    [Fact]
    public void Reads_progress_and_fragmentation_from_its_output()
    {
        Assert.Equal(45, DriveOptimizer.PercentOf("Defragmentation:  45% complete..."));
        Assert.Equal(100, DriveOptimizer.PercentOf("Retrim:  100% complete."));
        Assert.Null(DriveOptimizer.PercentOf("Invoking retrim on Data (D:)..."));
        Assert.Equal(3, DriveOptimizer.FragmentedOf("\tTotal fragmented space\t= 3%"));
        Assert.Null(DriveOptimizer.FragmentedOf("\tFree space\t= 120 GB (60%)"));
    }
}

public sealed class WipeLevelAndFormatTests
{
    private const long GB = 1024L * 1024 * 1024;

    [Fact]
    public void Thirty_passes_repeat_the_dod_pattern_ten_times()
    {
        var passes = WipeMethods.Passes(WipeMethod.Pass30);
        Assert.Equal(30, passes.Count);
        Assert.All(passes.Chunk(3), c => Assert.Equal(new byte?[] { 0x00, 0xFF, null }, c.Select(p => p.Fill)));
        Assert.Equal(30, WipeMethods.ForCount(30).Count);
        Assert.Contains("not a standard", WipeMethods.Standard(WipeMethod.Pass30));
    }

    [Theory]
    [InlineData(WipeLevel.Quick, MediaKind.HardDrive, null)]
    [InlineData(WipeLevel.Quick, MediaKind.NvmeSsd, null)]
    [InlineData(WipeLevel.Thorough, MediaKind.HardDrive, WipeMethod.Zeros)]
    [InlineData(WipeLevel.Thorough, MediaKind.Ssd, WipeMethod.Firmware)]
    [InlineData(WipeLevel.Secure, MediaKind.FlashDrive, WipeMethod.Pass30)]
    [InlineData(WipeLevel.Secure, MediaKind.NvmeSsd, WipeMethod.Firmware)]
    public void Each_level_picks_the_method_for_the_drive(WipeLevel level, MediaKind kind, WipeMethod? expected) =>
        Assert.Equal(expected, WipeMethods.ForLevel(level, kind, WipeMethod.Pass30));

    [Fact]
    public void Leaving_it_uninitialized_only_cleans()
    {
        Assert.Equal(new[] { "rescan", "select disk 3", "attributes disk clear readonly noerr", "online disk noerr", "clean" }, WipeMethods.DiskpartScript(3, null));
    }

    [Fact]
    public void A_format_sets_style_file_system_and_a_safe_label()
    {
        Assert.Equal(new[] { "rescan", "select disk 2", "attributes disk clear readonly noerr", "online disk noerr", "clean", "convert gpt", "create partition primary", "format fs=ntfs quick label=\"My Drive\"", "assign noerr" },
            WipeMethods.DiskpartScript(2, new DiskFormat("NTFS", true, "My \"Drive\"")));
        // A cleaned USB drive is already MBR, so converting it to MBR may say no without stopping the setup.
        Assert.Equal(new[] { "rescan", "select disk 1", "attributes disk clear readonly noerr", "online disk noerr", "clean", "convert mbr noerr", "create partition primary", "format fs=fat32 quick label=\"BACKUPS2026\"", "assign noerr" },
            WipeMethods.DiskpartScript(1, new DiskFormat("FAT32", false, "BACKUPS2026-OLD")));
        Assert.Equal("format fs=exfat quick", WipeMethods.DiskpartScript(1, new DiskFormat("exFAT", true, ""))[7]);
    }

    [Theory]
    [InlineData(MediaKind.FlashDrive, true, 16, "FAT32", false)]
    [InlineData(MediaKind.FlashDrive, true, 128, "exFAT", false)]
    [InlineData(MediaKind.FlashDrive, true, 4000, "exFAT", true)]
    [InlineData(MediaKind.Ssd, false, 500, "NTFS", true)]
    [InlineData(MediaKind.NvmeSsd, false, 2000, "NTFS", true)]
    [InlineData(MediaKind.HardDrive, false, 8000, "NTFS", true)]
    [InlineData(MediaKind.HardDrive, true, 5000, "exFAT", true)]
    [InlineData(MediaKind.Unknown, false, 100, "NTFS", true)]
    public void Each_kind_of_drive_gets_a_suggested_format(MediaKind kind, bool removable, long gb, string fs, bool gpt)
    {
        var f = WipeMethods.Recommended(kind, removable, gb * GB, WipeMethods.DefaultLabel(kind, removable));
        Assert.Equal((fs, gpt), (f.FileSystem, f.Gpt));
        Assert.Null(WipeMethods.FormatProblem(gb * GB, f));
        Assert.StartsWith("Recommended for a", WipeMethods.RecommendedWhy(kind, removable, gb * GB));
    }

    [Fact]
    public void Diskpart_errors_are_picked_out_of_its_output()
    {
        Assert.Equal("Virtual Disk Service error: The specified disk is not convertible.",
            WipeMethods.DiskpartError("DiskPart succeeded in cleaning the disk.\r\n\r\nVirtual Disk Service error:\r\nThe specified disk is not convertible.\r\n"));
        Assert.Equal("DiskPart has encountered an error: The media is write protected.",
            WipeMethods.DiskpartError("DiskPart has encountered an error: The media is write protected.\r\nSee the System Event Log for more information.\r\n"));
        Assert.Null(WipeMethods.DiskpartError("DiskPart successfully formatted the volume.\r\n"));
    }

    [Fact]
    public void Formats_that_cannot_fit_the_drive_are_refused()
    {
        Assert.Null(WipeMethods.FormatProblem(4000 * GB, null));
        Assert.Null(WipeMethods.FormatProblem(4000 * GB, new DiskFormat("NTFS", true, "")));
        Assert.Contains("GPT", WipeMethods.FormatProblem(4000 * GB, new DiskFormat("NTFS", false, "")));
        Assert.Contains("32 GB", WipeMethods.FormatProblem(64 * GB, new DiskFormat("FAT32", false, "")));
        Assert.Null(WipeMethods.FormatProblem(16 * GB, new DiskFormat("FAT32", false, "")));
    }
}
