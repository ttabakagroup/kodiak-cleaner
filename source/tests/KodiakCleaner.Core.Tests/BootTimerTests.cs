using KodiakCleaner.Core.Tools;
using Xunit;

namespace KodiakCleaner.Core.Tests;

public class BootTimerTests
{
    [Theory]
    [InlineData(0, 0, 0, BootImpact.Low)]
    [InlineData(300, 300 * 1024, 0, BootImpact.Low)]
    [InlineData(301, 0, 0, BootImpact.Medium)]
    [InlineData(0, 300 * 1024 + 1, 0, BootImpact.Medium)]
    [InlineData(1001, 0, 0, BootImpact.High)]
    [InlineData(0, 3 * 1024 * 1024 + 1, 0, BootImpact.High)]
    [InlineData(0, 0, 400, BootImpact.Medium)]
    [InlineData(0, 0, 1000, BootImpact.High)]
    public void Impact_uses_task_manager_thresholds(long cpuMs, long disk, long delay, BootImpact expected) =>
        Assert.Equal(expected, BootTimer.ImpactOf(cpuMs, disk, delay));

    [Fact]
    public void A_new_start_after_the_click_finishes_the_timing()
    {
        var click = new DateTime(2026, 10, 12, 4, 0, 0, DateTimeKind.Utc);
        var state = new BootTimerState { PendingSinceUtc = click, PendingBootUtc = click.AddDays(-2) };
        Assert.False(BootTimer.RestartHappened(state, click.AddDays(-2)));          // still the old start
        Assert.False(BootTimer.RestartHappened(state, click.AddDays(-2).AddSeconds(5))); // clock wobble, same start
        Assert.True(BootTimer.RestartHappened(state, click.AddSeconds(40)));
        Assert.False(BootTimer.RestartHappened(new BootTimerState(), click.AddSeconds(40))); // nothing pending
    }

    [Fact]
    public void Report_splits_the_restart_into_parts()
    {
        var t = new DateTime(2026, 10, 12, 4, 0, 0, DateTimeKind.Utc);
        var r = new BootReport { RequestedUtc = t, BootUtc = t.AddSeconds(20), SignInUtc = t.AddSeconds(50), ReadyUtc = t.AddSeconds(95) };
        Assert.Equal(TimeSpan.FromSeconds(20), r.ShutdownAndRestart);
        Assert.Equal(TimeSpan.FromSeconds(30), r.WindowsStarting);
        Assert.Equal(TimeSpan.FromSeconds(45), r.DesktopLoading);
        Assert.Equal(TimeSpan.FromSeconds(95), r.Total);
        r.ReadyUtc = null;
        Assert.Null(r.DesktopLoading);
        Assert.Equal(TimeSpan.FromSeconds(50), r.Total);
    }

    [Fact]
    public void Ranking_puts_the_slowest_first_then_a_to_z()
    {
        var ranked = BootTimer.Rank(new[]
        {
            new BootItem { Name = "Beta", Impact = BootImpact.Low, CpuMs = 10 },
            new BootItem { Name = "Alpha", Impact = BootImpact.Low, CpuMs = 10 },
            new BootItem { Name = "Busy", Impact = BootImpact.High, CpuMs = 5000 },
            new BootItem { Name = "Driver", Impact = BootImpact.High, DelayMs = 2500 },
        });
        Assert.Equal(new[] { "Driver", "Busy", "Alpha", "Beta" }, ranked.Select(i => i.Name));
    }

