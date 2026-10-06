namespace KodiakCleaner.Core.Startup;

/// <summary>
/// Encodes the 12-byte values under Explorer\StartupApproved that Task Manager and Settings use
/// to enable/disable startup entries. An odd first byte means disabled; the remaining 8 bytes
/// hold the FILETIME when it was disabled.
/// </summary>
public static class StartupApprovedValue
{
    public static bool IsEnabled(byte[]? data) => data is null || data.Length == 0 || (data[0] & 1) == 0;

    public static byte[] Create(bool enabled, DateTime? whenUtc = null)
    {
        var bytes = new byte[12];
        bytes[0] = enabled ? (byte)0x02 : (byte)0x03;
        if (!enabled)
        {
            long ft = (whenUtc ?? DateTime.UtcNow).ToFileTimeUtc();
            BitConverter.GetBytes(ft).CopyTo(bytes, 4);
        }
        return bytes;
    }
}
