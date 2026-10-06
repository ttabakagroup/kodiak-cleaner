using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace KodiakCleaner.Core.Tools;

public sealed record RestorePoint(int Sequence, string Description, DateTime Created, string Kind);

/// <summary>Lists, creates and removes Windows System Restore points (WMI root\default:SystemRestore).</summary>
[SupportedOSPlatform("windows")]
public static partial class RestorePoints
{
    private const string Scope = @"\\.\root\default";

    public static List<RestorePoint> List()
    {
        using var searcher = new ManagementObjectSearcher(new ManagementScope(Scope), new ObjectQuery("SELECT * FROM SystemRestore"));
        var list = new List<RestorePoint>();
        foreach (ManagementObject mo in searcher.Get())
        {
            using (mo)
            {
                var created = mo["CreationTime"] is string cim ? ManagementDateTimeConverter.ToDateTime(cim) : DateTime.MinValue;
                list.Add(new RestorePoint(
                    Convert.ToInt32(mo["SequenceNumber"]),
                    mo["Description"]?.ToString() ?? "",
                    created,
                    KindName(Convert.ToInt32(mo["RestorePointType"]))));
            }
        }
        return list.OrderByDescending(r => r.Created).ToList();
    }

    /// <summary>Asks Windows to create a restore point. Needs administrator rights and System Protection turned on.</summary>
    public static uint Create(string description)
    {
        using var cls = new ManagementClass(new ManagementScope(Scope), new ManagementPath("SystemRestore"), null);
        // 12 = MODIFY_SETTINGS, 100 = BEGIN_SYSTEM_CHANGE
        var result = cls.InvokeMethod("CreateRestorePoint", new object[] { description, 12, 100 });
        return Convert.ToUInt32(result);
    }

    /// <summary>Removes one restore point. Returns 0 on success, otherwise a Win32 error code.</summary>
    public static int Remove(int sequence) => SRRemoveRestorePoint(sequence);

    [LibraryImport("srclient.dll")]
    private static partial int SRRemoveRestorePoint(int dwRPNum);

    private static string KindName(int type) => type switch
    {
        0 => "Application installed",
        1 => "Application removed",
        7 => "System checkpoint",
        10 => "Device driver installed",
        12 => "Settings changed",
        13 => "Cancelled operation",
        _ => "Restore point",
    };
}
