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
        };
    }

    public string Name => AppInfo.Name;
    public string VersionName => AppInfo.VersionName;
    public string Channel => AppInfo.ChannelLabel;
    public string ReleaseDate => CurrentRelease?.Date ?? "";

    public string PlanName => Supporter.PlanName;
    public string HowUnlocked => Supporter.HowUnlocked;

    /// <summary>This version's CHANGELOG.md entry (bundled into the app; no network request).</summary>
    public ChangelogRelease? CurrentRelease => AppInfo.Releases.FirstOrDefault(r => r.Version == AppInfo.Version) ?? AppInfo.Releases.FirstOrDefault();
    public List<ChangelogRelease> Releases => AppInfo.Releases;

    public string PrivacySummary =>
        $"{AppInfo.ShortName} works entirely on this PC. It sends no usage data, crash reports or identifiers, and shows no ads. " +
        "Your settings and registry backups are saved in plain files next to KodiakCleaner.exe and never leave your computer. " +
        "The only time it goes online is to check GitHub for a new version, and only when you click Check for Updates or turn on " +
        "automatic updates. Feedback opens in your own email app, so nothing is sent unless you send it.";

    public IReadOnlyList<Credit> Credits => global::KodiakCleaner.App.Credits.All;

    private bool _showChangelog;
    public bool ShowChangelog { get => _showChangelog; set { if (Set(ref _showChangelog, value)) OnPropertyChanged(nameof(ShowAbout)); } }
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
