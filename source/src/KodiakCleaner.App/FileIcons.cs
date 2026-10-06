using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KodiakCleaner.Core.Cleaning;
using Microsoft.Win32;

namespace KodiakCleaner.App;

/// <summary>
/// Icons read from files already on the PC (a startup entry's program, an installed browser), the way Explorer shows them.
/// Nothing is bundled: a program that isn't found gets a plain placeholder instead.
/// </summary>
public static class FileIcons
{
    private static readonly ConcurrentDictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Reads each item's icon in the background and hands it over on the UI thread.</summary>
    public static void LoadInto<T>(IReadOnlyList<T> items, Func<T, string> fileOf, Action<T, ImageSource> set)
    {
        var dispatcher = Application.Current.Dispatcher;
        Task.Run(() =>
        {
            foreach (var item in items)
            {
                string file;
                try { file = fileOf(item); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException) { continue; }
                if (For(file) is { } icon) dispatcher.BeginInvoke(() => set(item, icon));
            }
        });
    }

    /// <summary>The file's icon (a shortcut shows its target's icon), or null.</summary>
    public static ImageSource? For(string file) =>
        string.IsNullOrEmpty(file) || !File.Exists(file) ? null : Cache.GetOrAdd(file, Extract);

    /// <summary>
    /// The program a startup command runs: "C:\App\app.exe /min" gives C:\App\app.exe, a bare "app.exe" is looked up in
    /// the Windows folders and App Paths, and a startup-folder shortcut is the shortcut itself. "" when it can't be found.
    /// </summary>
    public static string ProgramOf(string command)
    {
        var (exe, _) = Core.Util.CommandLine.Split(Environment.ExpandEnvironmentVariables(command ?? ""));
        exe = exe.Trim().Trim('"');
        if (exe.Length == 0 || exe.IndexOfAny(Path.GetInvalidPathChars()) >= 0) return "";
        if (Path.IsPathFullyQualified(exe)) return File.Exists(exe) ? exe : "";
        if (exe.Contains('\\') || exe.Contains('/')) return "";
        var name = Path.HasExtension(exe) ? exe : exe + ".exe";
        foreach (var dir in new[] { Environment.SystemDirectory, Environment.GetFolderPath(Environment.SpecialFolder.Windows) })
        {
            var p = Path.Combine(dir, name);
            if (File.Exists(p)) return p;
        }
        return AppPath(name);
    }

    /// <summary>The installed program behind a Cleaner group (Chrome, Edge, ...): its usual places, then Windows' App Paths.</summary>
    public static string ProgramOf(CleanTarget target)
    {
        foreach (var f in target.AppFiles)
            if (File.Exists(f)) return f;
        return target.BlockingProcesses.Count > 0 ? AppPath(target.BlockingProcesses[0] + ".exe") : "";
    }

    /// <summary>A browser's program by its name in Kodiak Cleaner ("Google Chrome", "Ablaze Floorp", ...): its usual places, or "".</summary>
    public static string ProgramOfBrowser(string browser) => CleanCatalog.AppFiles(browser).FirstOrDefault(File.Exists) ?? "";

    /// <summary>A picture from its bytes (an extension's PNG icon), ready to show; null when it can't be decoded.</summary>
    public static ImageSource? FromBytes(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0) return null;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 32;
            image.StreamSource = new MemoryStream(bytes);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception e) when (e is NotSupportedException or IOException or ArgumentException or InvalidOperationException or COMException) { return null; }
    }

    /// <summary>Where Windows' App Paths list says a program is (what Win+R uses), or "".</summary>
    private static string AppPath(string exeName)
    {
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            try
            {
                using var key = hive.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{exeName}");
                if (key?.GetValue(null) is string s)
                {
                    var path = Environment.ExpandEnvironmentVariables(s).Trim().Trim('"');
                    if (Path.IsPathFullyQualified(path) && File.Exists(path)) return path;
                }
            }
            catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
        }
        return "";
    }

    private static ImageSource? Extract(string file)
    {
        // Programs carry their own icons: read them directly. Anything else (a shortcut, a script) as Explorer shows it.
        var ext = Path.GetExtension(file);
        if ((ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) || ext.Equals(".dll", StringComparison.OrdinalIgnoreCase)) &&
            ProgramIcons.Extract(file, 0) is { } own) return own;
        // A shortcut: the icon it sets, or its program's own icon.
        if (ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase) &&
            Core.Startup.ShellLink.IconOf(file) is { File.Length: > 0 } link && ProgramIcons.Extract(link.File, link.Index) is { } linked) return linked;
        var info = new SHFILEINFO();
        try
        {
            if (SHGetFileInfo(file, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_LARGEICON) == IntPtr.Zero ||
                info.hIcon == IntPtr.Zero) return null;
            var image = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
            image.Freeze(); // made on a background thread, shown on the UI thread
            return image;
        }
        catch (Exception e) when (e is COMException or ArgumentException or ExternalException or IOException) { return null; }
        finally
        {
            if (info.hIcon != IntPtr.Zero) DestroyIcon(info.hIcon);
        }
    }

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_LARGEICON = 0x0;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path, uint attributes, ref SHFILEINFO info, uint size, uint flags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}
