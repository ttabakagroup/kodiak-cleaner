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
            : IsNewChannel(previous)
                ? $"{AppInfo.Name} is now in {AppInfo.ChannelLabel}, and its changelog starts fresh here. About > All Changes Since the Start lists every earlier change."
                : $"Everything that changed since version {previous.ToString(3)}, the version you had before.";
        ReleaseList.ItemsSource = releases;
        ShowAgain.IsChecked = Settings.Current.ShowWhatsNewAfterUpdate;
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
    }

    /// <summary>
    /// True when the person had an earlier channel's build (0.8.x Dev before 0.9.0 Beta). The changelog was reset for the Beta
    /// (Ted), so What's New then shows only this channel's entries, which <see cref="Changelog.Since"/> already returns.
    /// </summary>
    private static bool IsNewChannel(Version previous) => Range(previous) < Range(AppInfo.Version);

    // Dev 0.8.x, Beta 0.9.x, Stable 1.x and up.
    private static int Range(Version v) => v.Major >= 1 ? 2 : v.Minor >= 9 ? 1 : 0;

    public static Window ShowFor(IReadOnlyList<ChangelogRelease> releases, Version? previous)
    {
        var w = new WhatsNewWindow(releases, previous) { Owner = Application.Current.MainWindow };
        w.Show();
        return w;
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
