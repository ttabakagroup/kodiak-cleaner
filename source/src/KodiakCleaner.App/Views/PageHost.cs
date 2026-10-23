using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KodiakCleaner.App.Views;

/// <summary>
/// Shows the open page and keeps the pages already visited, hidden, so going back to one shows it straight away with its
/// lists, scroll position and sorting as they were. A ContentControl threw each page away and built it again from scratch
/// on every visit, which was the short pause when switching pages (Ted, 2026-10-23). Hidden pages are Collapsed, so they
/// take no layout or drawing, and the busy bars stop moving while hidden (they only move while visible).
/// </summary>
public sealed class PageHost : Grid
{
    public static readonly DependencyProperty PageProperty = DependencyProperty.Register(nameof(Page), typeof(object), typeof(PageHost),
        new PropertyMetadata(null, (d, e) => ((PageHost)d).Show(e.NewValue)));

    /// <summary>The page's view model; its view comes from the DataTemplates in MainWindow.xaml.</summary>
    public object? Page { get => GetValue(PageProperty); set => SetValue(PageProperty, value); }

    private readonly Dictionary<object, ContentPresenter> _built = new();

    private void Show(object? page)
    {
        ContentPresenter? shown = null;
        if (page is not null && !_built.TryGetValue(page, out shown))
        {
            shown = new ContentPresenter { Content = page };
            _built[page] = shown;
            Children.Add(shown);
        }
        foreach (var child in _built.Values)
        {
            // The page being left may hold the keyboard focus (opened from the corner card or a jump list): let it go, so
            // keys never go to a hidden page.
            if (child != shown && child.IsKeyboardFocusWithin) Keyboard.ClearFocus();
            child.Visibility = child == shown ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
