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
