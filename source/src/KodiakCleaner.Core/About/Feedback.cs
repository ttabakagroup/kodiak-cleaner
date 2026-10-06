using System.Text;

namespace KodiakCleaner.Core.About;

public enum FeedbackKind
{
    Feedback,
    Bug,
    Idea,
    Security,
}

/// <summary>Where each kind of message goes. Empty = use <see cref="Default"/>. Each is a mailto: or https: address.</summary>
public sealed record FeedbackAddresses(string Default, string Feedback = "", string Bug = "", string Idea = "", string Security = "")
{
    public string For(FeedbackKind kind)
    {
        var specific = kind switch
        {
            FeedbackKind.Feedback => Feedback,
            FeedbackKind.Bug => Bug,
            FeedbackKind.Idea => Idea,
            _ => Security,
        };
        return string.IsNullOrWhiteSpace(specific) ? Default : specific;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Default);

    /// <summary>Security reports should go to a private address, not the general one.</summary>
    public bool HasPrivateSecurityAddress => !string.IsNullOrWhiteSpace(Security) || Default.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);
}

/// <summary>What the person typed into the Contact &amp; Feedback form. Name and reply address are optional.</summary>
public sealed record FeedbackForm(FeedbackKind Kind, string Name, string ReplyTo, string Subject, string Message);

/// <summary>
/// Non-personal facts about this copy of the app, for bug reports. Never holds file names, folders,
/// user names or anything the person cleaned.
/// </summary>
public sealed record ReportDetails(string AppName, string VersionName, string Windows, string Plan, string InstallType, bool Administrator, string Theme)
{
    public override string ToString() =>
        $"App: {AppName} {VersionName}\n" +
        $"Windows: {Windows}\n" +
        $"Plan: {Plan}\n" +
        $"Installed with: {InstallType}\n" +
        $"Running as administrator: {(Administrator ? "yes" : "no")}\n" +
        $"Theme: {Theme}";
}

/// <summary>
/// Builds feedback links. Nothing is ever sent by the app: each link opens in the person's own email app
/// (mailto:) or browser (https: form), where they read, edit and send it themselves.
/// </summary>
public static class FeedbackLinks
{
    public static string Subject(FeedbackKind kind, string shortName) => kind switch
    {
        FeedbackKind.Bug => $"[Bug Report] {shortName} ",
        FeedbackKind.Idea => $"[Feature Request] {shortName} ",
        FeedbackKind.Security => $"[Security] {shortName} ",
        _ => $"[Feedback] {shortName} ",
    };

    /// <summary>The starting text of the message. Only bug reports include <paramref name="details"/>.</summary>
    public static string Body(FeedbackKind kind, ReportDetails details)
    {
        var sb = new StringBuilder();
        switch (kind)
        {
            case FeedbackKind.Bug:
                sb.Append("What I was doing:\n\n\nWhat I expected:\n\n\nWhat happened:\n\n\nSteps to repeat:\n1. \n2. \n\n");
                sb.Append("Details:\n").Append(details).Append('\n');
                break;
            case FeedbackKind.Idea:
                sb.Append("What I'd like ").Append(ShortName(details)).Append(" to do:\n\n\nWhy it would help:\n\n");
                break;
            case FeedbackKind.Security:
                sb.Append("Steps to reproduce the problem:\n1. \n2. \n\n");
                sb.Append("What someone could do with it:\n\n\n(Please don't include passwords or other personal data.)\n");
                break;
        }
        return sb.ToString();
    }

    /// <summary>
    /// mailto: links get a subject and body. https: forms get ?type=feedback|bug|idea|security&amp;version=…
    /// Returns null when no address is set.
    /// </summary>
    public static string? Build(FeedbackKind kind, FeedbackAddresses addresses, ReportDetails details)
    {
        var address = addresses.For(kind).Trim();
        if (address.Length == 0) return null;
        if (address.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            var sep = address.Contains('?') ? '&' : '?';
            var link = $"{address}{sep}subject={Uri.EscapeDataString(Subject(kind, ShortName(details)))}";
            var body = Body(kind, details);
            return body.Length == 0 ? link : $"{link}&body={Uri.EscapeDataString(body)}";
        }
        if (address.StartsWith("https:", StringComparison.OrdinalIgnoreCase))
        {
            var sep = address.Contains('?') ? '&' : '?';
            return $"{address}{sep}type={TypeName(kind)}&version={Uri.EscapeDataString(details.VersionName)}";
        }
        throw new ArgumentException($"Feedback addresses must start with mailto: or https: ({address}).");
    }

    /// <summary>Longest mailto: link sent to the email app; some email apps cut longer links off.</summary>
    public const int MaxMailtoLength = 2000;

    /// <summary>
    /// The full message from the Contact &amp; Feedback form: what the person wrote, who to reply to (if they said),
    /// and the safe details when they left that switch on.
    /// </summary>
    public static string FormBody(FeedbackForm form, ReportDetails? details)
    {
        var sb = new StringBuilder(form.Message.Trim().Replace("\r\n", "\n"));
        var from = (form.Name.Trim(), form.ReplyTo.Trim()) switch
        {
            ("", "") => "",
            (var n, "") => n,
            ("", var e) => e,
            (var n, var e) => $"{n} <{e}>",
        };
        if (from.Length > 0 || details is not null) sb.Append("\n\n---");
        if (from.Length > 0) sb.Append("\nFrom: ").Append(from);
        if (details is not null) sb.Append("\n").Append(details);
        return sb.Append('\n').ToString();
    }

    /// <summary>The subject line: the person's own, or the standard one for that kind of message.</summary>
    public static string FormSubject(FeedbackForm form, ReportDetails details) =>
        string.IsNullOrWhiteSpace(form.Subject) ? Subject(form.Kind, ShortName(details)).TrimEnd() : $"{Subject(form.Kind, ShortName(details))}- {form.Subject.Trim()}";

    /// <summary>A mailto: link holding the whole form, ready for the person's email app. Null without a mailto: address.</summary>
    public static string? BuildFromForm(FeedbackForm form, FeedbackAddresses addresses, ReportDetails details, bool includeDetails)
    {
        var address = addresses.For(form.Kind).Trim();
        if (!address.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) return null;
        var sep = address.Contains('?') ? '&' : '?';
        return $"{address}{sep}subject={Uri.EscapeDataString(FormSubject(form, details))}" +
               $"&body={Uri.EscapeDataString(FormBody(form, includeDetails ? details : null))}";
    }

    /// <summary>The plain email address in a mailto: link ("mailto:a@b.c?x" becomes "a@b.c").</summary>
    public static string EmailAddress(string mailto)
    {
        var a = mailto.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ? mailto[7..] : mailto;
        var q = a.IndexOf('?');
        return q >= 0 ? a[..q] : a;
    }

    public static string TypeName(FeedbackKind kind) => kind switch
    {
        FeedbackKind.Bug => "bug",
        FeedbackKind.Idea => "idea",
        FeedbackKind.Security => "security",
        _ => "feedback",
    };

    /// <summary>The app name without its channel word ("Kodiak Cleaner Dev" becomes "Kodiak Cleaner").</summary>
    private static string ShortName(ReportDetails d)
    {
        foreach (var suffix in new[] { " Dev", " Beta" })
            if (d.AppName.EndsWith(suffix, StringComparison.Ordinal)) return d.AppName[..^suffix.Length];
        return d.AppName;
    }
}
