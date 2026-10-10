namespace KodiakCleaner.App;

/// <summary>
/// Asking for donations (Ted, 0.8.55): a pop-up at startup that can be turned off only after 10 launches, and a small
/// banner in the window on every 10th launch. Supporters see neither, and their pop-up switch turns itself off.
/// </summary>
public static class Donation
{
    public const int LaunchesBeforeOptOut = 10;

    /// <summary>Counts this launch of the window (called once per start, before anything is shown).</summary>
    public static void CountLaunch()
    {
        var s = Settings.Current;
        if (s.LaunchCount < int.MaxValue) s.LaunchCount++;
        SupporterTurnsPromptOff();
        s.Save();
    }

    /// <summary>The pop-up switch may be turned off once Kodiak Cleaner has started this many times.</summary>
    public static bool CanOptOut => Settings.Current.LaunchCount >= LaunchesBeforeOptOut || Supporter.IsSupporter;

    public static int LaunchesLeft => Math.Max(0, LaunchesBeforeOptOut - Settings.Current.LaunchCount);

    public static bool ShouldPrompt => Settings.Current.ShowDonationPrompt && !Supporter.IsSupporter;

    /// <summary>The banner shows on launches 10, 20, 30… for everyone who isn't a Supporter.</summary>
    public static bool ShouldShowBanner =>
        !Supporter.IsSupporter && Settings.Current.LaunchCount > 0 && Settings.Current.LaunchCount % LaunchesBeforeOptOut == 0;

    /// <summary>A Supporter never needs asking again: the switch goes off for good.</summary>
    public static void SupporterTurnsPromptOff()
    {
        if (!Supporter.IsSupporter || !Settings.Current.ShowDonationPrompt) return;
        Settings.Current.ShowDonationPrompt = false;
        Settings.Current.Save();
    }

    public static string OptOutHint => CanOptOut
        ? "Turn this on to stop the reminder at startup. You can turn it back on in Settings > General."
        : $"You can turn this reminder off after Kodiak Cleaner has started {LaunchesBeforeOptOut} times ({LaunchesLeft} more to go).";

    public static void OpenKoFi() => Shell.OpenUrl(AppInfo.KoFiUrl);
}
