using System.Globalization;

namespace KodiakCleaner.Core.Util;

/// <summary>
/// Elapsed time and an estimate of the time left for a running task, shown under its progress bar (Ted, 2026-10-23).
/// The estimate uses the speed since the percentage last started over (a new stage starts again from 0), counts down
/// between updates, and is only given once there is enough to go on.
/// </summary>
public sealed class ProgressClock
{
    /// <summary>No estimate before this long has passed, or before this much progress was made, since the baseline.</summary>
    public static readonly TimeSpan MinTime = TimeSpan.FromSeconds(5);
    public const double MinProgress = 2;

    private readonly DateTime _started;
    private DateTime _baseTime;
    private double _basePercent;
    private double? _percent;
    private DateTime _estimatedAt;
    private TimeSpan? _estimate;
    private double _gained;

    public ProgressClock(DateTime startedUtc)
    {
        _started = _baseTime = _estimatedAt = startedUtc;
    }

    /// <summary>Records a new percentage (null = can't tell). A first value, or one that went down, starts the speed over.</summary>
    public void Report(double? percent, DateTime nowUtc)
    {
        if (percent is not { } p)
        {
            _percent = null;
            _estimate = null;
            return;
        }
        if (_percent is not { } old || p < old)
        {
            _baseTime = nowUtc;
            _basePercent = p;
            _estimate = null;
        }
        else if (p > old)
        {
            var spent = nowUtc - _baseTime;
            var gained = p - _basePercent;
            if (spent >= MinTime && gained >= MinProgress && p < 100)
            {
                _estimate = TimeSpan.FromTicks((long)(spent.Ticks * ((100 - p) / gained)));
                _estimatedAt = nowUtc;
                _gained = gained;
            }
        }
        _percent = p;
    }

    public TimeSpan Elapsed(DateTime nowUtc) => nowUtc - _started;

    /// <summary>The time left, counted down since the last estimate; null while there isn't enough to go on.</summary>
    public TimeSpan? Remaining(DateTime nowUtc)
    {
        if (_estimate is not { } e || _percent is not { } p) return null;
        var left = e - (nowUtc - _estimatedAt);
        if (left >= TimeSpan.FromSeconds(5)) return left;
        // The countdown ran out but the percentage hasn't moved since: go by the speed so far (slower the longer it waits).
        return TimeSpan.FromTicks((long)((nowUtc - _baseTime).Ticks * ((100 - p) / _gained)));
    }

    /// <summary>"1:05 elapsed", then "1:05 elapsed · about 3 min left" once there's an estimate.</summary>
    /// <param name="rough">The percentage itself is an estimate (the time left says so).</param>
    public string Text(DateTime nowUtc, bool rough = false)
    {
        var text = FormatElapsed(Elapsed(nowUtc)) + " elapsed";
        if (_percent is null) return text;
        return Remaining(nowUtc) is { } left
            ? text + " · " + FormatRemaining(left) + (rough ? " (rough guess)" : "")
            : text + " · working out the time left";
    }

    /// <summary>0:42, 12:05, 1:02:03.</summary>
    public static string FormatElapsed(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        return t.TotalHours >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{(int)t.TotalMinutes}:{t.Seconds:00}");
    }

    /// <summary>"almost done", "less than a minute left", "about 3 min left", "about 1 h 20 min left".</summary>
    public static string FormatRemaining(TimeSpan t)
    {
        if (t < TimeSpan.FromSeconds(5)) return "almost done";
        if (t < TimeSpan.FromMinutes(1)) return "less than a minute left";
        var minutes = (int)Math.Ceiling(t.TotalMinutes);
        if (minutes < 60) return $"about {minutes} min left";
        var h = minutes / 60;
        var m = minutes % 60;
        return m == 0 ? $"about {h} h left" : $"about {h} h {m} min left";
    }
}
