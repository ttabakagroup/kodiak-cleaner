namespace KodiakCleaner.Core.Updates;

/// <summary>How often automatic updates check the website (Ted, 0.8.44). Listed shortest to longest, the order Ted gave.</summary>
public enum UpdateInterval { OnStartup, EveryHour, Every4Hours, Daily, Every3Days, Weekly, TwiceAMonth, Monthly }

public static class UpdateSchedule
{
    /// <summary>The choices shown under Settings > Updates, in interval order.</summary>
    public static readonly IReadOnlyList<(UpdateInterval Interval, string Label)> Options =
    [
        (UpdateInterval.OnStartup, "On Startup"), (UpdateInterval.EveryHour, "Every Hour"), (UpdateInterval.Every4Hours, "Every 4 Hours"),
        (UpdateInterval.Daily, "Daily"), (UpdateInterval.Every3Days, "Every 3 Days"), (UpdateInterval.Weekly, "Weekly"),
        (UpdateInterval.TwiceAMonth, "Twice a Month"), (UpdateInterval.Monthly, "Monthly"),
    ];

    /// <summary>When the next check is due after one at <paramref name="lastUtc"/>; null for On Startup (only when Kodiak Cleaner starts).</summary>
    public static DateTime? Next(UpdateInterval interval, DateTime lastUtc) => interval switch
    {
        UpdateInterval.EveryHour => lastUtc.AddHours(1),
        UpdateInterval.Every4Hours => lastUtc.AddHours(4),
        UpdateInterval.Daily => lastUtc.AddDays(1),
        UpdateInterval.Every3Days => lastUtc.AddDays(3),
        UpdateInterval.Weekly => lastUtc.AddDays(7),
        UpdateInterval.TwiceAMonth => lastUtc.AddDays(15),
        UpdateInterval.Monthly => lastUtc.AddMonths(1),
        _ => null,
    };

    /// <summary>
    /// Whether to check now. On Startup checks only at start; the others check at start or while Kodiak Cleaner is open once
    /// the interval has passed since the last check that reached the website (or right away if there never was one).
    /// </summary>
    public static bool IsDue(UpdateInterval interval, DateTime? lastUtc, DateTime nowUtc, bool atStartup)
    {
        if (interval == UpdateInterval.OnStartup) return atStartup;
        return lastUtc is not { } last || nowUtc >= Next(interval, last);
    }
}
