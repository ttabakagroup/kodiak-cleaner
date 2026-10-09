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
        Write(@"big/a.bin", 5000);
        Write(@"big/deeper/b.bin", 3000);
        Write(@"small/c.bin", 100);
        Write("loose.bin", 50);

        var report = DiskAnalyzer.Analyze(_root, largestCount: 2);

        Assert.Equal(8150, report.TotalBytes);
        Assert.Equal(4, report.Files);
        Assert.Equal("big", report.Children[0].Name);
        Assert.Equal(8000, report.Children[0].Bytes);
        Assert.Equal(2, report.Children[0].Files);
        Assert.Contains(report.Children, c => !c.IsFolder && c.Bytes == 50);
        Assert.Equal(new[] { "a.bin", "b.bin" }, report.LargestFiles.Select(f => f.Name));
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
}
