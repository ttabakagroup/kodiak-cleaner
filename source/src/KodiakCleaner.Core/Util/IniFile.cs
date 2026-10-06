using System.Globalization;
using System.Text;

namespace KodiakCleaner.Core.Util;

/// <summary>
/// Minimal INI reader/writer: <c>[Section]</c> headers, <c>Key=Value</c> lines, <c>;</c> or <c>#</c> comments.
/// Keys are case-insensitive. Unknown keys survive a load/save round trip.
/// </summary>
public sealed class IniFile
{
    private readonly Dictionary<string, Dictionary<string, string>> _sections = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _sectionOrder = new();

    public static IniFile Parse(string text)
    {
        var ini = new IniFile();
        string section = "General";
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim().TrimEnd('\r');
            if (line.Length == 0 || line[0] is ';' or '#') continue;
            if (line[0] == '[' && line[^1] == ']')
            {
                section = line[1..^1].Trim();
                continue;
            }
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            ini.Set(section, line[..eq].Trim(), line[(eq + 1)..].Trim());
        }
        return ini;
    }

    public string? Get(string section, string key) =>
        _sections.TryGetValue(section, out var s) && s.TryGetValue(key, out var v) ? v : null;

    /// <summary>The sections in file order.</summary>
    public IReadOnlyList<string> SectionNames => _sectionOrder;

    /// <summary>Every key and value in one section (empty when it doesn't exist).</summary>
    public IEnumerable<KeyValuePair<string, string>> Section(string section) =>
        _sections.TryGetValue(section, out var s) ? s.ToList() : Enumerable.Empty<KeyValuePair<string, string>>();

    public string GetString(string section, string key, string fallback) => Get(section, key) ?? fallback;

    public bool GetBool(string section, string key, bool fallback) => Get(section, key)?.ToLowerInvariant() switch
    {
        "1" or "true" or "yes" or "on" => true,
        "0" or "false" or "no" or "off" => false,
        _ => fallback,
    };

    public int GetInt(string section, string key, int fallback) =>
        int.TryParse(Get(section, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    public void Set(string section, string key, string? value)
    {
        if (!_sections.TryGetValue(section, out var s))
        {
            s = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _sections[section] = s;
            _sectionOrder.Add(section);
        }
        // Values are single-line by definition.
        s[key] = (value ?? "").Replace("\r", " ").Replace("\n", " ");
    }

    public void Set(string section, string key, bool value) => Set(section, key, value ? "1" : "0");
    public void Set(string section, string key, int value) => Set(section, key, value.ToString(CultureInfo.InvariantCulture));

    public string Serialize(string? headerComment = null)
    {
        var sb = new StringBuilder();
        if (headerComment is not null)
            foreach (var line in headerComment.Split('\n')) sb.Append("; ").Append(line.TrimEnd('\r')).Append("\r\n");
        foreach (var name in _sectionOrder)
        {
            if (sb.Length > 0) sb.Append("\r\n");
            sb.Append('[').Append(name).Append("]\r\n");
            foreach (var (k, v) in _sections[name]) sb.Append(k).Append('=').Append(v).Append("\r\n");
        }
        return sb.ToString();
    }
}
