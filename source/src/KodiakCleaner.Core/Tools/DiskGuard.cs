namespace KodiakCleaner.Core.Tools;

/// <summary>
/// A drive letter that must never be erased while Windows runs (Windows, Kodiak Cleaner, your user folder, programs, a page file):
/// <paramref name="Holds"/> names what is on it for the "couldn't tell" message, <paramref name="Reason"/> is shown on its disk.
/// An empty <paramref name="Root"/> means Windows didn't say where that thing is.
/// </summary>
public sealed record KeptDrive(string Root, string Holds, string Reason);

/// <summary>
/// Decides which physical disks the Drive Wiper may erase (pre-Beta review, 2026-10-25). It fails closed: when the disk under
/// any kept drive, or the disk Windows starts from, can't be found, every disk is refused, because the one that can't be placed
/// might be any of them. Pure, so the rules are tested without touching a real disk.
/// </summary>
public static class DiskGuard
{
    /// <summary>The reason every disk gets when the disk under a kept drive can't be found.</summary>
    public static string CouldNotPlace(string holds) => $"Kodiak Cleaner couldn't tell which disk holds {holds}, so no drive can be erased.";

    /// <summary>The reason every disk gets when the start-up partition's disk can't be found.</summary>
    public const string StartupUnknown = "Kodiak Cleaner couldn't tell which disk Windows starts from, so no drive can be erased.";

    /// <param name="disks">Every attached physical disk.</param>
    /// <param name="volumes">Every mounted volume with its paths and the disks under it.</param>
    /// <param name="startup">The disk holding the start-up (EFI or boot) partition; null = Windows didn't say.</param>
    /// <param name="kept">The drives that must stay: each one's disk has to be found, or nothing can be erased.</param>
    /// <param name="disksOf">Asks Windows which disks a drive letter lives on (empty or an exception = it didn't say).</param>
    /// <param name="protectedReason">Why a drive letter on a disk must stay (null = it may go).</param>
    public static List<WipeDisk> Judge(IEnumerable<DiskMedia> disks, IReadOnlyList<VolumeOnDisk> volumes, int? startup,
        IEnumerable<KeptDrive> kept, Func<string, IReadOnlyList<int>> disksOf, Func<string, string?> protectedReason)
    {
        string? everyDisk = startup is null ? StartupUnknown : null;
        var keptDisks = new Dictionary<int, string>();
        foreach (var k in kept)
        {
            var under = DisksUnder(k.Root, volumes, disksOf);
            if (under.Count == 0) { everyDisk ??= CouldNotPlace(k.Holds); continue; }
            foreach (var n in under) keptDisks.TryAdd(n, k.Reason);
        }

        var list = new List<WipeDisk>();
        foreach (var disk in disks)
        {
            var mine = volumes.Where(v => v.Disks.Contains(disk.Number)).ToList();
            var letters = mine.SelectMany(v => v.Paths).Where(p => p.Length == 3).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            string? reason = null;
            if (disk.Number == startup) reason = "Windows starts from this drive.";
            if (reason is null && keptDisks.TryGetValue(disk.Number, out var why)) reason = why;
            foreach (var letter in letters) reason ??= protectedReason(letter);
            if (reason is null && mine.Any(v => v.Disks.Count > 1)) reason = "A drive on this disk spans another disk.";
            if (reason is null && disk.Size <= 0) reason = "Windows doesn't report this disk's size.";
            reason ??= everyDisk;
            list.Add(new WipeDisk(disk.Number, disk.Model, disk.Serial, disk.Size, disk.Kind, disk.Removable || disk.IsUsb, letters, reason));
        }
        return list;
    }

    /// <summary>The disks under a drive letter: from the volume list and by asking Windows directly; empty when neither says.</summary>
    internal static IReadOnlyList<int> DisksUnder(string root, IReadOnlyList<VolumeOnDisk> volumes, Func<string, IReadOnlyList<int>> disksOf)
    {
        if (string.IsNullOrWhiteSpace(root)) return [];
        var found = new SortedSet<int>();
        foreach (var v in volumes.Where(v => v.Paths.Any(p => string.Equals(p, root, StringComparison.OrdinalIgnoreCase))))
            found.UnionWith(v.Disks);
        try { found.UnionWith(disksOf(root)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or EntryPointNotFoundException or InvalidOperationException) { }
        return found.ToList();
    }
}
