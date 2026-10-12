using System.Text.RegularExpressions;

namespace KodiakCleaner.Core.Tools;

/// <summary>The driver makers the Driver Cleaner knows. Listed A to Z.</summary>
public enum DriverVendor { Amd, Intel, Nvidia, Realtek }

/// <summary>A third-party driver package in the Windows driver store (C:\Windows\INF\oemNN.inf).</summary>
public sealed record DriverPackage(
    string PublishedName,
    string Provider,
    string ClassName,
    string ClassGuid,
    string Version,
    string Date,
    string Description,
    IReadOnlyList<string> Devices);

/// <summary>A graphics adapter Windows has a driver for, read from the display class key.</summary>
public sealed record GraphicsAdapter(string Name, string Provider, string DriverVersion, string DriverDate, string HardwareId, string InfName)
{
    public DriverVendor? Vendor => DriverCleaner.VendorFromHardwareId(HardwareId) ?? DriverCleaner.VendorFromProvider(Provider);
}

/// <summary>A leftover folder or registry key the vendor's driver or its control panel leaves behind.</summary>
/// <param name="Root">For folders: the Windows folder it sits in (Environment.SpecialFolder name, or "SystemDrive", "LocalLow").
/// For keys: HKCU or HKLM.</param>
public sealed record DriverLeftover(string Root, string Relative);

/// <summary>
/// What the Driver Cleaner removes for each vendor: driver store packages (by provider and device class), leftover folders and
/// leftover registry keys. Everything here is plain data and parsing, so it is tested on any platform; the app does the removing.
/// </summary>
public static class DriverCleaner
{
    // Device setup classes the cleaner looks in. Network, storage, USB, chipset and other classes are never touched.
    public const string DisplayClass = "{4d36e968-e325-11ce-bfc1-08002be10318}";
    public const string MediaClass = "{4d36e96c-e325-11ce-bfc1-08002be10318}";
    public const string SoftwareComponentClass = "{5c4c3332-344d-483c-8739-259e934c9cc8}";
    public const string ExtensionClass = "{e2f84ce7-8efa-411c-aa69-97454ca4cb57}";
    public const string AudioProcessingObjectClass = "{5989fce8-9cd0-467d-8a6a-5419e31529d4}";

    private static readonly HashSet<string> GraphicsClasses = new(StringComparer.OrdinalIgnoreCase)
        { DisplayClass, MediaClass, SoftwareComponentClass, ExtensionClass };

    private static readonly HashSet<string> AudioClasses = new(StringComparer.OrdinalIgnoreCase)
        { MediaClass, SoftwareComponentClass, ExtensionClass, AudioProcessingObjectClass };

    public static string Name(DriverVendor vendor) => vendor switch
    {
        DriverVendor.Amd => "AMD",
        DriverVendor.Intel => "Intel",
        DriverVendor.Nvidia => "NVIDIA",
        _ => "Realtek",
    };

    /// <summary>Realtek is audio only; the other three are graphics.</summary>
    public static bool IsAudio(DriverVendor vendor) => vendor == DriverVendor.Realtek;

    /// <summary>The vendor's own driver download page, opened in the browser only when the person clicks Get Latest Driver.</summary>
    public static string DownloadUrl(DriverVendor vendor) => vendor switch
    {
        DriverVendor.Amd => "https://www.amd.com/en/support/download/drivers.html",
        DriverVendor.Intel => "https://www.intel.com/content/www/us/en/support/detect.html",
        DriverVendor.Nvidia => "https://www.nvidia.com/en-us/drivers/",
        _ => "https://www.realtek.com/Download/List?cate_id=593",
    };

