using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace KodiakCleaner.Core.Tools;

/// <summary>How a drive is erased.</summary>
public enum WipeMethod
{
    /// <summary>One pass of zeros: enough for any modern hard drive (NIST SP 800-88 Clear).</summary>
    Zeros,
    /// <summary>DoD 5220.22-M: zeros, ones, random, then the last pass is read back and checked.</summary>
    Dod3,
    /// <summary>DoD 5220.22-M ECE: the 3-pass pattern, a random pass, then the 3-pass pattern again (7 passes).</summary>
    Dod7,
    /// <summary>
    /// 30 passes: the DoD 3-pass pattern (zeros, ones, random) ten times over. Not a published standard; for people who
    /// want the most passes. No modern drive needs more than one.
    /// </summary>
    Pass30,
    /// <summary>
    /// The drive's own built-in secure erase (crypto or block erase), the only way to reach every flash cell. Shown to users as
    /// "Built-In Secure Erase" (Ted, 0.8.115): it erases the data, not the drive's firmware, and the drive keeps working.
    /// </summary>
    Firmware,
}

/// <summary>One overwrite pass: a fixed byte, or random data (<see cref="Fill"/> == null).</summary>
public sealed record WipePass(byte? Fill)
{
    public bool IsRandom => Fill is null;
    public string Name => Fill switch { null => "Random Data", 0x00 => "Zeros", 0xFF => "Ones", var b => $"0x{b:X2}" };
}

/// <summary>How far an Entire Drive wipe goes.</summary>
public enum WipeLevel
{
    /// <summary>Removes the partition tables (MBR or GPT) and boot sectors only: the drive looks empty, but the data is still on it.</summary>
    Quick,
    /// <summary>Every sector once with zeros (NIST SP 800-88 Clear); SSD and NVMe use their built-in secure erase.</summary>
    Thorough,
    /// <summary>The chosen security method (DoD 3, DoD 7 or 30 passes) with the read-back check; SSD and NVMe use their built-in secure erase.</summary>
    Secure,
}

/// <summary>How the drive is set up after wiping; null everywhere means "leave it uninitialized, like new from the factory".</summary>
public sealed record DiskFormat(string FileSystem, bool Gpt, string Label)
{
    public static readonly string[] FileSystems = ["exFAT", "FAT32", "NTFS"];
    public string PartitionStyle => Gpt ? "GPT" : "MBR";

    /// <summary>
    /// Only letters, digits, spaces, hyphens and underscores, and no longer than the file system allows (<see cref="LabelLimit"/>).
    /// FAT32 keeps names in capitals, so it's shown that way too. Empty means the drive gets no name.
    /// </summary>
    public string SafeLabel
    {
        get
        {
            var safe = new string(Label.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_').Take(LabelLimit(FileSystem)).ToArray()).Trim();
            return FileSystem.Equals("FAT32", StringComparison.OrdinalIgnoreCase) ? safe.ToUpperInvariant() : safe;
        }
    }

    /// <summary>The longest drive name a file system takes: 32 characters on NTFS, 11 on FAT32 and exFAT.</summary>
    public static int LabelLimit(string fileSystem) => fileSystem.Equals("NTFS", StringComparison.OrdinalIgnoreCase) ? 32 : 11;
}

public static class WipeMethods
{
    public static string LevelName(WipeLevel l) => l switch { WipeLevel.Quick => "Quick", WipeLevel.Thorough => "Thorough", _ => "Secure" };

    /// <summary>What actually happens to one drive for a wipe level (null = no overwrite, only the partition data is removed).</summary>
    public static WipeMethod? ForLevel(WipeLevel level, MediaKind kind, WipeMethod secure) => level switch
    {
        WipeLevel.Quick => null,
        WipeLevel.Thorough => Recommended(kind, WipeMethod.Zeros),
        _ => Recommended(kind, secure),
    };

