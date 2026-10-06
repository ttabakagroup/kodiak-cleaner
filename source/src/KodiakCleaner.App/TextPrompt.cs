using System.Windows;
using System.Windows.Controls;

namespace KodiakCleaner.App;

/// <summary>A small themed window that asks for one line of text.</summary>
public static class TextPrompt
{
    public static string? Ask(string title, string message, string initial = "")
    {
        var box = new TextBox { Margin = new Thickness(0, 12, 0, 0), Text = initial };
        var ok = new Button { Content = "Confirm", IsDefault = true, Style = (Style)Application.Current.FindResource("AccentButton"), Margin = new Thickness(0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        var window = new Window
        {
            Title = title,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Application.Current.MainWindow,
            ShowInTaskbar = false,
        };
        window.SetResourceReference(Window.BackgroundProperty, "Bg");
        window.SetResourceReference(Window.ForegroundProperty, "Text");
        window.SetResourceReference(Window.FontFamilyProperty, "FontBody");
        window.FontSize = 14;
        ok.Click += (_, _) => window.DialogResult = true;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(box);
        panel.Children.Add(buttons);
        window.Content = panel;
        window.SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(window);
        window.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };
        return window.ShowDialog() == true ? box.Text : null;
    }
}
