using System.Windows;
using KodiakCleaner.Core.About;

namespace KodiakCleaner.App;

public partial class BetaNoticeWindow : Window
{
    private BetaNoticeWindow()
    {
        InitializeComponent();
        Title = $"Beta Software - {AppInfo.Name}";
        Heading.Text = $"{AppInfo.Name} Is Beta Software";
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
    }

    /// <summary>Dev and Beta builds show the notice at every start until the person turns it off.</summary>
    public static bool ShouldShow => AppInfo.Channel != ReleaseChannel.Stable && Settings.Current.ShowBetaNotice;

    public static void ShowFor(Window owner) => new BetaNoticeWindow { Owner = owner }.ShowDialog();

    // Only closing with the switch on stops the notice; Kodiak Cleaner keeps starting either way.
    protected override void OnClosed(EventArgs e)
    {
        if (DontShowAgain.IsChecked == true)
        {
            Settings.Current.ShowBetaNotice = false;
            Settings.Current.Save();
        }
        base.OnClosed(e);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
