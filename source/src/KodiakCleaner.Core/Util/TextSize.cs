namespace KodiakCleaner.Core.Util;

/// <summary>
/// Settings › Appearance › Text Size (Ted, 2026-10-24): makes every Kodiak Cleaner window bigger or smaller, on top of Windows'
/// display scaling (which WPF already follows; Windows' separate Text size slider it doesn't). A percentage in steps of 10.
/// </summary>
public static class TextSize
{
    public const int Min = 80;
    public const int Max = 200;
    public const int Step = 10;
    public const int Default = 100;

    /// <summary>A saved or typed percentage made valid: the nearest step, inside Min and Max (anything unreadable is Default).</summary>
    public static int Normalize(int percent) =>
        percent <= 0 ? Default : Math.Clamp((int)Math.Round(percent / (double)Step, MidpointRounding.AwayFromZero) * Step, Min, Max);

    public static int Normalize(double percent) => double.IsFinite(percent) ? Normalize((int)Math.Round(percent)) : Default;

    /// <summary>A window measurement at a new size: scaled, but never bigger than the screen space there is
    /// (a window that doesn't fit would have its buttons off screen). Unset sizes (NaN, infinity, 0) stay as they are.</summary>
    public static double Fit(double size, double factor, double available) =>
        !double.IsFinite(size) || size <= 0 ? size : Math.Min(size * factor, available > 0 ? available : double.PositiveInfinity);

    /// <summary>The biggest scale at which a window still gets its smallest designed size on the screen (no limit when it has none).
    /// Past it, the page would be squeezed below the size it was laid out for and cut off, so a window is never scaled further.</summary>
    public static double MaxScale(double minWidth, double minHeight, double availableWidth, double availableHeight)
    {
        var max = double.PositiveInfinity;
        if (double.IsFinite(minWidth) && minWidth > 0 && availableWidth > 0) max = Math.Min(max, availableWidth / minWidth);
        if (double.IsFinite(minHeight) && minHeight > 0 && availableHeight > 0) max = Math.Min(max, availableHeight / minHeight);
        return Math.Max(1, max);
    }
}
