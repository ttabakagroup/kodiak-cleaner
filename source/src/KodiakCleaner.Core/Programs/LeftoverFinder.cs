using KodiakCleaner.Core.RegistryTools;

namespace KodiakCleaner.Core.Programs;

public enum LeftoverKind { Folder, Shortcut, RegistryKey, StartupEntry }

/// <summary>Something an uninstaller left behind. <see cref="Key"/> is set for registry keys, <see cref="Item"/> for startup entries.</summary>
public sealed record Leftover(LeftoverKind Kind, string Path, string Where, RegistryLocation? Key = null, object? Item = null);

/// <summary>Where to look for a program's leftover folders and shortcuts, and what must never be offered.</summary>
public sealed record LeftoverPlaces
{
    /// <summary>Folders whose direct children (or Publisher\Name grandchildren) can be a program's folder: Program Files, AppData, ProgramData.</summary>
    public IReadOnlyList<(string Folder, string Where)> AppFolders { get; init; } = Array.Empty<(string, string)>();
    /// <summary>Start Menu Programs folders: a folder or a .lnk named after the program.</summary>
    public IReadOnlyList<string> StartMenus { get; init; } = Array.Empty<string>();
    /// <summary>Desktops: only a .lnk named after the program.</summary>
    public IReadOnlyList<string> Desktops { get; init; } = Array.Empty<string>();
    /// <summary>Never offered, and nothing that contains one is offered either (Windows, the user folder, Kodiak Cleaner's own folder).</summary>
    public IReadOnlyList<string> Protected { get; init; } = Array.Empty<string>();
    /// <summary>Install folders of every other installed program: never offered, and nothing containing one is offered.</summary>
    public IReadOnlyList<string> OtherPrograms { get; init; } = Array.Empty<string>();
    /// <summary>Names of every other installed program, so a name two programs share is never used.</summary>
    public IReadOnlyList<string> OtherNames { get; init; } = Array.Empty<string>();
}

/// <summary>Finds the folders and shortcuts a program leaves behind. Read-only; removing is up to the caller.</summary>
public static class LeftoverFinder
{
    public static List<Leftover> FindFiles(string name, string publisher, string installLocation, LeftoverPlaces places)
    {
        places = Sanitize(places);
        var others = new HashSet<string>(places.OtherNames.SelectMany(LeftoverNames.ForProgram), StringComparer.OrdinalIgnoreCase);
        var names = LeftoverNames.ForProgram(name).Where(n => !others.Contains(n)).ToList();
        var publishers = LeftoverNames.ForPublisher(publisher);
        var found = new List<Leftover>();

        void Folder(string path, string where)
        {
            if (Directory.Exists(path) && IsSafeFolder(path, places)) found.Add(new Leftover(LeftoverKind.Folder, Full(path), where));
        }
        void Shortcut(string path, string where)
        {
            if (File.Exists(path) && !IsLink(path) && !IsInsideOrEqual(Full(path), places.OtherPrograms))
                found.Add(new Leftover(LeftoverKind.Shortcut, Full(path), where));
        }

        // The folder the program's own entry said it was installed in.
        if (!string.IsNullOrWhiteSpace(installLocation))
        {
            var loc = Environment.ExpandEnvironmentVariables(installLocation.Trim().Trim('"'));
            if (Path.IsPathFullyQualified(loc) && places.AppFolders.Any(a => IsInside(Full(loc), Full(a.Folder))))
                Folder(loc, "Install folder");
        }

        foreach (var (root, where) in places.AppFolders)
            foreach (var n in names)
            {
                Folder(Path.Combine(root, n), where);
                foreach (var p in publishers) Folder(Path.Combine(root, p, n), where);
            }

        foreach (var menu in places.StartMenus)
            foreach (var n in names)
            {
                Folder(Path.Combine(menu, n), "Start Menu");
                Shortcut(Path.Combine(menu, n + ".lnk"), "Start Menu");
                foreach (var p in publishers) Folder(Path.Combine(menu, p, n), "Start Menu");
            }

        foreach (var desktop in places.Desktops)
            foreach (var n in names)
                Shortcut(Path.Combine(desktop, n + ".lnk"), "Desktop");

        // One entry per path, and nothing inside a folder that's already listed.
        var unique = found.GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
        return unique.Where(f => !unique.Any(o => o.Kind == LeftoverKind.Folder && !ReferenceEquals(o, f) && IsInside(f.Path, o.Path)))
                     .OrderBy(f => f.Kind).ThenBy(f => f.Path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// A folder can be offered only if it is a real folder (not a link), at least two levels deep, not one of the
    /// search roots or protected folders, holds none of them, and holds no other program's install folder.
    /// </summary>
    public static bool IsSafeFolder(string path, LeftoverPlaces places)
    {
        if (!Path.IsPathFullyQualified(path)) return false;
        var full = Full(path);
        if (Util.PathSafety.Depth(full) < 2) return false;
        if (IsLink(full)) return false;
        var roots = places.AppFolders.Select(a => a.Folder).Concat(places.StartMenus).Concat(places.Desktops);
        foreach (var r in roots.Where(r => !string.IsNullOrWhiteSpace(r)))
            if (IsInsideOrEqual(Full(r), new[] { full })) return false; // the root itself, or something holding it
        foreach (var p in places.Protected.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            var guarded = Full(p);
            if (string.Equals(full, guarded, StringComparison.OrdinalIgnoreCase) || IsInside(guarded, full)) return false;
        }
        return !IsInsideOrEqual(full, places.OtherPrograms) && !places.OtherPrograms.Any(o => !string.IsNullOrWhiteSpace(o) && IsInside(Full(o), full));
    }

    /// <summary>Drops other programs' "install folders" that are really a whole drive or a search root (badly written entries).</summary>
    private static LeftoverPlaces Sanitize(LeftoverPlaces places)
    {
        var roots = places.AppFolders.Select(a => a.Folder).Concat(places.StartMenus).Concat(places.Desktops).Concat(places.Protected)
            .Where(r => !string.IsNullOrWhiteSpace(r) && Path.IsPathFullyQualified(r)).Select(Full).ToList();
        var others = places.OtherPrograms
            .Select(o => Environment.ExpandEnvironmentVariables(o ?? "").Trim().Trim('"'))
            .Where(o => o.Length > 0 && Path.IsPathFullyQualified(o)).Select(Full)
            .Where(o => Util.PathSafety.Depth(o) >= 2 && !roots.Any(r => string.Equals(r, o, StringComparison.OrdinalIgnoreCase) || IsInside(r, o)))
            .ToList();
        return places with { OtherPrograms = others };
    }

    private static bool IsInsideOrEqual(string path, IEnumerable<string> folders) =>
        folders.Where(f => !string.IsNullOrWhiteSpace(f)).Any(f =>
            string.Equals(path, Full(f), StringComparison.OrdinalIgnoreCase) || IsInside(path, Full(f)));

    /// <summary>True when <paramref name="path"/> is somewhere below <paramref name="folder"/>.</summary>
    public static bool IsInside(string path, string folder) =>
        path.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string Full(string p) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(p));

    private static bool IsLink(string path)
    {
        try { return File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return true; } // unknown: don't touch
    }
}
