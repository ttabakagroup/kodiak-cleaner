namespace KodiakCleaner.Core.Cleaning;

/// <summary>When the Cleaner should suggest Advanced Cleaning: only when it would make a real difference.</summary>
public static class AdvancedAdvice
{
    /// <summary>Always worth mentioning, however much space is free.</summary>
    public const long LargeAmount = 1L << 30; // 1 GB

    /// <summary>Worth mentioning when the Windows drive is nearly full.</summary>
    public const long FairAmount = 200L << 20; // 200 MB

    /// <param name="found">Bytes Advanced Cleaning's items would free.</param>
    /// <param name="drive">Free and total bytes of the Windows drive, if known.</param>
    public static bool ShouldRecommend(long found, (long Free, long Total)? drive)
    {
        if (found >= LargeAmount) return true;
        if (found < FairAmount || drive is not { Total: > 0 } d) return false;
        return d.Free < d.Total / 10 || d.Free < 10L << 30; // under 10% or under 10 GB free
    }
}
