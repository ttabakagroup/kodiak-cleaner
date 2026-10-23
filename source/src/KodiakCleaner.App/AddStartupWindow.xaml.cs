using System.IO;
using System.Windows;
using System.Windows.Controls;
using KodiakCleaner.Core.Startup;
using Microsoft.Win32;

namespace KodiakCleaner.App;

public partial class AddStartupWindow : Window
{
    private string _suggestedName = "";

    private AddStartupWindow()
    {
        InitializeComponent();
        if (!Elevation.IsAdministrator)
        {
            AllUsersSwitch.IsEnabled = false;
            AllUsersCaption.Text += " Needs administrator: use Restart as Administrator at the top.";
        }
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
    }

    /// <summary>The shortcut that was added, or null when nothing was.</summary>
    public string? Added { get; private set; }

    public static string? Ask()
    {
        var w = new AddStartupWindow { Owner = Application.Current.MainWindow };
        return w.ShowDialog() == true ? w.Added : null;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose a Program or Shortcut",
            Filter = "Programs and shortcuts|*.exe;*.lnk;*.bat;*.cmd;*.com|All files|*.*",
            DereferenceLinks = false, // keep the shortcut itself, not what it points to
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        };
        if (dialog.ShowDialog(this) == true) FileBox.Text = dialog.FileName;
    }

    private void FileBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var file = FileBox.Text.Trim().Trim('"');
        var exists = Path.IsPathFullyQualified(file) && File.Exists(file);
        AddButton.IsEnabled = exists;
        MoveRow.Visibility = exists && IsShortcut(file) ? Visibility.Visible : Visibility.Collapsed;
        // Suggest a name from the file, without overwriting one the person typed.
        if (!exists || (NameBox.Text.Length > 0 && NameBox.Text != _suggestedName)) return;
        _suggestedName = Path.GetFileNameWithoutExtension(file);
        NameBox.Text = _suggestedName;
    }

    private static bool IsShortcut(string file) => Path.GetExtension(file).Equals(".lnk", StringComparison.OrdinalIgnoreCase);

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var file = FileBox.Text.Trim().Trim('"');
        try
        {
            Added = StartupManager.AddShortcut(file, NameBox.Text, AllUsersSwitch.IsChecked == true,
                IsShortcut(file) && MoveSwitch.IsChecked == true);
            DialogResult = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
                                         System.Runtime.InteropServices.COMException or System.Security.SecurityException)
        {
            if (ex is UnauthorizedAccessException) Elevation.OfferRestart("Adding to everyone's startup needs administrator rights.");
            else Dialogs.Error("That couldn't be added: " + ex.Message);
        }
    }
}
