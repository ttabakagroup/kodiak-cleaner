using System.Text;
using System.Text.RegularExpressions;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.Core.About;

/// <summary>
/// The seven Docs files Ted's standard puts in the project's Docs folder, in Program Files\Kodiak Cleaner\Docs and in a
/// Docs folder inside every ZIP. People read them in Notepad, so they are plain text made from the repository's Markdown.
/// </summary>
public static class DocsFolder
{
    public const string Homepage = "https://www.kodiakcleaner.app/";
    public const string Repository = "https://github.com/ttabakagroup/kodiakcleaner";

    /// <summary>The file names, A to Z.</summary>
    public static readonly string[] Files =
        ["Changelog.txt", "Download.url", "Homepage.url", "License.txt", "Privacy.txt", "ReadMe.txt", "TermsOfUse.txt"];

    /// <summary>
    /// The direct download of the newest installer for a channel. GitHub's releases/latest skips pre-releases, so Dev and
    /// Beta use a fixed release (latest-dev, latest-beta) that each build replaces. It links the bare MSI, the installer
    /// every release carries (Ted, 0.8.31; the only installer file since 0.8.37).
    /// </summary>
    public static string DownloadUrl(ReleaseChannel channel) => channel == ReleaseChannel.Stable
        ? $"{Repository}/releases/latest/download/KodiakCleaner-{channel}-Setup.msi"
        : $"{Repository}/releases/download/latest-{channel.ToString().ToLowerInvariant()}/KodiakCleaner-{channel}-Setup.msi";

    /// <summary>KodiakCleaner-Dev-Setup.msi, KodiakCleaner-Stable-Portable.zip: the same name every release, so links keep working.</summary>
    public static string PackageName(ReleaseChannel channel, string kind) => $"KodiakCleaner-{channel}-{kind}.zip";

    /// <summary>Writes the seven files from the repository's sources into <paramref name="outDir"/>.</summary>
    public static void Write(string repo, string outDir, ReleaseChannel channel)
    {
        Directory.CreateDirectory(outDir);
        string Read(params string[] parts) => File.ReadAllText(Path.Combine([repo, .. parts]));
        void Save(string name, string text) => File.WriteAllText(Path.Combine(outDir, name), text.Replace("\r\n", "\n").Replace("\n", "\r\n"), new UTF8Encoding(false));

        Save("Changelog.txt", FromMarkdown(Read("CHANGELOG.md")));
        Save("Download.url", Shortcut(DownloadUrl(channel)));
        Save("Homepage.url", Shortcut(Homepage));
        Save("License.txt", Read("LICENSE").TrimEnd() + "\n");
        Save("Privacy.txt", FromMarkdown(Read("PRIVACY.md")));
        Save("ReadMe.txt", FromMarkdown(Read("dist-files", "ReadMe.md")));
        Save("TermsOfUse.txt", FromMarkdown(Read("terms-of-use.md")));
    }

    /// <summary>A Windows Internet shortcut.</summary>
    public static string Shortcut(string url) => $"[InternetShortcut]\nURL={url}\n";

    /// <summary>Markdown to clean text: underlined headings, "- " bullets, no # or ** marks. Empty sections (Unreleased) are dropped.</summary>
    public static string FromMarkdown(string markdown)
    {
        var blocks = SimpleMarkdown.Parse(Regex.Replace(markdown, "<!--.*?-->", "", RegexOptions.Singleline));
        var sb = new StringBuilder();
        MarkdownBlockKind? previous = null;
        for (int i = 0; i < blocks.Count; i++)
        {
            var kind = blocks[i].Kind;
            var text = Regex.Replace(blocks[i].Text, @"^\[([^\]]+)\]", "$1"); // "[0.8.30 Dev] - date" headings
            // Skip a heading with nothing under it before the next heading of the same or a higher level.
            if (kind < MarkdownBlockKind.Paragraph && (i + 1 == blocks.Count || blocks[i + 1].Kind <= kind)) continue;
            // A blank line between blocks; bullets in a row stay together.
            if (previous is not null && !(kind == MarkdownBlockKind.Bullet && previous == MarkdownBlockKind.Bullet)) sb.Append('\n');
            sb.Append(kind == MarkdownBlockKind.Bullet ? "- " : "").Append(text).Append('\n');
            if (kind == MarkdownBlockKind.Heading1) sb.Append('=', text.Length).Append('\n');
            if (kind == MarkdownBlockKind.Heading2) sb.Append('-', text.Length).Append('\n');
            previous = kind;
        }
        return sb.ToString().TrimEnd() + "\n";
    }
}
