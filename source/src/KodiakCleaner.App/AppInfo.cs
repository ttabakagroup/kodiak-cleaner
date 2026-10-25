using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using KodiakCleaner.Core.About;

namespace KodiakCleaner.App;

/// <summary>Names, version, channel and addresses. The version and channel come from version.json at build time.</summary>
public static class AppInfo
{
    public const string ShortName = "Kodiak Cleaner";
    public const string Slogan = "Not a Crap Cleaner. A Cleaner App, Built Around What Matters.";
    public const string Developer = "Ted Tabaka";
    public const string KoFiUrl = "https://ko-fi.com/s/aabd61b988";

    // First of the static fields: the properties below read build metadata from it while the class initializes, in textual order.
    private static readonly Assembly Me = Assembly.GetExecutingAssembly();

    /// <summary>GitHub "owner/name": source code and Star on GitHub.</summary>
    public const string Repository = "ttabakagroup/kodiakcleaner";
    /// <summary>
    /// version.json's "repositoryPublic" (false when missing). Ted made the repository public at 0.9.0 Beta; while the flag is
    /// false, the GitHub links point at the website or are hidden, in every channel.
    /// </summary>
    public static bool RepositoryIsPublic { get; } = string.Equals(Metadata("RepositoryPublic"), "true", StringComparison.OrdinalIgnoreCase);
    /// <summary>The source code on GitHub, or the website's Open Source page while the repository is still private.</summary>
    public static string ProjectUrl => RepositoryIsPublic ? $"https://github.com/{Repository}" : HomepageUrl + "open-source/";
    /// <summary>Kodiak Cleaner's own website (Ted registered it in 0.8.28). Updates come from it since 0.8.40; code stays on the repository.</summary>
    public const string HomepageUrl = "https://www.kodiakcleaner.app/";
    /// <summary>A hosted privacy policy. Empty = show the copy built into the app.</summary>
    public const string PrivacyUrl = "";

    /// <summary>The website's Contact Us form. Ted's email address is never shown anywhere (Ted, 0.8.56).</summary>
    public const string ContactUrl = "https://www.kodiakcleaner.app/contact/";

    /// <summary>Where Contact &amp; Feedback messages go: the Contact Us form, with the topic picked for each button.</summary>
    public static readonly FeedbackAddresses Feedback = new(Default: ContactUrl);

    public static Version Version { get; } = Changelog.Normalize(Me.GetName().Version ?? new Version(0, 8, 0));
    public static ReleaseChannel Channel { get; } = ReleaseChannels.Parse(Metadata("Channel"));
    public static Version? TargetStable { get; } = Version.TryParse(Metadata("TargetStable"), out var v) ? v : null;

    /// <summary>
    /// The name people see: always "Kodiak Cleaner" (Ted's choice, 0.8.7). The channel shows in <see cref="VersionName"/>
    /// ("0.8.7 Dev") and the About chip instead.
    /// </summary>
    public static string Name => ShortName;
    /// <summary>"Kodiak Cleaner Dev" etc. Only for Windows registrations made before 0.8.7, so they keep their names.</summary>
    public static string RegistrationName => ReleaseChannels.AppName(ShortName, Channel);
    /// <summary>"0.8.0 Dev". Use this wherever a version is shown or sent.</summary>
    public static string VersionName => ReleaseChannels.VersionName(Version, Channel, TargetStable);
    public static string ChannelLabel => ReleaseChannels.Label(Channel);

    /// <summary>Keeps the Dev, Beta and Stable copies from sharing Windows registrations ("KodiakCleaner", "KodiakCleaner.Dev").</summary>
    public static string Id => IdFor(Channel);
    public static string IdFor(ReleaseChannel channel) => channel == ReleaseChannel.Stable ? "KodiakCleaner" : $"KodiakCleaner.{ReleaseChannels.Label(channel)}";

    /// <summary>
    /// The other channels' ids. The MSI installs every channel in one place, so a Dev install updated to Beta still has the
    /// Dev build's Windows registrations; <see cref="Integration.MoveFromOtherChannels"/> moves them over (0.9.0 Beta).
    /// </summary>
    public static IEnumerable<ReleaseChannel> OtherChannels => Enum.GetValues<ReleaseChannel>().Where(c => c != Channel);

    private static List<ChangelogRelease>? _releases;

    /// <summary>CHANGELOG.md, embedded into the exe at build time. It starts at 0.9.0 Beta (Ted reset it for the Beta).</summary>
    public static List<ChangelogRelease> Releases => _releases ??= Changelog.Parse(ReadResource("CHANGELOG.md") ?? "");

    private static List<ChangelogRelease>? _history;

    /// <summary>
    /// Every change since the start, for About's All Changes Since the Start: this channel's changelog followed by the Dev
    /// builds' history (docs/history/dev-changelog.md, 0.8.0 Dev to 0.8.121 Dev), embedded too.
    /// </summary>
    public static List<ChangelogRelease> History => _history ??=
        Releases.Concat(Changelog.Parse(ReadResource("dev-changelog.md") ?? "")).OrderByDescending(r => r.Version).ToList();

    /// <summary>Safe facts for bug reports and Copy Version Info. Never file names, folders or user names.</summary>
    public static ReportDetails Details => new(
        Name,
        VersionName,
        $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})",
        Supporter.PlanName,
        Integration.IsInstalled ? "MSI installer" : "portable ZIP",
        Elevation.IsAdministrator,
        $"{Settings.Current.Palette}, {Settings.Current.Theme switch { AppTheme.Light => "light", AppTheme.Dark => "dark", _ => "match device" }}");

    public static string? ReadResource(string name)
    {
        using var stream = Me.GetManifestResourceStream(name);
        if (stream is null) return null;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string? Metadata(string key) =>
        Me.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == key)?.Value;
}
