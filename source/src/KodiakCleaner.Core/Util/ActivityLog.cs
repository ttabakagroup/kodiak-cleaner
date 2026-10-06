using System.Text;

namespace KodiakCleaner.Core.Util;

/// <summary>
/// A plain-text record of what Kodiak Cleaner did (cleanings, registry fixes, backups, startup and program changes, errors),
/// one file per month in the Logs folder, the newest twelve kept (Ted, 0.8.36). It stays on this PC and is never sent
/// anywhere. Lines hold counts, sizes and item names, never the contents of files. Settings > Application can turn it off.
/// </summary>
public static class ActivityLog
{
    public const int MonthsKept = 12;
    private static readonly object Gate = new();

    /// <summary>The Logs folder; null (the default) writes nothing.</summary>
    public static string? Folder { get; set; }
    public static bool Enabled { get; set; } = true;

    /// <summary>Kodiak Cleaner 2026-10.log for October 2026.</summary>
    public static string FileName(DateTime when) => $"Kodiak Cleaner {when:yyyy-MM}.log";

    public static string? CurrentFile => Folder is null ? null : Path.Combine(Folder, FileName(DateTime.Now));

    /// <summary>Adds one line: date and time, the area (Cleaner, Registry, ...) and what happened.</summary>
    public static void Write(string area, string message) => Write(area, message, DateTime.Now);

    public static void Write(string area, string message, DateTime when)
    {
        if (!Enabled || Folder is not { } folder) return;
        var line = $"{when:yyyy-MM-dd HH:mm:ss}  {area,-10}  {message.Replace("\r", " ").Replace("\n", " ")}\r\n";
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(folder);
                var file = Path.Combine(folder, FileName(when));
                if (!File.Exists(file))
                {
                    File.WriteAllText(file, Header(when), Encoding.UTF8);
                    Prune(folder);
                }
                File.AppendAllText(file, line, Encoding.UTF8);
            }
            // A log that can't be written must never stop the work it describes.
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    public static void Error(string area, Exception e) => Write(area, $"Error: {e.GetType().Name}: {e.Message}");

    private static string Header(DateTime when) =>
        $"Kodiak Cleaner activity log for {when:MMMM yyyy}. It stays on this PC and is never sent anywhere.\r\n" +
        "Turn it off in Settings > Application > Keep an Activity Log. Logs older than a year are deleted.\r\n\r\n";

    /// <summary>Deletes all but the newest <see cref="MonthsKept"/> monthly logs.</summary>
    public static void Prune(string folder)
    {
        foreach (var old in Directory.EnumerateFiles(folder, "Kodiak Cleaner ????-??.log").OrderByDescending(f => f, StringComparer.Ordinal).Skip(MonthsKept))
        {
            try { File.Delete(old); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }
}
