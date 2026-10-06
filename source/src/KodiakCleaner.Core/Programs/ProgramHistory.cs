using System.Text.Json;
using System.Text.Json.Serialization;

namespace KodiakCleaner.Core.Programs;

/// <summary>What Kodiak Cleaner did on the Programs page.</summary>
public enum ProgramChange
{
    /// <summary>Delete Entry: the uninstall entry was removed (backed up to a .reg file).</summary>
    DeletedEntry,
    /// <summary>Rename: the old entry was backed up first.</summary>
    Renamed,
    /// <summary>The program's own uninstaller removed it. Record only: a program can't be put back from here.</summary>
    Uninstalled,
    /// <summary>Leftover registry keys removed after an uninstall (backed up to a .reg file).</summary>
    LeftoverKeys,
    /// <summary>A leftover folder or shortcut, sent to the Recycle Bin.</summary>
    LeftoverFile,
    /// <summary>A leftover startup entry (also in the Startup history).</summary>
    LeftoverStartup,
}

/// <summary>One thing removed or changed on the Programs page, with what is needed to put it back.</summary>
public sealed class ProgramRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public ProgramChange Change { get; set; }
    public string Program { get; set; } = "";
    public string Publisher { get; set; } = "";
    public string Version { get; set; } = "";
    /// <summary>The path, key or new name the record is about.</summary>
    public string Detail { get; set; } = "";
    public DateTime When { get; set; }
    /// <summary>The .reg backup that restores it (entries, renames, leftover keys).</summary>
    public string? RegistryBackup { get; set; }
    /// <summary>For a leftover startup entry: its key in the Startup history.</summary>
    public string? StartupKey { get; set; }
    public DateTime? RestoredOn { get; set; }

    [JsonIgnore] public bool IsRestored => RestoredOn is not null;

    /// <summary>Whether the record can be put back in principle (an uninstalled program can't).</summary>
    [JsonIgnore] public bool Restorable => Change switch
    {
        ProgramChange.Uninstalled => false,
        ProgramChange.DeletedEntry or ProgramChange.Renamed or ProgramChange.LeftoverKeys => RegistryBackup is not null,
        ProgramChange.LeftoverStartup => StartupKey is not null,
        _ => true,
    };

    [JsonIgnore] public string What => Change switch
    {
        ProgramChange.DeletedEntry => "Entry Deleted",
        ProgramChange.Renamed => "Renamed",
        ProgramChange.Uninstalled => "Uninstalled",
        ProgramChange.LeftoverKeys => "Leftover Registry Keys",
        ProgramChange.LeftoverFile => "Leftover File or Folder",
        ProgramChange.LeftoverStartup => "Leftover Startup Entry",
        _ => Change.ToString(),
    };
}

/// <summary>
/// Everything removed or changed on the Programs page (Ted, 0.8.22), kept in Backups\Programs History\history.json
/// beside Kodiak Cleaner. Newest first.
/// </summary>
public sealed class ProgramHistory
{
    public const string FileName = "history.json";
    public const string FolderName = "Programs History";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    public List<ProgramRecord> Records { get; set; } = new();

    public static ProgramHistory Load(string folder)
    {
        try
        {
            var path = Path.Combine(folder, FileName);
            if (!File.Exists(path)) return new ProgramHistory();
            return JsonSerializer.Deserialize<ProgramHistory>(File.ReadAllText(path), Json) ?? new ProgramHistory();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return new ProgramHistory();
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

    public ProgramRecord Add(ProgramChange change, InstalledProgram program, string detail, DateTime when, string? registryBackup = null, string? startupKey = null)
    {
        var r = new ProgramRecord
        {
            Change = change, Program = program.Name, Publisher = program.Publisher, Version = program.Version,
            Detail = detail, When = when, RegistryBackup = registryBackup, StartupKey = startupKey,
        };
        Records.Insert(0, r);
        return r;
    }
}
