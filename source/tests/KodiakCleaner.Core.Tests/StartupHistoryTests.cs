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
    public void NotYetSeenListsNewEntriesAndOnesThatCameBack()
    {
        var h = new StartupHistory();
        h.Observe(new[] { Run("Chat", "chat.exe"), Run("Sync", "sync.exe") }, Day1, _ => null);
        Assert.Empty(h.NotYetSeen(new[] { Run("Chat", "chat.exe --tray"), Run("Sync", "sync.exe") }));

        h.Observe(new[] { Run("Sync", "sync.exe") }, Day2, _ => null); // Chat left startup
        var fresh = h.NotYetSeen(new[] { Run("Chat", "chat.exe"), Run("Sync", "sync.exe"), Run("Updater", "upd.exe") });
        Assert.Equal(new[] { "Chat", "Updater" }, fresh.Select(f => f.Name));
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
    public void AnEntryThatComesBackAfterKodiakCleanerRemovedItIsCountedEachTime()
    {
        var h = new StartupHistory();
        var chat = Run("Chat", "chat.exe");
        Assert.Empty(h.Observe(new[] { chat }, Day1, _ => null));

        h.MarkRemoved(chat, null, Day1);
        Assert.Empty(h.Observe(Array.Empty<StartupItem>(), Day1, _ => null));
        var back = Assert.Single(h.Observe(new[] { chat }, Day2, _ => null));
        Assert.Equal("Chat", back.Item.Name);
        Assert.Same(h.Records[0], back.Record);
        Assert.Equal(1, back.Record.TimesReturned);
        Assert.Equal(Day2, back.Record.LastReturned);
        Assert.Same(back.Record, h.Find(chat));

        Assert.Empty(h.Observe(new[] { chat }, Day2, _ => null)); // still there: not a second return
        h.MarkRemoved(chat, null, Day2);
        h.Observe(Array.Empty<StartupItem>(), Day2, _ => null);
        Assert.Single(h.Observe(new[] { chat }, Day3, _ => null));
        Assert.Equal(2, h.Records[0].TimesReturned);
        Assert.Equal(Day3, h.Records[0].LastReturned);
    }

    [Fact]
    public void AnEntryRemovedElsewhereThatComesBackIsNotCounted()
    {
        var h = new StartupHistory();
        h.Observe(new[] { Run("Chat", "chat.exe") }, Day1, _ => null);
        h.Observe(Array.Empty<StartupItem>(), Day2, _ => null); // an uninstall took it out
        Assert.Empty(h.Observe(new[] { Run("Chat", "chat.exe") }, Day3, _ => null));
        Assert.Equal(0, h.Records[0].TimesReturned);
    }

    [Fact]
    public void ComebackPointsAtTheProgramItselfWhenItWasRunningFirst()
    {
        var program = @"C:\Users\Ted\AppData\Local\Chat\chat.exe";
        var running = new[]
        {
            new RunningProgram("explorer.exe", @"C:\Windows\explorer.exe", Day1),
            new RunningProgram("Chat.exe", program, Day2.AddMinutes(-1)),
        };
        var why = StartupComeback.Explain(program, Day2, running);
        Assert.StartsWith("chat.exe was running when it came back", why);
        Assert.EndsWith(StartupComeback.TurnOffTip, why);
        Assert.Contains("turn off its own startup setting", why);

        // Started only after the entry was written back, so it can't be what wrote it.
        var later = new[] { new RunningProgram("chat.exe", program, Day3) };
        Assert.StartsWith("chat.exe wasn't running", StartupComeback.Explain(program, Day2, later));
    }

    [Fact]
    public void ComebackPointsAtAHelperFromTheSameProgramFolder()
    {
        var program = @"C:\Users\Ted\AppData\Local\Chat\Update.exe";
        var running = new[] { new RunningProgram("Chat.exe", @"C:\Users\Ted\AppData\Local\Chat\app-1.2\Chat.exe", Day1) };
        Assert.StartsWith("Chat.exe, from the same program folder as Update.exe", StartupComeback.Explain(program, Day2, running));

        var updater = new[] { new RunningProgram("ChatUpdater.exe", @"C:\Program Files\Chat\ChatUpdater.exe", null) };
        Assert.StartsWith("ChatUpdater.exe, from the same program folder", StartupComeback.Explain(@"C:\Program Files\Chat\bin\chat.exe", null, updater));

        var stranger = new[] { new RunningProgram("other.exe", @"C:\Program Files\Other\other.exe", Day1) };
        Assert.StartsWith("chat.exe wasn't running", StartupComeback.Explain(@"C:\Program Files\Chat\chat.exe", Day2, stranger));
        Assert.StartsWith("Kodiak Cleaner couldn't tell", StartupComeback.Explain("", Day2, stranger));
    }

    [Fact]
    public void ComebackFieldsSaveAndLoad()
    {
        var h = new StartupHistory();
        var chat = Run("Chat", "chat.exe");
        h.Observe(new[] { chat }, Day1, _ => null);
        h.MarkRemoved(chat, null, Day1);
        h.Observe(new[] { chat }, Day2, _ => null)[0].Record.ReturnedBy = "chat.exe was running";
        h.Save(_root);
        var r = Assert.Single(StartupHistory.Load(_root).Records);
        Assert.Equal(1, r.TimesReturned);
        Assert.Equal(Day2, r.LastReturned);
        Assert.Equal("chat.exe was running", r.ReturnedBy);
    }

    private static StartupItem TaskEntry(string path, bool enabled = true) =>
        new() { Name = path[(path.LastIndexOf('\\') + 1)..], Command = "app.exe /background", Source = StartupSource.ScheduledTask, TaskPath = path, Enabled = enabled };

    [Fact]
    public void ScheduledTasksAndStartupEntriesAreObservedSeparately()
    {
        var h = new StartupHistory();
        h.Observe(new[] { Run("Chat", "chat.exe") }, Day1, _ => null);
        h.Observe(new[] { TaskEntry(@"\Sync\Sync Startup") }, Day1, _ => null, tasks: true);
        Assert.Equal(2, h.Records.Count);

        // Reading startup entries never marks tasks removed, and reading tasks never marks startup entries removed.
        h.Observe(new[] { Run("Chat", "chat.exe") }, Day2, _ => null);
        h.Observe(Array.Empty<StartupItem>(), Day2, _ => null, tasks: true);
        var task = Assert.Single(h.Records, r => r.Source == StartupSource.ScheduledTask);
        Assert.Equal(Day2, task.RemovedOn);
        Assert.Equal(@"\Sync\Sync Startup", task.FileName);
        Assert.Null(Assert.Single(h.Records, r => r.Name == "Chat").RemovedOn);
        Assert.False(StartupHistory.CanRestore(task)); // removed elsewhere: no backup of ours
    }

    [Fact]
    public void ADeletedTaskCanBeRestoredAndRestoringIsNotCountedAsComingBack()
    {
        var h = new StartupHistory();
        var task = TaskEntry(@"\Sync Startup");
        h.Observe(new[] { task }, Day1, _ => null, tasks: true);
        h.MarkRemoved(task, null, Day2);
        h.Observe(Array.Empty<StartupItem>(), Day2, _ => null, tasks: true);
        var r = Assert.Single(h.Records);
        Assert.True(StartupHistory.CanRestore(r));
        Assert.Equal("Scheduled Task", task.Location);

        h.MarkRestored(r);
        Assert.Empty(h.Observe(new[] { task }, Day3, _ => null, tasks: true));
        Assert.Equal(0, r.TimesReturned);

        // Two tasks with the same name in different folders are different entries.
        h.Observe(new[] { task, TaskEntry(@"\Other\Sync Startup") }, Day3, _ => null, tasks: true);
        Assert.Equal(2, h.Records.Count);
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
