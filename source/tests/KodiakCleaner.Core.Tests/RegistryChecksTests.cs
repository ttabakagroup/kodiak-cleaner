using KodiakCleaner.Core.RegistryTools;
using Xunit;

namespace KodiakCleaner.Core.Tests;

public class RegistryChecksTests
{
    [Fact]
    public void Look_for_has_Teds_fifteen_checks_a_to_z()
    {
        // Ted's list (0.8.30), shown A to Z per his standing rule.
        var teds = new[]
        {
            "ActiveX and Class Issues", "Applications", "Application Paths", "Fonts", "Help Files", "Installer", "Missing Shared DLLs",
            "MUI Cache", "Obsolete Software", "Run At Startup", "Sound Events", "Start Menu Ordering", "Type Libraries",
            "Unused File Extensions", "Windows Services",
        };
#pragma warning disable CA1416 // only the category names are read
        var categories = RegistryScanner.Categories;
#pragma warning restore CA1416
        Assert.Equal(teds.OrderBy(c => c), categories.OrderBy(c => c));
        Assert.Equal(categories.OrderBy(c => c, StringComparer.CurrentCultureIgnoreCase), categories);
    }

    [Theory]
    [InlineData("arial.ttf", @"C:\Windows\Fonts\arial.ttf")]
    [InlineData(@"C:\Users\ted\AppData\Local\Microsoft\Windows\Fonts\Mine.otf", @"C:\Users\ted\AppData\Local\Microsoft\Windows\Fonts\Mine.otf")]
    [InlineData("sserife.fon,sseriff.fon", "")]
    [InlineData(@"sub\odd.ttf", "")]
    [InlineData("", "")]
    public void Font_values_become_checkable_files(string data, string expected)
    {
        // Full Windows paths only count as absolute on Windows; elsewhere just the folder join is checked.
        if (!OperatingSystem.IsWindows() && data.Contains(':')) return;
        var file = RegistryPaths.FontFile(data, @"C:\Windows\Fonts");
        Assert.Equal(expected.Length == 0 ? "" : OperatingSystem.IsWindows() ? expected : Path.Combine(@"C:\Windows\Fonts", data), file);
    }

    [Theory]
    [InlineData(@"C:\Program Files\App\app.tlb", @"C:\Program Files\App\app.tlb")]
    [InlineData(@"C:\Program Files\App\app.dll\2", @"C:\Program Files\App\app.dll")]
    [InlineData("\"C:\\Program Files\\App\\app.dll\"", @"C:\Program Files\App\app.dll")]
    [InlineData("", "")]
    public void Type_library_paths_drop_the_resource_number(string data, string expected) =>
        Assert.Equal(expected, RegistryPaths.TypeLibFile(data));

    [Fact]
    public void Help_values_join_name_and_folder_only_when_safe()
    {
        Assert.Equal("", RegistryPaths.HelpFile(@"..\evil.chm", @"C:\Help"));
        Assert.Equal("", RegistryPaths.HelpFile("app.chm", "relative"));
        Assert.Equal("", RegistryPaths.HelpFile("", @"C:\Help"));
        if (OperatingSystem.IsWindows()) Assert.Equal(@"C:\Help\app.chm", RegistryPaths.HelpFile("app.chm", @"C:\Help"));
    }

    [Theory]
    [InlineData(@"C:\Windows\System32\x.dll", true)]
    [InlineData(@"c:\windows\SysWOW64\x.dll", true)]
    [InlineData(@"C:\WindowsApps\x.dll", false)]
    [InlineData(@"C:\Program Files\x.dll", false)]
    public void Windows_folder_files_are_recognized(string path, bool inside) =>
        Assert.Equal(inside, RegistryPaths.IsInFolder(path, @"C:\Windows"));

    [Fact]
    public void A_default_value_shows_as_default()
    {
#pragma warning disable CA1416
        var loc = new RegistryLocation(Microsoft.Win32.RegistryHive.CurrentUser, Microsoft.Win32.RegistryView.Registry64, @"AppEvents\Schemes\Apps\.Default\Ding\.Current", "");
#pragma warning restore CA1416
        Assert.EndsWith(@"\.Current : (Default)", loc.ToString());
    }
}
