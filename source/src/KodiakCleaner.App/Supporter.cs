using KodiakCleaner.Core.Supporter;

namespace KodiakCleaner.App;

/// <summary>
/// The Free / Supporter plan. Supporter codes are checked offline against the public key built into the app;
/// nothing is sent anywhere. Development builds can also preview a plan from the Developer page.
/// </summary>
public static class Supporter
{
    /// <summary>Ted's public key once he has made one; otherwise the test key, which release builds refuse.</summary>
    public static bool IsTestKey { get; } = AppInfo.ReadResource("supporter-public.pem") is null;

    private static readonly string PublicKey =
        AppInfo.ReadResource("supporter-public.pem") ?? AppInfo.ReadResource("supporter-test-public.pem") ?? "";

    private static readonly IReadOnlyList<string> Withdrawn = SupporterCode.ParseWithdrawnList(AppInfo.ReadResource("withdrawn-codes.txt") ?? "");

    public static event Action? Changed;

    public static CodeStatus CodeStatus { get; private set; } = CodeStatus.Empty;
    public static SupporterLicense? License { get; private set; }

    /// <summary>The preview chosen on the Developer page (development builds only).</summary>
    public static PlanPreview Preview => AppInfo.HasDeveloperTools ? Settings.Current.DevPreview : PlanPreview.None;

    public static bool IsSupporter => Preview switch
    {
        PlanPreview.Supporter => true,
        PlanPreview.Free => false,
        _ => License is not null,
    };

    public static string PlanName => IsSupporter ? "Supporter" : "Free";

    /// <summary>How the current plan was unlocked: supporter code, dev preview, or none.</summary>
    public static string HowUnlocked => Preview != PlanPreview.None
        ? "Developer preview"
        : License is not null ? "Supporter code" : "None";

    /// <summary>Name on the code (or the preview name), for the badge and About.</summary>
    public static string? SupporterName => Preview != PlanPreview.None
        ? (string.IsNullOrWhiteSpace(Settings.Current.DevPreviewName) ? null : Settings.Current.DevPreviewName.Trim())
        : string.IsNullOrWhiteSpace(License?.Name) ? null : License!.Name;

    /// <summary>"Supporter" or "Supporter · Dev" while previewing.</summary>
    public static string BadgeText => Preview == PlanPreview.Supporter ? "Supporter · Dev" : "Supporter";

    public static bool ShowBadge => IsSupporter && Settings.Current.ShowSupporterBadge;

    /// <summary>Re-checks the saved code (on start-up and after any change).</summary>
    public static void Refresh()
    {
        (CodeStatus, License) = SupporterCode.Verify(Settings.Current.SupporterCode, PublicKey, Withdrawn);
        Changed?.Invoke();
    }

    /// <summary>Saves the code if it is valid and returns what to tell the person.</summary>
    public static CodeStatus Apply(string code)
    {
        var (status, _) = SupporterCode.Verify(code, PublicKey, Withdrawn);
        if (status == CodeStatus.Valid)
        {
            Settings.Current.SupporterCode = code.Trim();
            Settings.Current.Save();
            Refresh();
            ThemeManager.Apply();
        }
        return status;
    }

    public static void RemoveCode()
    {
        Settings.Current.SupporterCode = "";
        Settings.Current.Save();
        Refresh();
        ThemeManager.Apply();
    }
}
