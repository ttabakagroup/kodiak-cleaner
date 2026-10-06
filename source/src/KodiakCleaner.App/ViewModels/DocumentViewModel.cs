using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App.ViewModels;

/// <summary>A built-in document (Terms of Use, Privacy Policy) shown offline in the app's theme.</summary>
public sealed class DocumentViewModel
{
    public DocumentViewModel(string markdown, double bodySize = 14)
    {
        Blocks = SimpleMarkdown.Parse(markdown);
        BodySize = bodySize;
    }

    public IReadOnlyList<MarkdownBlock> Blocks { get; }
    /// <summary>Body text size. Why It Was Made is larger (Ted, 0.8.24), so the story fills more of the page.</summary>
    public double BodySize { get; }
    public double TitleSize => BodySize >= 16 ? 28 : 22;
    public double HeadingSize => BodySize >= 16 ? 20 : 16;
    public double LineHeight => Math.Round(BodySize * 1.5);
}
