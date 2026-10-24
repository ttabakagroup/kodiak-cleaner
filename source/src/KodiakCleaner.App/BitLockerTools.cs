using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace KodiakCleaner.App;

/// <summary>Where BitLocker stands on the Windows drive.</summary>
public enum BitLockerState
{
    /// <summary>Couldn't be read: no administrator rights, or BitLocker isn't part of this Windows.</summary>
    Unknown,
    /// <summary>The drive isn't encrypted.</summary>
    Off,
    /// <summary>Encrypted and protected: a start-up change can ask for the recovery key.</summary>
    On,
    /// <summary>Encrypted, but protection is suspended, so Windows starts without asking for the key.</summary>
    Suspended,
    /// <summary>Windows is still encrypting the drive (or paused part way); protection turns on when it finishes.</summary>
    Encrypting,
    /// <summary>Windows is decrypting the drive because BitLocker is being turned off.</summary>
    Decrypting,
}

/// <summary>
/// BitLocker on the Windows drive, read and changed through Windows' own Win32_EncryptableVolume class (it needs administrator
/// rights, so without them the state is Unknown). Kodiak Cleaner never stores or logs a recovery key: it is only shown,
/// copied or saved when the person asks. Turning BitLocker off is left to Windows' own BitLocker page.
/// </summary>
public static class BitLockerTools
{
    private const string Scope = @"root\CIMV2\Security\MicrosoftVolumeEncryption";
    private const uint NumericalPassword = 3;

    /// <summary>The Windows drive, such as C:.</summary>
    public static string WindowsDrive => Path.GetPathRoot(Environment.SystemDirectory)?.TrimEnd('\\') ?? "C:";

    private static bool Expected(Exception e) =>
        e is ManagementException or COMException or UnauthorizedAccessException or System.Security.SecurityException
            or PlatformNotSupportedException or TypeInitializationException;

    private static ManagementObject? Volume()
    {
        var options = new System.Management.EnumerationOptions { Timeout = TimeSpan.FromSeconds(15) };
        using var searcher = new ManagementObjectSearcher(new ManagementScope(Scope),
            new ObjectQuery($"SELECT * FROM Win32_EncryptableVolume WHERE DriveLetter = '{WindowsDrive}'"), options);
        foreach (ManagementObject mo in searcher.Get()) return mo;
        return null;
    }

    /// <summary>
    /// Reads the Windows drive's state. ConversionStatus 0 means fully decrypted, 2 and 4 encrypting (4 paused), 3 and 5 decrypting;
    /// ProtectionStatus 1 means protection is on, so a fully encrypted drive with protection off is suspended. While Windows is still
    /// encrypting, protection reads off too, which is not a suspend (Ted saw "Suspended" mid-encryption, 2026-10-24).
    /// </summary>
    public static BitLockerState State()
    {
        try
        {
            using var v = Volume();
            if (v is null) return BitLockerState.Unknown;
            var conversion = Convert.ToUInt32(v["ConversionStatus"] ?? 0u);
            var protection = Convert.ToUInt32(v["ProtectionStatus"] ?? 0u);
            if (conversion == 0 && protection == 0) return BitLockerState.Off;
            if (conversion is 2 or 4) return BitLockerState.Encrypting;
            if (conversion is 3 or 5) return BitLockerState.Decrypting;
            // 1 is on and 2 is "can't tell" (a locked drive), so only 0 counts as suspended.
            return protection == 0 ? BitLockerState.Suspended : BitLockerState.On;
        }
        catch (Exception e) when (Expected(e) || e is InvalidCastException or FormatException) { return BitLockerState.Unknown; }
    }

