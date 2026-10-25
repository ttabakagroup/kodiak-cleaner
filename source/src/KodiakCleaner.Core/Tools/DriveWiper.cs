using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.Core.Tools;

public sealed record WipeableDrive(string Root, string Label, string Format, long Size, long Free, DriveType Type, bool CanWipeEntireDrive, string? Reason)
{
    public string Letter => Root[..1].ToUpperInvariant();
    public string Display => $"{Letter}:  {(string.IsNullOrEmpty(Label) ? "Local Disk" : Label)}";
    public MediaKind Kind { get; init; }
    public string KindName => MediaKindText.Name(Kind);
    /// <summary>The drive's size for the Free Space Only picker, e.g. "(14.4 GB)" (Ted, 0.8.117).</summary>
    public string SizeText => $"({ByteSize.Format(Size)})";
}

/// <summary>A whole physical disk that can be erased, or the reason it can't (Windows, Kodiak Cleaner or programs live on it).</summary>
public sealed record WipeDisk(int Number, string Model, string Serial, long Size, MediaKind Kind, bool Removable, IReadOnlyList<string> Letters, string? Reason)
{
    public bool CanWipe => Reason is null;
    public string KindName => MediaKindText.Name(Kind);
    public string Drives => Letters.Count == 0 ? "No Drive Letter" : string.Join(", ", Letters.Select(l => l.TrimEnd('\\')));
    public string Name => $"Disk {Number}: {Model}";
}

public sealed record DiskEraseResult(WipeMethod Method, bool FirmwareFellBack, bool? Verified, long Mismatches, bool Reformatted, string? NewLetter)
{
    public WipeLevel Level { get; init; } = WipeLevel.Secure;
    /// <summary>The format the drive was given; null = left uninitialized (Reformatted then means the partition data was removed).</summary>
    public DiskFormat? Format { get; init; }
    /// <summary>What diskpart said when setting the drive up failed.</summary>
    public string? SetupError { get; init; }
}

/// <summary>
/// An Entire Drive wipe whose overwrite failed part way (a write error, or the drive was unplugged). The message says so honestly:
/// the drive was partly overwritten and is not "not erased". The inner exception is what Windows said.
/// </summary>
public sealed class PartialWipeException(string message, Exception inner) : IOException(message, inner);

