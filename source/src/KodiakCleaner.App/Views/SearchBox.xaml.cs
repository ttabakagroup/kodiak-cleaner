using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KodiakCleaner.App.Views;

public partial class SearchBox : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(SearchBox),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(SearchBox),
        new PropertyMetadata("Search"));

    public SearchBox() => InitializeComponent();

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    // Clicking into the search box starts a fresh search: the box empties and the cursor sits at the start.
    private void Box_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Box.IsKeyboardFocused) return;
        Box.Clear();
        Box.Focus();
        e.Handled = true; // don't let the click move the cursor
    }

    private void Box_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        Box.Clear();
        Box.CaretIndex = 0;
    }
}
