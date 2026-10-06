using System.Windows;

namespace KodiakCleaner.App;

public partial class WelcomeWindow : Window
{
    private WelcomeWindow()
    {
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height - 24;
        Title = $"Getting Started - {AppInfo.Name}";
        SloganText.Text = AppInfo.Slogan;
        FeatureList.ItemsSource = Features;
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
        LogoDance.Attach(Logo);
        ContentRendered += (_, _) => LogoDance.Play(Logo);
    }

    public sealed record Feature(string Name, string Detail);

    /// <summary>A to Z (Ted's rule for every list); updated in 0.8.24 with everything added since 0.8.16.</summary>
    private static readonly Feature[] Features =
    {
        new("Advanced Cleaning", ": deeper temporary files and Windows tasks, each with its caution"),
        new("Browser Extensions", ": list add-ons in every browser, with their icons"),
        new("Cleaner", ": temporary files, browser caches, logs and the Recycle Bin"),
        new("Disk Analyzer", ": see which folders take the most space"),
        new("Drive Wiper", ": securely erase free space or a drive"),
        new("Duplicate Finder", ": spot copies of the same file"),
        new("Empty Folders", ": find and remove empty folders safely"),
        new("Large Files", ": find the files taking the most space"),
        new("Private", ": no ads, no tracking, nothing leaves your PC"),
        new("Programs", ": uninstall several at once, clear leftovers, restore from History"),
        new("Registry", ": finds leftovers and backs up before every fix"),
        new("Scheduled Cleaning", ": clean chosen items every week or month"),
        new("Scheduled Tasks", ": turn background tasks off, and back on"),
        new("Search and Sort", ": find any item, sort by last cleaned or space freed"),
        new("Startup", ": add, turn off or remove programs, restore from History"),
        new("System Information", ": your PC at a glance, with details and reports"),
        new("System Restore", ": create and manage restore points"),
        new("Your Look", ": Honey, Classic or Glacier, light or dark, any accent"),
    };

    public static void ShowFor(Window? owner) => new WelcomeWindow { Owner = owner }.Show();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Terms_Click(object sender, RoutedEventArgs e)
    {
        (Owner as MainWindow)?.OpenSettings("terms");
        Close();
    }
}
