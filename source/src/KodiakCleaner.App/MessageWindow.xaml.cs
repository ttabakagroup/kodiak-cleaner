using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace KodiakCleaner.App;

public enum MessageKind { Question, Warning, Info, Problem }

/// <summary>A themed message box: a heading, the message, an optional caution box and up to three buttons.</summary>
public partial class MessageWindow : Window
{
    private int _result = -1;

    private MessageWindow(MessageKind kind, string heading, string message, string? caution, IReadOnlyList<string> buttons, int defaultButton, int cancelButton)
    {
        InitializeComponent();
        Title = heading;
        Heading.Text = heading;
        Body.Text = message;
        BodyScroll.Visibility = message.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (!string.IsNullOrEmpty(caution))
        {
            CautionText.Text = caution;
            CautionBox.Visibility = Visibility.Visible;
        }

        (Glyph.Text, var warn) = kind switch
        {
            MessageKind.Warning => ("", true),
            MessageKind.Problem => ("", true),
            MessageKind.Info => ("", false),
            _ => ("", false),
        };
        if (warn)
        {
            Badge.SetResourceReference(Border.BackgroundProperty, "WarnBg");
            Glyph.SetResourceReference(TextBlock.ForegroundProperty, "WarnText");
        }

        for (int i = 0; i < buttons.Count; i++)
        {
            var index = i;
            var b = new Button { Content = buttons[i], MinWidth = 96, Margin = new Thickness(0, 0, i == buttons.Count - 1 ? 0 : 10, 0) };
            // On a warning, Yes is shown as the risky choice (even as the default); otherwise the default button is the highlighted one.
            if (kind == MessageKind.Warning && buttons.Count > 1 && buttons[i] == "Yes") b.Style = (Style)FindResource("DangerButton");
            else if (index == defaultButton) b.Style = (Style)FindResource("AccentButton");
            b.IsDefault = index == defaultButton;
            b.IsCancel = index == cancelButton;
            b.Click += (_, _) => { _result = index; DialogResult = true; };
            Buttons.Children.Add(b);
        }
        Loaded += (_, _) => (Buttons.Children[defaultButton] as Button)?.Focus();
        SourceInitialized += (_, _) =>
        {
            ThemeManager.ApplyTitleBar(this);
            // Rounded corners on Windows 11 (ignored elsewhere).
            int round = 2;
            DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 33, ref round, sizeof(int));
        };
        // Letters pick a button (Y for Yes, N for No, ...), like the Windows boxes did.
        PreviewKeyDown += (_, e) =>
        {
            var letter = e.Key.ToString();
            if (letter.Length != 1) return;
            for (int i = 0; i < buttons.Count; i++)
                if (buttons[i].StartsWith(letter, StringComparison.OrdinalIgnoreCase))
                {
                    _result = i;
                    DialogResult = true;
                    e.Handled = true;
                    return;
                }
        };
    }

    /// <returns>The index of the button that was clicked, or <paramref name="cancelButton"/> when the window was closed.</returns>
    public static int Show(MessageKind kind, string heading, string message, string? caution, IReadOnlyList<string> buttons,
        int defaultButton, int cancelButton)
    {
        var w = new MessageWindow(kind, heading, message, caution, buttons, defaultButton, cancelButton);
        if (Application.Current?.MainWindow is { IsLoaded: true, IsVisible: true } owner && owner != w) w.Owner = owner;
        else
        {
            w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            w.ShowInTaskbar = true;
            w.Topmost = true;
        }
        w.ShowDialog();
        return w._result < 0 ? cancelButton : w._result;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // No title bar: drag the window from anywhere that isn't a button.
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