    /// <summary>Why a format can't be used on a drive of this size (null = it can).</summary>
    public static string? FormatProblem(long size, DiskFormat? format)
    {
        const long GB = 1024L * 1024 * 1024;
        if (format is null) return null;
        if (!format.Gpt && size > 2048 * GB) return "MBR can only use the first 2 TB of a drive this size. Choose GPT.";
        if (format.FileSystem.Equals("FAT32", StringComparison.OrdinalIgnoreCase) && size > 32 * GB)
            return "Windows only formats drives up to 32 GB as FAT32. Choose exFAT or NTFS.";
        return null;
    }

    /// <summary>
    /// The diskpart script run after wiping: clean always removes the partition tables (MBR, GPT and its backup at the end of the
    /// drive) and boot sectors, leaving the drive uninitialized; a format then sets the partition style, makes one partition and formats it.
    /// </summary>
    public static List<string> DiskpartScript(int disk, DiskFormat? format)
    {
        // rescan: Windows rereads the disk after the raw erase. A disk left offline or read-only (some USB drives) can't be set up.
        var lines = new List<string> { "rescan", $"select disk {disk}", "attributes disk clear readonly noerr", "online disk noerr", "clean" };
        if (format is null) return lines;
        // A cleaned removable drive is already MBR, and converting it again is an error, so only GPT has to succeed.
        lines.Add(format.Gpt ? "convert gpt" : "convert mbr noerr");
        lines.Add("create partition primary");
        var label = format.SafeLabel;
        lines.Add($"format fs={format.FileSystem.ToLowerInvariant()} quick" + (label.Length > 0 ? $" label=\"{label}\"" : ""));
        lines.Add("assign noerr");
        return lines;
    }

    /// <summary>The line where diskpart says what went wrong (and the one after it, which usually holds the reason).</summary>
    public static string? DiskpartError(string output)
    {
        var lines = output.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        int i = lines.FindLastIndex(l => l.Contains("error", StringComparison.OrdinalIgnoreCase));
        if (i < 0) return null;
        var text = lines[i].TrimEnd(':');
        if (i + 1 < lines.Count && lines[i].EndsWith(':')) text += ": " + lines[i + 1];
        return text.Length > 240 ? text[..240] + "…" : text;
    }

    /// <summary>
    /// The suggested format for a drive (Ted): flash drives MBR with FAT32 up to 32 GB or exFAT above, which TVs, cars and cameras
    /// read; other USB drives GPT with exFAT, so Macs can use them too; SSDs and internal hard drives GPT with NTFS for Windows.
    /// MBR can't use more than 2 TB, so bigger drives are always GPT.
    /// </summary>
    public static DiskFormat Recommended(MediaKind kind, bool removable, long size, string label)
    {
        const long GB = 1024L * 1024 * 1024;
        if (kind == MediaKind.FlashDrive) return new DiskFormat(size <= 32 * GB ? "FAT32" : "exFAT", size > 2048 * GB, label);
        return new DiskFormat(removable ? "exFAT" : "NTFS", true, label);
    }

    /// <summary>Why the suggestion was made, for the line under the format choices.</summary>
    public static string RecommendedWhy(MediaKind kind, bool removable, long size)
    {
        var f = Recommended(kind, removable, size, "");
        var why = kind == MediaKind.FlashDrive
            ? (f.FileSystem == "FAT32" ? "works in almost every device, including old ones" : "works in Windows, Macs, TVs, cars and cameras")
            : removable ? "works in Windows and Macs, and on drives over 2 TB" : "is what Windows uses for its own drives";
        var name = kind switch
        {
            MediaKind.FlashDrive => "flash drive",
            MediaKind.HardDrive => removable ? "USB hard drive" : "hard drive",
            MediaKind.Ssd or MediaKind.NvmeSsd => removable ? "USB SSD" : "SSD",
            _ => removable ? "USB drive" : "drive",
        };
        return $"Recommended for a {name}: {f.PartitionStyle} with {f.FileSystem}, which {why}.";
    }

