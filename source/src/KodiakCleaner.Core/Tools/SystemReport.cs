using System.Net;
using System.Text;

namespace KodiakCleaner.Core.Tools;

/// <summary>
/// One line of System Information. <see cref="Item"/> names the thing a fact belongs to when a group has several of them
/// (a drive, a memory module, a network adapter); it is empty for facts about the PC as a whole.
/// </summary>
public sealed record SystemFact(string Group, string Name, string Value, string Item = "");

/// <summary>The file formats System Information can export a report in (Ted, 0.8.46), A to Z.</summary>
public enum ReportFormat { Csv, Html, Text }

/// <summary>The theme colors (as #RRGGBB) and font an HTML report is drawn with, so it matches Kodiak Cleaner's look.</summary>
public sealed record ReportStyle(string Background, string Surface, string Text, string Muted, string Line, string Accent, string AccentText, string Font);

/// <summary>
/// Turns System Information's facts into text for Copy to Clipboard, and into Text, HTML or CSV reports saved in the
/// Reports folder. Everything stays on the PC; nothing is sent anywhere.
/// </summary>
public static class SystemReport
{
    public static readonly IReadOnlyList<(ReportFormat Format, string Label)> Formats = new[]
    {
        (ReportFormat.Csv, "CSV Spreadsheet (.csv)"),
        (ReportFormat.Html, "HTML Web Page (.html)"),
        (ReportFormat.Text, "Text Document (.txt)"),
    };

    public static string Extension(ReportFormat format) => format switch
    {
        ReportFormat.Csv => ".csv",
        ReportFormat.Html => ".html",
        _ => ".txt",
    };

    /// <summary>"System Report 2026-10-06 153012.html": sorts by date and never collides within a second.</summary>
    public static string FileName(DateTime when, ReportFormat format) =>
        $"System Report {when:yyyy-MM-dd HHmmss}{Extension(format)}";

    public static string Create(ReportFormat format, IEnumerable<SystemFact> facts, string title, DateTime created, ReportStyle style) => format switch
    {
        ReportFormat.Csv => ToCsv(facts),
        ReportFormat.Html => ToHtml(facts, title, created, style),
        _ => ToText(facts, title, created),
    };

