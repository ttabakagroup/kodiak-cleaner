using System.Windows;
using System.Windows.Input;
using KodiakCleaner.Core.About;

namespace KodiakCleaner.App.ViewModels;

/// <summary>Settings › About: version and channel, plan, what's new, privacy, credits and links.</summary>
public sealed class AboutViewModel : ObservableObject
{
    private readonly SettingsViewModel _settings;

    public AboutViewModel(SettingsViewModel settings)
    {
        _settings = settings;
        SeeAllChangesCommand = new RelayCommand(() => ShowChangelog = true);
        BackCommand = new RelayCommand(() => ShowChangelog = false);
        OpenSupporterCommand = new RelayCommand(() => _settings.Open("supporter"));
        OpenFeedbackCommand = new RelayCommand(() => _settings.Open("feedback"));
        OpenStoryCommand = new RelayCommand(() => _settings.Open("story"));
        GettingStartedCommand = new RelayCommand(() => WelcomeWindow.ShowFor(Application.Current.MainWindow));
        PrivacyPolicyCommand = new RelayCommand(() =>
        {
            if (AppInfo.PrivacyUrl.Length > 0) Shell.OpenUrl(AppInfo.PrivacyUrl);
            else _settings.Open("privacy");
        });
        ProjectPageCommand = new RelayCommand(() => Shell.OpenUrl(AppInfo.ProjectUrl));
        WebsiteCommand = new RelayCommand(() => Shell.OpenUrl(AppInfo.HomepageUrl));
        Supporter.Changed += () =>
        {
            OnPropertyChanged(nameof(PlanName));
            OnPropertyChanged(nameof(HowUnlocked));
            OnPropertyChanged(nameof(UnlockedText));
        };
    }

    public string Name => AppInfo.Name;
    public string VersionName => AppInfo.VersionName;
    public string Channel => AppInfo.ChannelLabel;
    public string ReleaseDate => CurrentRelease?.Date ?? "";

    /// <summary>Check for Updates is on About (0.8.41, Ted) as well as Settings > Updates; both share SettingsViewModel's check and status.</summary>
    public SettingsViewModel Updates => _settings;

    public string PlanName => Supporter.PlanName;
    public string HowUnlocked => Supporter.HowUnlocked;
    /// <summary>"· unlocked by: Supporter code" after the plan name; nothing at all on the free plan (never "unlocked by: None").</summary>
    public string UnlockedText => Supporter.IsSupporter || Supporter.Preview != PlanPreview.None ? "· unlocked by: " + HowUnlocked : "";

    /// <summary>This version's CHANGELOG.md entry (bundled into the app; no network request).</summary>
    public ChangelogRelease? CurrentRelease => AppInfo.Releases.FirstOrDefault(r => r.Version == AppInfo.Version) ?? AppInfo.Releases.FirstOrDefault();
    public List<ChangelogRelease> Releases => AppInfo.Releases;

    /// <summary>
    /// The full changelog, only while it is open. Built while hidden, its 90-odd releases made About take most of a
    /// second to open every time (Ted, 2026-10-23).
    /// </summary>
    public List<ChangelogRelease>? ShownReleases => ShowChangelog ? Releases : null;

    public string PrivacySummary =>
        $"{AppInfo.ShortName} works entirely on this PC. It sends no usage data, crash reports or identifiers, and shows no ads. " +
        "Your settings, backups and activity log are saved in plain files on this PC and never leave your computer. " +
        "The only time it goes online is to check its website for a new version, and only when you click Check for Updates or turn on " +
        "automatic updates. Contact & Feedback opens the Contact Us form on the website in your browser, so nothing is sent unless you send it.";

    public IReadOnlyList<Credit> Credits => global::KodiakCleaner.App.Credits.All;

    private bool _showChangelog;
    public bool ShowChangelog { get => _showChangelog; set { if (Set(ref _showChangelog, value)) { OnPropertyChanged(nameof(ShowAbout)); OnPropertyChanged(nameof(ShownReleases)); } } }
    public bool ShowAbout => !ShowChangelog;

    public ICommand SeeAllChangesCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand OpenSupporterCommand { get; }
    public ICommand OpenFeedbackCommand { get; }
    public ICommand OpenStoryCommand { get; }
    public string Slogan => AppInfo.Slogan;
    public ICommand GettingStartedCommand { get; }
    public ICommand PrivacyPolicyCommand { get; }
    public ICommand ProjectPageCommand { get; }
    public ICommand WebsiteCommand { get; }
}
