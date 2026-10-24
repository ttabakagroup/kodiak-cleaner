using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace KodiakCleaner.App.Views;

/// <summary>
/// A line of muted text in the middle of a table or list while it has no entries, saying what shows there and how to fill it
/// (Ted, 2026-10-23: an empty card looked barren, as if something was missing). Set <c>views:Placeholder.Text</c> on a DataGrid
/// or ItemsControl; PlaceholderTests makes every table and list that can be empty have one. The text is drawn over the list (an
/// adorner), so it takes no room and needs no change to the list's template; it hides as soon as an entry arrives.
/// </summary>
public static class Placeholder
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(Placeholder), new PropertyMetadata("", OnTextChanged));

    public static string GetText(DependencyObject d) => (string)d.GetValue(TextProperty);
    public static void SetText(DependencyObject d, string value) => d.SetValue(TextProperty, value);

    private static readonly DependencyProperty AdornerProperty = DependencyProperty.RegisterAttached(
        "Adorner", typeof(TextAdorner), typeof(Placeholder), new PropertyMetadata(null));

    /// <summary>The list's own MinHeight while the text has made it taller, so a list sized to its rows doesn't shrink to nothing.</summary>
    private static readonly DependencyProperty SavedMinHeightProperty = DependencyProperty.RegisterAttached(
        "SavedMinHeight", typeof(double?), typeof(Placeholder), new PropertyMetadata(null));

    /// <summary>Room for a line or two of text, below the column headers in a table.</summary>
    private const double ListRoom = 48, TableRoom = 96;

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ItemsControl list) return;
        if (e.OldValue is "" or null)
        {
            list.Loaded += (_, _) => Update(list);
            list.IsVisibleChanged += (_, _) => Update(list);
            ((INotifyCollectionChanged)list.Items).CollectionChanged += (_, _) => Update(list);
        }
        Update(list);
    }

    private static void Update(ItemsControl list)
    {
        var text = GetText(list);
        var show = list.IsVisible && list.Items.Count == 0 && text.Length > 0;
        var adorner = (TextAdorner?)list.GetValue(AdornerProperty);
        if (adorner is null)
        {
            if (!show || AdornerLayer.GetAdornerLayer(list) is not { } layer) return;
            adorner = new TextAdorner(list);
            layer.Add(adorner);
            list.SetValue(AdornerProperty, adorner);
        }
        adorner.Text = text;
        adorner.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

        var saved = (double?)list.GetValue(SavedMinHeightProperty);
        if (show && saved is null)
        {
            list.SetValue(SavedMinHeightProperty, list.MinHeight);
            list.MinHeight = Math.Max(list.MinHeight, list is DataGrid ? TableRoom : ListRoom);
        }
        else if (!show && saved is { } own)
        {
            list.MinHeight = own;
            list.ClearValue(SavedMinHeightProperty);
        }
    }

    /// <summary>The text, centred in the list with a little room at the sides; clicks go through to the list.</summary>
    private sealed class TextAdorner : Adorner
    {
        private readonly TextBlock _text = new()
        {
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            MaxWidth = 460,
        };

        // A faint list icon above the text, so the empty area reads as "entries go here" at a glance.
        private readonly TextBlock _icon = new()
        {
            Text = "\uE8FD",
            FontSize = 26,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8),
            Opacity = 0.6,
        };

        private readonly StackPanel _panel = new();

        public TextAdorner(UIElement list) : base(list)
        {
            IsHitTestVisible = false;
            _icon.SetResourceReference(StyleProperty, "Icon");
            _icon.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
            _text.SetResourceReference(StyleProperty, "Caption");
            _text.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
            _panel.Children.Add(_icon);
            _panel.Children.Add(_text);
            AddVisualChild(_panel);
        }

        public string Text { set => _text.Text = value; }

        private static double HeaderHeight(DependencyObject grid)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(grid); i++)
            {
                var child = VisualTreeHelper.GetChild(grid, i);
                if (child is System.Windows.Controls.Primitives.DataGridColumnHeadersPresenter presenter) return presenter.ActualHeight;
                var inner = HeaderHeight(child);
                if (inner > 0) return inner;
            }
            return 0;
        }

        protected override int VisualChildrenCount => 1;
        protected override Visual GetVisualChild(int index) => _panel;

        protected override Size MeasureOverride(Size constraint)
        {
            var size = AdornedElement.RenderSize;
            // The icon only where there's room for it: a list sized to the text alone shows just the text.
            var room = size.Height - (AdornedElement is DataGrid ? HeaderHeight(AdornedElement) : 0);
            _icon.Visibility = room >= 110 ? Visibility.Visible : Visibility.Collapsed;
            _panel.Measure(new Size(Math.Max(0, size.Width - 32), Math.Max(0, size.Height)));
            return size;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var size = AdornedElement.RenderSize;
            var want = _panel.DesiredSize;
            // In a table the text goes below the column headers; a short list shows it near its top, a tall one in the middle.
            var headers = AdornedElement is DataGrid ? HeaderHeight(AdornedElement) : 0;
            var room = size.Height - headers;
            var top = headers + (room < 160 ? Math.Min(14, Math.Max(0, room - want.Height)) : (room - want.Height) / 2);
            _panel.Arrange(new Rect(Math.Max(0, (size.Width - want.Width) / 2), Math.Max(0, top), want.Width, want.Height));
            return size;
        }
    }
}