    /// <summary>
    /// The drive name suggested when a drive is ticked (Ted, 0.8.116; it used to be "Wiped" for every drive): what the drive is, in
    /// 11 characters or fewer so FAT32 and exFAT keep all of it.
    /// </summary>
    public static string DefaultLabel(MediaKind kind, bool removable) => kind switch
    {
        MediaKind.FlashDrive => "Flash Drive",
        MediaKind.HardDrive => removable ? "USB Drive" : "Hard Drive",
        MediaKind.Ssd or MediaKind.NvmeSsd => removable ? "USB SSD" : "SSD",
        _ => removable ? "USB Drive" : "Data Drive",
    };

    /// <summary>The warning under the drive list once two or more drives are ticked (Ted, 0.8.116); null for none or one.</summary>
    public static string? SelectedWarning(int ticked) => ticked < 2 ? null
        : $"{ticked} Drives Selected: all {ticked} will be erased, one after another.";

    public static string Name(WipeMethod m) => m switch
    {
        WipeMethod.Zeros => "1 Pass (Zeros)",
        WipeMethod.Dod3 => "DoD 3 Passes",
        WipeMethod.Dod7 => "DoD 7 Passes",
        WipeMethod.Pass30 => "30 Passes",
        _ => "Built-In Secure Erase",
    };

    public static string Standard(WipeMethod m) => m switch
    {
        WipeMethod.Zeros => "NIST SP 800-88 Clear",
        WipeMethod.Dod3 => "DoD 5220.22-M",
        WipeMethod.Dod7 => "DoD 5220.22-M ECE",
        WipeMethod.Pass30 => "DoD 5220.22-M pattern, 10 times (not a standard)",
        _ => "NIST SP 800-88 Purge",
    };

    private static readonly WipePass Zero = new(0x00), One = new(0xFF), Random = new(null);

    /// <summary>The overwrite passes for a method. A built-in secure erase has none (the drive does the work).</summary>
    public static IReadOnlyList<WipePass> Passes(WipeMethod m) => m switch
    {
        WipeMethod.Zeros => [Zero],
        WipeMethod.Dod3 => [Zero, One, Random],
        WipeMethod.Dod7 => [Zero, One, Random, Random, Zero, One, Random],
        WipeMethod.Pass30 => Enumerable.Repeat(new[] { Zero, One, Random }, 10).SelectMany(p => p).ToList(),
        _ => [],
    };

    /// <summary>
    /// Passes for a plain pass count (free space wiping): 1 = zeros, 3 = DoD, 7 = DoD ECE, anything else zeros then random.
    /// </summary>
    public static IReadOnlyList<WipePass> ForCount(int passes) => passes switch
    {
        1 => Passes(WipeMethod.Zeros),
        3 => Passes(WipeMethod.Dod3),
        7 => Passes(WipeMethod.Dod7),
        30 => Passes(WipeMethod.Pass30),
        _ => Enumerable.Range(0, passes).Select(i => i == 0 ? Zero : Random).ToList(),
    };

    /// <summary>
    /// The method that suits a drive: flash (SSD, NVMe) runs its own built-in secure erase, because overwriting can't reach the spare
    /// cells it keeps hidden; hard drives and flash drives are overwritten with the chosen pattern.
    /// </summary>
    public static WipeMethod Recommended(MediaKind kind, WipeMethod overwrite) =>
        kind is MediaKind.Ssd or MediaKind.NvmeSsd ? WipeMethod.Firmware : overwrite;
}

public sealed record OverwriteProgress(int Pass, int Passes, string PassName, long Done, long Total, bool Verifying);

/// <summary>
/// Writes wipe patterns over a whole device (or, in tests, a file) through a raw handle, sector-aligned, and reads the last
/// pass back to prove it landed. Random passes come from a keyed generator so the check can recreate them without storing them.
/// </summary>
public static unsafe class Overwriter
{
    public const int Chunk = 4 * 1024 * 1024;

