using System.Windows;
using System.Windows.Input;
using KodiakCleaner.Core.About;

namespace KodiakCleaner.App.ViewModels;

/// <summary>
/// Settings › Contact &amp; Feedback. Each button opens a form; Send Email hands the finished message to the person's
/// own email app, where they press Send. Kodiak Cleaner never sends anything itself and needs no mail service.
/// </summary>
public sealed class FeedbackViewModel : ObservableObject
{
    public FeedbackViewModel()
    {
        SendFeedbackCommand = new RelayCommand(() => OpenForm(FeedbackKind.Feedback), () => IsConfigured);
        ReportBugCommand = new RelayCommand(() => OpenForm(FeedbackKind.Bug), () => IsConfigured);
        RequestFeatureCommand = new RelayCommand(() => OpenForm(FeedbackKind.Idea), () => IsConfigured);
        ReportSecurityCommand = new RelayCommand(() => OpenForm(FeedbackKind.Security), () => IsConfigured);
        SendEmailCommand = new RelayCommand(SendForm, () => Message.Trim().Length > 0);
        CopyMessageCommand = new RelayCommand(CopyForm, () => Message.Trim().Length > 0);
        BackCommand = new RelayCommand(() => IsComposing = false);
        foreach (var (label, kind) in new[] { ("Feedback", FeedbackKind.Feedback), ("Bug Report", FeedbackKind.Bug),
                                              ("Feature Request", FeedbackKind.Idea), ("Security", FeedbackKind.Security) })
            Kinds.Add(new Choice<FeedbackKind>(label, kind, kind == FeedbackKind.Feedback, k => Kind = k));
        CopyDetailsCommand = new RelayCommand(() =>
        {
            if (ClipboardHelper.CopyOrExplain(Details)) Status = "Details copied.";
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

    public ICommand SendEmailCommand { get; }
    public ICommand CopyMessageCommand { get; }
    public ICommand BackCommand { get; }

    // ---- The form ----

    private bool _isComposing;
    /// <summary>True while the form is showing instead of the Contact &amp; Feedback overview.</summary>
    public bool IsComposing
    {
        get => _isComposing;
        set { if (Set(ref _isComposing, value)) OnPropertyChanged(nameof(IsOverview)); }
    }
    public bool IsOverview => !IsComposing;

    public System.Collections.ObjectModel.ObservableCollection<Choice<FeedbackKind>> Kinds { get; } = new();

    private FeedbackKind _kind;
    public FeedbackKind Kind
    {
        get => _kind;
        set
        {
            if (!Set(ref _kind, value)) return;
            IncludeDetails = value == FeedbackKind.Bug;
            foreach (var k in Kinds) k.IsSelected = k.Value == value;
            OnPropertyChanged(nameof(FormTitle));
            OnPropertyChanged(nameof(MessageHint));
            OnPropertyChanged(nameof(IsSecurity));
            OnPropertyChanged(nameof(SendNote));
        }
    }

    public string FormTitle => Kind switch
    {
        FeedbackKind.Bug => "Report a Bug",
        FeedbackKind.Idea => "Request a Feature",
        FeedbackKind.Security => "Report a Security Problem",
        _ => "Send Feedback",
    };

    public string MessageHint => Kind switch
    {
        FeedbackKind.Bug => "What were you doing, what did you expect, and what happened instead? Steps to repeat it help most.",
        FeedbackKind.Idea => $"What would you like {AppInfo.ShortName} to do, and how would it help you?",
        FeedbackKind.Security => "How can the problem be reproduced, and what could someone do with it? Please don't include passwords.",
        _ => "What's working, what isn't, and what would you like next?",
    };

    public bool IsSecurity => Kind == FeedbackKind.Security;

    private string _name = "";
    public string Name { get => _name; set => Set(ref _name, value); }

    private string _replyTo = "";
    public string ReplyTo { get => _replyTo; set => Set(ref _replyTo, value); }

    private string _subject = "";
    public string Subject { get => _subject; set => Set(ref _subject, value); }

    private string _message = "";
    public string Message { get => _message; set => Set(ref _message, value); }

    private bool _includeDetails;
    public bool IncludeDetails { get => _includeDetails; set => Set(ref _includeDetails, value); }

    private string _formStatus = "";
    public string FormStatus { get => _formStatus; private set => Set(ref _formStatus, value); }

    public string SendNote => $"Send Email opens your email app with this message to {FeedbackLinks.EmailAddress(AppInfo.Feedback.For(Kind))}, ready to go. Press Send there. Kodiak Cleaner never sends anything by itself.";

    public void OpenForm(FeedbackKind kind)
    {
        _kind = (FeedbackKind)(-1); // make sure the setter runs and resets the details switch
        Kind = kind;
        Subject = "";
        Message = "";
        FormStatus = "";
        OnPropertyChanged(nameof(Details));
        OnPropertyChanged(nameof(SendNote));
        IsComposing = true;
    }

    /// <summary>Opens the general form (used by About and other pages).</summary>
    public void Send(FeedbackKind kind) => OpenForm(kind);

    private FeedbackForm Form => new(Kind, Name, ReplyTo, Subject, Message);

    private void SendForm()
    {
        var link = FeedbackLinks.BuildFromForm(Form, AppInfo.Feedback, AppInfo.Details, IncludeDetails);
        if (link is null)
        {
            FormStatus = "Feedback by email isn't set up in this build.";
            return;
        }
        if (link.Length > FeedbackLinks.MaxMailtoLength)
        {
            FormStatus = "This message is too long for some email apps to receive this way. Click Copy Message and paste it into a new email instead.";
            return;
        }
        Shell.OpenUrl(link);
        FormStatus = "Your email app opened with your message. Press Send there to email it. Thank you!";
    }

    private void CopyForm()
    {
        var to = FeedbackLinks.EmailAddress(AppInfo.Feedback.For(Kind));
        var text = $"To: {to}\nSubject: {FeedbackLinks.FormSubject(Form, AppInfo.Details)}\n\n{FeedbackLinks.FormBody(Form, IncludeDetails ? AppInfo.Details : null)}";
        if (ClipboardHelper.CopyOrExplain(text)) FormStatus = $"Copied. Paste it into a new email to {to}.";
    }
}
