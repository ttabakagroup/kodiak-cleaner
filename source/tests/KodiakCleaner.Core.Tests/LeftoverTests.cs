using KodiakCleaner.Core.Programs;

namespace KodiakCleaner.Core.Tests;

public class LeftoverNamesTests
{
    [Theory]
    [InlineData("7-Zip 23.01 (x64)", new[] { "7-Zip 23.01 (x64)", "7-Zip" })]
    [InlineData("Notepad++ (64-bit x64)", new[] { "Notepad++ (64-bit x64)", "Notepad++" })]
    [InlineData("VLC media player", new[] { "VLC media player" })]
    [InlineData("Foo Tool v2.4.1 x64", new[] { "Foo Tool v2.4.1 x64", "Foo Tool" })]
    public void Program_names_drop_versions_and_editions(string name, string[] expected) =>
        Assert.Equal(expected, LeftoverNames.ForProgram(name));

    [Theory]
    [InlineData("Microsoft")]
    [InlineData("Steam")]
    [InlineData("Common Files")]
    [InlineData("AB")]
    [InlineData("..")]
    [InlineData(@"Evil\..\Windows")]
    [InlineData("2024")]
    public void Generic_short_or_unsafe_names_are_never_used(string name) =>
        Assert.Empty(LeftoverNames.ForProgram(name));

    [Fact]
    public void Publisher_names_drop_company_endings() =>
        Assert.Equal(new[] { "Piriform Software Ltd", "Piriform Software" }, LeftoverNames.ForPublisher("Piriform Software Ltd."));
}

public sealed class LeftoverFinderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kodiakcleaner-left-" + Guid.NewGuid().ToString("N"));

    public LeftoverFinderTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch (IOException) { }
    }

    private string Dir(string relative) => Directory.CreateDirectory(Path.Combine(_root, relative)).FullName;

    private string File(string relative)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, "x");
        return path;
    }

    private LeftoverPlaces Places(params string[] otherPrograms) => new()
    {
        AppFolders = new[] { (Dir("ProgramFiles"), "Program Files"), (Dir("Roaming"), "AppData\\Roaming") },
        StartMenus = new[] { Dir("StartMenu") },
        Desktops = new[] { Dir("Desktop") },
        Protected = new[] { Dir("Windows") },
        OtherPrograms = otherPrograms,
        OtherNames = new[] { "Shared Thing" },
    };

    private List<string> Rel(IEnumerable<Leftover> found) =>
        found.Select(f => Path.GetRelativePath(_root, f.Path).Replace('\\', '/')).ToList();

    [Fact]
    public void Finds_install_publisher_appdata_start_menu_and_desktop_leftovers()
    {
        File("ProgramFiles/Acme/Rocket Editor/data.bin");
        File("Roaming/Rocket Editor/settings.ini");
        Dir("StartMenu/Rocket Editor");
        File("Desktop/Rocket Editor.lnk");
        File("Desktop/Other.lnk");
        File("ProgramFiles/Unrelated/keep.txt");

        var found = LeftoverFinder.FindFiles("Rocket Editor 3.2", "Acme Inc.", "", Places());

        Assert.Equal(new[] { "ProgramFiles/Acme/Rocket Editor", "Roaming/Rocket Editor", "StartMenu/Rocket Editor", "Desktop/Rocket Editor.lnk" }, Rel(found));
    }

    [Fact]
    public void Install_location_is_used_only_inside_a_known_app_folder_and_never_when_it_is_a_root()
    {
        Dir("ProgramFiles/RE");
        Assert.Equal(new[] { "ProgramFiles/RE" }, Rel(LeftoverFinder.FindFiles("Rocket Editor", "", Path.Combine(_root, "ProgramFiles", "RE"), Places())));
        Assert.Empty(LeftoverFinder.FindFiles("Rocket Editor", "", Path.Combine(_root, "ProgramFiles"), Places()));
        Assert.Empty(LeftoverFinder.FindFiles("Rocket Editor", "", Dir("Elsewhere/RE"), Places()));
        Assert.Empty(LeftoverFinder.FindFiles("Rocket Editor", "", Path.Combine(_root, "Windows"), Places()));
    }

    [Fact]
    public void Never_offers_a_folder_holding_another_program_or_a_name_another_program_uses()
    {
        Dir("ProgramFiles/Rocket Editor/Plugins/Other App");
        Dir("ProgramFiles/Shared Thing");
        var other = Path.Combine(_root, "ProgramFiles", "Rocket Editor", "Plugins", "Other App");

        Assert.Empty(LeftoverFinder.FindFiles("Rocket Editor", "", "", Places(other)));
        Assert.Empty(LeftoverFinder.FindFiles("Shared Thing", "", "", Places()));
        // A badly written entry that claims a whole root doesn't block everything.
        Assert.Single(LeftoverFinder.FindFiles("Rocket Editor", "", "", Places(Path.Combine(_root, "ProgramFiles"))));
    }

    [Fact]
    public void Nested_matches_collapse_to_the_outermost_folder()
    {
        Dir("ProgramFiles/Rocket Editor/Rocket Editor");
        Assert.Equal(new[] { "ProgramFiles/Rocket Editor" }, Rel(LeftoverFinder.FindFiles("Rocket Editor", "", "", Places())));
    }
}