    /// <returns>Bytes found different on the read-back (0 = verified, or not checked).</returns>
    public static long Run(SafeFileHandle handle, long length, IReadOnlyList<WipePass> passes, bool verifyLast,
        IProgress<OverwriteProgress>? progress, CancellationToken ct)
    {
        if (length <= 0) throw new IOException("The drive reported no size, so it wasn't wiped.");
        var memory = (byte*)NativeMemory.AlignedAlloc(Chunk, 4096);
        var check = verifyLast ? (byte*)NativeMemory.AlignedAlloc(Chunk, 4096) : null;
        try
        {
            var buffer = new Span<byte>(memory, Chunk);
            ulong key = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8));
            for (int p = 0; p < passes.Count; p++)
            {
                var pass = passes[p];
                ulong passKey = key + (ulong)p * 0x9E3779B97F4A7C15UL;
                if (!pass.IsRandom) buffer.Fill(pass.Fill!.Value);
                for (long at = 0; at < length; at += Chunk)
                {
                    ct.ThrowIfCancellationRequested();
                    int count = (int)Math.Min(Chunk, length - at);
                    if (pass.IsRandom) FillRandom(buffer[..count], passKey, at);
                    RandomAccess.Write(handle, buffer[..count], at);
                    if ((at / Chunk) % 16 == 0) progress?.Report(new OverwriteProgress(p + 1, passes.Count, pass.Name, at, length, false));
                }
                FlushFileBuffers(handle);
                progress?.Report(new OverwriteProgress(p + 1, passes.Count, pass.Name, length, length, false));
            }

            if (!verifyLast || passes.Count == 0) return 0;
            var last = passes[^1];
            ulong lastKey = key + (ulong)(passes.Count - 1) * 0x9E3779B97F4A7C15UL;
            var read = new Span<byte>(check, Chunk);
            long bad = 0;
            if (!last.IsRandom) buffer.Fill(last.Fill!.Value);
            for (long at = 0; at < length; at += Chunk)
            {
                ct.ThrowIfCancellationRequested();
                int count = (int)Math.Min(Chunk, length - at);
                if (last.IsRandom) FillRandom(buffer[..count], lastKey, at);
                int got = RandomAccess.Read(handle, read[..count], at);
                if (got < count || !read[..count].SequenceEqual(buffer[..count]))
                    bad += Differences(read[..got], buffer[..count]);
                if ((at / Chunk) % 16 == 0) progress?.Report(new OverwriteProgress(passes.Count, passes.Count, last.Name, at, length, true));
            }
            progress?.Report(new OverwriteProgress(passes.Count, passes.Count, last.Name, length, length, true));
            return bad;
        }
        finally
        {
            NativeMemory.AlignedFree(memory);
            if (check is not null) NativeMemory.AlignedFree(check);
        }
    }

    private static long Differences(ReadOnlySpan<byte> got, ReadOnlySpan<byte> expected)
    {
        long bad = expected.Length - got.Length;
        for (int i = 0; i < got.Length; i++) if (got[i] != expected[i]) bad++;
        return bad;
    }

    /// <summary>Random-looking data that depends only on (key, offset): xoshiro256** seeded per chunk by SplitMix64.</summary>
    internal static void FillRandom(Span<byte> buffer, ulong key, long offset)
    {
        ulong seed = key ^ (ulong)offset;
        ulong s0 = SplitMix(ref seed), s1 = SplitMix(ref seed), s2 = SplitMix(ref seed), s3 = SplitMix(ref seed);
        var words = MemoryMarshal.Cast<byte, ulong>(buffer);
        for (int i = 0; i < words.Length; i++)
        {
            ulong result = System.Numerics.BitOperations.RotateLeft(s1 * 5, 7) * 9;
            ulong t = s1 << 17;
            s2 ^= s0; s3 ^= s1; s1 ^= s2; s0 ^= s3; s2 ^= t;
            s3 = System.Numerics.BitOperations.RotateLeft(s3, 45);
            words[i] = result;
        }
        for (int i = words.Length * 8; i < buffer.Length; i++) buffer[i] = (byte)SplitMix(ref seed);
    }

    private static ulong SplitMix(ref ulong x)
    {
        ulong z = x += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    private static void FlushFileBuffers(SafeFileHandle handle)
    {
        if (OperatingSystem.IsWindows()) FlushWin(handle); // tests on other systems write a plain file and skip this
    }

    [DllImport("kernel32.dll", EntryPoint = "FlushFileBuffers", SetLastError = true)]
    private static extern bool FlushWin(SafeFileHandle handle);
}
