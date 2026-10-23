using System.Text.Json;
using System.Text.Json.Serialization;

namespace KodiakCleaner.Core.Startup;

/// <summary>One startup entry Kodiak Cleaner has seen, now or in the past.</summary>
public sealed class StartupRecord
{
    public string Name { get; set; } = "";
    public string Command { get; set; } = "";
    public StartupSource Source { get; set; }
    /// <summary>The shortcut's file name, for Startup-folder entries; the task's full path, for scheduled tasks.</summary>
    public string? FileName { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    /// <summary>When it left startup; null while it is still there.</summary>
    public DateTime? RemovedOn { get; set; }
    /// <summary>true when Kodiak Cleaner removed it; false when something else did (noticed on a later look).</summary>
    public bool RemovedByKodiakCleaner { get; set; }
    /// <summary>Kodiak Cleaner's own copy of the shortcut (a file name in the history folder), so it can be put back.</summary>
    public string? ShortcutCopy { get; set; }
    /// <summary>The .reg backup made when Kodiak Cleaner removed it.</summary>
    public string? RegistryBackup { get; set; }
    /// <summary>How many times it came back after Kodiak Cleaner removed it (0.8.71).</summary>
    public int TimesReturned { get; set; }
    /// <summary>When it last came back after Kodiak Cleaner removed it: when it was written back, if Windows says.</summary>
    public DateTime? LastReturned { get; set; }
    /// <summary>Kodiak Cleaner's best guess at what put it back, noted when it came back.</summary>
    public string? ReturnedBy { get; set; }

    [JsonIgnore] public bool IsRemoved => RemovedOn is not null;
    [JsonIgnore] public string Key => KeyOf(Source, FileName ?? Name);

    public static string KeyOf(StartupSource source, string name) => $"{source}|{name}".ToUpperInvariant();
    public static string KeyOf(StartupItem item) => KeyOf(item.Source, NameOf(item));

    /// <summary>What tells entries apart: the task path, the shortcut's file name, or the registry value's name.</summary>
    private static string NameOf(StartupItem item) => item.TaskPath ?? (item.FilePath is not null ? Path.GetFileName(item.FilePath) : item.Name);
}

/// <summary>
/// Every startup entry Kodiak Cleaner has seen, kept in Backups\Startup History. Entries are recorded whenever the
/// Startup list is read (Kodiak Cleaner never watches in the background); one that has gone is marked removed, so it can be restored.
/// </summary>
public sealed class StartupHistory
{
    public const string FileName = "history.json";
    public const string FolderName = "Startup History";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    public List<StartupRecord> Records { get; set; } = new();

    /// <summary>Reads the history in <paramref name="folder"/>; a missing or damaged file gives an empty history.</summary>
    public static StartupHistory Load(string folder)
    {
        try
        {
            var path = Path.Combine(folder, FileName);
            if (!File.Exists(path)) return new StartupHistory();
            return JsonSerializer.Deserialize<StartupHistory>(File.ReadAllText(path), Json) ?? new StartupHistory();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return new StartupHistory();
        }
    }

    public void Save(string folder)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, FileName);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, Json));
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>
    /// Records what is in startup now. New entries are added, ones seen before are updated, and ones no longer there are
    /// marked removed. <paramref name="copyShortcut"/> keeps a copy of a Startup-folder shortcut and returns its file name.
    /// Returns the entries that came back after Kodiak Cleaner removed them, so the caller can find out why.
    /// Startup entries and scheduled tasks are read separately, so <paramref name="tasks"/> says which kind <paramref name="current"/>
    /// holds: only records of that kind are marked removed when they're missing.
    /// </summary>
    public List<(StartupItem Item, StartupRecord Record)> Observe(IEnumerable<StartupItem> current, DateTime now, Func<StartupItem, string?> copyShortcut,
        bool tasks = false)
    {
        var returned = new List<(StartupItem, StartupRecord)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in current)
        {
            var key = StartupRecord.KeyOf(item);
            seen.Add(key);
            // A removed entry that comes back is the same record again.
            var r = Records.FirstOrDefault(x => x.Key == key && !x.IsRemoved) ?? Records.LastOrDefault(x => x.Key == key);
            if (r is null)
            {
                r = new StartupRecord { FirstSeen = now };
                Records.Add(r);
            }
            else if (r.IsRemoved && r.RemovedByKodiakCleaner)
            {
                // Removed by Kodiak Cleaner and back again: the program (or its updater) put it back.
                r.TimesReturned++;
                r.LastReturned = now;
                r.ReturnedBy = null;
                returned.Add((item, r));
            }
            r.Name = item.Name;
            r.Command = item.Command;
            r.Source = item.Source;
            r.FileName = item.TaskPath ?? (item.FilePath is not null ? Path.GetFileName(item.FilePath) : null);
            r.Enabled = item.Enabled;
            r.LastSeen = now;
            r.RemovedOn = null;
            r.RemovedByKodiakCleaner = false;
            if (item.FilePath is not null && r.ShortcutCopy is null) r.ShortcutCopy = copyShortcut(item);
        }
        foreach (var r in Records.Where(r => !r.IsRemoved && !seen.Contains(r.Key) && (r.Source == StartupSource.ScheduledTask) == tasks))
            r.RemovedOn = now;
        return returned;
    }

    /// <summary>The record of an entry in startup now, or null when the history hasn't seen it.</summary>
    public StartupRecord? Find(StartupItem item)
    {
        var key = StartupRecord.KeyOf(item);
        return Records.FirstOrDefault(r => r.Key == key && !r.IsRemoved);
    }

    /// <summary>
    /// Entries in <paramref name="current"/> that aren't in startup as far as the history knows: new ones, and ones that
    /// came back after being removed. Used for the new startup programs pop-up (0.8.53).
    /// </summary>
    public List<StartupItem> NotYetSeen(IEnumerable<StartupItem> current)
    {
        var known = Records.Where(r => !r.IsRemoved).Select(r => r.Key).ToHashSet(StringComparer.Ordinal);
        return current.Where(i => !known.Contains(StartupRecord.KeyOf(i))).ToList();
    }

    /// <summary>Notes that Kodiak Cleaner removed this entry, with the backup it made.</summary>
    public void MarkRemoved(StartupItem item, string? registryBackup, DateTime now)
    {
        var key = StartupRecord.KeyOf(item);
        var r = Records.FirstOrDefault(x => x.Key == key && !x.IsRemoved);
        if (r is null) return;
        r.RemovedOn = now;
        r.RemovedByKodiakCleaner = true;
        r.RegistryBackup = registryBackup;
    }

    /// <summary>
    /// Notes that Kodiak Cleaner is putting this entry back, so seeing it again isn't counted as coming back by itself.
    /// </summary>
    public void MarkRestored(StartupRecord r) => r.RemovedByKodiakCleaner = false;

    /// <summary>
    /// Removed entries that can be put back: a registry entry with its command, a shortcut with a saved copy, or a scheduled
    /// task Kodiak Cleaner deleted (it always backs a task up first).
    /// </summary>
    public static bool CanRestore(StartupRecord r) =>
        r.IsRemoved && r.Source switch
        {
            StartupSource.UserStartupFolder or StartupSource.CommonStartupFolder => r.ShortcutCopy is not null,
            StartupSource.ScheduledTask => r.RemovedByKodiakCleaner,
            _ => r.Command.Length > 0,
        };
}
