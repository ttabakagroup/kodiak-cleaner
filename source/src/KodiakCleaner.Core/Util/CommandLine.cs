namespace KodiakCleaner.Core.Util;

/// <summary>Helpers for the command strings Windows stores in the registry (Run keys, UninstallString, ...).</summary>
public static class CommandLine
{
    private static readonly string[] ExecutableExtensions =
    {
        ".exe", ".com", ".bat", ".cmd", ".dll", ".msc", ".lnk", ".scr", ".cpl",
        ".vbs", ".vbe", ".js", ".jse", ".wsf", ".ps1", ".hta", ".msi", ".jar", ".py", ".ocx",
    };

    /// <summary>The program path to check for existence, or "" when it could only be guessed (never reported missing).</summary>
    public static string CheckablePath(string? command)
    {
        var (exe, _, guessed) = SplitChecked(command);
        return guessed ? "" : exe;
    }

    /// <summary>
    /// Like <see cref="Split"/>, but also says whether the program path was a guess (unquoted, no known extension,
    /// cut at the first space). A guessed path must never be reported as missing.
    /// </summary>
    public static (string Executable, string Arguments, bool Guessed) SplitChecked(string? command)
    {
        var (exe, args) = Split(command);
        var text = command?.Trim() ?? "";
        bool guessed = text.Length > 0 && text[0] != '"' && args.Length > 0 &&
                       !ExecutableExtensions.Any(e => exe.EndsWith(e, StringComparison.OrdinalIgnoreCase));
        return (exe, args, guessed);
    }

    /// <summary>
    /// Splits a command into the program path and its arguments. Handles quoted paths and
    /// unquoted paths that contain spaces (e.g. <c>C:\Program Files\App\app.exe /min</c>).
    /// Environment variables are not expanded here.
    /// </summary>
    public static (string Executable, string Arguments) Split(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return ("", "");
        var text = command.Trim();

        if (text[0] == '"')
        {
            int close = text.IndexOf('"', 1);
            if (close < 0) return (text.Trim('"'), "");
            return (text[1..close], text[(close + 1)..].Trim());
        }

        // Unquoted: take the shortest prefix that ends in a known executable extension
        // and is followed by whitespace, a comma (rundll32 style) or the end of string.
        // Shortest across every extension, so "start.cmd C:\Apps\app.exe" is the script with the program as its argument.
        int shortest = -1;
        foreach (var ext in ExecutableExtensions)
        {
            int searchFrom = 0;
            while (true)
            {
                int idx = text.IndexOf(ext, searchFrom, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) break;
                int end = idx + ext.Length;
                if (end == text.Length || char.IsWhiteSpace(text[end]) || text[end] == ',')
                {
                    if (shortest < 0 || end < shortest) shortest = end;
                    break;
                }
                searchFrom = end;
            }
        }
        if (shortest > 0) return (text[..shortest], text[shortest..].TrimStart(',').Trim());

        int space = text.IndexOf(' ');
        return space < 0 ? (text, "") : (text[..space], text[(space + 1)..].Trim());
    }
}
