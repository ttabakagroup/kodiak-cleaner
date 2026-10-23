using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Win32;

namespace KodiakCleaner.Core.Tools;

/// <summary>
/// Turns browser extensions off and on, and removes them (Ted, 0.8.65). Chrome, Edge and Brave are changed through their
/// own extension policies, the supported way companies manage browsers: Kodiak Cleaner never edits the browser's
/// protected settings files, which the browser would treat as tampering. Turning off adds the extension to the browser's
/// block list (turning on takes it off again); removing asks the browser to uninstall it. While any of these is set the
/// browser says it is managed by your organization; Kodiak Cleaner deletes the policy again once nothing needs it.
/// Firefox, Ablaze Floorp and Mullvad Browser add-ons are removed by deleting the add-on's file while the browser is closed,
/// which the browser notices at its next start. Those browsers have no setting to turn an add-on off from outside.
/// </summary>
public static class ExtensionControl
{
    public const string BlocklistName = "ExtensionInstallBlocklist";
    public const string SettingsName = "ExtensionSettings";

    /// <summary>The browser's policy key under HKEY_CURRENT_USER, or null when it can't be managed this way.</summary>
    public static string? PolicyKey(string browser) => browser switch
    {
        "Google Chrome" => @"Software\Policies\Google\Chrome",
        "Microsoft Edge" => @"Software\Policies\Microsoft\Edge",
        "Brave" => @"Software\Policies\BraveSoftware\Brave",
        _ => null,
    };

    /// <summary>The browser's program name, to tell whether it is open.</summary>
    public static string? ProcessName(string browser) => browser switch
    {
        "Google Chrome" => "chrome",
        "Microsoft Edge" => "msedge",
        "Brave" => "brave",
        "Vivaldi" => "vivaldi",
        "Helium" => "chrome",
        "Mozilla Firefox" => "firefox",
        "Ablaze Floorp" => "floorp",
        "Mullvad Browser" => "mullvadbrowser",
        _ => null,
    };

    public static bool IsFirefoxFamily(string browser) => browser is "Mozilla Firefox" or "Ablaze Floorp" or "Mullvad Browser";

    public static bool CanTurnOff(BrowserExtension e) => PolicyKey(e.Browser) is not null && e.Id.Length > 0;

    public static bool CanRemove(BrowserExtension e) =>
        CanTurnOff(e) || (IsFirefoxFamily(e.Browser) && AddonFile(e) is not null);

    /// <summary>Why an extension can't be changed here, for its tooltip and the message when someone tries.</summary>
    public static string WhyNot(BrowserExtension e) => IsFirefoxFamily(e.Browser)
        ? $"{e.Browser} only lets you turn add-ons off on its own Add-ons page ({BrowserExtensions.ManagePage(e.Browser)})."
        : $"{e.Browser} has no setting Kodiak Cleaner can use for this. Use {BrowserExtensions.ManagePage(e.Browser)} in {e.Browser}.";

