using System.Windows;
using System.Windows.Input;
using KodiakCleaner.Core.About;

namespace KodiakCleaner.App.ViewModels;

/// <summary>
/// Settings › Contact &amp; Feedback. Each button opens the website's Contact Us form in the person's browser with the
/// topic picked (Ted, 0.8.56: no email address anywhere). Kodiak Cleaner never sends anything itself.
/// </summary>
public sealed class FeedbackViewModel : ObservableObject
{
    public FeedbackViewModel()
    {
        SendFeedbackCommand = new RelayCommand(() => Send(FeedbackKind.Feedback), () => IsConfigured);
        ReportBugCommand = new RelayCommand(() => Send(FeedbackKind.Bug), () => IsConfigured);
        RequestFeatureCommand = new RelayCommand(() => Send(FeedbackKind.Idea), () => IsConfigured);
        ReportSecurityCommand = new RelayCommand(() => Send(FeedbackKind.Security), () => IsConfigured);
        CopyDetailsCommand = new RelayCommand(() =>
        {
            if (ClipboardHelper.CopyOrExplain(Details)) Status = "Details copied. Paste them into your message.";
        });
        StarCommand = new RelayCommand(() => Shell.OpenUrl(AppInfo.ProjectUrl));
    }

    public bool IsConfigured => AppInfo.Feedback.IsConfigured;
    public bool IsNotConfigured => !IsConfigured;

    /// <summary>Beta and dev builds say so at the top.</summary>
    public string BuildNote => AppInfo.Channel switch
    {
        ReleaseChannel.Beta => "You're using a beta. Your reports shape the next release.",
        ReleaseChannel.Dev => "Internal Test Build",
        _ => "",
    };
    public bool HasBuildNote => BuildNote.Length > 0;

    public string Intro => "Tell us what's working, what isn't, and what you'd like next. Missing a feature you'd like? Let us know and we'll look into it.";
    public string RateTitle => $"Rate {AppInfo.ShortName}";
    public string RateText => $"If {AppInfo.ShortName} has earned it, please give it a star on GitHub.";
    public string RateNote => "Before leaving a negative review, please contact us first so we can address any issues and meet your needs.";

    /// <summary>Safe, non-personal facts: version, Windows, plan, install type, theme. Never files or folders.</summary>
    public string Details => AppInfo.Details.ToString();

    private string _status = "";
    public string Status { get => _status; private set => Set(ref _status, value); }

    public ICommand SendFeedbackCommand { get; }
    public ICommand ReportBugCommand { get; }
    public ICommand RequestFeatureCommand { get; }
    public ICommand ReportSecurityCommand { get; }
    public ICommand CopyDetailsCommand { get; }
    public ICommand StarCommand { get; }

    /// <summary>Opens the Contact Us form for this kind of message (also used by About and other pages).</summary>
    public void Send(FeedbackKind kind)
    {
        var link = FeedbackLinks.Build(kind, AppInfo.Feedback, AppInfo.Details);
        if (link is null) return;
        Shell.OpenUrl(link);
        Status = kind == FeedbackKind.Bug
            ? "The Contact Us form opened in your browser. Copy Details below and paste them into your message."
            : "The Contact Us form opened in your browser.";
    }
}
