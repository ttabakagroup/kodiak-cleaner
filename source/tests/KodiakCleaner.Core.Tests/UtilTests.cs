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
