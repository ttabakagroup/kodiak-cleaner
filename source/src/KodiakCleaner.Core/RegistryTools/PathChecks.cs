using System.Runtime.Versioning;

namespace KodiakCleaner.Core.RegistryTools;

[SupportedOSPlatform("windows")]
public static class PathChecks
{
    /// <summary>
    /// True only when we are confident the file is gone: the path is absolute, on a fixed
    /// local drive that is present, and nothing exists there. Anything uncertain
    /// (network share, unplugged USB drive, bare program name) counts as not missing.
    /// </summary>
    public static bool IsMissing(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        path = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        if (path.Contains('%') || !Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal)) return false;
        try
        {
            var root = Path.GetPathRoot(path);
            if (string.IsNullOrEmpty(root)) return false;
            var drive = new DriveInfo(root);
            if (!drive.IsReady || drive.DriveType != DriveType.Fixed) return false;
            if (File.Exists(path) || Directory.Exists(path)) return false;
            // 32-bit programs record System32 for files that really live in SysWOW64, and Sysnative only exists for them.
            // Their %ProgramFiles% and %CommonProgramFiles% mean the (x86) folders, which a 64-bit program expands differently.
            foreach (var alt in new[]
                     {
                         path.Replace(@"\System32\", @"\SysWOW64\", StringComparison.OrdinalIgnoreCase),
                         path.Replace(@"\Sysnative\", @"\System32\", StringComparison.OrdinalIgnoreCase),
                         path.Replace(@"\Program Files\", @"\Program Files (x86)\", StringComparison.OrdinalIgnoreCase),
                         path.Replace(@"\Program Files (x86)\", @"\Program Files\", StringComparison.OrdinalIgnoreCase),
                     })
            {
                if (alt != path && (File.Exists(alt) || Directory.Exists(alt))) return false;
            }
            // File.Exists is also false when access is denied; only call it missing when Windows says it isn't there.
            return NotFound(path);
        }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFileAttributesW(string path);

    private static bool NotFound(string path)
    {
        if (GetFileAttributesW(path) != uint.MaxValue) return false;
        var error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
        return error is 2 or 3; // ERROR_FILE_NOT_FOUND, ERROR_PATH_NOT_FOUND (not access denied or anything else)
    }
}
