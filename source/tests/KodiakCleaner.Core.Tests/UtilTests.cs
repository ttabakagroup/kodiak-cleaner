using KodiakCleaner.Core.Startup;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.Core.Tests;

public class CommandLineTests
{
    [Theory]
    [InlineData("\"C:\\Program Files\\App\\app.exe\" --min", "C:\\Program Files\\App\\app.exe", "--min")]
    [InlineData("C:\\Program Files\\App\\app.exe /background", "C:\\Program Files\\App\\app.exe", "/background")]
    [InlineData("C:\\Tools\\tool.exe", "C:\\Tools\\tool.exe", "")]
    [InlineData("rundll32.exe C:\\x\\y.dll,Entry", "rundll32.exe", "C:\\x\\y.dll,Entry")]
    [InlineData("C:\\Windows\\system32\\foo.dll,Run", "C:\\Windows\\system32\\foo.dll", "Run")]
    [InlineData("MsiExec.exe /X{1234}", "MsiExec.exe", "/X{1234}")]
    [InlineData("\"C:\\unterminated\\app.exe", "C:\\unterminated\\app.exe", "")]
    [InlineData("", "", "")]
    // The earliest program path wins, whatever its extension: a script that takes a program as its argument.
    [InlineData("C:\\Tools\\start.cmd C:\\Apps\\app.exe", "C:\\Tools\\start.cmd", "C:\\Apps\\app.exe")]
    [InlineData("C:\\Scripts\\sync.bat --run C:\\Apps\\app.exe /quiet", "C:\\Scripts\\sync.bat", "--run C:\\Apps\\app.exe /quiet")]
    public void Split_extracts_executable_and_arguments(string input, string exe, string args)
    {
        var (e, a) = CommandLine.Split(input);
        Assert.Equal(exe, e);
        Assert.Equal(args, a);
    }
}

public class PathSafetyTests
{
    [Fact]
    public void Is_inside_means_below_the_folder_only()
    {
        var root = Path.GetPathRoot(Path.GetTempPath())!;
        var a = Path.Combine(root, "data");
        Assert.True(PathSafety.IsInside(Path.Combine(a, "x"), a));
        Assert.True(PathSafety.IsInside(a, root));
        Assert.False(PathSafety.IsInside(a, a));
        Assert.False(PathSafety.IsInside(a + "2", a));
    }

    [Fact]
    public void Rejects_root_relative_and_unexpanded_paths()
    {
        var root = Path.GetPathRoot(Path.GetTempPath())!;
        Assert.False(PathSafety.IsSafeCleanRoot(root, recursive: true, "*"));
        Assert.False(PathSafety.IsSafeCleanRoot("relative/dir", recursive: true, "*"));
        Assert.False(PathSafety.IsSafeCleanRoot(Path.Combine(root, "%TEMP%", "x"), recursive: true, "*"));
        Assert.False(PathSafety.IsSafeCleanRoot(null, recursive: true, "*"));
    }

    [Fact]
    public void Recursive_needs_two_levels_non_recursive_needs_a_pattern()
    {
        var root = Path.GetPathRoot(Path.GetTempPath())!;
        var oneLevel = Path.Combine(root, "Windows");
        Assert.False(PathSafety.IsSafeCleanRoot(oneLevel, recursive: true, "*"));
        Assert.True(PathSafety.IsSafeCleanRoot(Path.Combine(oneLevel, "Temp"), recursive: true, "*"));
        Assert.True(PathSafety.IsSafeCleanRoot(oneLevel, recursive: false, "MEMORY.DMP"));
        Assert.False(PathSafety.IsSafeCleanRoot(oneLevel, recursive: false, "*"));
    }
}

public class ByteSizeTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(5L * 1024 * 1024 * 1024, "5.0 GB")]
    public void Formats(long bytes, string expected) => Assert.Equal(expected, ByteSize.Format(bytes));
}

public class StartupApprovedValueTests
{
    [Fact]
    public void Missing_value_means_enabled() => Assert.True(StartupApprovedValue.IsEnabled(null));

    [Fact]
    public void Round_trips()
    {
        Assert.True(StartupApprovedValue.IsEnabled(StartupApprovedValue.Create(true)));
        var disabled = StartupApprovedValue.Create(false, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        Assert.False(StartupApprovedValue.IsEnabled(disabled));
        Assert.Equal(12, disabled.Length);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToFileTimeUtc(), BitConverter.ToInt64(disabled, 4));
    }

    [Theory]
    [InlineData(new byte[] { 0x02, 0, 0, 0 }, true)]
    [InlineData(new byte[] { 0x06, 0, 0, 0 }, true)]
    [InlineData(new byte[] { 0x03, 0, 0, 0 }, false)]
    [InlineData(new byte[] { 0x07, 0, 0, 0 }, false)]
    public void Reads_values_written_by_task_manager(byte[] data, bool enabled) =>
        Assert.Equal(enabled, StartupApprovedValue.IsEnabled(data));
}

