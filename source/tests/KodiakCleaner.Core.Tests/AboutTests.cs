using KodiakCleaner.Core.About;

namespace KodiakCleaner.Core.Tests;

public class ChangelogTests
{
    private const string Sample = """
        # Changelog
        Intro text.

        ## [Unreleased]
        ### Added
        - Not shipped yet

        ## [1.2.0] - 2026-11-01
        ### Fixed
        - Crash when
          the bin is empty

        ## [1.1.0] - 2026-10-03
        ### Added
        - Themes
        - About page
        ### Changed
        * New look

        ## [1.0.0] - 2026-10-03
        ### Added
        - First release
        """;

    [Fact]
    public void Parses_releases_newest_first_and_skips_unreleased()
    {
        var r = Changelog.Parse(Sample);
        Assert.Equal(new[] { "1.2.0", "1.1.0", "1.0.0" }, r.Select(x => x.Version.ToString(3)));
        Assert.Equal("2026-10-03", r[1].Date);
        Assert.Equal(new[] { "Added", "Changed" }, r[1].Sections.Select(s => s.Title));
        Assert.Equal(new[] { "Themes", "About page" }, r[1].Sections[0].Items);
        Assert.Equal("New look", r[1].Sections[1].Items[0]);
        Assert.Equal("Crash when the bin is empty", r[0].Sections[0].Items[0]);
    }

    [Fact]
    public void Since_returns_only_releases_after_last_seen_up_to_current()
    {
        var r = Changelog.Parse(Sample);
        var since = Changelog.Since(r, new Version(1, 0, 0), new Version(1, 1, 57));
        Assert.Equal(new[] { "1.1.0" }, since.Select(x => x.Version.ToString(3)));

        var all = Changelog.Since(r, null, new Version(1, 2, 0, 0));
        Assert.Equal(3, all.Count);

        Assert.Empty(Changelog.Since(r, new Version(1, 2, 0), new Version(1, 2, 0)));
    }

    [Fact]
    public void Reads_channel_labels_in_headings()
    {
        var r = Changelog.Parse("## [0.8.3 Dev] - 2026-10-04\n### Added\n- Thing\n\n## [0.9.12 Beta (for 1.2.0)] — 2026-11-01\n- Other\n");
        Assert.Equal(new[] { "0.9.12 Beta (for 1.2.0)", "0.8.3 Dev" }, r.Select(x => x.VersionName));
        Assert.Equal("2026-10-04", r[1].Date);
        Assert.Equal("Version 0.8.3 Dev", r[1].Title);
    }

    [Fact]
    public void Accepts_two_part_versions_and_v_prefix()
    {
        var r = Changelog.Parse("## [v2.0] - today\n- Big change\n");
        Assert.Equal(new Version(2, 0, 0), r.Single().Version);
        Assert.Equal("Changes", r.Single().Sections.Single().Title);
    }

    [Fact]
    public void Repo_changelog_parses()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "CHANGELOG.md");
        var releases = Changelog.Parse(File.ReadAllText(path));
        Assert.NotEmpty(releases);
        Assert.All(releases, r => Assert.NotEmpty(r.Sections));
    }
}

public class FeedbackLinksTests
{
    // Seeded strings that must never end up in a report.
    private static readonly ReportDetails Details = new("Kodiak Cleaner Dev", "0.8.0 Dev", "Windows 11 (X64)", "Free", "portable ZIP", false, "Honey, match device");
    private static readonly FeedbackAddresses Mail = new("mailto:someone@example.com");
    private static readonly FeedbackAddresses Form = new("https://example.com/form");

    private static string Param(string url, string name)
    {
        var query = url[(url.IndexOf('?') + 1)..].Split('&');
        var pair = query.FirstOrDefault(p => p.StartsWith(name + "=", StringComparison.Ordinal));
        return pair is null ? "" : Uri.UnescapeDataString(pair[(name.Length + 1)..]);
    }

