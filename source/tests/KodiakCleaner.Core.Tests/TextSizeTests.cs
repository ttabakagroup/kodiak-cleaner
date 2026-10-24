using KodiakCleaner.Core.Util;

namespace KodiakCleaner.Core.Tests;

/// <summary>Settings › Appearance › Text Size (Ted, 2026-10-24): every window scaled on top of Windows' display scaling.</summary>
public class TextSizeTests
{
    [Theory]
    [InlineData(100, 100)]
    [InlineData(125, 130)]
    [InlineData(124, 120)]
    [InlineData(50, 80)]
    [InlineData(500, 200)]
    [InlineData(0, 100)]
    [InlineData(-20, 100)]
    public void Saved_sizes_are_made_valid(int saved, int expected) => Assert.Equal(expected, TextSize.Normalize(saved));

    [Fact]
    public void Unreadable_slider_values_fall_back_to_the_default()
    {
        Assert.Equal(TextSize.Default, TextSize.Normalize(double.NaN));
        Assert.Equal(150, TextSize.Normalize(149.6));
    }

    [Fact]
    public void Windows_grow_but_never_past_the_screen()
    {
        Assert.Equal(1500, TextSize.Fit(1000, 1.5, 1920));
        Assert.Equal(1040, TextSize.Fit(760, 1.5, 1040));   // the main window at 150% on a short screen
        Assert.Equal(720, TextSize.Fit(900, 0.8, 1920));
        Assert.True(double.IsNaN(TextSize.Fit(double.NaN, 1.5, 1920)));  // sized to its content: left alone
        Assert.Equal(double.PositiveInfinity, TextSize.Fit(double.PositiveInfinity, 1.5, 1920));
        Assert.Equal(0, TextSize.Fit(0, 1.5, 1920));
    }

    [Fact]
    public void Windows_stop_growing_when_their_smallest_size_would_not_fit()
    {
        Assert.Equal(1046 / 580.0, TextSize.MaxScale(900, 580, 1920, 1046), 6);  // main window on a 1080p screen: 180%
        Assert.Equal(1920 / 420.0, TextSize.MaxScale(420, double.NaN, 1920, 1046), 6);
        Assert.Equal(double.PositiveInfinity, TextSize.MaxScale(0, 0, 1920, 1046));
        Assert.Equal(1, TextSize.MaxScale(2000, 580, 1920, 1046));  // already too big: left at its normal size, never shrunk
    }

    [Fact]
    public void The_slider_saves_applies_and_resets()
    {
        var xaml = Repo.Read("src", "KodiakCleaner.App", "Views", "SettingsView.xaml");
        Assert.Contains($"Minimum=\"{TextSize.Min}\" Maximum=\"{TextSize.Max}\" TickFrequency=\"{TextSize.Step}\" SmallChange=\"{TextSize.Step}\"", xaml);
        // Sent on release, not while dragging: the window rescaling would move the slider out from under the pointer.
        Assert.Contains("UpdateSourceTrigger=Explicit", xaml);
        Assert.Contains("AutomationProperties.Name=\"Text Size\"", xaml);
        Assert.Contains("TextScale.Register()", Repo.Read("src", "KodiakCleaner.App", "App.xaml.cs"));
        var settings = Repo.Read("src", "KodiakCleaner.App", "Settings.cs");
        Assert.Contains("ini.Get" + "Int(\"Appearance\", \"TextSize\"", settings);
        Assert.Contains("ini.Set(\"Appearance\", \"TextSize\"", settings);
        var vm = Repo.Read("src", "KodiakCleaner.App", "ViewModels", "SettingsViewModel.cs");
        var reset = vm[vm.IndexOf("private void Reset()", StringComparison.Ordinal)..];
        Assert.Contains("TextScale.Refresh();", reset);
    }
}
