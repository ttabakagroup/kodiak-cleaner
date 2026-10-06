namespace KodiakCleaner.Core.Util;

public enum MarkdownBlockKind
{
    Heading1,
    Heading2,
    Heading3,
    Paragraph,
    Bullet,
}

public sealed record MarkdownBlock(MarkdownBlockKind Kind, string Text);

/// <summary>
/// Just enough Markdown for the built-in Terms of Use and Privacy pages: #/##/### headings, paragraphs and
/// "- " bullets. **bold** markers and link syntax are reduced to plain text.
/// </summary>
public static class SimpleMarkdown
{
    public static List<MarkdownBlock> Parse(string markdown)
    {
        var blocks = new List<MarkdownBlock>();
        var paragraph = new List<string>();

        void Flush()
        {
            if (paragraph.Count > 0) blocks.Add(new MarkdownBlock(MarkdownBlockKind.Paragraph, Clean(string.Join(" ", paragraph))));
            paragraph.Clear();
        }

        foreach (var raw in markdown.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) { Flush(); continue; }
            if (line.StartsWith("### ")) { Flush(); blocks.Add(new(MarkdownBlockKind.Heading3, Clean(line[4..]))); }
            else if (line.StartsWith("## ")) { Flush(); blocks.Add(new(MarkdownBlockKind.Heading2, Clean(line[3..]))); }
            else if (line.StartsWith("# ")) { Flush(); blocks.Add(new(MarkdownBlockKind.Heading1, Clean(line[2..]))); }
            else if (line.StartsWith("- ") || line.StartsWith("* ")) { Flush(); blocks.Add(new(MarkdownBlockKind.Bullet, Clean(line[2..]))); }
            else if (blocks.Count > 0 && paragraph.Count == 0 && blocks[^1].Kind == MarkdownBlockKind.Bullet && raw.StartsWith("  "))
                blocks[^1] = blocks[^1] with { Text = blocks[^1].Text + " " + Clean(line) }; // wrapped bullet
            else paragraph.Add(line);
        }
        Flush();
        return blocks;
    }

    // "**Bold**" -> "Bold", "[text](url)" -> "text (url)", "`code`" -> "code"
    private static string Clean(string s)
    {
        s = s.Replace("**", "").Replace("`", "");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\[([^\]]+)\]\(([^)]+)\)", m =>
            m.Groups[1].Value == m.Groups[2].Value || m.Groups[2].Value.StartsWith("mailto:") ? m.Groups[1].Value : $"{m.Groups[1].Value} ({m.Groups[2].Value})");
        return s;
    }
}
