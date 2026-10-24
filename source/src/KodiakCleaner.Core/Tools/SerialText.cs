using System.Text;
using System.Text.RegularExpressions;

namespace KodiakCleaner.Core.Tools;

/// <summary>
/// Tidies a disk serial number the way the label shows it (Ted, 0.8.117: his Windows drive showed a jumble). Windows pads serials
/// with spaces or a trailing dot, and for many NVMe drives hands back a drive ID instead of the serial on the label.
/// </summary>
public static partial class SerialText
{
    /// <summary>Trims padding and a trailing dot.</summary>
    public static string Clean(string? raw) => (raw ?? "").Trim().TrimEnd('.').Trim();

    /// <summary>An NVMe drive ID (EUI-64 or NGUID: groups of four hex digits joined by underscores), not the label's serial.</summary>
    public static bool IsDriveId(string? serial) => serial is not null && DriveId().IsMatch(Clean(serial));

    /// <summary>The 20-byte ASCII serial from NVMe Identify Controller data, space padded; null if it isn't printable text.</summary>
    public static string? FromIdentify(ReadOnlySpan<byte> bytes)
    {
        var s = Encoding.ASCII.GetString(bytes).Replace('\0', ' ').Trim();
        return s.Length > 0 && s.All(c => c is >= ' ' and < (char)0x7F) ? s : null;
    }

    [GeneratedRegex("^([0-9A-Fa-f]{4}_){3,7}[0-9A-Fa-f]{4}$")]
    private static partial Regex DriveId();
}