    /// <summary>PCI vendor IDs in a hardware ID such as PCI\VEN_10DE&amp;DEV_2684.</summary>
    public static DriverVendor? VendorFromHardwareId(string hardwareId)
    {
        var m = Regex.Match(hardwareId ?? "", @"VEN_([0-9A-F]{4})", RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        return m.Groups[1].Value.ToUpperInvariant() switch
        {
            "1002" or "1022" => DriverVendor.Amd,
            "8086" => DriverVendor.Intel,
            "10DE" => DriverVendor.Nvidia,
            "10EC" => DriverVendor.Realtek,
            _ => null,
        };
    }

    public static DriverVendor? VendorFromProvider(string provider)
    {
        var p = provider ?? "";
        if (Regex.IsMatch(p, @"\bNVIDIA\b", RegexOptions.IgnoreCase)) return DriverVendor.Nvidia;
        if (Regex.IsMatch(p, @"\b(AMD|ATI|Advanced Micro Devices)\b", RegexOptions.IgnoreCase)) return DriverVendor.Amd;
        if (Regex.IsMatch(p, @"\bIntel\b", RegexOptions.IgnoreCase)) return DriverVendor.Intel;
        if (Regex.IsMatch(p, @"\bRealtek\b", RegexOptions.IgnoreCase)) return DriverVendor.Realtek;
        return null;
    }

    /// <summary>Whether a package belongs to the vendor and sits in a class the cleaner looks at.</summary>
    public static bool IsCandidate(DriverPackage package, DriverVendor vendor) =>
        VendorFromProvider(package.Provider) == vendor
        && (IsAudio(vendor) ? AudioClasses : GraphicsClasses).Contains(package.ClassGuid);

    // Words that mark a package as part of the graphics (or Realtek audio) driver, and words that mark it as something else
    // from the same maker (chipset, storage, wireless...), which is listed but never ticked for you.
    private static readonly Dictionary<DriverVendor, (Regex Yes, Regex No)> Words = new()
    {
        [DriverVendor.Amd] = (Rx(@"radeon|graphics|display|high definition audio|hdmi|amd software|firepro"),
                              Rx(@"chipset|psp|gpio|smbus|i2c|pci|sfh|sensor|streaming audio|ryzen|usb|sata|raid|power plan|uart")),
        [DriverVendor.Intel] = (Rx(@"graphics|display audio|iris|arc|uhd|igcc|media sdk|video"),
                                Rx(@"management engine|chipset|serial io|smart sound|sst|bluetooth|wireless|wi-fi|rapid storage|thunderbolt|dynamic tuning|gna|npu|ethernet|sound wire|soundwire")),
        [DriverVendor.Nvidia] = (Rx(@"."), Rx(@"$^")),
        [DriverVendor.Realtek] = (Rx(@"audio|sound|hd audio|speaker|codec"),
                                  Rx(@"card reader|ethernet|lan|wireless|bluetooth|usb|network|camera")),
    };

    private static Regex Rx(string words) => new($@"\b({words})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Ticked out of the box: every display driver of a graphics vendor, and other packages from that vendor whose device names say
    /// they belong to the graphics (or Realtek audio) driver. Anything else from the same maker is listed for you to decide.
    /// </summary>
    public static bool TickedByDefault(DriverPackage package, DriverVendor vendor)
    {
        if (!IsCandidate(package, vendor)) return false;
        if (!IsAudio(vendor) && string.Equals(package.ClassGuid, DisplayClass, StringComparison.OrdinalIgnoreCase)) return true;
        var text = string.Join(" ", package.Devices.Append(package.Description));
        var (yes, no) = Words[vendor];
        return yes.IsMatch(text) && !no.IsMatch(text);
    }

    /// <summary>Leftover folders by vendor, A to Z by path. Only ones that exist are listed in the app.</summary>
    public static IReadOnlyList<DriverLeftover> Folders(DriverVendor vendor) => vendor switch
    {
        DriverVendor.Amd => new DriverLeftover[]
        {
            new("LocalApplicationData", @"AMD\DxCache"),
            new("LocalApplicationData", @"AMD\DxcCache"),
            new("LocalApplicationData", @"AMD\GLCache"),
            new("LocalApplicationData", @"AMD\VkCache"),
            new("LocalApplicationData", "RadeonInstaller"),
            new("ProgramFiles", @"AMD\CNext"),
            new("ProgramFiles", "ATI Technologies"),
            new("SystemDrive", "AMD"),
        },
        DriverVendor.Intel => new DriverLeftover[]
        {
            new("LocalApplicationData", @"Intel\ShaderCache"),
            new("LocalLow", @"Intel\ShaderCache"),
        },
        DriverVendor.Nvidia => new DriverLeftover[]
        {
            new("CommonApplicationData", "NVIDIA"),
            new("CommonApplicationData", "NVIDIA Corporation"),
            new("LocalApplicationData", "NVIDIA"),
            new("LocalApplicationData", "NVIDIA Corporation"),
            new("LocalLow", "NVIDIA"),
            new("ProgramFiles", "NVIDIA Corporation"),
            new("ProgramFilesX86", "NVIDIA Corporation"),
            new("SystemDrive", "NVIDIA"),
        },
        _ => new DriverLeftover[]
        {
            new("ProgramFiles", @"Realtek\Audio"),
            new("ProgramFilesX86", @"Realtek\Audio"),
        },
    };

    /// <summary>Leftover registry keys by vendor, under SOFTWARE. Every key is backed up to a .reg file before it is removed.</summary>
    public static IReadOnlyList<DriverLeftover> RegistryKeys(DriverVendor vendor) => vendor switch
    {
        DriverVendor.Amd => new DriverLeftover[]
        {
            new("HKCU", @"SOFTWARE\AMD\CN"),
            new("HKCU", @"SOFTWARE\ATI"),
            new("HKLM", @"SOFTWARE\AMD\CN"),
            new("HKLM", @"SOFTWARE\ATI Technologies"),
        },
        DriverVendor.Intel => new DriverLeftover[]
        {
            new("HKCU", @"SOFTWARE\Intel\Display"),
            new("HKLM", @"SOFTWARE\Intel\Display"),
            new("HKLM", @"SOFTWARE\Intel\GMM"),
            new("HKLM", @"SOFTWARE\Intel\IGFX"),
            new("HKLM", @"SOFTWARE\Intel\KMD"),
        },
        DriverVendor.Nvidia => new DriverLeftover[]
        {
            new("HKCU", @"SOFTWARE\NVIDIA Corporation"),
            new("HKLM", @"SOFTWARE\NVIDIA Corporation"),
        },
        _ => new DriverLeftover[]
        {
            new("HKCU", @"SOFTWARE\Realtek\Audio"),
            new("HKLM", @"SOFTWARE\Realtek\Audio"),
        },
    };
}

/// <summary>Reads the parts of a driver .inf file the Driver Cleaner shows: provider, class, version and device names.</summary>
public static class InfReader
{
    public static DriverPackage Parse(string publishedName, string text)
    {
        var sections = Sections(text);
        var strings = StringTable(sections);
        string Resolve(string value) => Regex.Replace(Unquote(value), "%([^%]+)%",
            m => strings.TryGetValue(m.Groups[1].Value, out var s) ? s : m.Value);

        var version = sections.GetValueOrDefault("version") ?? new List<(string, string)>();
        string Get(string key) => Resolve(version.FirstOrDefault(kv => kv.Item1.Equals(key, StringComparison.OrdinalIgnoreCase)).Item2 ?? "");

        var driverVer = Get("DriverVer").Split(',', 2);
        var devices = DeviceNames(sections, Resolve);
        var provider = Get("Provider");
        var className = Get("Class");
        return new DriverPackage(
            publishedName,
            provider,
            className,
            Get("ClassGuid").ToLowerInvariant(),
            driverVer.Length > 1 ? driverVer[1].Trim() : "",
            driverVer[0].Trim(),
            devices.FirstOrDefault() ?? (provider + " " + className).Trim(),
            devices);
    }

    /// <summary>Device names from [Manufacturer] and the model sections it points to, without repeats, A to Z.</summary>
    private static List<string> DeviceNames(Dictionary<string, List<(string Key, string Value)>> sections, Func<string, string> resolve)
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, value) in sections.GetValueOrDefault("manufacturer") ?? new())
        {
            var parts = value.Split(',').Select(p => p.Trim()).ToArray();
            if (parts.Length == 0 || parts[0].Length == 0) continue;
            var models = new List<string> { parts[0] };
            models.AddRange(parts.Skip(1).Where(p => p.Length > 0).Select(p => parts[0] + "." + p));
            foreach (var model in models)
                foreach (var (key, _) in sections.GetValueOrDefault(model.ToLowerInvariant()) ?? new())
                {
                    var name = resolve(key).Trim();
                    if (name.Length > 0 && !name.Contains('%')) names.Add(name);
                }
        }
        return names.ToList();
    }

    private static Dictionary<string, List<(string Key, string Value)>> Sections(string text)
    {
        var sections = new Dictionary<string, List<(string, string)>>();
        List<(string, string)>? current = null;
        foreach (var raw in text.Split('\n'))
        {
            var line = StripComment(raw).Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith('[') && line.Contains(']'))
            {
                var name = line[1..line.IndexOf(']')].Trim().ToLowerInvariant();
                if (!sections.TryGetValue(name, out current)) sections[name] = current = new();
                continue;
            }
            if (current is null) continue;
            int eq = line.IndexOf('=');
            current.Add(eq < 0 ? (line, "") : (line[..eq].Trim(), line[(eq + 1)..].Trim()));
        }
        return sections;
    }

    /// <summary>[Strings] first, then any language-specific [Strings.xxxx] for names [Strings] doesn't have.</summary>
    private static Dictionary<string, string> StringTable(Dictionary<string, List<(string Key, string Value)>> sections)
    {
        var table = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in sections.Keys.Where(k => k == "strings").Concat(sections.Keys.Where(k => k.StartsWith("strings.")).OrderBy(k => k)))
            foreach (var (key, value) in sections[name])
                table.TryAdd(key, Unquote(value));
        return table;
    }

    private static string StripComment(string line)
    {
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') quoted = !quoted;
            else if (line[i] == ';' && !quoted) return line[..i];
        }
        return line;
    }

    private static string Unquote(string value)
    {
        value = value.Trim();
        return value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1].Replace("\"\"", "\"") : value;
    }
}
