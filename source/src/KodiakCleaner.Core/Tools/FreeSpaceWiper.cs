using System.Security.Cryptography;

namespace KodiakCleaner.Core.Tools;

public sealed record WipeProgress(int Pass, int Passes, long BytesWritten, long BytesTotal);

/// <summary>
/// Overwrites a drive's free space so deleted files cannot be recovered: fills it with filler files
/// (the DoD 5220.22-M pattern for 3 or 7 passes: zeros, ones, random; one pass is zeros), flushes them to disk, then deletes them.
/// Existing files are never touched.
/// </summary>
public static class FreeSpaceWiper
{
    public const string WorkFolderName = "KodiakCleaner-Wipe.tmp";
    private const int ChunkSize = 4 * 1024 * 1024;
    private const long FillerFileSize = 1024L * 1024 * 1024; // keep individual files under FAT32's 4 GB limit

    // Wipes in progress. Closing Kodiak Cleaner ends the process without running a background wipe's finally,
    // which would leave the drive completely full of filler files, so on exit each one is stopped and cleaned up first.
    private static readonly object Gate = new();
    private static readonly List<(CancellationTokenSource Stop, ManualResetEventSlim Done)> Running = new();

    static FreeSpaceWiper() => AppDomain.CurrentDomain.ProcessExit += (_, _) => StopAll(TimeSpan.FromSeconds(30));

    /// <summary>Cancels every wipe in progress and waits (up to <paramref name="wait"/> each) until its filler files are gone.</summary>
    internal static void StopAll(TimeSpan wait)
    {
        List<(CancellationTokenSource Stop, ManualResetEventSlim Done)> running;
        lock (Gate) running = Running.ToList();
        foreach (var (stop, _) in running)
        {
            try { stop.Cancel(); }
            catch (ObjectDisposedException) { } // it just finished
        }
        foreach (var (_, done) in running) done.Wait(wait);
    }

    /// <param name="root">Drive root (or, for tests, any folder) to fill.</param>
    /// <param name="limitBytes">Stop after this many bytes per pass (tests); null = until the disk is full.</param>
    /// <param name="freeBytes">Function returning current free space, used for progress only.</param>
    public static long Wipe(string root, int passes, Func<long> freeBytes, IProgress<WipeProgress>? progress = null,
        CancellationToken ct = default, long? limitBytes = null)
    {
        if (passes < 1) throw new ArgumentOutOfRangeException(nameof(passes));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        (CancellationTokenSource Stop, ManualResetEventSlim Done) entry = (stop, new ManualResetEventSlim());
        lock (Gate) Running.Add(entry);
        try
        {
            return WipeCore(root, passes, freeBytes, progress, stop.Token, limitBytes);
        }
        finally
        {
            lock (Gate) Running.Remove(entry);
            entry.Done.Set();
        }
    }

    private static long WipeCore(string root, int passes, Func<long> freeBytes, IProgress<WipeProgress>? progress,
        CancellationToken ct, long? limitBytes)
    {
        var work = Path.Combine(root, WorkFolderName);
        long totalWritten = 0;
        // A work folder left over from a crash would make every CreateNew fail; never follow a link planted there.
        if (Directory.Exists(work) && new DirectoryInfo(work).Attributes.HasFlag(FileAttributes.ReparsePoint))
            throw new IOException($"{work} is a link, so free space wasn't wiped.");
        DeleteWorkFolder(work);
        var patterns = WipeMethods.ForCount(passes);
        try
        {
            for (int pass = 1; pass <= passes; pass++)
            {
                Directory.CreateDirectory(work);
                long target = limitBytes ?? Math.Max(freeBytes(), 1);
                long written = 0;
                var buffer = new byte[ChunkSize];
                var pattern = patterns[pass - 1];
                if (!pattern.IsRandom) Array.Fill(buffer, pattern.Fill!.Value);
                int fileIndex = 0;
                bool full = false;

                while (!full && written < target)
                {
                    ct.ThrowIfCancellationRequested();
                    var file = Path.Combine(work, $"fill{fileIndex++:D5}.tmp");
                    try
                    {
                        using var fs = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.WriteThrough);
                        long inFile = 0;
                        while (inFile < FillerFileSize && written < target)
                        {
                            ct.ThrowIfCancellationRequested();
                            if (pattern.IsRandom) RandomNumberGenerator.Fill(buffer);
                            int count = (int)Math.Min(buffer.Length, Math.Min(FillerFileSize - inFile, target - written));
                            fs.Write(buffer, 0, count);
                            inFile += count;
                            written += count;
                            if (written % (64L * 1024 * 1024) < count)
                                progress?.Report(new WipeProgress(pass, passes, written, target));
                        }
                        fs.Flush(flushToDisk: true);
                    }
                    catch (IOException e) when (limitBytes is null && IsDiskFull(e))
                    {
                        full = true; // disk full: this pass is done
                    }
                }
                // Anything other than a full disk (a locked file, a read-only drive) must not be reported as a wipe.
                if (written == 0 && target > 16L * 1024 * 1024)
                    throw new IOException("Nothing could be written to the drive, so free space wasn't wiped.");
                totalWritten += written;
                progress?.Report(new WipeProgress(pass, passes, written, written));
                DeleteWorkFolder(work);
            }
        }
        finally
        {
            DeleteWorkFolder(work); // always give the space back, even on cancel or error
        }
        return totalWritten;
    }

    // ERROR_HANDLE_DISK_FULL (39) and ERROR_DISK_FULL (112).
    private static bool IsDiskFull(IOException e) => (e.HResult & 0xFFFF) is 39 or 112;

    private static void DeleteWorkFolder(string work)
    {
        try { if (Directory.Exists(work)) Directory.Delete(work, recursive: true); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
