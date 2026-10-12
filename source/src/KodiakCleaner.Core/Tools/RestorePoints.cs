using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using KodiakCleaner.Core.RegistryTools;
using Microsoft.Win32;

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

    private const string FrequencyKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore";
    private const string FrequencyValue = "SystemRestorePointCreationFrequency";

    /// <summary>
    /// Creates a restore point even when Windows made one in the last 24 hours (it normally skips those quietly), so a
    /// point can be made right before a big install. Windows' SystemRestorePointCreationFrequency is set to 0 for this one
    /// call, after a .reg backup, and put back as it was afterwards.
    /// </summary>
    public static uint CreateNow(string description)
    {
        using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = root.CreateSubKey(FrequencyKey, writable: true);
        var before = key.GetValue(FrequencyValue);
        if (before is not null)
            RegistryBackup.Save(new[] { new RegistryLocation(RegistryHive.LocalMachine, RegistryView.Registry64, FrequencyKey, FrequencyValue) },
                "System_Restore_Frequency");
        key.SetValue(FrequencyValue, 0, RegistryValueKind.DWord);
        try { return Create(description); }
        finally
        {
            if (before is null) key.DeleteValue(FrequencyValue, throwOnMissingValue: false);
            else key.SetValue(FrequencyValue, before, RegistryValueKind.DWord);
        }
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
