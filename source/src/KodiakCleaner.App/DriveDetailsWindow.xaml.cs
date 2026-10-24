using System.Windows;
using System.Windows.Controls;
using KodiakCleaner.Core.Tools;

namespace KodiakCleaner.App;

/// <summary>
/// Drive Details (Ted, 0.8.116): the info button on each Drive Wiper row. Shows the drive's model, serial number, type, connection,
/// size, firmware, partition style and partitions, its own health report (the same values Check Drive reads) and the disk errors
/// Windows logged for it, with Copy. Read in the background when it opens; only queries, nothing is written to the drive.
/// </summary>
public partial class DriveDetailsWindow : Window
{
    private readonly WipeDisk _disk;
    private string _text = "";

    private DriveDetailsWindow(WipeDisk disk)
    {
        InitializeComponent();
        _disk = disk;
        Title = $"Drive Details: {disk.Name}";
        Subheading.Text = $"{disk.Name} · {Core.Util.ByteSize.Format(disk.Size)} · {disk.KindName}";
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
        Loaded += async (_, _) => await LoadAsync();
    }

    public static void Show(WipeDisk disk)
    {
        var w = new DriveDetailsWindow(disk);
        if (Application.Current?.MainWindow is { IsLoaded: true } owner) w.Owner = owner;
        else w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        w.ShowDialog();
    }

    private async Task LoadAsync()
    {
        DriveDetailsInfo info;
        try
        {
            info = await Task.Run(() => DriveDetails.Read(_disk, Elevation.IsAdministrator));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or PlatformNotSupportedException)
        {
            Reading.Text = "The drive's details couldn't be read: " + e.Message;
            return;
        }
        if (!IsLoaded) return;
        var sections = DriveDetails.Sections(info);
        _text = DriveDetails.Text(info, sections);
        Sections.Children.Clear();
        foreach (var s in sections) Sections.Children.Add(Section(s, Sections.Children.Count == 0));
        CopyButton.IsEnabled = true;
    }

    /// <summary>A heading, then each name and value in two columns, then the section's note.</summary>
    private UIElement Section(DriveDetailSection section, bool first)
    {
        var panel = new StackPanel { Margin = new Thickness(0, first ? 0 : 22, 0, 0) };
        panel.Children.Add(new TextBlock { Text = section.Title, Style = (Style)FindResource("SectionTitle"), TextWrapping = TextWrapping.Wrap });
        var grid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        foreach (var line in section.Lines)
        {
            int row = grid.RowDefinitions.Count;
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var name = new TextBlock { Text = line.Name, Margin = new Thickness(0, 3, 16, 3), TextWrapping = TextWrapping.Wrap };
            name.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
            var value = new TextBox
            {
                // Read-only text that can be selected and copied (serial numbers especially), drawn like plain text.
                Text = line.Value, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, BorderThickness = new Thickness(0),
                Background = System.Windows.Media.Brushes.Transparent, Padding = new Thickness(0), Margin = new Thickness(0, 3, 0, 3),
                Template = PlainText, IsTabStop = false,
            };
            value.SetResourceReference(ForegroundProperty, "Text");
            System.Windows.Automation.AutomationProperties.SetName(value, line.Name.Length > 0 ? line.Name : section.Title);
            Grid.SetRow(name, row);
            Grid.SetRow(value, row);
            Grid.SetColumn(value, 1);
            grid.Children.Add(name);
            grid.Children.Add(value);
        }
        if (section.Lines.Count > 0) panel.Children.Add(grid);
        if (section.Note is { Length: > 0 } note)
            panel.Children.Add(new TextBlock { Text = note, Style = (Style)FindResource("Caption"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) });
        return panel;
    }

    /// <summary>A text box with no border or background of its own, so a value reads as text but can still be selected.</summary>
    private static readonly ControlTemplate PlainText = MakePlainText();

    private static ControlTemplate MakePlainText()
    {
        var template = new ControlTemplate(typeof(TextBox));
        var host = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost");
        host.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
        host.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
        host.SetValue(FocusableProperty, false);
        template.VisualTree = host;
        return template;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (_text.Length == 0) return;
        if (ClipboardHelper.CopyOrExplain(_text)) CopyNote.Text = "Copied to the clipboard.";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
