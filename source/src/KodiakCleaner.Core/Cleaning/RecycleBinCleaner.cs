using System.Runtime.Versioning;

namespace KodiakCleaner.Core.Cleaning;

public sealed record RecycleBinAnalysis(long Bytes, long Items, IReadOnlyList<RecycledItem>? OnlyThese);

/// <summary>Empties the Recycle Bin entirely, or only items deleted more than <c>minAge</c> ago.</summary>
[SupportedOSPlatform("windows")]
public static class RecycleBinCleaner
{
    public static RecycleBinAnalysis Analyze(TimeSpan? minAge, DateTime? nowUtc = null)
    {
        if (minAge is null)
        {
            var (bytes, items) = RecycleBin.Query();
            return new RecycleBinAnalysis(bytes, items, null);
        }
        var cutoff = (nowUtc ?? DateTime.UtcNow) - minAge.Value;
        var old = RecycleBinItems.ReadCurrentUser().Where(i => i.DeletedUtc <= cutoff).ToList();
        return new RecycleBinAnalysis(old.Sum(i => i.Size), old.Count, old);
    }

    /// <returns>Bytes freed and items that could not be removed.</returns>
    public static (long Freed, int Failed) Clean(RecycleBinAnalysis analysis)
    {
        if (analysis.OnlyThese is null)
            return RecycleBin.Empty() ? (analysis.Bytes, 0) : (0, (int)analysis.Items);

        long freed = 0;
        int failed = 0;
        foreach (var item in analysis.OnlyThese)
        {
            if (RecycleBinItems.Delete(item)) freed += item.Size;
            else failed++;
        }
        return (freed, failed);
    }
}
