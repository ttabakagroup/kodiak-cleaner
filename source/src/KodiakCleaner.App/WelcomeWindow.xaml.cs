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
        new("Boot Timer", ": restart and time startup, then see what slows it down"),
        new("Browser Extensions", ": every browser's add-ons; turn them off or remove them"),
        new("Cleaner", ": temporary files, browser caches, logs and the Recycle Bin"),
        new("Defrag and TRIM", ": defragment hard drives and trim SSDs, chosen per drive"),
        new("Disk Analyzer", ": a map of what fills your drives or a folder, colored by kind of file"),
        new("Drive Wiper", ": DoD wipes and firmware erase, matched to each drive type"),
        new("Driver Cleaner", ": remove old graphics drivers completely before a fresh install"),
        new("Duplicate Finder", ": spot copies of the same file on any drive, and files that are alike"),
        new("Empty Folders", ": find and remove empty folders on whole drives or in folders"),
        new("Integrity Check", ": check and repair Windows files, drives and memory with Windows' own tools"),
        new("Large Files", ": find the files taking the most space"),
        new("Mass Rename", ": rename many files at once by a pattern, see every new name first, undo it after"),
        new("Private", ": no ads, no tracking, nothing leaves your PC"),
        new("Programs", ": uninstall several at once, clear leftovers, restore from History"),
        new("Registry", ": finds leftovers and backs up before every fix"),
        new("Scheduled Cleaning", ": clean chosen items every week or month"),
        new("Scheduled Tasks", ": see each task's program, turn tasks off and back on"),
        new("Search and Sort", ": find any item, sort by last cleaned or space freed"),
        new("Startup", ": add, turn off or remove programs, restore from History"),
        new("System Information", ": your PC at a glance, with details and reports"),
        new("System Restore", ": create and manage restore points and Point-in-Time Restore snapshots"),
        new("Your Look", ": Honey, Classic, Glacier or Lagoon, light or dark, any accent"),
    };

    public static Window ShowFor(Window? owner)
    {
        var w = new WelcomeWindow { Owner = owner };
        w.Show();
        return w;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Terms_Click(object sender, RoutedEventArgs e)
    {
        (Owner as MainWindow)?.OpenSettings("terms");
        Close();
    }
}
