using System.Text.RegularExpressions;

namespace KodiakCleaner.Core.Programs;

/// <summary>
/// The folder and registry names a program is likely to leave behind, worked out from its name and publisher.
/// Deliberately strict: only exact names, never partial matches, and never generic or shared names.
/// </summary>
public static partial class LeftoverNames
{
    // Names that many programs share or that Windows itself uses. Never offered, whatever the program is called.
    private static readonly HashSet<string> Generic = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft", "Microsoft Corporation", "Windows", "Common Files", "Temp", "Tmp", "Packages", "Programs", "Program Files",
        "AppData", "Local", "LocalLow", "Roaming", "Users", "Public", "Default", "Desktop", "Documents", "Downloads",
        "Classes", "Policies", "Clients", "WOW6432Node", "Wow6432Node", "System", "System32", "Fonts", "Drivers", "Driver",
        "Google", "Mozilla", "Apple", "Intel", "NVIDIA", "NVIDIA Corporation", "AMD", "Adobe", "Oracle", "Java", "Python",
        "Steam", "Valve", "Epic Games", "Update", "Updater", "Updates", "Installer", "Setup", "Uninstall", "Runtime",
        "Tools", "Software", "Data", "Cache", "Logs", "Log", "Settings", "Config", "Plugins", "Games", "Shared", "Support",
        "Help", "Kodiak Cleaner", "KodiakCleaner", "Startup", "Start Menu", "Accessories", "Administrative Tools",
        "Maintenance", "Crashpad", "CrashReports", "Application Data", "Package Cache", "SoftwareDistribution",
    };

    /// <summary>The program's name, plus the same name without a version, edition in brackets or 32/64-bit tag.</summary>
    public static IReadOnlyList<string> ForProgram(string displayName)
    {
        var list = new List<string>();
        void Add(string s)
        {
            s = s.Trim().TrimEnd('-', '_', ',', '.').Trim();
            if (IsUsable(s) && !list.Contains(s, StringComparer.OrdinalIgnoreCase)) list.Add(s);
        }
        var name = displayName ?? "";
        Add(name);
        var shorter = Brackets().Replace(name, " ");
        for (int i = 0; i < 4; i++)
            shorter = TrailingVersion().Replace(TrailingArch().Replace(shorter.Trim(), ""), "").Trim();
        Add(shorter);
        return list;
    }

    /// <summary>The publisher, plus the same name without "Inc.", "LLC" and similar endings.</summary>
    public static IReadOnlyList<string> ForPublisher(string publisher)
    {
        var list = new List<string>();
        void Add(string s)
        {
            s = s.Trim().TrimEnd(',', '.').Trim();
            if (s.Length >= 2 && IsSafeSegment(s) && !list.Contains(s, StringComparer.OrdinalIgnoreCase)) list.Add(s);
        }
        Add(publisher ?? "");
        Add(CompanySuffix().Replace(publisher ?? "", ""));
        return list;
    }

    /// <summary>Long enough, not generic, and a single plain folder name.</summary>
    public static bool IsUsable(string name) =>
        name.Length >= 3 && !Generic.Contains(name) && IsSafeSegment(name) && name.Any(char.IsLetter);

    private static bool IsSafeSegment(string s) =>
        s.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && s.IndexOfAny(new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' }) < 0 &&
        s != "." && s != ".." && !s.StartsWith(' ') && !s.EndsWith('.');

    [GeneratedRegex(@"\s*[\(\[][^\)\]]*[\)\]]")]
    private static partial Regex Brackets();

    [GeneratedRegex(@"[\s_-]+(v(er(sion)?)?\.?\s*)?\d+([\.\-]\d+)*[a-z]?\d*$", RegexOptions.IgnoreCase)]
    private static partial Regex TrailingVersion();

    [GeneratedRegex(@"[\s_-]+(x64|x86|amd64|arm64|win64|win32|64[\s-]?bit|32[\s-]?bit)$", RegexOptions.IgnoreCase)]
    private static partial Regex TrailingArch();

    [GeneratedRegex(@",?\s+(Inc|LLC|L\.L\.C|Ltd|Limited|Corporation|Corp|GmbH|Co|S\.A|SA|AG|B\.V|BV|Pty|Pvt|SRL|S\.r\.l|AB|AS|Oy|KG|Software|Technologies)\.?$", RegexOptions.IgnoreCase)]
    private static partial Regex CompanySuffix();
}
