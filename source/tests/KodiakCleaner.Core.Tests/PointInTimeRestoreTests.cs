using KodiakCleaner.Core.Tools;

namespace KodiakCleaner.Core.Tests;

public sealed class PointInTimeRestoreTests
{
    [Theory]
    [InlineData(240, "4 Hours")]
    [InlineData(60, "1 Hour")]
    [InlineData(4320, "72 Hours")]
    [InlineData(90, "90 Minutes")]
    public void Minutes_are_shown_as_hours_where_they_can_be(int minutes, string label) =>
        Assert.Equal(label, PointInTimeRestore.MinutesLabel(minutes));

    [Fact]
    public void Default_space_is_two_percent_held_to_two_to_fifty_gigabytes()
    {
        const long gb = 1024L * 1024 * 1024;
        Assert.Equal(2048, PointInTimeRestore.DefaultSpaceMb(64 * gb));      // 2% would be 1.3 GB
        Assert.Equal(10240, PointInTimeRestore.DefaultSpaceMb(500 * gb));    // 2% = 10 GB
        Assert.Equal(51200, PointInTimeRestore.DefaultSpaceMb(8000 * gb));   // 2% would be 160 GB
    }

    [Fact]
    public void Policy_beats_the_settings_choice_which_beats_the_default()
    {
        Assert.Equal(240, PointInTimeRestore.Effective(240, 720, 1440));
        Assert.Equal(720, PointInTimeRestore.Effective(null, 720, 1440));
        Assert.Equal(1440, PointInTimeRestore.Effective(null, null, 1440));
        Assert.Equal(1440, PointInTimeRestore.Effective(0, 0, 1440));
    }

    [Fact]
    public void Choices_match_windows_settings_and_stay_inside_its_limits()
    {
        Assert.Equal(new[] { 4, 6, 12, 16, 24 }, PointInTimeRestore.FrequencyHours);
        Assert.Equal(new[] { 4, 6, 12, 16, 24, 72 }, PointInTimeRestore.RetentionHours);
        Assert.Equal(72 * 60, PointInTimeRestore.RetentionHours.Max() * 60);
        Assert.All(PointInTimeRestore.SpaceGb, g => Assert.InRange(g * 1024, PointInTimeRestore.MinSpaceMb, PointInTimeRestore.MaxSpaceMb));
        Assert.Equal(PointInTimeRestore.SpaceGb.Order(), PointInTimeRestore.SpaceGb);
    }

    [Fact]
    public void A_snapshot_is_kept_for_the_retention_time()
    {
        var made = new DateTime(2026, 10, 12, 9, 0, 0);
        Assert.Equal(new DateTime(2026, 10, 15, 9, 0, 0), PointInTimeRestore.KeptUntil(made, 72 * 60));
    }
}
