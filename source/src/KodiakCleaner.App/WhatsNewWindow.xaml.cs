using System.Windows;
using KodiakCleaner.Core.About;

namespace KodiakCleaner.App;

public partial class WhatsNewWindow : Window
{
    private WhatsNewWindow(IReadOnlyList<ChangelogRelease> releases, Version? previous)
    {
        InitializeComponent();
        Title = $"What's New in {AppInfo.Name}";
        Heading.Text = $"What's New in {AppInfo.Name} {AppInfo.VersionName}";
        Subheading.Text = previous is null
            ? "Highlights of this version."
            : $"Everything that changed since version {previous.ToString(3)}, the version you had before.";
        ReleaseList.ItemsSource = releases;
        ShowAgain.IsChecked = Settings.Current.ShowWhatsNewAfterUpdate;
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
    }

    public static void ShowFor(IReadOnlyList<ChangelogRelease> releases, Version? previous)
    {
        var w = new WhatsNewWindow(releases, previous) { Owner = Application.Current.MainWindow };
        w.Show();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (ShowAgain.IsChecked != Settings.Current.ShowWhatsNewAfterUpdate)
        {
            Settings.Current.ShowWhatsNewAfterUpdate = ShowAgain.IsChecked == true;
            Settings.Current.Save();
        }
        base.OnClosed(e);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void FullChangelog_Click(object sender, RoutedEventArgs e)
    {
        (Owner as MainWindow)?.ShowFullChangelog();
        Close();
    }
}
