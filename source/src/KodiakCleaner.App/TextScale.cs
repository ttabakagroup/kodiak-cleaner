using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App;

/// <summary>
/// Settings › Appearance › Text Size (Ted, 2026-10-24). WPF follows Windows' display scaling but not Windows' separate Text size
/// slider, and the fonts are fixed in the styles, so the whole content of every window is scaled instead: text, icons, buttons
/// and spacing grow together and nothing is cut off. Registered once at startup (<see cref="Register"/>), each window is scaled at
/// its first layout; <see cref="Refresh"/> rescales the open ones when the setting changes. A window's size and minimum size grow
/// with it, and it stays centered where it was. A window is never scaled past the size at which its smallest designed size still
/// fits on the screen, so on a small screen the biggest sizes stop growing.
/// </summary>
public static class TextScale
{
    private static bool _registered;

    /// <summary>The scale a window was last given, so a later change grows its current size (a resized window keeps its shape).</summary>
    private static readonly DependencyProperty AppliedProperty = DependencyProperty.RegisterAttached(
        "Applied", typeof(double), typeof(TextScale), new PropertyMetadata(0.0));

    /// <summary>The window's own minimum and maximum sizes from its XAML, before any scaling.</summary>
    private static readonly DependencyProperty LimitsProperty = DependencyProperty.RegisterAttached(
        "Limits", typeof(Size[]), typeof(TextScale), new PropertyMetadata(null));

    public static double Factor => TextSize.Normalize(Settings.Current.TextSize) / 100.0;

    public static void Register()
    {
        if (_registered) return;
        _registered = true;
        // SizeChanged, not Loaded: a class handler for Loaded never runs (see AccessibleNames). A window's first SizeChanged comes
        // with its first layout.
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.SizeChangedEvent, new SizeChangedEventHandler((s, _) =>
        {
            if (s is Window w && (double)w.GetValue(AppliedProperty) == 0) Apply(w);
        }));
        // Tooltips and right-click menus open in pop-ups of their own, outside the window's scaled content.
        EventManager.RegisterClassHandler(typeof(ToolTip), ToolTip.OpenedEvent, new RoutedEventHandler((s, _) => ScalePopup((Control)s, ((ToolTip)s).PlacementTarget)));
        EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.OpenedEvent, new RoutedEventHandler((s, _) => ScalePopup((Control)s, ((ContextMenu)s).PlacementTarget)));
    }

    /// <summary>Rescales every open window after Text Size changes (or Reset to Default).</summary>
    public static void Refresh()
    {
        foreach (Window w in Application.Current.Windows)
            if ((double)w.GetValue(AppliedProperty) != 0) Apply(w);
    }

    /// <summary>A tooltip or menu takes the scale of the window it opens from.</summary>
    private static void ScalePopup(Control popup, UIElement? target)
    {
        var window = target is null ? Application.Current.MainWindow : Window.GetWindow(target) ?? Application.Current.MainWindow;
        var factor = window?.GetValue(AppliedProperty) is double f && f > 0 ? f : 1;
        if (popup.LayoutTransform is ScaleTransform current && current.ScaleX == factor) return;
        popup.LayoutTransform = factor == 1 ? Transform.Identity : new ScaleTransform(factor, factor);
    }

    private static void Apply(Window w)
    {
        if (w.Content is not FrameworkElement content) return;
        var area = SystemParameters.WorkArea;
        if (w.GetValue(LimitsProperty) is not Size[] limits)
            w.SetValue(LimitsProperty, limits = new[] { new Size(w.MinWidth, w.MinHeight), new Size(w.MaxWidth, w.MaxHeight) });
        // Never so big that the window can't get its smallest designed size on this screen (the page would be cut off).
        var factor = Math.Min(Factor, TextSize.MaxScale(limits[0].Width, limits[0].Height, area.Width, area.Height));
        var before = (double)w.GetValue(AppliedProperty);
        w.SetValue(AppliedProperty, factor);
        if (before == factor || (before == 0 && factor == 1)) return;

        content.LayoutTransform = factor == 1 ? Transform.Identity : new ScaleTransform(factor, factor);
        // Display text is drawn to fit whole pixels at its own size; scaled, it looks smudged, so scaled windows draw text the
        // way Windows draws resizable text.
        if (factor == 1) content.ClearValue(TextOptions.TextFormattingModeProperty);
        else TextOptions.SetTextFormattingMode(content, TextFormattingMode.Ideal);

        w.MinWidth = TextSize.Fit(limits[0].Width, factor, area.Width);
        w.MinHeight = TextSize.Fit(limits[0].Height, factor, area.Height);
        w.MaxWidth = Math.Max(w.MinWidth, TextSize.Fit(limits[1].Width, factor, double.PositiveInfinity));
        w.MaxHeight = Math.Max(w.MinHeight, TextSize.Fit(limits[1].Height, factor, double.PositiveInfinity));
        if (w.WindowState != WindowState.Normal) return;

        // Sized windows grow by the change, around their center; windows sized to their content follow it on their own.
        var ratio = factor / (before == 0 ? 1 : before);
        if (w.SizeToContent != SizeToContent.Manual) KeepCentered(w, area);
        var old = new Size(w.ActualWidth > 0 ? w.ActualWidth : w.Width, w.ActualHeight > 0 ? w.ActualHeight : w.Height);
        var size = old;
        if (w.SizeToContent is SizeToContent.Manual or SizeToContent.Height) w.Width = size.Width = TextSize.Fit(old.Width, ratio, area.Width);
        if (w.SizeToContent is SizeToContent.Manual or SizeToContent.Width) w.Height = size.Height = TextSize.Fit(old.Height, ratio, area.Height);
        if (w.SizeToContent == SizeToContent.Manual) Recenter(w, old, size, area);
    }

    /// <summary>Moves the window so it grows around its center, with its title bar kept on the screen.</summary>
    private static void Recenter(Window w, Size old, Size now, Rect area)
    {
        if (double.IsNaN(w.Left) || double.IsNaN(w.Top) || !double.IsFinite(old.Width) || !double.IsFinite(old.Height)) return;
        w.Left = Math.Clamp(w.Left - (now.Width - old.Width) / 2, area.Left, Math.Max(area.Left, area.Right - now.Width));
        w.Top = Math.Clamp(w.Top - (now.Height - old.Height) / 2, area.Top, Math.Max(area.Top, area.Bottom - now.Height));
    }

    /// <summary>A window sized to its content: its size changes once the new scale is laid out, so it is moved then.</summary>
    private static void KeepCentered(Window w, Rect area)
    {
        void Resized(object sender, SizeChangedEventArgs e)
        {
            if (e.PreviousSize.Width > 0 && e.PreviousSize.Height > 0) Recenter(w, e.PreviousSize, e.NewSize, area);
        }
        w.SizeChanged += Resized;
        // Only the change the new scale makes: a later resize (by hand, or new content) doesn't move the window.
        w.Dispatcher.BeginInvoke(() => w.SizeChanged -= Resized, DispatcherPriority.ContextIdle);
    }
}
