using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;

namespace KodiakCleaner.Core.Cleaning;

public sealed record RecycledItem(string InfoFile, string DataPath, string OriginalPath, long Size, DateTime DeletedUtc);

/// <summary>
/// Reads the Recycle Bin directly so items can be filtered by deletion date. Each deleted item is a pair
/// of files in X:\$Recycle.Bin\&lt;SID&gt;: "$Ixxxx" (metadata) and "$Rxxxx" (the data, file or folder).
/// </summary>
public static class RecycleBinItems
{
    /// <summary>Parses a $I file (Vista format v1 and Windows 10+ format v2).</summary>
    public static (long Size, DateTime DeletedUtc, string OriginalPath)? ParseInfo(byte[] data)
    {
        if (data.Length < 24) return null;
        long version = BitConverter.ToInt64(data, 0);
        long size = BitConverter.ToInt64(data, 8);
        long fileTime = BitConverter.ToInt64(data, 16);
        DateTime deleted;
        try { deleted = DateTime.FromFileTimeUtc(fileTime); }
        catch (ArgumentOutOfRangeException) { return null; }

        string path;
        if (version == 1)
        {
            if (data.Length < 24 + 520) return null;
            path = Encoding.Unicode.GetString(data, 24, 520);
        }
        else if (version == 2)
        {
            if (data.Length < 28) return null;
            int chars = BitConverter.ToInt32(data, 24);
            if (chars < 0 || chars > (data.Length - 28) / 2) return null; // no int overflow on a crafted count
            path = Encoding.Unicode.GetString(data, 28, chars * 2);
        }
        else return null;

        int nul = path.IndexOf('\0');
        return (size, deleted, nul >= 0 ? path[..nul] : path);
    }

    /// <summary>Items in one $Recycle.Bin\&lt;SID&gt; folder.</summary>
    public static List<RecycledItem> ReadFolder(string sidFolder)
    {
        var items = new List<RecycledItem>();
        if (!Directory.Exists(sidFolder)) return items;
        List<string> infos;
        try { infos = Directory.EnumerateFiles(sidFolder, "$I*").ToList(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { return items; }
        foreach (var info in infos)
        {
            try
            {
                var parsed = ParseInfo(File.ReadAllBytes(info));
                if (parsed is null) continue;
                var name = Path.GetFileName(info);
                var dataPath = Path.Combine(sidFolder, "$R" + name[2..]);
                items.Add(new RecycledItem(info, dataPath, parsed.Value.OriginalPath, parsed.Value.Size, parsed.Value.DeletedUtc));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return items;
    }

    [SupportedOSPlatform("windows")]
    public static List<RecycledItem> ReadCurrentUser()
    {
        var sid = WindowsIdentity.GetCurrent().User?.Value;
        if (sid is null) return new List<RecycledItem>();
        return DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType is DriveType.Fixed or DriveType.Removable)
            .SelectMany(d => ReadFolder(Path.Combine(d.RootDirectory.FullName, "$Recycle.Bin", sid)))
            .ToList();
    }

    /// <summary>Permanently deletes one item (data first, then its metadata). Returns false if anything was locked.</summary>
    public static bool Delete(RecycledItem item)
    {
        try
        {
            if (Directory.Exists(item.DataPath))
            {
                var dir = new DirectoryInfo(item.DataPath);
                // Clear read-only flags so the recursive delete succeeds; links are removed, not followed.
                foreach (var f in dir.EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }))
                    if (f.IsReadOnly) f.IsReadOnly = false;
                dir.Delete(recursive: true);
            }
            else if (File.Exists(item.DataPath))
            {
                var f = new FileInfo(item.DataPath);
                if (f.IsReadOnly) f.IsReadOnly = false;
                f.Delete();
            }
            File.Delete(item.InfoFile);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
