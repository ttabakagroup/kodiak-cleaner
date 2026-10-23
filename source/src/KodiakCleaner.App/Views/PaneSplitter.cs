using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace KodiakCleaner.App.Views;

/// <summary>
/// The draggable divider between two areas of a page, such as the Startup list and its scheduled tasks (Ted, 0.8.99). Put a
/// GridSplitter with the PaneRows or PaneColumns style in its own row or column between the two areas, and give it
/// <c>views:PaneSplitter.Remember="key"</c>: where it was dragged to is kept in Settings.PaneSizes and put back next time, and
/// a double-click puts the page's own sizes back.
/// </summary>
public static class PaneSplitter
{
    public static readonly DependencyProperty RememberProperty = DependencyProperty.RegisterAttached(
        "Remember", typeof(string), typeof(PaneSplitter), new PropertyMetadata(null, OnRememberChanged));

    public static string? GetRemember(DependencyObject d) => (string?)d.GetValue(RememberProperty);
    public static void SetRemember(DependencyObject d, string? value) => d.SetValue(RememberProperty, value);

    // The page's own sizes, kept so a double-click can put them back.
    private static readonly DependencyProperty DefaultsProperty = DependencyProperty.RegisterAttached(
        "Defaults", typeof(GridLength[]), typeof(PaneSplitter));

    private static void OnRememberChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not GridSplitter splitter || e.OldValue is not null) return;
        splitter.Loaded += (_, _) => Restore(splitter);
        splitter.DragCompleted += (_, _) => Save(splitter);
        splitter.MouseDoubleClick += (_, args) => { ResetToDefault(splitter); args.Handled = true; };
    }

    /// <summary>The two rows or columns either side of the splitter, or null if it isn't between two.</summary>
    private static (DefinitionBase Before, DefinitionBase After)? Pair(GridSplitter s)
    {
        if (s.Parent is not Grid grid) return null;
        if (s.ResizeDirection == GridResizeDirection.Rows)
        {
            int r = Grid.GetRow(s);
            return r > 0 && r + 1 < grid.RowDefinitions.Count ? (grid.RowDefinitions[r - 1], grid.RowDefinitions[r + 1]) : null;
        }
        int c = Grid.GetColumn(s);
        return c > 0 && c + 1 < grid.ColumnDefinitions.Count ? (grid.ColumnDefinitions[c - 1], grid.ColumnDefinitions[c + 1]) : null;
    }

    private static GridLength Get(DefinitionBase d) => d is RowDefinition r ? r.Height : ((ColumnDefinition)d).Width;
    private static double Actual(DefinitionBase d) => d is RowDefinition r ? r.ActualHeight : ((ColumnDefinition)d).ActualWidth;
    private static void Put(DefinitionBase d, GridLength size)
    {
        if (d is RowDefinition r) r.Height = size;
        else ((ColumnDefinition)d).Width = size;
    }

    private static void Restore(GridSplitter s)
    {
        if (Pair(s) is not var (before, after) || s.GetValue(DefaultsProperty) is not null) return;
        s.SetValue(DefaultsProperty, new[] { Get(before), Get(after) });
        if (GetRemember(s) is not { } key || !Read().TryGetValue(key, out var sizes)) return;
        // Kept as shares of the room, so the two areas keep their proportion when the window is resized.
        Put(before, new GridLength(sizes.Before, GridUnitType.Star));
        Put(after, new GridLength(sizes.After, GridUnitType.Star));
    }

    private static void Save(GridSplitter s)
    {
        if (GetRemember(s) is not { } key || Pair(s) is not var (before, after)) return;
        double a = Actual(before), b = Actual(after);
        if (a <= 0 || b <= 0) return;
        Put(before, new GridLength(a, GridUnitType.Star));
        Put(after, new GridLength(b, GridUnitType.Star));
        var all = Read();
        all[key] = (Math.Round(a), Math.Round(b));
        Write(all);
    }

    private static void ResetToDefault(GridSplitter s)
    {
        if (Pair(s) is not var (before, after) || s.GetValue(DefaultsProperty) is not GridLength[] defaults) return;
        Put(before, defaults[0]);
        Put(after, defaults[1]);
        if (GetRemember(s) is { } key)
        {
            var all = Read();
            if (all.Remove(key)) Write(all);
        }
    }

    /// <summary>Settings.PaneSizes as key → (before, after); entries that don't read are skipped.</summary>
    internal static Dictionary<string, (double Before, double After)> Read()
    {
        var all = new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in Settings.Current.PaneSizes.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = entry.Split('=', 2);
            if (parts.Length != 2) continue;
            var sizes = parts[1].Split('|');
            if (sizes.Length == 2 &&
                double.TryParse(sizes[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var a) && a > 0 &&
                double.TryParse(sizes[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var b) && b > 0)
                all[parts[0].Trim()] = (a, b);
        }
        return all;
    }

    private static void Write(Dictionary<string, (double Before, double After)> all)
    {
        Settings.Current.PaneSizes = string.Join(";", all.Select(p =>
            $"{p.Key}={p.Value.Before.ToString(CultureInfo.InvariantCulture)}|{p.Value.After.ToString(CultureInfo.InvariantCulture)}"));
        Settings.Current.Save();
    }
}
