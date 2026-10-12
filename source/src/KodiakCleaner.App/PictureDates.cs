using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;

namespace KodiakCleaner.App;

/// <summary>
/// A picture's Date Taken, for Mass Rename's {taken}. Read with Windows' own picture decoders from the picture's details
/// (only the details are read, not the picture), so it works for whatever File Explorer shows a Date Taken for.
/// </summary>
public static class PictureDates
{
    private static readonly HashSet<string> Kinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".jpe", ".jfif", ".tif", ".tiff", ".png", ".heic", ".heif", ".webp", ".dng", ".jxr", ".wdp",
    };

    public static DateTime? Taken(string path)
    {
        if (!Kinds.Contains(Path.GetExtension(path))) return null;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
            if (decoder.Frames.Count == 0 || decoder.Frames[0].Metadata is not BitmapMetadata meta || meta.DateTaken is not { } text) return null;
            return DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out var when) ||
                   DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out when)
                ? when : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException
                                       or InvalidOperationException or ArgumentException or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }
}
