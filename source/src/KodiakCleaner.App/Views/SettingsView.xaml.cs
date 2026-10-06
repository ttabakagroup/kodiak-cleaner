using System.Windows.Controls;
using System.Windows.Input;

namespace KodiakCleaner.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    /// <summary>Arrow keys pick the accent swatch they move to, like any radio group.</summary>
    private void SwatchFocused(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.OriginalSource is RadioButton swatch &&
            (Keyboard.IsKeyDown(Key.Left) || Keyboard.IsKeyDown(Key.Right) || Keyboard.IsKeyDown(Key.Up) || Keyboard.IsKeyDown(Key.Down)))
            swatch.IsChecked = true;
    }
}