    [Fact]
    public void Form_messages_carry_what_was_typed_and_only_the_chosen_extras()
    {
        var form = new FeedbackForm(FeedbackKind.Bug, "Sam", "sam@example.com", "Crash on start", "It closed.\r\nTwice.");
        var url = FeedbackLinks.BuildFromForm(form, Mail, Details, includeDetails: true)!;
        Assert.Equal("[Bug Report] Kodiak Cleaner - Crash on start", Param(url, "subject"));
        var body = Param(url, "body");
        Assert.StartsWith("It closed.\nTwice.\n\n---\nFrom: Sam <sam@example.com>\nApp: Kodiak Cleaner Dev 0.8.0 Dev", body);

        var bare = FeedbackLinks.BuildFromForm(form with { Name = "", ReplyTo = "", Subject = "" }, Mail, Details, includeDetails: false)!;
        Assert.Equal("[Bug Report] Kodiak Cleaner", Param(bare, "subject"));
        Assert.Equal("It closed.\nTwice.\n", Param(bare, "body"));
        Assert.Null(FeedbackLinks.BuildFromForm(form, new FeedbackAddresses("https://example.com/form"), Details, true));
        Assert.Equal("someone@example.com", FeedbackLinks.EmailAddress("mailto:someone@example.com?cc=x"));
    }

    [Theory]
    [InlineData(FeedbackKind.Feedback, "[Feedback] Kodiak Cleaner ")]
    [InlineData(FeedbackKind.Bug, "[Bug Report] Kodiak Cleaner ")]
    [InlineData(FeedbackKind.Idea, "[Feature Request] Kodiak Cleaner ")]
    [InlineData(FeedbackKind.Security, "[Security] Kodiak Cleaner ")]
    public void Mailto_links_have_the_right_subject(FeedbackKind kind, string subject)
    {
        var url = FeedbackLinks.Build(kind, Mail, Details)!;
        Assert.StartsWith("mailto:someone@example.com?subject=", url);
        Assert.Equal(subject, Param(url, "subject"));
    }

    [Theory]
    [InlineData(FeedbackKind.Feedback, "General Feedback")]
    [InlineData(FeedbackKind.Bug, "Bug Report")]
    [InlineData(FeedbackKind.Idea, "Feature Request")]
    [InlineData(FeedbackKind.Security, "Security")]
    public void Web_forms_get_topic_and_version(FeedbackKind kind, string topic)
    {
        var url = FeedbackLinks.Build(kind, Form, Details)!;
        Assert.Equal(topic, Param(url, "topic"));
        Assert.Equal("0.8.0 Dev", Param(url, "version"));
        Assert.DoesNotContain("body", url);
    }

    [Fact]
    public void Only_bug_reports_include_details()
    {
        Assert.Contains("0.8.0 Dev", Param(FeedbackLinks.Build(FeedbackKind.Bug, Mail, Details)!, "body"));
        Assert.Contains("What I expected", Param(FeedbackLinks.Build(FeedbackKind.Bug, Mail, Details)!, "body"));
        foreach (var kind in new[] { FeedbackKind.Feedback, FeedbackKind.Idea, FeedbackKind.Security })
            Assert.DoesNotContain("0.8.0 Dev", Param(FeedbackLinks.Build(kind, Mail, Details)!, "body"));
    }

    [Fact]
    public void Specific_addresses_override_the_default_and_empty_means_none()
    {
        var addresses = new FeedbackAddresses("mailto:general@example.com", Security: "mailto:private@example.com");
        Assert.StartsWith("mailto:private@example.com", FeedbackLinks.Build(FeedbackKind.Security, addresses, Details));
        Assert.StartsWith("mailto:general@example.com", FeedbackLinks.Build(FeedbackKind.Bug, addresses, Details));
        Assert.Null(FeedbackLinks.Build(FeedbackKind.Bug, new FeedbackAddresses(""), Details));
        Assert.False(new FeedbackAddresses("").IsConfigured);
    }

    [Fact]
    public void Details_hold_the_version_name_and_nothing_personal()
    {
        var text = Details.ToString();
        Assert.Contains("Kodiak Cleaner Dev 0.8.0 Dev", text);
        Assert.Contains("Plan: Free", text);
        // ReportDetails has no field that could carry a path, user name or file name.
        Assert.DoesNotContain(@"C:\", text);
        Assert.DoesNotContain("Users", text);
    }
}
