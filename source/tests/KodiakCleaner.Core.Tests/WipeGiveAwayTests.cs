using KodiakCleaner.Core.Tools;

namespace KodiakCleaner.Core.Tests;

/// <summary>Step 2's "Giving This Drive Away or Selling It?" tip and the Quick line in the first confirmation (Ted, 0.8.118).</summary>
public sealed class WipeGiveAwayTests
{
    [Fact]
    public void Ssds_alone_get_Thorough_everything_else_Secure()
    {
        Assert.Equal(WipeLevel.Thorough, WipeMethods.GiveAwayLevel([MediaKind.Ssd]));
        Assert.Equal(WipeLevel.Thorough, WipeMethods.GiveAwayLevel([MediaKind.NvmeSsd, MediaKind.Ssd]));
        Assert.Equal(WipeLevel.Secure, WipeMethods.GiveAwayLevel([MediaKind.HardDrive]));
        Assert.Equal(WipeLevel.Secure, WipeMethods.GiveAwayLevel([MediaKind.FlashDrive]));
        Assert.Equal(WipeLevel.Secure, WipeMethods.GiveAwayLevel([MediaKind.Unknown]));
        Assert.Equal(WipeLevel.Secure, WipeMethods.GiveAwayLevel([MediaKind.NvmeSsd, MediaKind.HardDrive]));
        Assert.Equal(WipeLevel.Secure, WipeMethods.GiveAwayLevel([]));
    }

    [Theory]
    [InlineData(WipeLevel.Quick, MediaKind.Ssd, false)]
    [InlineData(WipeLevel.Quick, MediaKind.HardDrive, false)]
    [InlineData(WipeLevel.Thorough, MediaKind.HardDrive, true)]
    [InlineData(WipeLevel.Thorough, MediaKind.NvmeSsd, true)]
    [InlineData(WipeLevel.Thorough, MediaKind.FlashDrive, false)]
    [InlineData(WipeLevel.Secure, MediaKind.FlashDrive, true)]
    [InlineData(WipeLevel.Secure, MediaKind.Ssd, true)]
    public void Quick_never_suits_giving_away(WipeLevel level, MediaKind kind, bool suits) =>
        Assert.Equal(suits, WipeMethods.SuitsGivingAway(level, [kind]));

    [Fact]
    public void The_recommended_level_always_suits_giving_away()
    {
        MediaKind[][] sets = [[], [MediaKind.Ssd], [MediaKind.FlashDrive], [MediaKind.HardDrive, MediaKind.NvmeSsd], [MediaKind.Unknown]];
        foreach (var kinds in sets) Assert.True(WipeMethods.SuitsGivingAway(WipeMethods.GiveAwayLevel(kinds), kinds));
    }

    [Fact]
    public void The_tip_covers_only_the_ticked_types()
    {
        var hdd = WipeMethods.GiveAwayAdvice([MediaKind.HardDrive]);
        Assert.Contains("Hard drives: choose Secure (DoD 3 Passes is plenty), or at least Thorough.", hdd);
        Assert.DoesNotContain("SSD", hdd);
        Assert.DoesNotContain("Flash", hdd);
        Assert.EndsWith("Quick is only for drives you're keeping: it leaves the old files recoverable.", hdd);

        var ssd = WipeMethods.GiveAwayAdvice([MediaKind.NvmeSsd]);
        Assert.Contains("Thorough or Secure; both run the drive's Built-In Secure Erase", ssd);
        Assert.DoesNotContain("Hard drives", ssd);

        var flash = WipeMethods.GiveAwayAdvice([MediaKind.FlashDrive, MediaKind.FlashDrive]);
        Assert.Contains("memory cards: choose Secure", flash);
        Assert.Contains("physically destroy", flash);
        Assert.Equal(2, flash.Split('\n').Length); // one line for the type, one for Quick

        // An unknown drive is overwritten like a hard drive, so it gets the hard drive line.
        Assert.Contains("Hard drives:", WipeMethods.GiveAwayAdvice([MediaKind.Unknown]));
    }

    [Fact]
    public void Before_a_drive_is_ticked_every_type_is_listed_A_to_Z()
    {
        var lines = WipeMethods.GiveAwayAdvice([]).Split('\n');
        Assert.Equal(4, lines.Length);
        Assert.StartsWith("Flash drives", lines[0]);
        Assert.StartsWith("Hard drives", lines[1]);
        Assert.StartsWith("SSDs", lines[2]);
        Assert.StartsWith("Quick", lines[3]);
        Assert.DoesNotContain(lines, l => l.Contains("firmware", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Titles_and_the_Quick_note_follow_the_drive_count()
    {
        Assert.Equal("Giving This Drive Away or Selling It?", WipeMethods.GiveAwayTitle(0));
        Assert.Equal("Giving This Drive Away or Selling It?", WipeMethods.GiveAwayTitle(1));
        Assert.Equal("Giving These Drives Away or Selling Them?", WipeMethods.GiveAwayTitle(2));
        Assert.Equal("Giving this drive away or selling it? Quick leaves old files recoverable: choose Thorough or Secure.",
            WipeMethods.QuickGiveAwayNote(1));
        Assert.StartsWith("Giving these drives away or selling them?", WipeMethods.QuickGiveAwayNote(3));
    }
}
