using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace KodiakCleaner.App;

/// <summary>
/// Switches between the Honey (default), Classic and Glacier color schemes, each in light or dark mode.
/// Only colors, fonts and corner radii change; layout stays the same. An optional accent color
/// (<see cref="AccentColors"/>) recolors buttons, links and highlights on top of either scheme.
/// </summary>
public static partial class ThemeManager
{
    public static bool IsDark { get; private set; }

    /// <summary>The scheme actually showing: Classic and Glacier are Supporter themes, so they fall back to Honey otherwise.</summary>
    public static Palette ActivePalette => Settings.Current.Palette switch
    {
        Palette.Glacier when Supporter.IsSupporter => Palette.Glacier,
        Palette.Classic when Supporter.IsSupporter => Palette.Classic,
        _ => Palette.Honey,
    };

    /// <summary>The scheme's own accent before any accent color is applied (for the "Scheme" swatch).</summary>
    public static System.Windows.Media.Brush? SchemeAccent { get; private set; }

    /// <summary>The scheme's colors before any accent color is applied, so accent swatches show the shade each would get.</summary>
    public static ResourceDictionary? SchemeColors { get; private set; }

    /// <summary>Raised after colors change, so swatches can refresh.</summary>
    public static event Action? Applied;

    private static bool _listening;

    public static void Apply()
    {
        var s = Settings.Current;
        IsDark = s.Theme == AppTheme.Dark || (s.Theme == AppTheme.System && !WindowsUsesLightTheme());

        var theme = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Themes/{ActivePalette}{(IsDark ? "Dark" : "Light")}.xaml"),
        };
        SchemeAccent = theme["Accent"] as System.Windows.Media.Brush;
        SchemeColors = new ResourceDictionary { Source = theme.Source };
        if (AccentColors.Effective(s.Accent, ActivePalette) is { Length: > 0 } accent) AccentColors.ApplyTo(theme, accent, IsDark);
        Application.Current.Resources.MergedDictionaries[0] = theme;

        foreach (Window w in Application.Current.Windows) ApplyTitleBar(w);
        Applied?.Invoke();

        if (!_listening)
        {
            _listening = true;
            SystemEvents.UserPreferenceChanged += (_, e) =>
            {
                if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color &&
                    (Settings.Current.Theme == AppTheme.System || Settings.Current.Accent == "Windows"))
                    Application.Current.Dispatcher.BeginInvoke(Apply);
            };
        }
    }

    /// <summary>Gives the window a dark title bar in dark mode (Windows 10 20H1+ / Windows 11).</summary>
    public static void ApplyTitleBar(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        int dark = IsDark ? 1 : 0;
        if (DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int)) != 0)
            DwmSetWindowAttribute(hwnd, 19, ref dark, sizeof(int)); // pre-20H1 attribute id
    }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private static bool WindowsUsesLightTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is not int v || v != 0;
    }
}
