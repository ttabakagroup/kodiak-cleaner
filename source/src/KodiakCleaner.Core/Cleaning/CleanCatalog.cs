using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace KodiakCleaner.Core.Cleaning;

/// <summary>The built-in list of things the cleaner knows how to clean.</summary>
[SupportedOSPlatform("windows")]
public static class CleanCatalog
{
    private static readonly TimeSpan OneDay = TimeSpan.FromDays(1);

    /// <param name="tempMinAge">Temporary files younger than this are left alone (null = any age).</param>
    /// <param name="recycleBinMinAge">Only Recycle Bin items deleted longer ago than this (null = everything).</param>
    public static IReadOnlyList<CleanTarget> Build(TimeSpan? tempMinAge = null, TimeSpan? recycleBinMinAge = null)
    {
        var list = new List<CleanTarget>();
        list.AddRange(WindowsTargets(tempMinAge, recycleBinMinAge));
        list.AddRange(ChromiumTargets("Google Chrome", "chrome", "chrome", Local(@"Google\Chrome\User Data"), AppFiles("Google Chrome")));
        list.AddRange(ChromiumTargets("Microsoft Edge", "msedge", "msedge", Local(@"Microsoft\Edge\User Data"), AppFiles("Microsoft Edge")));
        list.AddRange(ChromiumTargets("Brave", "brave", "brave", Local(@"BraveSoftware\Brave-Browser\User Data"), AppFiles("Brave")));
        list.AddRange(ChromiumTargets("Vivaldi", "vivaldi", "vivaldi", Local(@"Vivaldi\User Data"), AppFiles("Vivaldi")));
        list.AddRange(FirefoxTargets("Mozilla Firefox", "firefox", Roaming(@"Mozilla\Firefox\Profiles"), Local(@"Mozilla\Firefox\Profiles"),
            AppFiles("Mozilla Firefox")));
        // Firefox-based: Ablaze Floorp keeps its profiles under "Floorp" (its MOZ_APP_PROFILE).
        list.AddRange(FirefoxTargets("Ablaze Floorp", "floorp", Roaming(@"Floorp\Profiles"), Local(@"Floorp\Profiles"), AppFiles("Ablaze Floorp")));
        list.AddRange(MullvadTargets());
        // Chromium-based: Helium keeps Chrome's process name (chrome.exe), so Google Chrome running also pauses it. That is the safe side.
        list.AddRange(ChromiumTargets("Helium", "helium", "chrome", Local(@"imput\Helium\User Data"), AppFiles("Helium")));
        list.AddRange(OperaTargets("Opera", "opera", "Opera Stable"));
        list.AddRange(OperaTargets("Opera GX", "operagx", "Opera GX Stable"));
        list.AddRange(FirefoxTargets("Waterfox", "waterfox", Roaming(@"Waterfox\Profiles"), Local(@"Waterfox\Profiles"), AppFiles("Waterfox")));
        list.AddRange(AdvancedTargets(tempMinAge));
        return Order(list);
    }

    /// <summary>
    /// The order the Cleaner lists things in (Ted, 0.8.20): Windows first, then each program A to Z, and Advanced Cleaning last.
    /// Inside every group the items are A to Z. Groups stay together, so new entries never need placing by hand.
    /// </summary>
    public static IReadOnlyList<CleanTarget> Order(IEnumerable<CleanTarget> targets) => Order(targets, _ => 0);