    /// <summary>A Firefox add-on's own file (profile\extensions\id.xpi), or null.</summary>
    public static string? AddonFile(BrowserExtension e)
    {
        if (e.Id.Length == 0 || e.Id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return null;
        var file = Path.Combine(e.Folder, e.Id + ".xpi");
        return File.Exists(file) ? file : null;
    }

    public static bool IsBrowserOpen(string browser)
    {
        var name = ProcessName(browser);
        if (name is null) return false;
        var running = Process.GetProcessesByName(name);
        foreach (var p in running) p.Dispose();
        return running.Length > 0;
    }

    // ---- The policy values, kept free of the registry so they can be tested anywhere ----

    /// <summary>The block list with <paramref name="id"/> added or taken out, in order, without duplicates.</summary>
    internal static List<string> WithBlocked(IEnumerable<string> blocklist, string id, bool blocked)
    {
        var list = blocklist.Where(x => !string.IsNullOrWhiteSpace(x) && !string.Equals(x, id, StringComparison.Ordinal)).Distinct().ToList();
        if (blocked) list.Add(id);
        return list;
    }

    /// <summary>
    /// The ExtensionSettings policy (JSON) with the extension marked to be removed, or that mark taken out. Everything
    /// else in it, such as settings a company made, is kept. Returns null when nothing is left in it.
    /// </summary>
    internal static string? WithRemoved(string? json, string id, bool removed)
    {
        JsonObject root;
        try { root = (string.IsNullOrWhiteSpace(json) ? null : JsonNode.Parse(json) as JsonObject) ?? new JsonObject(); }
        catch (JsonException) { root = new JsonObject(); }
        if (removed)
        {
            root[id] = new JsonObject { ["installation_mode"] = "removed" };
        }
        else if (root[id] is JsonObject entry && (string?)entry["installation_mode"] == "removed")
        {
            if (entry.Count == 1) root.Remove(id);
            else entry.Remove("installation_mode");
        }
        return root.Count == 0 ? null : root.ToJsonString();
    }

    internal static bool IsMarkedRemoved(string? json, string id)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(json) && JsonNode.Parse(json) is JsonObject root &&
                   root[id] is JsonObject entry && (string?)entry["installation_mode"] == "removed";
        }
        catch (JsonException) { return false; }
    }

    // ---- Reading and writing the policies (Windows) ----

    /// <summary>The extensions turned off through the browser's block list.</summary>
    [SupportedOSPlatform("windows")]
    public static HashSet<string> Blocked(string browser)
    {
        var key = PolicyKey(browser);
        if (key is null) return new HashSet<string>();
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey($@"{key}\{BlocklistName}");
            return k is null ? new HashSet<string>() : ReadList(k).ToHashSet(StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException) { return new HashSet<string>(); }
    }

    /// <summary>True when the extension has been asked to be removed and the browser hasn't done it yet.</summary>
    [SupportedOSPlatform("windows")]
    public static bool RemovalPending(string browser, string id)
    {
        var key = PolicyKey(browser);
        if (key is null) return false;
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(key);
            return IsMarkedRemoved(k?.GetValue(SettingsName) as string, id);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException) { return false; }
    }

    /// <summary>Turns an extension off (block list) or back on. Returns an error to show, or null when done.</summary>
    [SupportedOSPlatform("windows")]
    public static string? SetOn(BrowserExtension e, bool on)
    {
        var key = PolicyKey(e.Browser);
        if (key is null || e.Id.Length == 0) return WhyNot(e);
        return Write(() =>
        {
            using var policy = Registry.CurrentUser.CreateSubKey(key);
            List<string> list;
            using (var k = policy.OpenSubKey(BlocklistName)) list = k is null ? new List<string>() : ReadList(k);
            list = WithBlocked(list, e.Id, blocked: !on);
            policy.DeleteSubKeyTree(BlocklistName, throwOnMissingSubKey: false);
            if (list.Count > 0)
            {
                using var k = policy.CreateSubKey(BlocklistName);
                for (int i = 0; i < list.Count; i++) k.SetValue((i + 1).ToString(), list[i], RegistryValueKind.String);
            }
            DeleteIfEmpty(key);
        });
    }

    /// <summary>
    /// Removes an extension: Chrome, Edge and Brave uninstall it (at once, or when they next start); a Firefox add-on's
    /// file is deleted, which needs the browser closed. Returns an error to show, or null when done.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static string? Remove(BrowserExtension e)
    {
        var key = PolicyKey(e.Browser);
        if (key is not null && e.Id.Length > 0)
        {
            return Write(() =>
            {
                using var policy = Registry.CurrentUser.CreateSubKey(key);
                var json = WithRemoved(policy.GetValue(SettingsName) as string, e.Id, removed: true);
                policy.SetValue(SettingsName, json!, RegistryValueKind.String);
            }) ?? Write(() => SetOn(e, true)); // a removed extension needs no block-list entry of its own
        }
        if (!IsFirefoxFamily(e.Browser)) return WhyNot(e);
        if (IsBrowserOpen(e.Browser)) return $"Close {e.Browser} first, then try again.";
        var file = AddonFile(e);
        if (file is null) return $"{e.Name} is no longer there.";
        try
        {
            File.Delete(file);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return ex.Message; }
    }

    /// <summary>
    /// Clears the remove mark for extensions the browser has already uninstalled, so they can be installed again later if
    /// wanted, and deletes the browser's policy key when nothing is left in it. Only marks Kodiak Cleaner made are touched.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static void Tidy(IEnumerable<(string Browser, string Id)> removedByUs, IReadOnlyCollection<BrowserExtension> installed)
    {
        foreach (var (browser, id) in removedByUs)
        {
            var key = PolicyKey(browser);
            if (key is null || installed.Any(x => x.Browser == browser && x.Id == id)) continue;
            Write(() =>
            {
                using var policy = Registry.CurrentUser.OpenSubKey(key, writable: true);
                if (policy is null) return;
                var json = WithRemoved(policy.GetValue(SettingsName) as string, id, removed: false);
                if (json is null) policy.DeleteValue(SettingsName, throwOnMissingValue: false);
                else policy.SetValue(SettingsName, json, RegistryValueKind.String);
                policy.Close();
                DeleteIfEmpty(key);
            });
        }
    }

    [SupportedOSPlatform("windows")]
    private static List<string> ReadList(RegistryKey k) =>
        k.GetValueNames().Select(n => (Order: int.TryParse(n, out var i) ? i : int.MaxValue, Value: k.GetValue(n) as string))
            .Where(x => !string.IsNullOrWhiteSpace(x.Value)).OrderBy(x => x.Order).Select(x => x.Value!).ToList();

    /// <summary>Deletes the browser's policy key when it holds nothing, so the browser stops saying it is managed.</summary>
    [SupportedOSPlatform("windows")]
    private static void DeleteIfEmpty(string key)
    {
        // The browser's key, then its maker's (Google, BraveSoftware) when that is empty too; never Software\Policies itself.
        for (var k = key; k.Count(c => c == '\\') >= 2; k = k[..k.LastIndexOf('\\')])
        {
            using (var open = Registry.CurrentUser.OpenSubKey(k))
            {
                if (open is null) continue;
                if (open.ValueCount > 0 || open.SubKeyCount > 0) return;
            }
            Registry.CurrentUser.DeleteSubKey(k, throwOnMissingSubKey: false);
        }
    }

    private static string? Write(Action change)
    {
        try
        {
            change();
            return null;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return NeedsAdmin;
        }
        catch (IOException ex) { return ex.Message; }
    }

    /// <summary>The reason given when Windows refuses a policy change; the page then offers to restart as administrator.</summary>
    public const string NeedsAdmin = "Windows only lets administrators change browser policies.";

    private static string? Write(Func<string?> change)
    {
        try { return change(); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return NeedsAdmin;
        }
    }
}
