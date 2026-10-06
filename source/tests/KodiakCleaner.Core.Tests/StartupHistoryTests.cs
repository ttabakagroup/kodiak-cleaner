using System.IO.Compression;
using System.Text.Json;
using KodiakCleaner.Core.Startup;
using KodiakCleaner.Core.Tools;
using Xunit;

namespace KodiakCleaner.Core.Tests;

public sealed class StartupHistoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kodiakcleaner-history-" + Guid.NewGuid().ToString("N"));
    public StartupHistoryTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static StartupItem Run(string name, string command, bool enabled = true) =>
        new() { Name = name, Command = command, Source = StartupSource.UserRunKey, Enabled = enabled };

    private static StartupItem Shortcut(string folder, string name) =>
        new() { Name = name, Command = Path.Combine(folder, name + ".lnk"), FilePath = Path.Combine(folder, name + ".lnk"), Source = StartupSource.UserStartupFolder, Enabled = true };

    private static readonly DateTime Day1 = new(2026, 10, 1, 9, 0, 0);
    private static readonly DateTime Day2 = new(2026, 10, 2, 9, 0, 0);
    private static readonly DateTime Day3 = new(2026, 10, 3, 9, 0, 0);

    [Fact]
    public void NewEntriesAreRecordedAndGoneOnesMarkedRemovedElsewhere()
    {
        var h = new StartupHistory();
        h.Observe(new[] { Run("Chat", "chat.exe"), Run("Sync", "sync.exe") }, Day1, _ => null);
        h.Observe(new[] { Run("Sync", "sync.exe --quiet") }, Day2, _ => null);

        var chat = Assert.Single(h.Records, r => r.Name == "Chat");
        Assert.Equal(Day2, chat.RemovedOn);
        Assert.False(chat.RemovedByKodiakCleaner);
        Assert.True(StartupHistory.CanRestore(chat));

        var sync = Assert.Single(h.Records, r => r.Name == "Sync");
        Assert.Null(sync.RemovedOn);
        Assert.Equal(Day1, sync.FirstSeen);
        Assert.Equal("sync.exe --quiet", sync.Command);
        Assert.False(StartupHistory.CanRestore(sync)); // still in startup
    }

    [Fact]
    public void RemovalByKodiakCleanerKeepsItsBackupAndAReturningEntryReusesItsRecord()
    {
        var h = new StartupHistory();
        var chat = Run("Chat", "chat.exe", enabled: false);
        h.Observe(new[] { chat }, Day1, _ => null);
        h.MarkRemoved(chat, @"C:\Backups\startup.reg", Day2);
        h.Observe(Array.Empty<StartupItem>(), Day2, _ => null);

        var r = Assert.Single(h.Records);
        Assert.True(r.RemovedByKodiakCleaner);
        Assert.Equal(Day2, r.RemovedOn);
        Assert.Equal(@"C:\Backups\startup.reg", r.RegistryBackup);
        Assert.False(r.Enabled); // restored off, as it was

        h.Observe(new[] { chat }, Day3, _ => null);
        r = Assert.Single(h.Records);
        Assert.Null(r.RemovedOn);
        Assert.Equal(Day1, r.FirstSeen);
    }

    [Fact]
    public void ShortcutsAreCopiedOnceAndOnlyRestorableWithACopy()
    {
        var h = new StartupHistory();
        int copies = 0;
        var item = Shortcut(_root, "Notes");
        h.Observe(new[] { item }, Day1, _ => { copies++; return "abc.lnk"; });
        h.Observe(new[] { item }, Day2, _ => { copies++; return "def.lnk"; });
        Assert.Equal(1, copies);
        Assert.Equal("abc.lnk", h.Records[0].ShortcutCopy);
        Assert.Equal("Notes.lnk", h.Records[0].FileName);

        h.Observe(Array.Empty<StartupItem>(), Day3, _ => null);
        Assert.True(StartupHistory.CanRestore(h.Records[0]));
        h.Records[0].ShortcutCopy = null;
        Assert.False(StartupHistory.CanRestore(h.Records[0]));
    }

    [Fact]
    public void HistorySavesAndLoadsAndADamagedFileGivesAnEmptyOne()
    {
        var h = new StartupHistory();
        h.Observe(new[] { Run("Chat", "chat.exe") }, Day1, _ => null);
        h.Save(_root);
        var loaded = StartupHistory.Load(_root);
        Assert.Equal("Chat", Assert.Single(loaded.Records).Name);
        Assert.Equal(StartupSource.UserRunKey, loaded.Records[0].Source);

        File.WriteAllText(Path.Combine(_root, StartupHistory.FileName), "{ not json");
        Assert.Empty(StartupHistory.Load(_root).Records);
        Assert.Empty(StartupHistory.Load(Path.Combine(_root, "missing")).Records);
    }

    [Fact]
    public void ExtensionIconPicksASizeOfAtLeast32AndSkipsSvg()
    {
        using var doc = JsonDocument.Parse("""{ "icons": { "16": "i16.png", "48": "i48.png", "128": "i128.png", "64": "logo.svg" } }""");
        Assert.Equal("i48.png", BrowserExtensions.BestIcon(doc.RootElement));
        using var small = JsonDocument.Parse("""{ "icons": { "16": "a.png", "19": "b.png" } }""");
        Assert.Equal("b.png", BrowserExtensions.BestIcon(small.RootElement));
        using var none = JsonDocument.Parse("""{ "name": "x" }""");
        Assert.Null(BrowserExtensions.BestIcon(none.RootElement));
    }

    [Fact]
    public void ExtensionIconsAreReadFromFoldersAndFirefoxPackages()
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3 };
        var file = Path.Combine(_root, "icon.png");
        File.WriteAllBytes(file, png);
        Assert.Equal(png, BrowserExtensions.ReadIcon(new BrowserExtension("Google Chrome", "", "", "x", "1", true, _root, file)));

        var xpi = Path.Combine(_root, "addon.xpi");
        using (var zip = ZipFile.Open(xpi, ZipArchiveMode.Create))
        using (var s = zip.CreateEntry("icons/icon-48.png").Open()) s.Write(png);
        Assert.Equal(png, BrowserExtensions.ReadIcon(new BrowserExtension("Mozilla Firefox", "", "", "x", "1", true, _root, xpi, "icons/icon-48.png")));
        Assert.Null(BrowserExtensions.ReadIcon(new BrowserExtension("Mozilla Firefox", "", "", "x", "1", true, _root, xpi, "missing.png")));
        Assert.Null(BrowserExtensions.ReadIcon(new BrowserExtension("Mozilla Firefox", "", "", "x", "1", true, _root)));
    }
}