    /// <summary>
    /// A custom order (Ted, 0.8.21): groups and the items inside them by <paramref name="value"/>, highest first (the latest
    /// clean, the most freed, the largest found). Ties and items with no value fall back to the A to Z order above, and
    /// Advanced Cleaning always stays last.
    /// </summary>
    /// <param name="windowsFirst">False for the plain A to Z sort (0.8.23), where Windows takes its alphabetical place.</param>
    /// <param name="ticked">Default sort (Ted, 0.8.26): ticked items go to the top of their group, A to Z, then the rest A to Z.</param>
    public static IReadOnlyList<CleanTarget> Order(IEnumerable<CleanTarget> targets, Func<CleanTarget, long> value, bool windowsFirst = true,
        Func<CleanTarget, bool>? ticked = null)
    {
        var list = targets.ToList();
        var groupValue = list.GroupBy(t => t.Group).ToDictionary(g => g.Key, g => g.Max(value));
        return list
            .OrderBy(t => t.Advanced ? 1 : 0)
            .ThenByDescending(t => groupValue[t.Group])
            .ThenBy(t => windowsFirst && t.Group == "Windows" ? 0 : 1)
            .ThenBy(t => t.Group, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(t => ticked?.Invoke(t) == true ? 0 : 1)
            .ThenByDescending(value)
            .ThenBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>Where each browser's program usually is (its icon is shown beside it). Empty for an unknown name.</summary>
    public static string[] AppFiles(string browser) => browser switch
    {
        "Google Chrome" => new[]
        {
            ProgramFiles(@"Google\Chrome\Application\chrome.exe"), ProgramFilesX86(@"Google\Chrome\Application\chrome.exe"),
            Local(@"Google\Chrome\Application\chrome.exe"),
        },
        "Microsoft Edge" => new[] { ProgramFilesX86(@"Microsoft\Edge\Application\msedge.exe"), ProgramFiles(@"Microsoft\Edge\Application\msedge.exe") },
        "Brave" => new[] { ProgramFiles(@"BraveSoftware\Brave-Browser\Application\brave.exe"), Local(@"BraveSoftware\Brave-Browser\Application\brave.exe") },
        "Vivaldi" => new[] { Local(@"Vivaldi\Application\vivaldi.exe"), ProgramFiles(@"Vivaldi\Application\vivaldi.exe") },
        "Mozilla Firefox" => new[] { ProgramFiles(@"Mozilla Firefox\firefox.exe"), ProgramFilesX86(@"Mozilla Firefox\firefox.exe") },
        "Ablaze Floorp" => new[] { ProgramFiles(@"Ablaze Floorp\floorp.exe"), Local(@"Ablaze Floorp\floorp.exe"), ProgramFilesX86(@"Ablaze Floorp\floorp.exe") },
        "Helium" => new[] { Local(@"imput\Helium\Application\chrome.exe"), ProgramFiles(@"imput\Helium\Application\chrome.exe") },
        "Opera" => new[] { Local(@"Programs\Opera\opera.exe"), ProgramFiles(@"Opera\opera.exe") },
        "Opera GX" => new[] { Local(@"Programs\Opera GX\opera.exe"), ProgramFiles(@"Opera GX\opera.exe") },
        "Waterfox" => new[] { ProgramFiles(@"Waterfox\waterfox.exe"), Local(@"Waterfox\waterfox.exe") },
        "Mullvad Browser" => new[]
        {
            Local(@"Mullvad\MullvadBrowser\Release\mullvadbrowser.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), @"Mullvad Browser\Browser\mullvadbrowser.exe"),
        },
        _ => Array.Empty<string>(),
    };

    private static string Local(string rel) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), rel);
    private static string Roaming(string rel) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), rel);
    private static string WinDir(string rel) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), rel);
    private static string SystemDrive => Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
    private static string ProgramFiles(string rel) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), rel);
    private static string ProgramFilesX86(string rel) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), rel);
    private static readonly TimeSpan OneWeek = TimeSpan.FromDays(7);
    private static string ProgramData(string rel) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), rel);

    private static IEnumerable<CleanTarget> WindowsTargets(TimeSpan? tempMinAge, TimeSpan? recycleBinMinAge)
    {
        var ageText = AgeText(tempMinAge);
        const string g = "Windows";
        var icon = new[] { WinDir("explorer.exe") };
        yield return new CleanTarget
        {
            Id = "win.usertemp", Group = g, AppFiles = icon, Name = "Temporary Files (User)", EnabledByDefault = true,
            Description = $"Files in your %TEMP% folder{ageText}.",
            Rules = UserTempRules(tempMinAge),
        };
        yield return new CleanTarget
        {
            Id = "win.systemtemp", Group = g, AppFiles = icon, Name = "Temporary Files (Windows)", EnabledByDefault = true, RequiresAdmin = true,
            Description = $@"Files in C:\Windows\Temp{ageText}.",
            Rules = new[] { new CleanRule(WinDir("Temp"), MinAge: tempMinAge) },
        };
        yield return new CleanTarget
        {
            Id = "win.recyclebin", Group = g, AppFiles = icon, Name = "Recycle Bin", EnabledByDefault = true, Kind = CleanTargetKind.RecycleBin,
            Description = recycleBinMinAge is null
                ? "Empties the Recycle Bin on all drives."
                : "Items deleted more than 24 hours ago, on all drives.",
        };
        yield return new CleanTarget
        {
            Id = "win.thumbcache", Group = g, AppFiles = icon, Name = "Thumbnail Cache", EnabledByDefault = true,
            Description = "Explorer thumbnail databases. Windows rebuilds them as needed; files in use are skipped.",
            Rules = new[] { new CleanRule(Local(@"Microsoft\Windows\Explorer"), "thumbcache_*.db", Recursive: false) },
        };
        yield return new CleanTarget
        {
            Id = "win.wer", Group = g, AppFiles = icon, Name = "Windows Error Reports", EnabledByDefault = true,
            Description = "Archived and queued crash reports.",
            Rules = new[]
            {
                new CleanRule(ProgramData(@"Microsoft\Windows\WER\ReportArchive")),
                new CleanRule(ProgramData(@"Microsoft\Windows\WER\ReportQueue")),
                new CleanRule(Local(@"Microsoft\Windows\WER\ReportArchive")),
                new CleanRule(Local(@"Microsoft\Windows\WER\ReportQueue")),
            },
        };
        yield return new CleanTarget
        {
            Id = "win.dumps", Group = g, AppFiles = icon, Name = "Memory Dumps", EnabledByDefault = true, RequiresAdmin = true,
            Description = "System crash dumps and per-app crash dumps.",
            Rules = new[]
            {
                new CleanRule(WinDir(""), "MEMORY.DMP", Recursive: false),
                new CleanRule(WinDir("Minidump"), "*.dmp", Recursive: false),
                new CleanRule(Local("CrashDumps")),
            },
        };
        yield return new CleanTarget
        {
            Id = "win.d3dcache", Group = g, AppFiles = icon, Name = "DirectX Shader Cache", EnabledByDefault = false,
            Description = "Compiled shaders. Games may stutter briefly while it is rebuilt.",
            Rules = new[] { new CleanRule(Local("D3DSCache")) },
        };
        yield return new CleanTarget
        {
            Id = "win.update", Group = g, AppFiles = icon, Name = "Windows Update Download Cache", EnabledByDefault = false, RequiresAdmin = true,
            Description = "Already-installed update packages. Skip this if an update is in progress.",
            Rules = new[] { new CleanRule(WinDir(@"SoftwareDistribution\Download"), MinAge: OneDay) },
        };
        yield return new CleanTarget
        {
            Id = "win.deliveryopt", Group = g, AppFiles = icon, Name = "Delivery Optimization Cache", EnabledByDefault = false, RequiresAdmin = true,
            Description = "Update files Windows keeps to share with other PCs.",
            Rules = new[] { new CleanRule(WinDir(@"ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache")) },
        };
        yield return new CleanTarget
        {
            Id = "win.recent", Group = g, AppFiles = icon, Name = "Recent Documents List", EnabledByDefault = false,
            Description = "Shortcuts behind File Explorer's 'Recent' list. The documents themselves are not touched.",
            Rules = new[] { new CleanRule(Roaming(@"Microsoft\Windows\Recent"), "*.lnk", Recursive: false) },
        };
    }

    // %TEMP% is user-configurable. If it points somewhere that is not clearly a temp folder
    // (say, the profile root), offer nothing rather than sweep someone's documents.
    private static string AgeText(TimeSpan? age) => age switch
    {
        null => "",
        { TotalDays: >= 1 } d => $" that are more than {d.TotalDays:0} day{(d.TotalDays >= 2 ? "s" : "")} old",
        { } h => $" that are more than {h.TotalHours:0} hours old",
    };

    private static CleanRule[] UserTempRules(TimeSpan? minAge)
    {
        var temp = Path.GetTempPath().TrimEnd('\\');
        var name = Path.GetFileName(temp);
        bool looksLikeTemp = name.Equals("Temp", StringComparison.OrdinalIgnoreCase) ||
                             name.Equals("Tmp", StringComparison.OrdinalIgnoreCase) ||
                             // Per-session temp folders such as ...\Temp
                             (int.TryParse(name, out _) && Path.GetFileName(Path.GetDirectoryName(temp) ?? "").Equals("Temp", StringComparison.OrdinalIgnoreCase));
        return looksLikeTemp ? new[] { new CleanRule(temp, MinAge: minAge) } : Array.Empty<CleanRule>();
    }

    /// <summary>Subfolders, or none when the folder is missing or can't be read (one locked profile mustn't break the list).</summary>
    private static IReadOnlyList<string> SafeDirectories(string root)
    {
        try { return Directory.Exists(root) ? Directory.EnumerateDirectories(root).ToList() : Array.Empty<string>(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { return Array.Empty<string>(); }
    }

    private static IEnumerable<string> ChromiumProfiles(string userData)
    {
        foreach (var dir in SafeDirectories(userData))
        {
            var name = Path.GetFileName(dir);
            if (name == "Default" || name.StartsWith("Profile ", StringComparison.Ordinal) || name == "Guest Profile")
                yield return dir;
        }
    }

    /// <param name="id">Start of the item ids; a fork that keeps Chrome's process name still needs its own.</param>
    private static IEnumerable<CleanTarget> ChromiumTargets(string browser, string id, string process, string userData, params string[] appFiles)
    {
        if (!Directory.Exists(userData)) yield break;
        var profiles = ChromiumProfiles(userData).ToList();
        var blocking = new[] { process };

        yield return new CleanTarget
        {
            Id = $"{id}.cache", Group = browser, AppFiles = appFiles, Name = "Internet Cache", EnabledByDefault = true, BlockingProcesses = blocking,
            Description = "Cached pages, images, scripts and GPU shader caches. Sign-ins are kept.",
            Rules = profiles.SelectMany(p => new[]
            {
                new CleanRule(Path.Combine(p, "Cache")),
                new CleanRule(Path.Combine(p, "Code Cache")),
                new CleanRule(Path.Combine(p, "GPUCache")),
                new CleanRule(Path.Combine(p, @"Service Worker\CacheStorage")),
            }).Append(new CleanRule(Path.Combine(userData, "ShaderCache"))).Append(new CleanRule(Path.Combine(userData, "GrShaderCache")))
            .ToArray(),
        };
        yield return new CleanTarget
        {
            Id = $"{id}.history", Group = browser, AppFiles = appFiles, Name = "Browsing History", EnabledByDefault = false, BlockingProcesses = blocking,
            Description = "Visited sites and download history. Bookmarks and passwords are kept.",
            Rules = profiles.SelectMany(p => new[]
            {
                new CleanRule(p, "History", Recursive: false),
                new CleanRule(p, "History-journal", Recursive: false),
                new CleanRule(p, "Visited Links", Recursive: false),
            }).ToArray(),
        };
        yield return new CleanTarget
        {
            Id = $"{id}.cookies", Group = browser, AppFiles = appFiles, Name = "Cookies", EnabledByDefault = false, BlockingProcesses = blocking,
            Description = "Signs you out of websites.",
            Rules = profiles.SelectMany(p => new[]
            {
                new CleanRule(Path.Combine(p, "Network"), "Cookies", Recursive: false),
                new CleanRule(Path.Combine(p, "Network"), "Cookies-journal", Recursive: false),
            }).ToArray(),
        };
    }

    /// <summary>Firefox and browsers built on it: settings and cookies in the roaming profile, the cache in the local one.</summary>
    private static IEnumerable<CleanTarget> FirefoxTargets(string browser, string process, string roamingProfiles, string localProfiles,
        params string[] appFiles)
        => FirefoxTargets(browser, process, process, roamingProfiles, localProfiles, appFiles);

    private static IEnumerable<CleanTarget> FirefoxTargets(string browser, string id, string process, string roamingProfiles, string localProfiles,
        string[] appFiles)
    {
        if (!Directory.Exists(roamingProfiles) && !Directory.Exists(localProfiles)) yield break;
        var blocking = new[] { process };

        IEnumerable<string> Dirs(string root) => SafeDirectories(root);

        yield return new CleanTarget
        {
            Id = $"{id}.cache", Group = browser, AppFiles = appFiles, Name = "Internet Cache", EnabledByDefault = true, BlockingProcesses = blocking,
            Description = "Cached pages, images and scripts. Sign-ins are kept.",
            Rules = Dirs(localProfiles).SelectMany(p => new[]
            {
                new CleanRule(Path.Combine(p, "cache2")),
                new CleanRule(Path.Combine(p, "startupCache")),
                new CleanRule(Path.Combine(p, "thumbnails")),
            }).ToArray(),
        };
        yield return new CleanTarget
        {
            Id = $"{id}.cookies", Group = browser, AppFiles = appFiles, Name = "Cookies", EnabledByDefault = false, BlockingProcesses = blocking,
            Description = "Signs you out of websites.",
            Rules = Dirs(roamingProfiles).SelectMany(p => new[]
            {
                new CleanRule(p, "cookies.sqlite", Recursive: false),
                new CleanRule(p, "cookies.sqlite-wal", Recursive: false),
            }).ToArray(),
        };
        // Firefox history lives in places.sqlite together with bookmarks, so it is deliberately not offered.
    }

    /// <summary>
    /// Mullvad Browser (built on Firefox): installed copies keep profiles under Mullvad\MullvadBrowser like Firefox does;
    /// the older portable copy keeps them beside itself, usually on the desktop.
    /// </summary>
    private static IEnumerable<CleanTarget> MullvadTargets()
    {
        var appFiles = AppFiles("Mullvad Browser");
        var roaming = Roaming(@"Mullvad\MullvadBrowser\Profiles");
        if (Directory.Exists(roaming))
            return FirefoxTargets("Mullvad Browser", "mullvad", "mullvadbrowser", roaming, Local(@"Mullvad\MullvadBrowser\Profiles"), appFiles);
        var portable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Mullvad Browser", "Browser");
        var data = Path.Combine(portable, @"TorBrowser\Data\Browser");
        return FirefoxTargets("Mullvad Browser", "mullvad", "mullvadbrowser", data, Path.Combine(data, "Caches"), appFiles);
    }

    /// <summary>
    /// Opera and Opera GX (built on Chromium) keep their profile in Roaming\Opera Software\&lt;edition&gt; itself, with newer
    /// versions adding profile folders inside it; the cache sits in the same place under Local.
    /// </summary>
    private static IEnumerable<CleanTarget> OperaTargets(string browser, string id, string edition)
    {
        var roaming = Roaming($@"Opera Software\{edition}");
        var local = Local($@"Opera Software\{edition}");
        if (!Directory.Exists(roaming) && !Directory.Exists(local)) yield break;
        var profiles = new[] { roaming }.Concat(ChromiumProfiles(roaming)).ToList();
        var localProfiles = new[] { local }.Concat(ChromiumProfiles(local)).ToList();
        yield return new CleanTarget
        {
            Id = $"{id}.cache", Group = browser, Name = "Internet Cache", EnabledByDefault = true, BlockingProcesses = new[] { "opera" },
            AppFiles = AppFiles(browser),
            Description = "Cached pages, images, scripts and GPU shader caches. Sign-ins are kept.",
            Rules = localProfiles.SelectMany(p => new[] { new CleanRule(Path.Combine(p, "Cache")) })
                .Concat(profiles.SelectMany(p => new[]
                {
                    new CleanRule(Path.Combine(p, "Code Cache")),
                    new CleanRule(Path.Combine(p, "GPUCache")),
                    new CleanRule(Path.Combine(p, "ShaderCache")),
                }))
                .ToArray(),
        };
    }

    /// <summary>The group Advanced Cleaning's extra items are listed under.</summary>
    public const string AdvancedGroup = "Advanced Cleaning";

    /// <summary>
    /// Deeper temporary-file cleaning: other temp folders Windows keeps, old logs, crash reports and caches.
    /// Only shown under Advanced Cleaning and never ticked by default; everything here is rebuilt or rewritten as needed.
    /// </summary>
    private static IEnumerable<CleanTarget> AdvancedTargets(TimeSpan? tempMinAge)
    {
        var ageText = AgeText(tempMinAge);
        const string g = AdvancedGroup;
        yield return new CleanTarget
        {
            Id = "adv.servicetemp", Group = g, Advanced = true, RequiresAdmin = true,
            Name = "Temporary Files (Windows Services)",
            Description = $"Temp folders of Windows' own background services and the system account{ageText}.",
            Rules = new[]
            {
                new CleanRule(WinDir("SystemTemp"), MinAge: tempMinAge),
                new CleanRule(WinDir(@"ServiceProfiles\LocalService\AppData\Local\Temp"), MinAge: tempMinAge),
                new CleanRule(WinDir(@"ServiceProfiles\NetworkService\AppData\Local\Temp"), MinAge: tempMinAge),
                new CleanRule(Path.Combine(Environment.SystemDirectory, @"config\systemprofile\AppData\Local\Temp"), MinAge: tempMinAge),
            },
        };
        yield return new CleanTarget
        {
            Id = "adv.storeapps", Group = g, Advanced = true,
            Name = "Temporary Files (Store Apps)",
            Description = $"The temp and web cache folders of apps from the Microsoft Store{ageText}. App data and settings are kept.",
            Rules = SafeDirectories(Local("Packages")).SelectMany(p => new[]
            {
                new CleanRule(Path.Combine(p, "TempState"), MinAge: tempMinAge),
                new CleanRule(Path.Combine(p, @"AC\Temp"), MinAge: tempMinAge),
                new CleanRule(Path.Combine(p, @"AC\INetCache"), MinAge: tempMinAge),
            }).ToArray(),
        };
        yield return new CleanTarget
        {
            Id = "adv.inetcache", Group = g, Advanced = true,
            Name = "Windows Internet Cache",
            Description = "Web files cached by Windows itself and by older programs that use its web engine (Disk Cleanup's Temporary Internet Files). Files in use are skipped.",
            Rules = new[] { new CleanRule(Local(@"Microsoft\Windows\INetCache"), MinAge: tempMinAge) },
        };
        yield return new CleanTarget
        {
            Id = "adv.logs", Group = g, Advanced = true, RequiresAdmin = true,
            Name = "Windows Log Files",
            Description = "Setup, Windows upgrade, servicing and update logs more than 7 days old (Disk Cleanup's Setup Log Files and Windows Upgrade Log Files). Only needed when troubleshooting an old problem.",
            Rules = new[]
            {
                new CleanRule(WinDir(@"Logs\CBS"), "*.log", MinAge: OneWeek),
                new CleanRule(WinDir(@"Logs\CBS"), "*.cab", MinAge: OneWeek),
                new CleanRule(WinDir(@"Logs\DISM"), "*.log", MinAge: OneWeek),
                new CleanRule(WinDir(@"Logs\WindowsUpdate"), "*.etl", MinAge: OneWeek),
                new CleanRule(WinDir(@"Logs\MoSetup"), "*.log", MinAge: OneWeek),
                new CleanRule(WinDir(@"Panther"), "*.log", MinAge: OneWeek),
                new CleanRule(Path.Combine(SystemDrive, @"$Windows.~BT\Sources\Panther"), "*.log", MinAge: OneWeek),
                new CleanRule(WinDir(""), "setupact.log", Recursive: false, MinAge: OneWeek),
                new CleanRule(WinDir(""), "setuperr.log", Recursive: false, MinAge: OneWeek),
                new CleanRule(WinDir(@"Logs\SIH"), "*.etl", MinAge: OneWeek),
                new CleanRule(WinDir(@"Logs\WaaSMedic"), "*.etl", MinAge: OneWeek),
            },
        };
        yield return new CleanTarget
        {
            Id = "adv.livekernel", Group = g, Advanced = true, RequiresAdmin = true,
            Name = "Live Kernel Reports",
            Description = "Large diagnostic dumps Windows saves after a driver or hardware hang. Only needed when troubleshooting.",
            Rules = new[] { new CleanRule(WinDir("LiveKernelReports"), "*.dmp") },
        };
        yield return new CleanTarget
        {
            Id = "adv.gpucache", Group = g, Advanced = true,
            Name = "Graphics Driver Shader Caches",
            Description = "Shaders saved by NVIDIA, AMD and Intel drivers. Games may stutter briefly while they are rebuilt.",
            Rules = new[]
            {
                new CleanRule(Local(@"NVIDIA\DXCache")),
                new CleanRule(Local(@"NVIDIA\GLCache")),
                new CleanRule(Local(@"AMD\DxCache")),
                new CleanRule(Local(@"AMD\DxcCache")),
                new CleanRule(Local(@"AMD\VkCache")),
                new CleanRule(Local(@"AMD\GLCache")),
                new CleanRule(Local(@"Intel\ShaderCache")),
            },
        };
        yield return new CleanTarget
        {
            Id = "adv.iconcache", Group = g, Advanced = true,
            Name = "Windows Icon Cache",
            Description = "The icons File Explorer has saved. Windows rebuilds them as needed; files in use are skipped, so some may stay until you restart.",
            Rules = new[]
            {
                new CleanRule(Local(@"Microsoft\Windows\Explorer"), "iconcache_*.db", Recursive: false),
                new CleanRule(Local(""), "IconCache.db", Recursive: false),
            },
        };
        yield return new CleanTarget
        {
            Id = "adv.inethistory", Group = g, Advanced = true,
            Name = "Windows Internet History",
            Description = "Sites visited with Windows' own web engine and older programs that use it. Files in use are skipped.",
            Rules = new[] { new CleanRule(Local(@"Microsoft\Windows\History")) },
        };
        yield return new CleanTarget
        {
            Id = "adv.rdpcache", Group = g, Advanced = true,
            Name = "Remote Desktop Cache",
            Description = "Screen images Remote Desktop saves to draw remote PCs faster. It is rebuilt on the next connection.",
            Rules = new[] { new CleanRule(Local(@"Microsoft\Terminal Server Client\Cache")) },
        };
        yield return new CleanTarget
        {
            Id = "adv.sidebar", Group = g, Advanced = true,
            Name = "Windows Sidebar Cache",
            Description = "Left over from desktop gadgets in older versions of Windows.",
            Rules = new[] { new CleanRule(Local(@"Microsoft\Windows Sidebar\Cache")) },
        };
        yield return new CleanTarget
        {
            Id = "adv.downloadedprograms", Group = g, Advanced = true, RequiresAdmin = true,
            Name = "Downloaded Program Files",
            Description = "Old ActiveX controls and Java applets that Internet Explorer downloaded.",
            Rules = new[] { "*.ocx", "*.inf", "*.dll", "*.cab", "*.exe" }.Select(p => new CleanRule(WinDir("Downloaded Program Files"), p)).ToArray(),
        };
        yield return new CleanTarget
        {
            Id = "adv.offlinepages", Group = g, Advanced = true, RequiresAdmin = true,
            Name = "Offline Web Pages",
            Description = "Pages Internet Explorer saved for reading offline.",
            Rules = new[] { new CleanRule(WinDir("Offline Web Pages")) },
        };
        yield return new CleanTarget
        {
            Id = "adv.activesetup", Group = g, Advanced = true, RequiresAdmin = true,
            Name = "Active Setup Temp Folders",
            Description = $@"Files Windows' Active Setup left in C:\Windows\msdownld.tmp{ageText}.",
            Rules = new[] { new CleanRule(WinDir("msdownld.tmp"), MinAge: tempMinAge) },
        };
        yield return new CleanTarget
        {
            Id = "adv.chkdsk", Group = g, Advanced = true, RequiresAdmin = true,
            Name = "Old Chkdsk Files",
            Description = @"Pieces of damaged files that Check Disk saved as FILE0000.CHK in FOUND.000 folders on the Windows drive. They can rarely be read.",
            Rules = SafeDirectories(SystemDrive)
                .Where(d => Regex.IsMatch(Path.GetFileName(d), @"^found\.\d{3}$", RegexOptions.IgnoreCase))
                .Select(d => new CleanRule(d, "*.chk", Recursive: false)).ToArray(),
        };
        yield return new CleanTarget
        {
            Id = "adv.prefetch", Group = g, Advanced = true, RequiresAdmin = true,
            Name = "Prefetch Data",
            Description = "Notes Windows keeps to start programs faster.",
            Warning = "Windows and your programs will start more slowly for a while, until Windows rebuilds this. Cleaning it doesn't make your PC faster.",
            Rules = new[] { new CleanRule(WinDir("Prefetch"), "*.pf", Recursive: false) },
        };
        yield return new CleanTarget
        {
            Id = "adv.printqueue", Group = g, Advanced = true, RequiresAdmin = true,
            Name = "Print Queue",
            Description = "Files of print jobs waiting in the queue. Jobs that are printing now are skipped.",
            Warning = "Anything waiting to print is cancelled and won't print.",
            Rules = new[] { new CleanRule(Path.Combine(Environment.SystemDirectory, @"spool\PRINTERS")) },
        };

        // Windows tasks: these run Windows' own tools instead of deleting files.
        yield return Task("task.dns", "DNS Cache", "Forgets the web addresses Windows has looked up, so they are looked up fresh. Often fixes sites that won't load after a change.");
        yield return Task("task.clipboard", "Clipboard", "Empties what is copied right now. Pinned items in Clipboard History (Windows+V) are kept.");
        yield return Task("task.runmru", "Run (in Start Menu)", "The list of commands typed into the Run box (Windows+R). It is backed up to a .reg file first.");
        yield return Task("task.branchcache", "BranchCache", "Files Windows keeps to share with other PCs on a work network. Most home PCs have none.", admin: true);
        yield return Task("task.components", "Windows Update Cleanup",
            "Runs Windows' own cleanup of the component store (DISM): old versions of updated Windows files. This can take several minutes.",
            admin: true,
            warning: "Updates installed before the cleanup can no longer be uninstalled. Don't cancel it part way, and don't run it while Windows is installing updates.");
        yield return Task("task.eventlogs", "Windows Event Viewer Logs",
            "Clears the Application, System and Setup logs in Event Viewer, after saving a copy of each to Backups\\Event Logs. The Security log is never cleared.",
            admin: true,
            warning: "These logs are how you and support people find out what went wrong. Clear them only when you don't have a problem to look into.");
        yield return Task("task.shadows", "Windows Shadow Copies",
            "Deletes older System Restore points and shadow copies on the Windows drive. The newest one is always kept.",
            admin: true,
            warning: "You won't be able to go back to the deleted restore points, or bring back older versions of files from them.");
        yield return Task("task.hibernate", "System Hibernation File",
            @"Turns hibernation off (powercfg), which deletes C:\hiberfil.sys. Turn it back on with 'powercfg /hibernate on'.",
            admin: true,
            warning: "Hibernate and Fast Startup stop working, and a laptop can no longer save your work to disk when its battery runs out while asleep.");
        // Disk Cleanup items whose files only Windows knows or may touch: these run Windows' own Disk Cleanup with just that one item ticked.
        yield return Task("task.dc.defender", "Microsoft Defender Antivirus",
            "Files Microsoft Defender no longer needs, such as old scan results. Defender itself decides what goes.", admin: true);
        yield return Task("task.dc.diagviewer", "Diagnostic Data Viewer Database Files",
            "The local copy of diagnostic data shown in Diagnostic Data Viewer. Nothing is sent to Microsoft.", admin: true);
        yield return Task("task.dc.feedbackhub", "Feedback Hub Archive Log Files",
            "Logs saved with feedback you sent through Feedback Hub.", admin: true);
        yield return Task("task.dc.language", "Language Resource Files",
            "Language and keyboard files Windows says aren't in use.", admin: true,
            warning: "If you add a language or keyboard later, Windows may need to download these files again.");
        yield return Task("task.dc.retaildemo", "RetailDemo Offline Content",
            "Store demo-mode videos and apps some PCs come with from the shop.", admin: true);
        yield return Task("task.dc.setup", "Temporary Setup Files",
            "Files that setup programs left behind and no longer need.", admin: true);
        yield return Task("task.dc.wininstall", "Temporary Windows Installation Files",
            "Files left over from installing a Windows update or upgrade.", admin: true,
            warning: "Don't run this while Windows is installing an update or upgrade; it may have to download the files again.");
        yield return Task("task.dc.filehistory", "User File History",
            "Older File History copies of your files. Windows keeps the newest copy of each file.", admin: true,
            warning: "You won't be able to bring back the older versions of your files that File History saved.");
        yield return Task("task.compactos", "Compress System Installation",
            "Compresses Windows' own files (compact /compactos) to save several GB. Undo it with 'compact /compactos:never'. This can take a long time.",
            admin: true,
            warning: "Windows may run a little slower on older or slower PCs, because files are uncompressed as they are used. Leave the PC on until it finishes.");
    }

    private static CleanTarget Task(string id, string name, string description, bool admin = false, string warning = "") => new()
    {
        Id = id, Group = AdvancedGroup, Advanced = true, Kind = CleanTargetKind.Task, TaskId = id,
        Name = name, Description = description, RequiresAdmin = admin, Warning = warning,
    };
}