    public static string ToText(IEnumerable<SystemFact> facts, string title, DateTime? created = null)
    {
        var lines = new List<string> { title };
        if (created is { } c) lines.Add($"Created {c:yyyy-MM-dd HH:mm}");
        foreach (var group in facts.GroupBy(f => f.Group))
        {
            lines.Add("");
            lines.Add(group.Key);
            foreach (var item in group.GroupBy(f => f.Item))
            {
                var indent = "  ";
                if (item.Key.Length > 0)
                {
                    lines.Add("  " + item.Key);
                    indent = "    ";
                }
                lines.AddRange(item.Select(f => $"{indent}{f.Name}: {f.Value}"));
            }
        }
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>Section, Item, Name, Value rows. Cells that a spreadsheet would run as a formula are made plain text.</summary>
    public static string ToCsv(IEnumerable<SystemFact> facts)
    {
        var sb = new StringBuilder();
        sb.Append("Section,Item,Name,Value\r\n");
        foreach (var f in facts)
            sb.Append(Cell(f.Group)).Append(',').Append(Cell(f.Item)).Append(',').Append(Cell(f.Name)).Append(',').Append(Cell(f.Value)).Append("\r\n");
        return sb.ToString();
    }

    internal static string Cell(string value)
    {
        // A leading = + - @ (or tab/return) makes Excel treat the cell as a formula; a leading ' keeps it text.
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0])) value = "'" + value;
        return value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 || value != value.Trim()
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }

    /// <summary>A single self-contained page (no scripts, no remote fonts or images) in Kodiak Cleaner's colors.</summary>
    public static string ToHtml(IEnumerable<SystemFact> facts, string title, DateTime created, ReportStyle style)
    {
        static string E(string s) => WebUtility.HtmlEncode(s);
        static string Color(string c) => System.Text.RegularExpressions.Regex.IsMatch(c, "^#[0-9A-Fa-f]{6}$") ? c : "inherit";
        static string Anchor(int i) => $"s{i}";
        var font = new string(style.Font.Where(ch => char.IsLetterOrDigit(ch) || ch is ' ' or ',' or '-').ToArray());
        var groups = facts.GroupBy(f => f.Group).ToList();

        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n")
          .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n")
          .Append("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'\">\n")
          .Append("<title>").Append(E(title)).Append("</title>\n<style>\n")
          .Append(":root{--bg:").Append(Color(style.Background)).Append(";--surface:").Append(Color(style.Surface))
          .Append(";--text:").Append(Color(style.Text)).Append(";--muted:").Append(Color(style.Muted))
          .Append(";--line:").Append(Color(style.Line)).Append(";--accent:").Append(Color(style.Accent))
          .Append(";--accent-text:").Append(Color(style.AccentText)).Append("}\n")
          .Append("*{box-sizing:border-box}\n")
          .Append("body{margin:0;background:var(--bg);color:var(--text);font:15px/1.5 ").Append(font).Append(",'Segoe UI',system-ui,sans-serif}\n")
          .Append("main{max-width:960px;margin:0 auto;padding:32px 20px 48px}\n")
          .Append("header{border-bottom:3px solid var(--accent);padding-bottom:16px;margin-bottom:20px}\n")
          .Append("h1{font-size:28px;margin:0 0 4px}\n")
          .Append(".meta{color:var(--muted);margin:0}\n")
          .Append("nav{display:flex;flex-wrap:wrap;gap:8px;margin:0 0 24px}\n")
          .Append("nav a{color:var(--accent-text);text-decoration:none;border:1px solid var(--line);background:var(--surface);border-radius:999px;padding:4px 12px;font-size:13px}\n")
          .Append("section{background:var(--surface);border:1px solid var(--line);border-radius:10px;padding:18px 20px;margin:0 0 18px;break-inside:avoid}\n")
          .Append("h2{font-size:19px;margin:0 0 10px;color:var(--accent-text)}\n")
          .Append("h3{font-size:15px;margin:14px 0 4px}\n")
          .Append("table{width:100%;border-collapse:collapse}\n")
          .Append("th,td{text-align:left;vertical-align:top;padding:6px 0;border-top:1px solid var(--line)}\n")
          .Append("tr:first-child th,tr:first-child td{border-top:0}\n")
          .Append("th{font-weight:normal;color:var(--muted);width:34%;padding-right:16px}\n")
          .Append("td{word-break:break-word}\n")
          .Append("footer{color:var(--muted);font-size:13px;margin-top:28px}\n")
          .Append("@media print{nav{display:none}}\n")
          .Append("</style>\n</head>\n<body>\n<main>\n<header>\n<h1>").Append(E(title)).Append("</h1>\n")
          .Append("<p class=\"meta\">Created ").Append(E(created.ToString("dddd, d MMMM yyyy, HH:mm"))).Append("</p>\n</header>\n");

        sb.Append("<nav>");
        for (int i = 0; i < groups.Count; i++)
            sb.Append("<a href=\"#").Append(Anchor(i)).Append("\">").Append(E(groups[i].Key)).Append("</a>");
        sb.Append("</nav>\n");

        for (int i = 0; i < groups.Count; i++)
        {
            sb.Append("<section id=\"").Append(Anchor(i)).Append("\">\n<h2>").Append(E(groups[i].Key)).Append("</h2>\n");
            foreach (var item in groups[i].GroupBy(f => f.Item))
            {
                if (item.Key.Length > 0) sb.Append("<h3>").Append(E(item.Key)).Append("</h3>\n");
                sb.Append("<table>\n");
                foreach (var f in item)
                    sb.Append("<tr><th scope=\"row\">").Append(E(f.Name)).Append("</th><td>").Append(E(f.Value)).Append("</td></tr>\n");
                sb.Append("</table>\n");
            }
            sb.Append("</section>\n");
        }
        sb.Append("<footer>Made on this PC by Kodiak Cleaner. It includes serial numbers and network addresses, so share it only with people you trust.</footer>\n")
          .Append("</main>\n</body>\n</html>\n");
        return sb.ToString();
    }

    /// <summary>"3 days, 4 hours" style uptime.</summary>
    public static string Uptime(TimeSpan span)
    {
        string Part(int n, string unit) => $"{n} {unit}{(n == 1 ? "" : "s")}";
        if (span.TotalDays >= 1) return $"{Part((int)span.TotalDays, "day")}, {Part(span.Hours, "hour")}";
        if (span.TotalHours >= 1) return $"{Part(span.Hours, "hour")}, {Part(span.Minutes, "minute")}";
        return Part(Math.Max(0, span.Minutes), "minute");
    }
}
