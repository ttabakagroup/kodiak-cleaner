using System.Diagnostics;
using System.IO;
using KodiakCleaner.Core.Programs;

namespace KodiakCleaner.App;

/// <summary>
/// Kodiak Cleaner never uninstalls itself or deletes its own entry (Ted, 0.8.54): it asks you to use Control Panel
/// (or Settings > Apps) instead, so its own uninstall cleanup runs as it should.
/// </summary>
public static class SelfGuard
{
    public const string Publisher = "Ted Tabaka";

    /// <summary>
    /// True for any Kodiak Cleaner entry, whatever the channel: a product registered under the installer's upgrade code,
    /// one installed in this copy's folder, or one named Kodiak Cleaner from its publisher.
    /// </summary>
    public static bool IsKodiakCleaner(InstalledProgram p)
    {
        if (p.IsProductCode && OurProductCodes().Contains(p.KeyName)) return true;
        if (p.Name.StartsWith(AppInfo.ShortName, StringComparison.OrdinalIgnoreCase) &&
            p.Publisher.Equals(Publisher, StringComparison.OrdinalIgnoreCase)) return true;
        var folder = UninstallFlow.InstallFolder(p);
        return folder.Length > 0 && SameFolder(folder, AppContext.BaseDirectory);
    }

    private static bool SameFolder(string a, string b)
    {
        try { return string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)), StringComparison.OrdinalIgnoreCase); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }

    private static HashSet<string>? _codes;

    /// <summary>The product codes Windows Installer has under Kodiak Cleaner's upgrade code (every installed channel).</summary>
    private static HashSet<string> OurProductCodes()
    {
        if (_codes is not null) return _codes;
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var product = new System.Text.StringBuilder(39);
            for (uint i = 0; MsiEnumRelatedProducts(UninstallCleanup.UpgradeCode, 0, i, product) == 0 && i < 16; i++)
                codes.Add(product.ToString());
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { }
        return _codes = codes;
    }

    [System.Runtime.InteropServices.DllImport("msi.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern uint MsiEnumRelatedProducts(string upgradeCode, uint reserved, uint index, System.Text.StringBuilder productCode);

    public const string Explanation =
        "Kodiak Cleaner can't uninstall itself. To remove it, open Control Panel > Programs and Features " +
        "(or Settings > Apps > Installed apps), choose Kodiak Cleaner and click Uninstall. That way its own uninstall " +
        "removes its scheduled tasks and Windows entries and asks about your backups.";

    /// <summary>Tells the person to use Control Panel, with a button that opens Programs and Features.</summary>
    public static void Explain(string? extra = null)
    {
        var choice = Dialogs.Choose(MessageKind.Info, "Uninstall Kodiak Cleaner From Control Panel",
            (extra is null ? "" : extra + "\n\n") + Explanation, null, new[] { "OK", "Open Control Panel" }, 1, 0);
        if (choice != 1) return;
        try { Process.Start(new ProcessStartInfo("control.exe", "appwiz.cpl") { UseShellExecute = true }); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { }
    }
}