[SupportedOSPlatform("windows")]
public static class DriveWiper
{
    /// <summary>Why a drive letter must never be erased while Windows runs (null = it may be).</summary>
    public static string? ProtectedReason(string root)
    {
        string? Root(Environment.SpecialFolder f) => Path.GetPathRoot(Environment.GetFolderPath(f));
        bool Is(string? other) => other is { Length: > 0 } && string.Equals(root, other, StringComparison.OrdinalIgnoreCase);
        if (Is(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\")) return "Windows is installed on this drive.";
        if (Is(Path.GetPathRoot(AppContext.BaseDirectory))) return "Kodiak Cleaner is running from this drive.";
        try { if (File.Exists(Path.Combine(root, "pagefile.sys"))) return "Windows uses this drive for its page file."; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        if (Is(Root(Environment.SpecialFolder.UserProfile))) return "Your user folder is on this drive.";
        if (Is(Root(Environment.SpecialFolder.ProgramFiles)) || Is(Root(Environment.SpecialFolder.ProgramFilesX86))) return "Programs are installed on this drive.";
        return null;
    }

    /// <summary>Drive letters (for free space wiping), each with its drive type.</summary>
    public static List<WipeableDrive> ListDrives()
    {
        var list = new List<WipeableDrive>();
        foreach (var d in DriveInfo.GetDrives())
        {
            if (!d.IsReady || d.DriveType is not (DriveType.Fixed or DriveType.Removable)) continue;
            var reason = ProtectedReason(d.Name);
            list.Add(new WipeableDrive(d.Name, d.VolumeLabel, d.DriveFormat, d.TotalSize, d.AvailableFreeSpace, d.DriveType, reason is null, reason)
            {
                Kind = KindOf(d.Name),
            });
        }
        return list;
    }

    private static MediaKind KindOf(string root)
    {
        try { return DriveMedia.DiskOf(root)?.Kind ?? MediaKind.Unknown; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or EntryPointNotFoundException) { return MediaKind.Unknown; }
    }

    /// <summary>
    /// Every attached physical disk. A disk is refused when any part of it holds Windows, its start-up partition, its page
    /// file, Kodiak Cleaner, your user folder or programs, or when a volume spans it and another disk. When Windows doesn't say
    /// which disk one of those is on, every disk is refused (<see cref="DiskGuard"/>: fail closed).
    /// </summary>
    public static List<WipeDisk> ListDisks()
    {
        var volumes = DriveMedia.ListVolumes();
        int? startup = DriveMedia.StartupDisk();
        return DiskGuard.Judge(DriveMedia.ListDisks(), volumes, startup, KeptDrives(), DriveMedia.DisksOf, ProtectedReason);
    }

    /// <summary>
    /// The drives whose disks must be found before anything is erased: Windows, Kodiak Cleaner's own drive, your user folder,
    /// programs and every page file. Network and CD/DVD drives are left out (they aren't on a local disk); a folder Windows
    /// doesn't place at all gets an empty root, which refuses every disk.
    /// </summary>
    internal static List<KeptDrive> KeptDrives()
    {
        var kept = new List<KeptDrive>();
        void Add(string? path, string holds, string reason)
        {
            if (LocalRoot(path) is { } root) kept.Add(new KeptDrive(root, holds, reason));
        }
        Add(Environment.SystemDirectory, "Windows", "Windows is installed on this drive.");
        Add(AppContext.BaseDirectory, "Kodiak Cleaner", "Kodiak Cleaner is running from this drive.");
        Add(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "your user folder", "Your user folder is on this drive.");
        Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "your programs", "Programs are installed on this drive.");
        var x86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (x86.Length > 0) Add(x86, "your programs", "Programs are installed on this drive.");
        foreach (var root in PageFileRoots()) Add(root, "Windows' page file", "Windows uses this drive for its page file.");
        return kept;
    }

    /// <summary>The drive root of a path ("C:\"); "" when Windows gave no path; null when it isn't on a local disk (network, CD/DVD).</summary>
    private static string? LocalRoot(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        string? root;
        try { root = Path.GetPathRoot(Path.GetFullPath(path)); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or IOException or System.Security.SecurityException) { return ""; }
        if (string.IsNullOrEmpty(root)) return "";
        if (root.StartsWith(@"\\", StringComparison.Ordinal) && !root.StartsWith(@"\\?\", StringComparison.Ordinal) && !root.StartsWith(@"\\.\", StringComparison.Ordinal))
            return null; // \\server\share: not on a disk in this PC
        if (root.Length != 3 || root[1] != ':') return root;
        try { if (new DriveInfo(root).DriveType is DriveType.Network or DriveType.CDRom) return null; }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException) { }
        return root.ToUpperInvariant();
    }

    /// <summary>
    /// Every drive with a page file: the ones Windows is set to use and has open (Session Manager\Memory Management, "?:" meaning
    /// the Windows drive) and any ready drive with a pagefile.sys at its root.
    /// </summary>
    private static IEnumerable<string> PageFileRoots()
    {
        var roots = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management");
            foreach (var name in new[] { "PagingFiles", "ExistingPageFiles" })
                if (key?.GetValue(name) is string[] entries)
                    foreach (var entry in entries)
                    {
                        var path = entry.Trim();
                        if (path.StartsWith(@"\??\", StringComparison.Ordinal)) path = path[4..];
                        if (path.Length >= 2 && path[1] == ':' && char.IsAsciiLetter(path[0])) roots.Add($"{char.ToUpperInvariant(path[0])}:\\");
                    }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (d.IsReady && d.DriveType is (DriveType.Fixed or DriveType.Removable) && File.Exists(Path.Combine(d.Name, "pagefile.sys"))) roots.Add(d.Name);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return roots;
    }

    /// <summary>
    /// Erases a whole disk at the given level, always removes its partition tables (MBR/GPT) and boot sectors, then either leaves it
    /// uninitialized like a new drive (<paramref name="format"/> null) or gives it one formatted partition. On Thorough and Secure,
    /// SSDs and NVMe drives get their own built-in secure erase (a random overwrite when the drive doesn't offer one); hard drives and flash
    /// drives get zeros (Thorough) or the passes of <paramref name="secure"/>. Needs administrator rights.
    /// </summary>
    public static Task<DiskEraseResult> EraseAsync(WipeDisk disk, WipeLevel level, WipeMethod secure, bool verify, DiskFormat? format,
        IProgress<string> status, IProgress<OverwriteProgress>? progress, CancellationToken ct) =>
        Task.Run(() => Erase(disk, level, secure, verify, format, status, progress, ct), CancellationToken.None);

    private static DiskEraseResult Erase(WipeDisk disk, WipeLevel level, WipeMethod secure, bool verify, DiskFormat? format,
        IProgress<string> status, IProgress<OverwriteProgress>? progress, CancellationToken ct)
    {
        if (WipeMethods.FormatProblem(disk.Size, format) is { } problem) throw new InvalidOperationException(problem);
        // Look again right before erasing: if the disk was swapped and another one took its number, stop.
        var now = ListDisks().FirstOrDefault(d => d.Number == disk.Number);
        if (now is null || !now.CanWipe || now.Size != disk.Size || now.Serial != disk.Serial || now.Model != disk.Model)
            throw new InvalidOperationException(now is { CanWipe: false } ? now.Reason : "The disk changed since it was listed. Refresh the list and choose it again.");

        var chosen = WipeMethods.ForLevel(level, disk.Kind, secure);
        var method = chosen ?? WipeMethod.Zeros;
        bool fellBack = false, cancelled = false, overwriting = false;
        Exception? failure = null;
        long mismatches = 0;
        var watch = new WriteWatch(progress);
        status.Report($"Locking {disk.Drives}…");
        var locks = LockVolumes(disk.Number);
        try
        {
            using var h = DriveMedia.Native.Open($@"\\.\PhysicalDrive{disk.Number}", DriveMedia.Native.GenericRead | DriveMedia.Native.GenericWrite,
                0x80000000 | 0x20000000); // FILE_FLAG_WRITE_THROUGH | FILE_FLAG_NO_BUFFERING
            if (h.IsInvalid) throw new UnauthorizedAccessException("Windows wouldn't open the disk for erasing. Kodiak Cleaner needs administrator rights.");
            long length = DriveMedia.Native.Length(h);
            try
            {
                if (chosen is null)
                {
                    // Quick: only the partition data is removed (diskpart clean, below).
                }
                else if (method == WipeMethod.Firmware)
                {
                    status.Report("The drive is running its own built-in secure erase, which wipes every memory cell. This usually takes seconds to a few minutes…");
                    if (!FirmwareErase(h))
                    {
                        fellBack = true;
                        status.Report("This drive doesn't offer a built-in secure erase, so it's being overwritten with random data instead…");
                        overwriting = true;
                        Overwriter.Run(h, length, [new WipePass(null)], verify, watch, ct);
                    }
                }
                else
                {
                    overwriting = true;
                    mismatches = Overwriter.Run(h, length, WipeMethods.Passes(method), verify, watch, ct);
                }
            }
            catch (OperationCanceledException)
            {
                cancelled = true; // still give the half-erased disk a fresh partition so Windows can use it
            }
            catch (Exception e) when (overwriting && e is not OperationCanceledException)
            {
                // A write failed part way (the drive was unplugged, or a sector couldn't be written): the start of the drive,
                // where its partitions are listed, is already overwritten, so this must not be reported as "not erased".
                failure = e;
            }
            DriveMedia.Native.Control(h, DriveMedia.Native.IoctlDiskUpdateProperties);
        }
        finally
        {
            foreach (var l in locks) l.Dispose();
        }

        if (failure is not null) throw StoppedPartway(disk, level, chosen ?? method, fellBack, watch.Wrote, failure, format, status);

        // A stopped wipe still loses its partition data, and gets the chosen format so Windows can use it again.
        status.Report(format is null ? "Removing the partition tables and boot sectors…" : $"Removing the partition data, then making a {format.PartitionStyle} drive with {format.FileSystem}…");
        var (ok, letter, used, error) = Repartition(disk, format, status);
        ActivityLog.Write("Drive Wiper", $"{(cancelled ? "Stopped erasing" : "Erased")} {disk.Name} ({ByteSize.Format(disk.Size)}, {disk.KindName}): " +
            $"{WipeMethods.LevelName(level)}" + (chosen is null ? ", partition data only" : $" with {(fellBack ? "a random overwrite (no built-in secure erase offered)" : WipeMethods.Name(method))}") +
            (verify && chosen is not null && method != WipeMethod.Firmware && !cancelled ? (mismatches == 0 ? ", verified" : $", {mismatches} bytes didn't verify") : "") +
            (used is null ? ", left uninitialized" : $", formatted {used.PartitionStyle} {used.FileSystem}") + (ok ? "" : $" (diskpart failed: {error ?? "no reason given"})"));
        if (cancelled) throw new OperationCanceledException(ct);
        bool? verified = !verify || chosen is null || (method == WipeMethod.Firmware && !fellBack) ? null : mismatches == 0;
        return new DiskEraseResult(chosen ?? method, fellBack, verified, mismatches, ok, letter) { Level = level, Format = used, SetupError = error };
    }

    /// <summary>
    /// A wipe whose overwrite failed part way: logged, and set up again only when the very same drive is still attached (same number,
    /// model, serial and size, and still allowed). After a drive is unplugged Windows can give its number to the next drive plugged in,
    /// so running diskpart on that number blindly could clean the wrong drive; a drive that's still there gets its partition data removed
    /// (and the chosen format) so Windows can use it, like a stopped wipe.
    /// </summary>
    private static PartialWipeException StoppedPartway(WipeDisk disk, WipeLevel level, WipeMethod method, bool fellBack, bool wrote, Exception failure,
        DiskFormat? format, IProgress<string> status)
    {
        bool stillThere;
        try
        {
            stillThere = ListDisks().FirstOrDefault(d => d.Number == disk.Number) is { CanWipe: true } n
                && n.Size == disk.Size && n.Serial == disk.Serial && n.Model == disk.Model;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or EntryPointNotFoundException or InvalidOperationException) { stillThere = false; }

        bool setUp = false;
        DiskFormat? used = null;
        string? error = null;
        if (stillThere)
        {
            status.Report("The wipe stopped partway. Removing the drive's partition data so Windows can use it…");
            (setUp, _, used, error) = Repartition(disk, format, status);
        }
        var text = WipeMethods.PartialWipeText(wrote, stillThere, setUp);
        ActivityLog.Write("Drive Wiper", $"Wipe of {disk.Name} ({ByteSize.Format(disk.Size)}, {disk.KindName}) stopped partway: {WipeMethods.LevelName(level)} with " +
            $"{(fellBack ? "a random overwrite (no built-in secure erase offered)" : WipeMethods.Name(method))}; {failure.Message}" +
            (!stillThere ? "; not set up again (the drive is no longer attached as it was)"
                : setUp ? (used is null ? "; partition data removed, left uninitialized" : $"; partition data removed, formatted {used.PartitionStyle} {used.FileSystem}")
                : $"; setting it up again failed (diskpart: {error ?? "no reason given"})"));
        return new PartialWipeException(text, failure);
    }

    /// <summary>Notes whether the overwrite reported any progress: the first report comes after the first block was written.</summary>
    private sealed class WriteWatch(IProgress<OverwriteProgress>? inner) : IProgress<OverwriteProgress>
    {
        public bool Wrote { get; private set; }
        public void Report(OverwriteProgress value)
        {
            Wrote = true;
            inner?.Report(value);
        }
    }

    /// <summary>Locks and dismounts every volume on the disk so nothing else writes while it's erased; disposing unlocks.</summary>
    private static List<Microsoft.Win32.SafeHandles.SafeFileHandle> LockVolumes(int number)
    {
        var handles = new List<Microsoft.Win32.SafeHandles.SafeFileHandle>();
        try
        {
            foreach (var v in DriveMedia.ListVolumes().Where(v => v.Disks.Contains(number)))
            {
                var h = DriveMedia.Native.Open(v.VolumeName, DriveMedia.Native.GenericRead | DriveMedia.Native.GenericWrite);
                if (h.IsInvalid) throw new IOException($"Windows wouldn't open {Describe(v)} for erasing.");
                handles.Add(h);
                bool locked = false;
                for (int i = 0; i < 10 && !locked; i++)
                {
                    locked = DriveMedia.Native.Control(h, DriveMedia.Native.FsctlLockVolume);
                    if (!locked) Thread.Sleep(300);
                }
                if (!locked) throw new IOException($"A program is using {Describe(v)}. Close it (and any open Explorer windows on it), then try again.");
                DriveMedia.Native.Control(h, DriveMedia.Native.FsctlDismountVolume);
            }
            return handles;
        }
        catch
        {
            foreach (var h in handles) h.Dispose();
            throw;
        }
    }

    private static string Describe(VolumeOnDisk v) => v.Paths.FirstOrDefault()?.TrimEnd('\\') ?? "a drive on this disk";

    /// <summary>
    /// IOCTL_STORAGE_REINITIALIZE_MEDIA: the drive erases itself. Tries crypto erase (throws away the drive's internal key),
    /// then block erase, then the drive's default, the last also on Windows versions that take no options.
    /// </summary>
    private static bool FirmwareErase(Microsoft.Win32.SafeHandles.SafeFileHandle h)
    {
        foreach (uint sanitize in new uint[] { 2, 1 })
        {
            var input = new byte[16]; // Version, Size, TimeoutInSeconds, SanitizeOption (method in the low 4 bits)
            BitConverter.TryWriteBytes(input.AsSpan(0), 16u);
            BitConverter.TryWriteBytes(input.AsSpan(4), 16u);
            BitConverter.TryWriteBytes(input.AsSpan(8), 3600u);
            BitConverter.TryWriteBytes(input.AsSpan(12), sanitize);
            if (DriveMedia.Native.Control(h, DriveMedia.Native.IoctlStorageReinitializeMedia, input)) return true;
        }
        return DriveMedia.Native.Control(h, DriveMedia.Native.IoctlStorageReinitializeMedia);
    }

    /// <summary>
    /// Windows' diskpart: remove the partition tables, then (when a format is chosen) make one partition, quick-format it (which also
    /// trims flash) and give it a letter. A drive that was just erased can still be busy, so a failed run is tried again; if GPT
    /// is refused (some USB drives only take MBR), MBR is used instead. Returns the format actually used.
    /// </summary>
    private static (bool Ok, string? Letter, DiskFormat? Used, string? Error) Repartition(WipeDisk disk, DiskFormat? format, IProgress<string> status)
    {
        var tries = new List<DiskFormat?> { format, format };
        if (format is { Gpt: true } && disk.Size <= 2048L * 1024 * 1024 * 1024) tries.Add(format with { Gpt = false });
        string? error = null;
        for (int i = 0; i < tries.Count; i++)
        {
            if (i > 0)
            {
                status.Report(tries[i] != tries[i - 1] ? "The drive didn't take GPT, so it's being set up as MBR…" : "Windows was still busy with the drive. Trying again…");
                Thread.Sleep(3000);
            }
            var (ok, output) = RunDiskpart(WipeMethods.DiskpartScript(disk.Number, tries[i]));
            if (ok)
            {
                var letter = tries[i] is null ? null
                    : DriveMedia.ListVolumes().Where(v => v.Disks.Contains(disk.Number)).SelectMany(v => v.Paths).FirstOrDefault(x => x.Length == 3);
                return (true, letter?.TrimEnd('\\'), tries[i], null);
            }
            error = WipeMethods.DiskpartError(output) ?? error;
        }
        return (false, null, format, error);
    }

    private static (bool Ok, string Output) RunDiskpart(IEnumerable<string> lines)
    {
        var script = Path.Combine(Path.GetTempPath(), $"KodiakCleaner-Wipe-{Environment.ProcessId}.txt");
        try
        {
            File.WriteAllLines(script, lines, Encoding.ASCII); // the drive name is ASCII only (DiskFormat.SafeLabel), so every code page reads it the same
            var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "diskpart.exe"), $"/s \"{script}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi);
            if (p is null) return (false, "");
            var err = p.StandardError.ReadToEndAsync();
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return (p.ExitCode == 0, output + err.Result);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return (false, e.Message);
        }
        finally
        {
            try { File.Delete(script); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }
}
