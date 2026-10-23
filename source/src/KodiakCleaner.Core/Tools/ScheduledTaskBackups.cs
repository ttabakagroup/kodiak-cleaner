using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KodiakCleaner.Core.Tools;

/// <summary>One saved copy of a scheduled task: its Task Scheduler XML file and where the task lived.</summary>
public sealed record TaskBackup(string File, string TaskPath, string Program, DateTime SavedAt, string Reason)
{
    /// <summary>The task's own name, the last part of its path ("\Google\Update" is "Update").</summary>
    public string Name => TaskPath[(TaskPath.LastIndexOf('\\') + 1)..];

    /// <summary>The Task Scheduler folder the task was in, "\" for the top.</summary>
    public string Folder => TaskPath.LastIndexOf('\\') is var i and > 0 ? TaskPath[..i] : "\\";
}

/// <summary>
/// Backups\Scheduled Tasks (Ted, 2026-10-23): every task is saved as the same XML Task Scheduler's own Export writes
/// (so Task Scheduler's Import Task can read it too) before Kodiak Cleaner deletes or replaces it, or when asked.
/// index.json remembers each file's task path, program and reason; a file without an entry (copied in by hand)
/// still shows, using the URI inside its XML.
/// </summary>
public sealed class ScheduledTaskBackups
{
    public const string FolderName = "Scheduled Tasks";
    public const string BeforeDelete = "Before Delete", Manual = "Backed Up", BeforeRestore = "Before Restore";
    private const string IndexName = "index.json";
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public ScheduledTaskBackups(string folder) => Folder = folder;

    public string Folder { get; }

    /// <summary>Writes the task's XML as UTF-16 (what its own header says) and records it. Returns the file's path.</summary>
    public string Save(string taskPath, string xml, string program, string reason, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(xml)) throw new InvalidDataException("Windows gave back an empty task.");
        Directory.CreateDirectory(Folder);
        var stem = SafeFileName(taskPath.Trim('\\').Replace('\\', ' ').Trim()) + " " + now.ToString("yyyy-MM-dd HHmmss");
        var path = Path.Combine(Folder, stem + ".xml");
        for (var n = 2; File.Exists(path); n++) path = Path.Combine(Folder, $"{stem} ({n}).xml");
        File.WriteAllText(path, xml, new UnicodeEncoding(false, true));

        var index = ReadIndex();
        index.Add(new TaskBackup(Path.GetFileName(path), taskPath, program, now, reason));
        var temp = Path.Combine(Folder, IndexName + ".tmp");
        File.WriteAllText(temp, JsonSerializer.Serialize(index, Json));
        File.Move(temp, Path.Combine(Folder, IndexName), true);
        return path;
    }

    /// <summary>Every backup still on disk, A to Z by task name, newest first for the same task.</summary>
    public List<TaskBackup> List()
    {
        if (!Directory.Exists(Folder)) return new List<TaskBackup>();
        var known = ReadIndex()
            .GroupBy(b => b.File, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);
        var list = new List<TaskBackup>();
        foreach (var file in Directory.EnumerateFiles(Folder, "*.xml"))
        {
            var name = Path.GetFileName(file);
            if (known.TryGetValue(name, out var b)) { list.Add(b with { File = name }); continue; }
            string xml;
            try { xml = File.ReadAllText(file); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { continue; }
            if (!xml.Contains("<Task", StringComparison.Ordinal)) continue;
            list.Add(new TaskBackup(name, UriFrom(xml) ?? "\\" + Path.GetFileNameWithoutExtension(name), "",
                File.GetLastWriteTime(file), Manual));
        }
        return list.OrderBy(b => b.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(b => b.Folder, StringComparer.CurrentCultureIgnoreCase)
            .ThenByDescending(b => b.SavedAt).ToList();
    }

    public string PathOf(TaskBackup backup) => Path.Combine(Folder, backup.File);

    /// <summary>
    /// True when the task signs in with a saved password (LogonType Password). Windows never hands that password out, so its
    /// backup may fail and Kodiak Cleaner can't register it again; only Task Scheduler's Import Task, with the password, can.
    /// </summary>
    public static bool UsesPassword(string xml) => Regex.IsMatch(xml, @"<LogonType>\s*Password\s*</LogonType>");

    /// <summary>The task path in the XML's RegistrationInfo URI ("\Folder\Name"), or null.</summary>
    public static string? UriFrom(string xml)
    {
        var m = Regex.Match(xml, @"<URI>\s*([^<]+?)\s*</URI>");
        if (!m.Success) return null;
        var uri = System.Net.WebUtility.HtmlDecode(m.Groups[1].Value);
        return uri.StartsWith('\\') ? uri : "\\" + uri;
    }

    /// <summary>The name with characters Windows doesn't allow in file names replaced, never empty, at most 120 characters.</summary>
    public static string SafeFileName(string name)
    {
        var bad = Path.GetInvalidFileNameChars().Concat(new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' }).ToHashSet();
        var safe = new string(name.Select(c => bad.Contains(c) || char.IsControl(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
        if (safe.Length > 120) safe = safe[..120].TrimEnd();
        return safe.Length == 0 ? "Task" : safe;
    }

    private List<TaskBackup> ReadIndex()
    {
        var path = Path.Combine(Folder, IndexName);
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<List<TaskBackup>>(File.ReadAllText(path), Json) ?? new() : new();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }
}
