using System.Windows;

namespace KodiakCleaner.App;

public partial class DonationWindow : Window
{
    private DonationWindow()
    {
        InitializeComponent();
        DontShowAgain.IsEnabled = Donation.CanOptOut;
        DontShowLabel.Opacity = Donation.CanOptOut ? 1 : 0.5;
        Hint.Text = Donation.OptOutHint;
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
    }

    public static void ShowFor(Window owner) => new DonationWindow { Owner = owner }.ShowDialog();

    // However it closes (X, Esc, Not Now or Donate), only the switch decides whether it comes back.
    protected override void OnClosed(EventArgs e)
    {
        if (DontShowAgain.IsChecked == true && Donation.CanOptOut)
        {
            Settings.Current.ShowDonationPrompt = false;
            Settings.Current.Save();
        }
        base.OnClosed(e);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Donate_Click(object sender, RoutedEventArgs e)
    {
        Donation.OpenKoFi();
        Close();
    }
}
