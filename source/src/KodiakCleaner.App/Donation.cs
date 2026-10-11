using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App;

/// <summary>
/// Asking for donations (Ted, 0.8.55): a pop-up at startup that can be turned off only after 10 launches, and a small
/// banner in the window on every 10th launch. Supporters see neither, and their pop-up switch turns itself off.
/// </summary>
public static class Donation
{
    public const int LaunchesBeforeOptOut = 10;

    /// <summary>True when this start was counted; the reminder and banner only show on counted starts, never on a copy reopened by an update or Restart as Administrator.</summary>
    public static bool CountedThisStart { get; private set; }

    /// <summary>Counts this start of the window, once, before anything is shown (see LaunchCounter for which starts count).</summary>
    public static void CountLaunch(bool countStart)
    {
        var s = Settings.Current;
        if (countStart) s.LaunchCount = LaunchCounter.Next(s.LaunchCount);
        CountedThisStart = countStart;
        SupporterTurnsPromptOff();
        s.Save();
    }

    /// <summary>The pop-up switch may be turned off once Kodiak Cleaner has started this many times.</summary>
    public static bool CanOptOut => Settings.Current.LaunchCount >= LaunchesBeforeOptOut || Supporter.IsSupporter;

    public static int LaunchesLeft => Math.Max(0, LaunchesBeforeOptOut - Settings.Current.LaunchCount);

    public static bool ShouldPrompt => CountedThisStart && Settings.Current.ShowDonationPrompt && !Supporter.IsSupporter;

    /// <summary>The banner shows on launches 10, 20, 30… for everyone who isn't a Supporter.</summary>
    public static bool ShouldShowBanner =>
        CountedThisStart && !Supporter.IsSupporter && Settings.Current.LaunchCount > 0 && Settings.Current.LaunchCount % LaunchesBeforeOptOut == 0;

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
