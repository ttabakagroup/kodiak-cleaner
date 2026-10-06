using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KodiakCleaner.App.ViewModels;
using KodiakCleaner.Core.Programs;

namespace KodiakCleaner.App;

/// <summary>Reads each program's own icon (its DisplayIcon, or its uninstaller as a last resort) in the background.</summary>
public static class ProgramIcons
{
    private static readonly ConcurrentDictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static void LoadInto(IReadOnlyList<ProgramItem> items)
    {
        var dispatcher = Application.Current.Dispatcher;
        Task.Run(() =>
        {
            foreach (var item in items)
            {
                var icon = For(item.Program);
                if (icon is not null) dispatcher.BeginInvoke(() => item.Icon = icon);
            }
        });
    }

    private static ImageSource? For(InstalledProgram p)
    {
        // Like Windows' Apps list: the entry's own icon, then Windows Installer's saved icon,
        // then the main program in its install folder, and the uninstaller only as a last resort.
        var (path, index) = Parse(p.DisplayIcon);
        if (path.Length == 0) (path, index) = Parse(SafeProductIcon(p));
        if (path.Length == 0) path = MainExe(UninstallFlow.InstallFolder(p), p.Name);
        if (path.Length == 0) path = GuessFolders(p).Select(f => MainExe(f, p.Name)).FirstOrDefault(e => e.Length > 0) ?? "";
        if (path.Length == 0 && !p.IsWindowsInstaller) (path, index) = Parse(Core.Util.CommandLine.CheckablePath(p.UninstallString));
        if (path.Length == 0) return null;
        return Cache.GetOrAdd($"{path}|{index}", _ => Extract(path, index));
    }

    private static string SafeProductIcon(InstalledProgram p)
    {
        try { return ProgramInventory.InstallerProductIcon(p); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return ""; }
    }

    /// <summary>Program Files\Name (and the x86 and per-user Programs folders) for programs that don't say where they are.</summary>
    private static IEnumerable<string> GuessFolders(InstalledProgram p)
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
        };
        foreach (var root in roots.Where(r => r.Length > 0))
            foreach (var n in LeftoverNames.ForProgram(p.Name))
                yield return Path.Combine(root, n);
    }

    /// <summary>The program's main .exe in its install folder: one named like the program, else the biggest. "" if none.</summary>
    private static string MainExe(string folder, string name)
    {
        try
        {
            if (folder.Length == 0 || !Directory.Exists(folder)) return "";
            var exes = new DirectoryInfo(folder).EnumerateFiles("*.exe")
                .Where(f => !f.Name.Contains("unins", StringComparison.OrdinalIgnoreCase) && !f.Name.Contains("setup", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length >= 3);
            return (exes.FirstOrDefault(f => words.Any(w => Path.GetFileNameWithoutExtension(f.Name).Contains(w, StringComparison.OrdinalIgnoreCase)))
                    ?? exes.OrderByDescending(f => f.Length).FirstOrDefault())?.FullName ?? "";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { return ""; }
    }

    /// <summary>Splits "C:\App\app.exe,2" (quotes allowed) into the file and the icon index.</summary>
    internal static (string Path, int Index) Parse(string displayIcon)
    {
        var s = Environment.ExpandEnvironmentVariables(displayIcon ?? "").Trim();
        int index = 0;
        int comma = s.LastIndexOf(',');
        if (comma > 0 && int.TryParse(s[(comma + 1)..].Trim(), out var i)) { index = i; s = s[..comma]; }
        s = s.Trim().Trim('"');
        return Path.IsPathFullyQualified(s) && File.Exists(s) ? (s, index) : ("", 0);
    }

    internal static ImageSource? Extract(string path, int index)
    {
        var small = IntPtr.Zero;
        var large = IntPtr.Zero;
        try
        {
            if (ExtractIconEx(path, index, out large, out small, 1) == 0) return null;
            var handle = large != IntPtr.Zero ? large : small;
            if (handle == IntPtr.Zero) return null;
            var image = Imaging.CreateBitmapSourceFromHIcon(handle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
            image.Freeze(); // made on a background thread, shown on the UI thread
            return image;
        }
        catch (Exception e) when (e is COMException or ArgumentException or ExternalException or IOException) { return null; }
        finally
        {
            if (small != IntPtr.Zero) DestroyIcon(small);
            if (large != IntPtr.Zero) DestroyIcon(large);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string file, int index, out IntPtr large, out IntPtr small, uint count);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}