    /// <summary>How far Windows has got encrypting or decrypting the drive, 0 to 100, or null when it can't be read.</summary>
    public static int? Percent()
    {
        try
        {
            using var v = Volume();
            if (v is null) return null;
            using var result = v.InvokeMethod("GetConversionStatus", null, null);
            if (Convert.ToUInt32(result["ReturnValue"]) != 0) return null;
            return (int)Math.Clamp(Convert.ToUInt32(result["EncryptionPercentage"] ?? 0u), 0u, 100u);
        }
        catch (Exception e) when (Expected(e) || e is InvalidCastException or FormatException) { return null; }
    }

    /// <summary>
    /// Suspends protection for the next <paramref name="restarts"/> restarts (1 to 15); Windows turns it back on by itself after
    /// that. The drive stays encrypted. Returns false when Windows refused.
    /// </summary>
    public static bool Suspend(int restarts)
    {
        try
        {
            using var v = Volume();
            if (v is null) return false;
            using var args = v.GetMethodParameters("DisableKeyProtectors");
            args["DisableCount"] = (uint)Math.Clamp(restarts, 1, 15);
            using var result = v.InvokeMethod("DisableKeyProtectors", args, null);
            return Convert.ToUInt32(result["ReturnValue"]) == 0;
        }
        catch (Exception e) when (Expected(e) || e is InvalidCastException) { return false; }
    }

    /// <summary>Turns protection back on now instead of waiting for the restarts to run out.</summary>
    public static bool Resume()
    {
        try
        {
            using var v = Volume();
            if (v is null) return false;
            using var result = v.InvokeMethod("EnableKeyProtectors", null, null);
            return Convert.ToUInt32(result["ReturnValue"]) == 0;
        }
        catch (Exception e) when (Expected(e) || e is InvalidCastException) { return false; }
    }

    /// <summary>
    /// The recovery keys (48-digit numerical passwords) with their key IDs, the ID that the blue recovery screen shows. Empty when
    /// the drive has none or they can't be read. Nothing is kept: the caller only shows them.
    /// </summary>
    public static List<(string Id, string Key)> RecoveryKeys()
    {
        var keys = new List<(string, string)>();
        try
        {
            using var v = Volume();
            if (v is null) return keys;
            using var args = v.GetMethodParameters("GetKeyProtectors");
            args["KeyProtectorType"] = NumericalPassword;
            using var found = v.InvokeMethod("GetKeyProtectors", args, null);
            if (found["VolumeKeyProtectorID"] is not string[] ids) return keys;
            foreach (var id in ids)
            {
                using var keyArgs = v.GetMethodParameters("GetKeyProtectorNumericalPassword");
                keyArgs["VolumeKeyProtectorID"] = id;
                using var key = v.InvokeMethod("GetKeyProtectorNumericalPassword", keyArgs, null);
                if (Convert.ToUInt32(key["ReturnValue"]) == 0 && key["NumericalPassword"] is string password && password.Length > 0)
                    keys.Add((id, password));
            }
        }
        catch (Exception e) when (Expected(e) || e is InvalidCastException) { }
        return keys;
    }

    /// <summary>Windows Home has no BitLocker Control Panel page; its drive encryption lives in Settings.</summary>
    private static bool IsHomeEdition
    {
        get
        {
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                return k?.GetValue("EditionID") is string edition && edition.StartsWith("Core", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { return false; }
        }
    }

    /// <summary>
    /// Opens Windows' own BitLocker page (Manage BitLocker in Control Panel, or Device Encryption in Settings on Windows Home),
    /// where the key can be backed up to a USB drive, a file, a printout or a Microsoft account, and BitLocker turned off.
    /// </summary>
    public static bool OpenWindowsPage()
    {
        try
        {
            var start = IsHomeEdition
                ? new ProcessStartInfo("ms-settings:deviceencryption") { UseShellExecute = true }
                : new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "control.exe"), "/name Microsoft.BitLockerDriveEncryption") { UseShellExecute = true };
            Process.Start(start);
            return true;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { return false; }
    }

    /// <summary>Microsoft's page that lists the recovery keys backed up to a Microsoft account.</summary>
    public const string AccountKeysUrl = "https://aka.ms/myrecoverykey";
}
