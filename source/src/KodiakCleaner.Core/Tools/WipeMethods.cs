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
    /// <summary>The drive's own firmware erase (NVMe crypto or block erase), the only way to reach every flash cell.</summary>
    Firmware,
}

/// <summary>One overwrite pass: a fixed byte, or random data (<see cref="Fill"/> == null).</summary>
public sealed record WipePass(byte? Fill)
{
    public bool IsRandom => Fill is null;
    public string Name => Fill switch { null => "Random Data", 0x00 => "Zeros", 0xFF => "Ones", var b => $"0x{b:X2}" };
}

public static class WipeMethods
{
    public static string Name(WipeMethod m) => m switch
    {
        WipeMethod.Zeros => "1 Pass (Zeros)",
        WipeMethod.Dod3 => "DoD 3 Passes",
        WipeMethod.Dod7 => "DoD 7 Passes",
        _ => "Firmware Secure Erase",
    };

    public static string Standard(WipeMethod m) => m switch
    {
        WipeMethod.Zeros => "NIST SP 800-88 Clear",
        WipeMethod.Dod3 => "DoD 5220.22-M",
        WipeMethod.Dod7 => "DoD 5220.22-M ECE",
        _ => "NIST SP 800-88 Purge",
    };

    private static readonly WipePass Zero = new(0x00), One = new(0xFF), Random = new(null);

    /// <summary>The overwrite passes for a method. Firmware erase has none (the drive does the work).</summary>
    public static IReadOnlyList<WipePass> Passes(WipeMethod m) => m switch
    {
        WipeMethod.Zeros => [Zero],
        WipeMethod.Dod3 => [Zero, One, Random],
        WipeMethod.Dod7 => [Zero, One, Random, Random, Zero, One, Random],
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
        _ => Enumerable.Range(0, passes).Select(i => i == 0 ? Zero : Random).ToList(),
    };

    /// <summary>
    /// The method that suits a drive: flash (SSD, NVMe) is erased by its firmware, because overwriting can't reach the spare
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
