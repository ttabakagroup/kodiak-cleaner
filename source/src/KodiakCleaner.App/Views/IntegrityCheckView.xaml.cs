using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KodiakCleaner.App.Views;

public partial class IntegrityCheckView : UserControl
{
    public IntegrityCheckView() => InitializeComponent();

    /// <summary>Keeps the newest line of Windows' messages in view.</summary>
    private void Details_TextChanged(object sender, TextChangedEventArgs e) => ((TextBox)sender).ScrollToEnd();

    /// <summary>
    /// The tables and Details sit inside the scrolling page: the wheel scrolls them while they have more to show, then the
    /// page (otherwise the page stops scrolling whenever the pointer is over a table).
    /// </summary>
    private void Inner_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var inner = FindScrollViewer((DependencyObject)sender);
        bool innerCanScroll = inner is not null && (e.Delta > 0 ? inner.VerticalOffset > 0 : inner.VerticalOffset < inner.ScrollableHeight);
        if (innerCanScroll) return;
        e.Handled = true;
        Page.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = MouseWheelEvent, Source = sender });
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer sv) return sv;
            if (FindScrollViewer(child) is { } found) return found;
        }
        return null;
    }
}
