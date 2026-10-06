using System.IO.Compression;
using System.Text.Json;

namespace KodiakCleaner.Core.Tools;

/// <summary>
/// One installed browser extension. <see cref="Enabled"/> is null when the browser doesn't say. Its icon is
/// <see cref="IconFile"/>, or the <see cref="IconEntry"/> inside that file when it is a Firefox add-on package (.xpi).
/// </summary>
public sealed record BrowserExtension(string Browser, string Profile, string Id, string Name, string Version, bool? Enabled, string Folder,
    string? IconFile = null, string? IconEntry = null);

/// <summary>
/// Lists extensions installed in Chrome, Edge, Brave, Vivaldi, Helium, Firefox, Ablaze Floorp and Mullvad Browser by reading their profile folders.
/// Read-only: extensions are removed from the browser's own Extensions page, which also keeps browser sync right.
/// </summary>
public static class BrowserExtensions
{
    /// <summary>Where each browser keeps its extensions page, shown as a hint.</summary>
    public static string ManagePage(string browser) => browser switch
    {
        "Google Chrome" => "chrome://extensions",
        "Microsoft Edge" => "edge://extensions",
        "Brave" => "brave://extensions",
        "Vivaldi" => "vivaldi://extensions",
        "Helium" => "chrome://extensions",
        "Mozilla Firefox" or "Ablaze Floorp" or "Mullvad Browser" => "about:addons",
        _ => "",
    };

    public static IReadOnlyList<BrowserExtension> FindAll(string localAppData, string roamingAppData)
    {
        var list = new List<BrowserExtension>();
        list.AddRange(ReadChromium("Google Chrome", Path.Combine(localAppData, @"Google\Chrome\User Data")));
        list.AddRange(ReadChromium("Microsoft Edge", Path.Combine(localAppData, @"Microsoft\Edge\User Data")));
        list.AddRange(ReadChromium("Brave", Path.Combine(localAppData, @"BraveSoftware\Brave-Browser\User Data")));
        list.AddRange(ReadChromium("Vivaldi", Path.Combine(localAppData, @"Vivaldi\User Data")));
        list.AddRange(ReadChromium("Helium", Path.Combine(localAppData, @"imput\Helium\User Data")));
        list.AddRange(ReadFirefox(Path.Combine(roamingAppData, @"Mozilla\Firefox\Profiles")));
        list.AddRange(ReadFirefox(Path.Combine(roamingAppData, @"Floorp\Profiles"), "Ablaze Floorp"));
        list.AddRange(ReadFirefox(Path.Combine(roamingAppData, @"Mullvad\MullvadBrowser\Profiles"), "Mullvad Browser"));
        return list;
    }

