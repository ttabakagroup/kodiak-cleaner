using KodiakCleaner.Core.Tools;

namespace KodiakCleaner.Core.Tests;

public sealed class MoreToolsTests : IDisposable
{
    // Beside the test assembly, not in %TEMP%: on Windows %TEMP% is inside AppData, which Empty Folders never searches (0.8.48).
    private readonly string _root = Path.Combine(AppContext.BaseDirectory, "kodiakcleaner-more-" + Guid.NewGuid().ToString("N"));

    public MoreToolsTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch (IOException) { }
    }

    private string Write(string relative, int bytes = 0, string? text = null)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (text is not null) File.WriteAllText(path, text);
        else File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }

    private string Dir(string relative) => Directory.CreateDirectory(Path.Combine(_root, relative)).FullName;

    [Fact]
    public void Disk_analyzer_sizes_folders_and_lists_largest_files()
    {
        Write(@"big/a.mp4", 5000);
        Write(@"big/deeper/b.zip", 3000);
        Write(@"small/c.bin", 100);
        Write("loose.jpg", 50);

        var scan = DiskAnalyzer.Scan(new[] { (_root, "") }, largestCount: 2);
        var root = scan.Root;

        Assert.Equal(8150, root.Bytes);
        Assert.Equal(4, root.Files);
        Assert.Equal(3, root.Folders);
        Assert.Equal(50, root.OwnBytes);
        Assert.Equal("big", root.Children[0].Name); // biggest first
        Assert.Equal(8000, root.Children[0].Bytes);
        Assert.Equal(2, root.Children[0].Files);
        Assert.Equal(3000, root.Children[0].Children[0].Bytes);
        Assert.Equal(5000, root.KindBytes[(int)FileKind.Videos]);
        Assert.Equal(3000, root.KindBytes[(int)FileKind.Archives]);
        Assert.Equal(50, root.KindBytes[(int)FileKind.Pictures]);
        Assert.Equal(FileKind.Videos, root.MainKind);
        Assert.Equal(FileKind.Pictures, root.OwnMainKind);
        Assert.Equal(new[] { "a.mp4", "b.zip" }, scan.LargestFiles.Select(f => f.Name));
        Assert.Equal(new[] { "loose.jpg" }, DiskAnalyzer.FilesIn(root).Select(f => f.Name));
        Assert.Equal(new[] { root, root.Children[0], root.Children[0].Children[0] }, root.Children[0].Children[0].Trail());

        // Moving a file to the Recycle Bin takes it off every folder above it.
        var deeper = root.Children[0].Children[0];
        deeper.FileRemoved(new DiskFile(Path.Combine(deeper.Path, "b.zip"), "b.zip", 3000, DateTime.Now));
        Assert.Equal(5150, root.Bytes);
        Assert.Equal(0, root.KindBytes[(int)FileKind.Archives]);
    }

    [Fact]
    public void Disk_analyzer_groups_several_roots_under_all_drives()
    {
        Write(@"one/a.bin", 10);
        Write(@"two/b.bin", 30);

        var scan = DiskAnalyzer.Scan(new[] { (Path.Combine(_root, "one"), "One"), (Path.Combine(_root, "two"), "Two") });

        Assert.True(scan.Root.IsGroup);
        Assert.Equal(DiskAnalyzer.AllDrivesName, scan.Root.Name);
        Assert.Equal(40, scan.Root.Bytes);
        Assert.Equal(new[] { "Two", "One" }, scan.Root.Children.Select(c => c.Name));
        Assert.True(DiskAnalyzer.IsUnder(Path.Combine(_root, "two", "b.bin"), scan.Root.Children[0]));
        Assert.False(DiskAnalyzer.IsUnder(Path.Combine(_root, "one", "a.bin"), scan.Root.Children[0]));
    }

    [Fact]
    public void Treemap_tiles_fill_the_box_in_proportion()
    {
        var sizes = new double[] { 6, 6, 4, 3, 2, 2, 1, 0 };
        var tiles = Treemap.Layout(sizes, 0, 0, 600, 400);

        Assert.Equal(7, tiles.Count); // the zero gets no tile
        Assert.Equal(600 * 400, tiles.Sum(t => t.Width * t.Height), 3);
        foreach (var t in tiles)
        {
            Assert.Equal(600 * 400 * sizes[t.Index] / 24, t.Width * t.Height, 3);
            Assert.InRange(t.X, -0.001, 600);
            Assert.InRange(t.X + t.Width, 0, 600.001);
            Assert.InRange(t.Y + t.Height, 0, 400.001);
        }
        Assert.Equal(FileKind.Videos, FileKinds.Of("Movie.MKV"));
        Assert.Equal(FileKind.Other, FileKinds.Of("README"));
    }

    [Fact]
    public void Empty_folder_finder_never_enters_or_lists_protected_folders()
    {
        Dir(@"AppData/Local/Vendor/cache");
        Dir(@"AppData/Roaming/Other");
        Dir(@"Documents/old");
        var appData = Path.Combine(_root, "AppData");

        var found = EmptyFolderFinder.Find(_root, new[] { appData })
            .Select(p => Path.GetRelativePath(_root, p).Replace('\\', '/')).ToList();

        Assert.Equal(new[] { "Documents" }, found); // AppData and everything in it stays, even though it's empty
        Assert.False(EmptyFolderFinder.IsStillEmpty(Path.Combine(appData, "Roaming", "Other"), new[] { appData }));
        Assert.True(EmptyFolderFinder.IsStillEmpty(Path.Combine(_root, "Documents"), new[] { appData }));
    }

    [Fact]
    public void Kept_folders_are_searched_inside_but_never_listed()
    {
        Dir(@"Desktop");
        Dir(@"Pictures/Old Album");
        var kept = new[] { Path.Combine(_root, "Desktop"), Path.Combine(_root, "Pictures") };

        var found = EmptyFolderFinder.Find(_root, Array.Empty<string>(), kept)
            .Select(p => Path.GetRelativePath(_root, p).Replace('\\', '/')).ToList();

        Assert.Equal(new[] { "Pictures/Old Album" }, found);
        Assert.False(EmptyFolderFinder.IsStillEmpty(Path.Combine(_root, "Desktop"), Array.Empty<string>(), kept));
    }

    [Fact]
    public void Empty_folder_finder_lists_only_the_outermost_empty_folders()
    {
        Dir(@"empty/inner/deeper");
        Dir(@"mixed/emptychild");
        Write(@"mixed/keep.txt", 1);
        Write(@"full/sub/file.txt", 1);

        var found = EmptyFolderFinder.Find(_root).Select(p => Path.GetRelativePath(_root, p).Replace('\\', '/')).ToList();

        Assert.Equal(new[] { "empty", "mixed/emptychild" }, found);
        Assert.True(EmptyFolderFinder.IsStillEmpty(Path.Combine(_root, "empty")));
        Write(@"empty/inner/new.txt", 1);
        Assert.False(EmptyFolderFinder.IsStillEmpty(Path.Combine(_root, "empty")));
    }

    [Fact]
    public void Empty_folder_finder_never_enters_another_users_AppData()
    {
        // Searching C:\Users: only the signed-in user's AppData is in the protected list, but every profile has one.
        var mine = Dir(@"Users/me/AppData");
        Dir(@"Users/other/AppData/Local/Vendor/cache");
        Dir(@"Users/Default/AppData/Roaming/Microsoft/Windows/Templates");
        Dir(@"Users/other/Documents/old");
        var users = Path.Combine(_root, "Users");

        var found = EmptyFolderFinder.Find(users, new[] { mine })
            .Select(p => Path.GetRelativePath(users, p).Replace('\\', '/')).ToList();

        Assert.Equal(new[] { "other/Documents" }, found);
        Assert.NotNull(EmptyFolderFinder.WhyNotAllowed(Path.Combine(users, "other", "AppData", "Local"), new[] { mine }));
        Assert.False(EmptyFolderFinder.IsStillEmpty(Path.Combine(users, "other", "AppData", "Local", "Vendor"), new[] { mine }));
    }

    [Fact]
    public void Empty_folder_finder_refuses_drives_and_protected_folders()
    {
        var windows = Dir("Windows");
        Assert.NotNull(EmptyFolderFinder.WhyNotAllowed(Path.GetPathRoot(_root)!, Array.Empty<string>()));
        Assert.NotNull(EmptyFolderFinder.WhyNotAllowed(windows, new[] { windows }));
        Assert.NotNull(EmptyFolderFinder.WhyNotAllowed(Path.Combine(windows, "System32"), new[] { windows }));
        Assert.Null(EmptyFolderFinder.WhyNotAllowed(Dir("Documents"), new[] { windows }));
        Assert.NotNull(EmptyFolderFinder.WhyNotAllowed("relative/path", Array.Empty<string>()));
    }

    [Fact]
    public void Chromium_extensions_use_localized_names_and_preferences()
    {
        var userData = Path.Combine(_root, "User Data");
        Write(@"User Data/Default/Extensions/aaa/1.0.0_0/manifest.json", text: """{ "name": "__MSG_appName__", "version": "1.0.0", "default_locale": "en" }""");
        Write(@"User Data/Default/Extensions/aaa/1.0.0_0/_locales/en/messages.json", text: """{ "APPNAME": { "message": "Handy Helper" } }""");
        Write(@"User Data/Default/Extensions/aaa/0.9.0_0/manifest.json", text: """{ "name": "Old Copy", "version": "0.9.0" }""");
        Write(@"User Data/Default/Extensions/bbb/2.1_0/manifest.json", text: """{ "name": "Dark Look", "version": "2.1", "theme": {} }""");
        Write(@"User Data/Default/Secure Preferences", text: """{ "extensions": { "settings": { "aaa": { "state": 1 }, "bbb": { "disable_reasons": [1] } } } }""");
        Write(@"User Data/System Profile/Extensions/ccc/1.0_0/manifest.json", text: """{ "name": "Not A Profile", "version": "1.0" }""");

        var list = BrowserExtensions.ReadChromium("Google Chrome", userData).OrderBy(e => e.Id).ToList();

        Assert.Equal(2, list.Count);
        Assert.Equal("Handy Helper", list[0].Name);
        Assert.Equal("1.0.0", list[0].Version);
        Assert.True(list[0].Enabled);
        Assert.Equal("Default profile", list[0].Profile);
        Assert.Equal("Dark Look (theme)", list[1].Name);
        Assert.False(list[1].Enabled);
    }

    [Fact]
    public void Firefox_lists_only_add_ons_the_person_installed()
    {
        var profiles = Path.Combine(_root, "Profiles");
        Write(@"Profiles/abc123.default-release/extensions.json", text: """
            { "addons": [
              { "id": "helper@example.com", "type": "extension", "location": "app-profile", "version": "3.2", "active": false, "defaultLocale": { "name": "Tab Helper" } },
              { "id": "builtin@mozilla.org", "type": "extension", "location": "app-builtin", "version": "1.0", "active": true, "defaultLocale": { "name": "Built In" } },
              { "id": "en-US@dictionaries", "type": "dictionary", "location": "app-profile", "version": "1.0", "active": true }
            ] }
            """);

        var list = BrowserExtensions.ReadFirefox(profiles).ToList();

        var only = Assert.Single(list);
        Assert.Equal("Tab Helper", only.Name);
        Assert.Equal("default-release", only.Profile);
        Assert.False(only.Enabled);
        Assert.Equal("about:addons", BrowserExtensions.ManagePage(only.Browser));
    }

    [Fact]
    public void Empty_folder_finder_searches_whole_drives_but_skips_hidden_and_windows_folders()
    {
        // A whole drive is allowed only when asked for; a folder is always allowed.
        var drive = Path.GetPathRoot(_root)!;
        Assert.NotNull(EmptyFolderFinder.WhyNotAllowed(drive, Array.Empty<string>()));
        Assert.Null(EmptyFolderFinder.WhyNotAllowed(drive, Array.Empty<string>(), allowDrive: true));
        // Hidden folders (a .git folder's empty refs, say) are never entered or listed.
        var hidden = Dir(".git/refs/tags");
        File.SetAttributes(Path.Combine(_root, ".git"), File.GetAttributes(Path.Combine(_root, ".git")) | FileAttributes.Hidden);
        Dir("plain/empty");
        var found = EmptyFolderFinder.Find(_root).Select(p => Path.GetRelativePath(_root, p).Replace('\\', '/')).ToList();
        Assert.Equal(new[] { "plain" }, found);
        Assert.True(Directory.Exists(hidden));
        Assert.True(EmptyFolderFinder.IsDriveSystemFolder("$Recycle.Bin"));
        Assert.True(EmptyFolderFinder.IsDriveSystemFolder("Recovery"));
        Assert.False(EmptyFolderFinder.IsDriveSystemFolder("Games"));
    }

    [Fact]
    public void Extension_policies_keep_other_settings_and_tidy_up()
    {
        Assert.Equal(new[] { "a", "c", "b" }, ExtensionControl.WithBlocked(new[] { "a", "b", "c" }, "b", blocked: true));
        Assert.Equal(new[] { "a", "c" }, ExtensionControl.WithBlocked(new[] { "a", "b", "c", "b" }, "b", blocked: false));

        var company = """{"*":{"installation_mode":"allowed"}}""";
        var removed = ExtensionControl.WithRemoved(company, "abc", removed: true)!;
        Assert.True(ExtensionControl.IsMarkedRemoved(removed, "abc"));
        Assert.Contains("\"*\"", removed);
        // Taking the mark out leaves what a company set exactly as it was, and nothing at all when only ours was there.
        Assert.False(ExtensionControl.IsMarkedRemoved(ExtensionControl.WithRemoved(removed, "abc", removed: false), "abc"));
        Assert.Contains("allowed", ExtensionControl.WithRemoved(removed, "abc", removed: false));
        Assert.Null(ExtensionControl.WithRemoved(ExtensionControl.WithRemoved(null, "abc", removed: true), "abc", removed: false));
        Assert.True(ExtensionControl.IsMarkedRemoved(ExtensionControl.WithRemoved("not json", "abc", removed: true), "abc"));

        var chrome = new BrowserExtension("Google Chrome", "Default profile", "abc", "Thing", "1", true, _root);
        var firefox = new BrowserExtension("Mozilla Firefox", "default", "thing@example.org", "Thing", "1", true, Dir("ff/extensions"));
        var vivaldi = chrome with { Browser = "Vivaldi" };
        Assert.True(ExtensionControl.CanTurnOff(chrome));
        Assert.False(ExtensionControl.CanTurnOff(firefox));
        Assert.False(ExtensionControl.CanRemove(firefox)); // no add-on file yet
        Write("ff/extensions/thing@example.org.xpi", 10);
        Assert.True(ExtensionControl.CanRemove(firefox));
        Assert.False(ExtensionControl.CanRemove(vivaldi));
        Assert.Contains("about:addons", ExtensionControl.WhyNot(firefox));
    }
}
