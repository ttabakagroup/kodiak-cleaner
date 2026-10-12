using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KodiakCleaner.App;

/// <summary>
/// The difference hash Duplicate Finder's in-depth search compares pictures by: the picture shrunk to 9x8 in gray, one bit per
/// pixel for "brighter than the pixel to its right". Re-saved, resized or recompressed copies of a picture keep nearly the same bits.
/// Uses Windows' own picture decoders, so it reads whatever Windows can show (JPEG, PNG, GIF, BMP, TIFF, and HEIC or WebP when installed).
/// </summary>
public static class PictureHasher
{
    public static ulong? Hash(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var picture = new BitmapImage();
            picture.BeginInit();
            picture.CacheOption = BitmapCacheOption.OnLoad;
            // Not IgnoreImageCache: with a stream (no address) it makes WPF throw while finishing the picture.
            picture.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            // Decoding straight to a tiny size is fast: JPEG decoders skip most of the work.
            picture.DecodePixelWidth = 9;
            picture.DecodePixelHeight = 8;
            picture.StreamSource = stream;
            picture.EndInit();

            BitmapSource small = picture;
            if (small.PixelWidth != 9 || small.PixelHeight != 8)
                small = new TransformedBitmap(small, new ScaleTransform(9.0 / small.PixelWidth, 8.0 / small.PixelHeight));
            var gray = new FormatConvertedBitmap(small, PixelFormats.Gray8, null, 0);
            if (gray.PixelWidth < 9 || gray.PixelHeight < 8) return null;
            var pixels = new byte[9 * 8];
            gray.CopyPixels(new System.Windows.Int32Rect(0, 0, 9, 8), pixels, 9, 0);

            ulong hash = 0;
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    if (pixels[y * 9 + x] > pixels[y * 9 + x + 1]) hash |= 1UL << (y * 8 + x);
            return hash;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or
                                       ArgumentException or InvalidOperationException or System.Runtime.InteropServices.COMException or
                                       OverflowException or FileFormatException)
        {
            return null;
        }
    }
}