    [Fact]
    public void History_keeps_the_newest_ten()
    {
        var state = new BootTimerState { PendingSinceUtc = DateTime.UtcNow };
        var t = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 12; i++)
            BootTimer.Finish(state, new BootReport { RequestedUtc = t.AddDays(i), BootUtc = t.AddDays(i).AddSeconds(i + 1) });
        Assert.Equal(BootTimer.HistoryKept, state.History.Count);
        Assert.Equal(t.AddDays(11), state.History[^1].RequestedUtc);
        Assert.Null(state.PendingSinceUtc);
        Assert.NotNull(state.Last);
    }

    [Fact]
    public void State_survives_a_save_and_load()
    {
        var file = Path.Combine(Path.GetTempPath(), "kc-boot-" + Guid.NewGuid().ToString("N"), "boot-timer.json");
        try
        {
            var state = new BootTimerState { PendingSinceUtc = new DateTime(2026, 10, 12, 4, 0, 0, DateTimeKind.Utc) };
            state.History.Add(new BootHistoryEntry(DateTime.UtcNow, 61.5));
            BootTimer.Save(file, state);
            var back = BootTimer.Load(file);
            Assert.Equal(state.PendingSinceUtc, back.PendingSinceUtc?.ToUniversalTime());
            Assert.Equal(61.5, Assert.Single(back.History).TotalSeconds);
            Assert.Null(BootTimer.Load(file + ".missing").PendingSinceUtc);
        }
        finally { Directory.Delete(Path.GetDirectoryName(file)!, true); }
    }

    private const string Events = """
        <Event xmlns='http://schemas.microsoft.com/win/2004/08/events/event'><System><EventID>101</EventID><TimeCreated SystemTime='2026-10-12T04:01:30.0000000Z'/></System>
          <EventData><Data Name='Name'>C:\Apps\Slow\slow.exe</Data><Data Name='FriendlyName'>Slow Helper</Data><Data Name='TotalTime'>4100</Data><Data Name='DegradationTime'>3100</Data></EventData></Event>
        <Event xmlns='http://schemas.microsoft.com/win/2004/08/events/event'><System><EventID>103</EventID><TimeCreated SystemTime='2026-10-12T04:01:31.0000000Z'/></System>
          <EventData><Data Name='Name'>SlowSvc</Data><Data Name='FriendlyName'>Slow Service</Data><Data Name='TotalTime'>900</Data><Data Name='DegradationTime'>0</Data></EventData></Event>
        <Event xmlns='http://schemas.microsoft.com/win/2004/08/events/event'><System><EventID>102</EventID><TimeCreated SystemTime='2026-10-10T08:00:00.0000000Z'/></System>
          <EventData><Data Name='FriendlyName'>Old Driver</Data><Data Name='DegradationTime'>5000</Data></EventData></Event>
        <Event xmlns='http://schemas.microsoft.com/win/2004/08/events/event'><System><EventID>100</EventID><TimeCreated SystemTime='2026-10-12T04:03:00.0000000Z'/></System>
          <EventData><Data Name='BootStartTime'>2026-10-12T04:00:20.0000000Z</Data><Data Name='BootTime'>48200</Data>
          <Data Name='MainPathBootTime'>21000</Data><Data Name='BootPostBootTime'>27200</Data></EventData></Event>
        """;

    [Fact]
    public void Windows_boot_events_for_this_start_are_read()
    {
        var boot = new DateTime(2026, 10, 12, 4, 0, 21, DateTimeKind.Utc);
        var data = BootTimer.ParseWindowsEvents(Events, boot);
        Assert.NotNull(data);
        Assert.Equal((48200L, 21000L, 27200L), (data!.BootMs, data.MainPathMs, data.PostBootMs));
        Assert.Equal(new[] { "Slow Helper", "Slow Service" }, data.Items.Select(i => i.Name)); // the old driver belongs to an earlier start
        Assert.Equal(@"C:\Apps\Slow\slow.exe", data.Items[0].Path);
        Assert.Equal(("App", 3100L, BootImpact.High), (data.Items[0].Kind, data.Items[0].DelayMs, data.Items[0].Impact));
        Assert.Equal(("Service", 900L, BootImpact.Medium), (data.Items[1].Kind, data.Items[1].DelayMs, data.Items[1].Impact));
    }

    [Fact]
    public void Windows_boot_events_are_null_until_this_start_is_logged()
    {
        Assert.Null(BootTimer.ParseWindowsEvents(Events, new DateTime(2026, 10, 13, 9, 0, 0, DateTimeKind.Utc)));
        Assert.Null(BootTimer.ParseWindowsEvents("", DateTime.UtcNow));
        Assert.Null(BootTimer.ParseWindowsEvents("<not xml", DateTime.UtcNow));
    }
}
