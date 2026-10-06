namespace KodiakCleaner.Core.RegistryTools;

/// <summary>Turns the file references found in registry values into paths that can be checked. No registry access.</summary>
public static class RegistryPaths
{
    /// <summary>
    /// A Fonts value names either a file in the Windows Fonts folder ("arial.ttf") or a full path (fonts installed
    /// for one user). Font collections can list several files ("a.fon,b.fon"), which are never reported.
    /// </summary>
    public static string FontFile(string? data, string fontsFolder)
    {
        if (string.IsNullOrWhiteSpace(data) || data.Contains(',')) return "";
        var file = data.Trim().Trim('"');
        return Path.IsPathFullyQualified(file) ? file : file.IndexOfAny(new[] { '\\', '/' }) >= 0 ? "" : Path.Combine(fontsFolder, file);
    }

    /// <summary>A type library path may end in "\2" (which library inside the file); the file is what's checked.</summary>
    public static string TypeLibFile(string? data)
    {
        if (string.IsNullOrWhiteSpace(data)) return "";
        var path = data.Trim().Trim('"');
        int slash = path.LastIndexOf('\\');
        if (slash > 0 && slash < path.Length - 1 && path[(slash + 1)..].All(char.IsDigit)) path = path[..slash];
        return path;
    }

    /// <summary>Help values are "file name" = "folder it's in"; the file is the two together.</summary>
    public static string HelpFile(string? name, string? folder)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(folder)) return "";
        if (name.IndexOfAny(new[] { '\\', '/' }) >= 0) return "";
        var dir = folder.Trim().Trim('"');
        return Path.IsPathFullyQualified(dir) ? Path.Combine(dir, name.Trim()) : "";
    }

    /// <summary>True for paths inside the Windows folder, which belong to Windows and are never reported.</summary>
    public static bool IsInFolder(string? path, string folder)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(folder)) return false;
        var full = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        var root = folder.TrimEnd('\\') + "\\";
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }
}