public class CheckablePathTests
{
    [Theory]
    [InlineData(@"C:\Tools\updater --quiet", "")]                              // no extension: only a guess
    [InlineData(@"C:\Program Files\App\app.exe /min", @"C:\Program Files\App\app.exe")]
    [InlineData(@"""C:\Odd Name\runner"" --x", @"C:\Odd Name\runner")]       // quoted: not a guess
    [InlineData(@"wscript.exe C:\s\a.vbs", "wscript.exe")]
    public void Checkable_path_is_empty_when_the_program_was_guessed(string command, string expected) =>
        Assert.Equal(expected, CommandLine.CheckablePath(command));
}

public class SettingsSearchTests
{
    private static readonly string[] Appearance = ["Appearance", "Color scheme, accent color and light or dark mode.", "Accent Color", "Animations"];

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("dark")]
    [InlineData("DARK MODE")]
    [InlineData("mode dark")]
    [InlineData("accent")]
    public void Every_typed_word_must_appear(string query) => Assert.True(SettingsSearch.Matches(query, Appearance));

    [Theory]
    [InlineData("tray")]
    [InlineData("dark tray")]
    public void A_missing_word_hides_the_page(string query) => Assert.False(SettingsSearch.Matches(query, Appearance));

    [Fact]
    public void Hints_name_the_matching_labels()
    {
        Assert.Equal(["Accent Color"], SettingsSearch.Hints("accent", ["Accent Color", "Animations", "accent color"]));
        Assert.Empty(SettingsSearch.Hints("", ["Accent Color"]));
        Assert.Equal(2, SettingsSearch.Hints("a", ["Accent Color", "Animations", "Appearance"]).Count);
        Assert.Equal(["Light or Dark"], SettingsSearch.Hints("dark", ["Honey theme in light and dark, with a long description after it.", "Light or Dark"], max: 1));
    }
}

public class ProgressClockTests
{
    private static readonly DateTime T0 = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Elapsed_and_remaining_read_plainly()
    {
        Assert.Equal("0:42", KodiakCleaner.Core.Util.ProgressClock.FormatElapsed(TimeSpan.FromSeconds(42)));
        Assert.Equal("12:05", KodiakCleaner.Core.Util.ProgressClock.FormatElapsed(TimeSpan.FromSeconds(725)));
        Assert.Equal("1:02:03", KodiakCleaner.Core.Util.ProgressClock.FormatElapsed(new TimeSpan(1, 2, 3)));
        Assert.Equal("almost done", KodiakCleaner.Core.Util.ProgressClock.FormatRemaining(TimeSpan.FromSeconds(3)));
        Assert.Equal("less than a minute left", KodiakCleaner.Core.Util.ProgressClock.FormatRemaining(TimeSpan.FromSeconds(40)));
        Assert.Equal("about 3 min left", KodiakCleaner.Core.Util.ProgressClock.FormatRemaining(TimeSpan.FromSeconds(150)));
        Assert.Equal("about 1 h 20 min left", KodiakCleaner.Core.Util.ProgressClock.FormatRemaining(TimeSpan.FromMinutes(80)));
        Assert.Equal("about 2 h left", KodiakCleaner.Core.Util.ProgressClock.FormatRemaining(TimeSpan.FromMinutes(120)));
    }

    [Fact]
    public void Time_left_waits_for_enough_progress_then_counts_down()
    {
        var c = new KodiakCleaner.Core.Util.ProgressClock(T0);
        Assert.Equal("0:00 elapsed", c.Text(T0));
        c.Report(0, T0);
        c.Report(1, T0.AddSeconds(10));
        Assert.Null(c.Remaining(T0.AddSeconds(10))); // 1% isn't enough to go on
        Assert.Equal("0:10 elapsed · working out the time left", c.Text(T0.AddSeconds(10)));
        c.Report(25, T0.AddSeconds(60));
        Assert.Equal(TimeSpan.FromSeconds(180), c.Remaining(T0.AddSeconds(60)));
        Assert.Equal(TimeSpan.FromSeconds(170), c.Remaining(T0.AddSeconds(70)));
        Assert.Equal("1:10 elapsed · about 3 min left (rough guess)", c.Text(T0.AddSeconds(70), rough: true));
    }

    [Fact]
    public void A_new_stage_starts_the_speed_over()
    {
        var c = new KodiakCleaner.Core.Util.ProgressClock(T0);
        c.Report(50, T0.AddSeconds(100));
        c.Report(5, T0.AddSeconds(120)); // the next stage starts again near 0
        Assert.Null(c.Remaining(T0.AddSeconds(120)));
        c.Report(55, T0.AddSeconds(140));
        Assert.Equal(TimeSpan.FromSeconds(18), c.Remaining(T0.AddSeconds(140)));
        c.Report(null, T0.AddSeconds(150));
        Assert.Equal("2:30 elapsed", c.Text(T0.AddSeconds(150)));
    }

    [Fact]
    public void A_stalled_percentage_makes_the_estimate_grow_instead_of_sitting_at_zero()
    {
        var c = new KodiakCleaner.Core.Util.ProgressClock(T0);
        c.Report(0, T0);
        c.Report(50, T0.AddSeconds(60)); // 60 s left by that speed
        Assert.Equal(TimeSpan.FromSeconds(120), c.Remaining(T0.AddSeconds(120))); // still 50% a minute later: 2 min left by the slower speed
    }
}
