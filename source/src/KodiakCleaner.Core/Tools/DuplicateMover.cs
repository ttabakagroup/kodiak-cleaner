using System.Globalization;
using System.Text;
using System.Text.Json;

namespace KodiakCleaner.Core.Tools;

/// <summary>One duplicate moved out of the way: where it was, where it is now and the identical copy that stayed.</summary>
public sealed class DuplicateMove
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    /// <summary>The copy that was kept, with the same SHA-256.</summary>
    public string Kept { get; set; } = "";
    public long Size { get; set; }
    public string Hash { get; set; } = "";
    /// <summary>When it was put back where it came from; null while it is still in the folder.</summary>
    public DateTime? RestoredOn { get; set; }
}

/// <summary>One Remove Selected into a folder: its own dated folder with the moved files, a report and the list used to restore them.</summary>
public sealed class DuplicateMoveBatch
{
    public DateTime MovedOn { get; set; }
    public List<DuplicateMove> Moves { get; set; } = new();

    /// <summary>The dated folder the batch lives in (not saved; it's wherever the list was read from).</summary>
    [System.Text.Json.Serialization.JsonIgnore] public string Folder { get; set; } = "";
}

/// <summary>
/// Duplicate Finder's other way to remove copies (Ted, 2026-10-23): instead of the Recycle Bin, move them to a folder the person
/// picks. Each Remove Selected gets its own folder "Duplicates yyyy-MM-dd HHmmss" holding the files under their old drive and
/// folders (so nothing clashes and they stay organized), "Duplicate Report.csv" (kept copy, where each copy was, where it is now)
/// and moves.json, which Restore reads to put files back.
/// </summary>
public static class DuplicateMover
{
    public const string ListName = "moves.json";
    public const string ReportName = "Duplicate Report.csv";
    public const string BatchPrefix = "Duplicates ";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string BatchFolder(string target, DateTime when) =>
        Path.Combine(target, BatchPrefix + when.ToString("yyyy-MM-dd HHmmss", CultureInfo.InvariantCulture));

    /// <summary>Where a file goes inside a batch: C:\Users\Ted\a.txt becomes &lt;batch&gt;\C\Users\Ted\a.txt.</summary>
    public static string PlaceFor(string batch, string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full) ?? "";
        var rest = full[root.Length..];
        var rootName = root.Trim('\\', '/').Replace(":", "").Replace('\\', '_').Replace('/', '_');
        return rootName.Length == 0 ? Path.Combine(batch, rest) : Path.Combine(batch, rootName, rest);
    }

    /// <summary>
    /// Moves each file to a new batch in <paramref name="target"/> and writes the report and list, even when some files can't
    /// be moved. Returns the batch and the files that stayed put, with why.
    /// </summary>
    public static (DuplicateMoveBatch Batch, List<(string Path, string Problem)> Failed) Move(string target,
        IReadOnlyList<(DuplicateFile File, string Kept, string Hash)> items, DateTime when, Action<int>? progress = null, CancellationToken ct = default)
    {
        var batch = new DuplicateMoveBatch { MovedOn = when, Folder = BatchFolder(target, when) };
        for (int n = 2; Directory.Exists(batch.Folder); n++) batch.Folder = BatchFolder(target, when) + $" ({n})";
        Directory.CreateDirectory(batch.Folder);
        var failed = new List<(string, string)>();
        try
        {
            int done = 0;
            foreach (var (file, kept, hash) in items)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Invoke(done++);
                var to = PlaceFor(batch.Folder, file.Path);
                try
                {
                    var info = new FileInfo(file.Path);
                    if (!info.Exists) { failed.Add((file.Path, "It's no longer there.")); continue; }
                    if (info.Length != file.Size) { failed.Add((file.Path, "It changed since the search.")); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                    File.Move(file.Path, to);
                    batch.Moves.Add(new DuplicateMove { From = file.Path, To = to, Kept = kept, Size = file.Size, Hash = hash });
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                {
                    failed.Add((file.Path, e.Message));
                }
            }
        }
        finally
        {
            Save(batch);
        }
        return (batch, failed);
    }

    /// <summary>Every batch in <paramref name="target"/>, newest first. Folders without a readable list are left out.</summary>
    public static List<DuplicateMoveBatch> List(string target)
    {
        var found = new List<DuplicateMoveBatch>();
        if (!Directory.Exists(target)) return found;
        IEnumerable<string> folders;
        try { folders = Directory.EnumerateDirectories(target, BatchPrefix + "*").ToList(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return found; }
        foreach (var folder in folders)
        {
            try
            {
                var list = Path.Combine(folder, ListName);
                if (!File.Exists(list)) continue;
                if (JsonSerializer.Deserialize<DuplicateMoveBatch>(File.ReadAllText(list), Json) is not { } batch) continue;
                batch.Folder = folder;
                found.Add(batch);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException) { }
        }
        return found.OrderByDescending(b => b.MovedOn).ToList();
    }

    /// <summary>Puts a moved file back where it was and notes it in the batch. Returns why it couldn't, or null.</summary>
    public static string? Restore(DuplicateMoveBatch batch, DuplicateMove move, DateTime when)
    {
        if (move.RestoredOn is not null) return "It was already put back.";
        try
        {
            if (!File.Exists(move.To)) return "It's no longer in the duplicates folder.";
            if (File.Exists(move.From)) return "Another file is already at its old place.";
            Directory.CreateDirectory(Path.GetDirectoryName(move.From)!);
            File.Move(move.To, move.From);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return e.Message;
        }
        move.RestoredOn = when;
        Save(batch);
        return null;
    }

    /// <summary>Writes moves.json and the report beside the moved files.</summary>
    public static void Save(DuplicateMoveBatch batch)
    {
        Directory.CreateDirectory(batch.Folder);
        var list = Path.Combine(batch.Folder, ListName);
        File.WriteAllText(list + ".tmp", JsonSerializer.Serialize(batch, Json));
        File.Move(list + ".tmp", list, overwrite: true);
        File.WriteAllText(Path.Combine(batch.Folder, ReportName), Report(batch), new UTF8Encoding(true));
    }

    /// <summary>The report: one row per moved copy with the copy that was kept, where it was and where it is now.</summary>
    public static string Report(DuplicateMoveBatch batch)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Status,Kept Copy (Original),Duplicate Was At,Duplicate Is Now At,Size (Bytes),SHA-256,Moved,Restored");
        foreach (var m in batch.Moves.OrderBy(m => m.Kept, StringComparer.OrdinalIgnoreCase).ThenBy(m => m.From, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine(string.Join(",",
                Csv(m.RestoredOn is null ? "Moved" : "Restored"), Csv(m.Kept), Csv(m.From), Csv(m.RestoredOn is null ? m.To : m.From),
                m.Size.ToString(CultureInfo.InvariantCulture), Csv(m.Hash),
                Csv(batch.MovedOn.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                Csv(m.RestoredOn?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "")));
        }
        return sb.ToString();
    }

    private static string Csv(string value) =>
        value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
}
