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
    public const string Email = "www.kodiakcleaner.app/contact";
    public const string KoFiUrl = "https://ko-fi.com/ttabaka";

    /// <summary>GitHub "owner/name": source code and Star on GitHub.</summary>
    public const string Repository = "ttabakagroup/kodiakcleaner";
    public static string ProjectUrl => $"https://github.com/{Repository}";
    /// <summary>Kodiak Cleaner's own website (Ted registered it in 0.8.28). Updates come from it since 0.8.40; code stays on the repository.</summary>
    public const string HomepageUrl = "https://www.kodiakcleaner.app/";
    /// <summary>A hosted privacy policy. Empty = show the copy built into the app.</summary>
    public const string PrivacyUrl = "";

    /// <summary>Where Contact &amp; Feedback messages go: mailto: or https: form. Empty entries use the default.</summary>
    public static readonly FeedbackAddresses Feedback = new(Default: "mailto:" + Email);

    private static readonly Assembly Me = Assembly.GetExecutingAssembly();

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
    public static string Id => Channel == ReleaseChannel.Stable ? "KodiakCleaner" : $"KodiakCleaner.{ChannelLabel}";

    /// <summary>Development builds have the Developer page; release builds leave it (and its settings) out.</summary>
#if DEVTOOLS
    public static readonly bool HasDeveloperTools = true;
#else
    public static readonly bool HasDeveloperTools = false;
#endif

    private static List<ChangelogRelease>? _releases;

    /// <summary>CHANGELOG.md, embedded into the exe at build time.</summary>
    public static List<ChangelogRelease> Releases => _releases ??= Changelog.Parse(ReadResource("CHANGELOG.md") ?? "");

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