    /// <summary>Reads a Chromium "User Data" folder: each profile's Extensions\id\version\manifest.json.</summary>
    public static IEnumerable<BrowserExtension> ReadChromium(string browser, string userData)
    {
        if (!Directory.Exists(userData)) yield break;
        foreach (var profile in SafeDirs(userData))
        {
            var profileName = Path.GetFileName(profile);
            if (profileName != "Default" && !profileName.StartsWith("Profile ", StringComparison.Ordinal)) continue;
            var extensions = Path.Combine(profile, "Extensions");
            if (!Directory.Exists(extensions)) continue;
            var states = ReadChromiumStates(profile);

            foreach (var idDir in SafeDirs(extensions))
            {
                var id = Path.GetFileName(idDir);
                if (id == "Temp") continue;
                var versionDir = SafeDirs(idDir).OrderByDescending(d => ParseVersion(Path.GetFileName(d))).FirstOrDefault();
                if (versionDir is null) continue;
                var manifest = ReadJson(Path.Combine(versionDir, "manifest.json"));
                if (manifest is null) continue;
                using (manifest)
                {
                    var root = manifest.RootElement;
                    var name = LocalizedName(root, versionDir);
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    var version = Str(root, "version") ?? Path.GetFileName(versionDir);
                    if (root.TryGetProperty("theme", out _)) name += " (theme)";
                    var icon = BestIcon(root);
                    yield return new BrowserExtension(browser, DisplayProfile(profileName), id, name, version,
                        states.TryGetValue(id, out var on) ? on : null, versionDir,
                        icon is null ? null : Path.Combine(versionDir, icon.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
                }
            }
        }
    }

    /// <summary>Reads Firefox's extensions.json in each profile. Only add-ons the person installed are listed.</summary>
    public static IEnumerable<BrowserExtension> ReadFirefox(string profilesRoot, string browser = "Mozilla Firefox")
    {
        if (!Directory.Exists(profilesRoot)) yield break;
        foreach (var profile in SafeDirs(profilesRoot))
        {
            var doc = ReadJson(Path.Combine(profile, "extensions.json"));
            if (doc is null) continue;
            using (doc)
            {
                if (!doc.RootElement.TryGetProperty("addons", out var addons) || addons.ValueKind != JsonValueKind.Array) continue;
                var folder = Path.GetFileName(profile);
                var profileName = folder.Contains('.') ? folder[(folder.IndexOf('.') + 1)..] : folder;
                foreach (var a in addons.EnumerateArray())
                {
                    if (Str(a, "type") is not ("extension" or "theme")) continue;
                    if (Str(a, "location") != "app-profile") continue;
                    var name = a.TryGetProperty("defaultLocale", out var loc) ? Str(loc, "name") : null;
                    name ??= Str(a, "id");
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    if (Str(a, "type") == "theme") name += " (theme)";
                    bool? active = a.TryGetProperty("active", out var act) && act.ValueKind is JsonValueKind.True or JsonValueKind.False
                        ? act.GetBoolean() : null;
                    // The icon sits inside the add-on's package (.xpi), named in its "icons" list.
                    var package = Str(a, "path");
                    var icon = BestIcon(a);
                    var inPackage = package is not null && package.EndsWith(".xpi", StringComparison.OrdinalIgnoreCase) && icon is not null;
                    yield return new BrowserExtension(browser, profileName, Str(a, "id") ?? "", name, Str(a, "version") ?? "",
                        active, Path.Combine(profile, "extensions"), inPackage ? package : null, inPackage ? icon!.TrimStart('/') : null);
                }
            }
        }
    }

    /// <summary>Extension id to on/off from Preferences and Secure Preferences (extensions.settings.id.state / disable_reasons).</summary>
    private static Dictionary<string, bool> ReadChromiumStates(string profile)
    {
        var states = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var file in new[] { "Preferences", "Secure Preferences" })
        {
            using var doc = ReadJson(Path.Combine(profile, file));
            if (doc is null) continue;
            if (!doc.RootElement.TryGetProperty("extensions", out var ext) || ext.ValueKind != JsonValueKind.Object ||
                !ext.TryGetProperty("settings", out var settings) || settings.ValueKind != JsonValueKind.Object) continue;
            foreach (var entry in settings.EnumerateObject())
            {
                if (entry.Value.ValueKind != JsonValueKind.Object) continue;
                if (entry.Value.TryGetProperty("disable_reasons", out var reasons))
                {
                    bool disabled = reasons.ValueKind switch
                    {
                        JsonValueKind.Number => reasons.TryGetInt32(out var r) && r != 0,
                        JsonValueKind.Array => reasons.GetArrayLength() > 0,
                        _ => false,
                    };
                    states[entry.Name] = !disabled;
                }
                else if (entry.Value.TryGetProperty("state", out var state) && state.ValueKind == JsonValueKind.Number)
                    states[entry.Name] = state.GetInt32() == 1;
                else if (!states.ContainsKey(entry.Name) && file == "Secure Preferences")
                    states[entry.Name] = true;
            }
        }
        return states;
    }

    /// <summary>Resolves "__MSG_appName__" names from _locales\default_locale\messages.json.</summary>
    internal static string? LocalizedName(JsonElement manifest, string folder)
    {
        var name = Str(manifest, "name");
        if (name is null || !name.StartsWith("__MSG_", StringComparison.Ordinal) || !name.EndsWith("__", StringComparison.Ordinal)) return name;
        var key = name[6..^2];
        var locale = Str(manifest, "default_locale") ?? "en";
        foreach (var candidate in new[] { locale, "en", "en_US" }.Distinct())
        {
            using var messages = ReadJson(Path.Combine(folder, "_locales", candidate, "messages.json"));
            if (messages is null) continue;
            foreach (var prop in messages.RootElement.EnumerateObject())
                if (string.Equals(prop.Name, key, StringComparison.OrdinalIgnoreCase) && prop.Value.ValueKind == JsonValueKind.Object)
                    return Str(prop.Value, "message");
        }
        return null;
    }

    private static readonly string[] ImageTypes = { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico" };

    /// <summary>From an "icons" list like {"16": "a.png", "48": "b.png"}: the smallest one at least 32 pixels, else the biggest.</summary>
    internal static string? BestIcon(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("icons", out var icons) || icons.ValueKind != JsonValueKind.Object) return null;
        var sized = icons.EnumerateObject()
            .Where(p => p.Value.ValueKind == JsonValueKind.String && int.TryParse(p.Name, out _))
            .Select(p => (Size: int.Parse(p.Name), Path: p.Value.GetString()!))
            .Where(p => ImageTypes.Contains(Path.GetExtension(p.Path), StringComparer.OrdinalIgnoreCase)) // not SVG
            .ToList();
        if (sized.Count == 0) return null;
        return (sized.Where(p => p.Size >= 32).OrderBy(p => p.Size).FirstOrDefault() is { Path: not null } fit ? fit
            : sized.OrderByDescending(p => p.Size).First()).Path;
    }

    /// <summary>The extension's icon image, or null when it has none or it can't be read. Read-only, at most 1 MB.</summary>
    public static byte[]? ReadIcon(BrowserExtension e)
    {
        const int Largest = 1 << 20;
        try
        {
            if (e.IconFile is null || !File.Exists(e.IconFile)) return null;
            if (e.IconEntry is null)
                return new FileInfo(e.IconFile).Length <= Largest ? File.ReadAllBytes(e.IconFile) : null;
            using var zip = ZipFile.OpenRead(e.IconFile);
            var entry = zip.GetEntry(e.IconEntry);
            if (entry is null || entry.Length > Largest) return null;
            using var stream = entry.Open();
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return copy.ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException) { return null; }
    }

    private static string DisplayProfile(string folder) => folder == "Default" ? "Default profile" : folder;

    private static Version ParseVersion(string text) => Version.TryParse(text.Split('_')[0], out var v) ? v : new Version(0, 0);

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static JsonDocument? ReadJson(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return JsonDocument.Parse(stream, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static IEnumerable<string> SafeDirs(string path)
    {
        try { return Directory.EnumerateDirectories(path).ToList(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return Array.Empty<string>(); }
    }
}
