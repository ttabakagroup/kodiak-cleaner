using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using KodiakCleaner.Core.Startup;

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
        list.AddRange(AppTargets());
        list.AddRange(AdvancedTargets(tempMinAge));
        list.AddRange(DownloadsTargets());
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
        yield return new CleanTarget
        {
            Id = "win.startmenulinks", Group = g, AppFiles = icon, Name = "Start Menu Shortcuts", EnabledByDefault = false,
            Description = "Start menu shortcuts whose program or file is gone. Shortcuts to network or removable drives are kept.",
            Rules = new[]
            {
                new CleanRule(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "*.lnk", RemoveEmptySubdirectories: false, Filter: ShellLink.IsBroken),
                new CleanRule(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "*.lnk", RemoveEmptySubdirectories: false, Filter: ShellLink.IsBroken),
            },
        };
        yield return new CleanTarget
        {
            Id = "win.desktoplinks", Group = g, AppFiles = icon, Name = "Desktop Shortcuts", EnabledByDefault = false,
            Description = "Desktop shortcuts whose program or file is gone. Shortcuts to network or removable drives are kept.",
            Rules = new[]
            {
                new CleanRule(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "*.lnk", Recursive: false, Filter: ShellLink.IsBroken),
                new CleanRule(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "*.lnk", Recursive: false, Filter: ShellLink.IsBroken),
            },
        };
        // Widgets is a packaged WebView2 app, so its web cache sits in LocalState\EBWebView (WebView2's folder for packaged apps).
        var widgets = Local(@"Packages\MicrosoftWindows.Client.WebExperience_cw5n1h2txyewy\LocalState\EBWebView");
        yield return new CleanTarget
        {
            Id = "win.widgets", Group = g, AppFiles = icon, Name = "Windows Widgets Cache", EnabledByDefault = false,
            Description = "Pictures and pages the Widgets board saved. Widgets downloads them again; files in use are skipped.",
            Rules = new[]
            {
                new CleanRule(Path.Combine(widgets, @"Default\Cache")),
                new CleanRule(Path.Combine(widgets, @"Default\Code Cache")),
                new CleanRule(Path.Combine(widgets, @"Default\GPUCache")),
                new CleanRule(Path.Combine(widgets, "ShaderCache")),
                new CleanRule(Path.Combine(widgets, "GrShaderCache")),
            },
        };
        yield return new CleanTarget
        {
            Id = "win.notifications", Group = g, AppFiles = icon, Name = "Windows Notifications", EnabledByDefault = false,
            Description = "Pictures saved with past notifications. Notifications you haven't read and your notification settings are kept.",
            Rules = new[] { new CleanRule(Local(@"Microsoft\Windows\Notifications\wpnidm")) },
        };
        yield return new CleanTarget
        {
            Id = "win.gameexplorer", Group = g, AppFiles = icon, Name = "Game Explorer Cache", EnabledByDefault = false,
            Description = "Game pictures and details older versions of Windows saved for the Games folder.",
            Rules = new[] { new CleanRule(Local(@"Microsoft\Windows\GameExplorer")) },
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
        yield return new CleanTarget
        {
            Id = $"{id}.metrics", Group = browser, AppFiles = appFiles, Name = "Metrics Temp Files", EnabledByDefault = true, BlockingProcesses = blocking,
            Description = "Usage statistics the browser keeps on disk before it sends or drops them.",
            Rules = new[]
            {
                new CleanRule(Path.Combine(userData, "BrowserMetrics"), "*.pma"),
                new CleanRule(userData, "BrowserMetrics-spare.pma", Recursive: false),
            },
        };
        yield return new CleanTarget
        {
            Id = $"{id}.session", Group = browser, AppFiles = appFiles, Name = "Session", EnabledByDefault = false, BlockingProcesses = blocking,
            Description = "The tabs and windows saved to reopen next time. The browser starts with a new tab instead.",
            Rules = profiles.SelectMany(p => new[]
            {
                new CleanRule(Path.Combine(p, "Sessions")),
                new CleanRule(p, "Current Session", Recursive: false),
                new CleanRule(p, "Current Tabs", Recursive: false),
                new CleanRule(p, "Last Session", Recursive: false),
                new CleanRule(p, "Last Tabs", Recursive: false),
            }).ToArray(),
        };
        yield return new CleanTarget
        {
            Id = $"{id}.bookmarksbackup", Group = browser, AppFiles = appFiles, Name = "Bookmarks Backup", EnabledByDefault = false, BlockingProcesses = blocking,
            Description = "The spare copy of your bookmarks the browser saves. Your bookmarks themselves are kept.",
            Rules = profiles.Select(p => new CleanRule(p, "Bookmarks.bak", Recursive: false)).ToArray(),
        };
        yield return new CleanTarget
        {
            Id = $"{id}.formdata", Group = AdvancedGroup, Advanced = true, Name = $"Saved Form Information ({browser})", BlockingProcesses = blocking,
            Description = $"What {browser} fills into forms: names, addresses and other typed entries (its Web Data file).",
            Warning = "The same file also holds saved payment cards and the search engines you added, so they go too. If you sync, they may come back from your account.",
            Rules = profiles.SelectMany(p => new[]
            {
                new CleanRule(p, "Web Data", Recursive: false),
                new CleanRule(p, "Web Data-journal", Recursive: false),
            }).ToArray(),
        };
        yield return new CleanTarget
        {
            Id = $"{id}.passwords", Group = AdvancedGroup, Advanced = true, Name = $"Saved Passwords ({browser})", BlockingProcesses = blocking,
            Description = $"Every password {browser} saved on this PC.",
            Warning = "You'll have to type or reset your passwords for every site. Export them from the browser first if you might need them. If you sync, they may come back from your account.",
            Rules = profiles.SelectMany(p => new[]
            {
                new CleanRule(p, "Login Data", Recursive: false),
                new CleanRule(p, "Login Data-journal", Recursive: false),
                new CleanRule(p, "Login Data For Account", Recursive: false),
                new CleanRule(p, "Login Data For Account-journal", Recursive: false),
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
        yield return new CleanTarget
        {
            Id = $"{id}.session", Group = browser, AppFiles = appFiles, Name = "Session", EnabledByDefault = false, BlockingProcesses = blocking,
            Description = "The tabs and windows saved to reopen next time. The browser starts with a new tab instead.",
            Rules = Dirs(roamingProfiles).SelectMany(p => new[]
            {
                new CleanRule(p, "sessionstore.jsonlz4", Recursive: false),
                new CleanRule(Path.Combine(p, "sessionstore-backups")),
            }).ToArray(),
        };
        yield return new CleanTarget
        {
            Id = $"{id}.bookmarksbackup", Group = browser, AppFiles = appFiles, Name = "Bookmarks Backup", EnabledByDefault = false, BlockingProcesses = blocking,
            Description = "The daily copies of your bookmarks the browser saves. Your bookmarks themselves are kept.",
            Rules = Dirs(roamingProfiles).Select(p => new CleanRule(Path.Combine(p, "bookmarkbackups"), "*.jsonlz4")).ToArray(),
        };
        // Crash reports sit beside the Profiles folder (Mozilla\Firefox\Crash Reports).
        var crashReports = Path.Combine(Path.GetDirectoryName(roamingProfiles) ?? roamingProfiles, "Crash Reports");
        yield return new CleanTarget
        {
            Id = $"{id}.crashreports", Group = browser, AppFiles = appFiles, Name = "Crash Reports", EnabledByDefault = false, BlockingProcesses = blocking,
            Description = "Reports saved after the browser crashed, sent or waiting to be sent.",
            Rules = new[]
            {
                new CleanRule(Path.Combine(crashReports, "submitted")),
                new CleanRule(Path.Combine(crashReports, "pending")),
                new CleanRule(Path.Combine(crashReports, "events")),
            },
        };
        yield return new CleanTarget
        {
            Id = $"{id}.siteprefs", Group = browser, AppFiles = appFiles, Name = "Site Preferences", EnabledByDefault = false, BlockingProcesses = blocking,
            Description = "Settings the browser remembers for each site, such as zoom level. Site permissions are kept.",
            Rules = Dirs(roamingProfiles).Select(p => new CleanRule(p, "content-prefs.sqlite", Recursive: false)).ToArray(),
        };
        yield return new CleanTarget
        {
            Id = $"{id}.formdata", Group = browser, AppFiles = appFiles, Name = "Saved Form Information", EnabledByDefault = false, BlockingProcesses = blocking,
            Description = "Entries typed into forms and search boxes that the browser offers again. Passwords are kept.",
            Rules = Dirs(roamingProfiles).Select(p => new CleanRule(p, "formhistory.sqlite", Recursive: false)).ToArray(),
        };
        yield return new CleanTarget
        {
            Id = $"{id}.passwords", Group = AdvancedGroup, Advanced = true, Name = $"Saved Passwords ({browser})", BlockingProcesses = blocking,
            Description = $"Every password {browser} saved on this PC.",
            Warning = "You'll have to type or reset your passwords for every site. Export them from the browser first if you might need them. If you sync, they may come back from your account.",
            Rules = Dirs(roamingProfiles).SelectMany(p => new[]
            {
                new CleanRule(p, "logins.json", Recursive: false),
                new CleanRule(p, "logins-backup.json", Recursive: false),
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

    /// <summary>
    /// Other programs' caches, logs and installer leftovers. A program is listed only when its folder is there, and only files
    /// it makes again by itself are offered: chats, settings, sign-ins and saved sites are never touched.
    /// </summary>
    private static IEnumerable<CleanTarget> AppTargets()
    {
        CleanTarget App(string group, string id, string name, string description, string[] appFiles, CleanRule[] rules,
            string[]? blocking = null, bool admin = false) => new()
        {
            Id = id, Group = group, Name = name, Description = description, AppFiles = appFiles, Rules = rules,
            BlockingProcesses = blocking ?? Array.Empty<string>(), RequiresAdmin = admin,
        };

        var amd = Path.Combine(SystemDrive, "AMD");
        if (Directory.Exists(amd))
            yield return App("AMD Software", "amd.installers", "Driver Installer Files",
                "Driver packages the AMD installer unpacked to C:\\AMD, more than a week old. The installed driver isn't touched.",
                new[] { ProgramFiles(@"AMD\CNext\CNext\RadeonSoftware.exe") },
                SafeDirectories(amd).Select(d => new CleanRule(d, MinAge: OneWeek)).ToArray());

        var anyDesk = Roaming("AnyDesk");
        if (Directory.Exists(anyDesk))
        {
            var files = new[] { ProgramFilesX86(@"AnyDesk\AnyDesk.exe"), ProgramFiles(@"AnyDesk\AnyDesk.exe") };
            yield return App("AnyDesk", "anydesk.logs", "Log Files", "AnyDesk's trace logs. The one in use is skipped.", files,
                new[] { new CleanRule(anyDesk, "*.trace", Recursive: false), new CleanRule(ProgramData("AnyDesk"), "*.trace", Recursive: false) });
            yield return App("AnyDesk", "anydesk.thumbnails", "Thumbnails",
                "Screen previews of PCs you connected to. Your address book and recent sessions are kept.", files,
                new[] { new CleanRule(Path.Combine(anyDesk, "thumbnails")) }, new[] { "AnyDesk" });
        }

        var discord = Roaming("discord");
        if (Directory.Exists(discord))
        {
            var files = new[] { Local(@"Discord\Update.exe") };
            yield return App("Discord", "discord.cache", "Internet Cache", "Cached pictures, scripts and GPU shaders. You stay signed in.", files,
                new[]
                {
                    new CleanRule(Path.Combine(discord, @"Cache\Cache_Data")), new CleanRule(Path.Combine(discord, "Code Cache")),
                    new CleanRule(Path.Combine(discord, "GPUCache")),
                }, new[] { "Discord" });
            yield return App("Discord", "discord.logs", "Log Files", "Discord's own log files.", files,
                new[] { new CleanRule(Path.Combine(discord, "logs")) }, new[] { "Discord" });
        }

        var fileZilla = Roaming("FileZilla");
        if (Directory.Exists(fileZilla))
        {
            var files = new[] { ProgramFiles(@"FileZilla FTP Client\filezilla.exe"), ProgramFilesX86(@"FileZilla FTP Client\filezilla.exe") };
            yield return App("FileZilla", "filezilla.cache", "Directory Listing Cache", "Folder listings saved from servers. Saved sites and passwords are kept.", files,
                new[] { new CleanRule(Path.Combine(fileZilla, "cache")) }, new[] { "filezilla" });
            yield return App("FileZilla", "filezilla.recent", "Recent Servers", "The Quickconnect list of servers you connected to. Saved sites are kept.", files,
                new[] { new CleanRule(fileZilla, "recentservers.xml", Recursive: false) }, new[] { "filezilla" });
        }

        var oneDrive = Local(@"Microsoft\OneDrive");
        if (Directory.Exists(oneDrive))
            yield return App("Microsoft OneDrive", "onedrive.logs", "Log Files", "OneDrive's sync and setup logs more than 7 days old. Your files aren't touched.",
                new[] { Local(@"Microsoft\OneDrive\OneDrive.exe"), ProgramFiles(@"Microsoft OneDrive\OneDrive.exe") },
                new[] { new CleanRule(Path.Combine(oneDrive, "logs"), MinAge: OneWeek), new CleanRule(Path.Combine(oneDrive, @"setup\logs"), MinAge: OneWeek) });

        var npmCache = Local("npm-cache");
        var npmOld = Roaming("npm-cache");
        if (Directory.Exists(npmCache) || Directory.Exists(npmOld))
            yield return App("Node.js", "node.npmcache", "NPM Cache", "Packages npm downloaded before. It downloads them again when a project needs them.",
                new[] { ProgramFiles(@"nodejs\node.exe") },
                new[] { new CleanRule(npmCache), new CleanRule(npmOld) }, new[] { "node", "npm" });

        var nvidiaDownloads = ProgramData(@"NVIDIA Corporation\Downloader");
        var nvidiaUnpacked = Path.Combine(SystemDrive, "NVIDIA");
        if (Directory.Exists(nvidiaDownloads) || Directory.Exists(nvidiaUnpacked))
            yield return App("NVIDIA", "nvidia.installers", "Driver Installer Files",
                "Driver downloads and the packages the installer unpacked to C:\\NVIDIA, more than a day old. The installed driver isn't touched.",
                new[] { ProgramFiles(@"NVIDIA Corporation\NVIDIA app\CEF\NVIDIA app.exe"), ProgramFiles(@"NVIDIA Corporation\Control Panel Client\nvcplui.exe") },
                SafeDirectories(nvidiaUnpacked).Select(d => new CleanRule(d, MinAge: OneDay)).Prepend(new CleanRule(nvidiaDownloads, MinAge: OneDay)).ToArray(),
                admin: true);

        var steam = SteamFolder();
        if (steam is not null)
        {
            var files = new[] { Path.Combine(steam, "steam.exe") };
            var html = Local(@"Steam\htmlcache");
            yield return App("Steam", "steam.cache", "Internet Cache", "The store and community pages Steam saved. Games and sign-in are kept.", files,
                new[]
                {
                    new CleanRule(Path.Combine(steam, @"appcache\httpcache")), new CleanRule(Path.Combine(html, @"Default\Cache")),
                    new CleanRule(Path.Combine(html, @"Default\Code Cache")), new CleanRule(Path.Combine(html, @"Default\GPUCache")),
                    new CleanRule(Path.Combine(html, "GrShaderCache")), new CleanRule(Path.Combine(html, "ShaderCache")),
                }, new[] { "steam" });
            yield return App("Steam", "steam.dumps", "Crash Dumps", "Files Steam saved after it crashed.", files,
                new[] { new CleanRule(Path.Combine(steam, "dumps")) }, new[] { "steam" });
            yield return App("Steam", "steam.logs", "Log Files", "Steam's own log files.", files,
                new[] { new CleanRule(Path.Combine(steam, "logs")) }, new[] { "steam" });
        }

        var telegram = Roaming(@"Telegram Desktop\tdata");
        if (Directory.Exists(telegram))
            yield return App("Telegram Desktop", "telegram.cache", "Cache Files",
                "Pictures, videos and files Telegram saved while you chatted (what its Clear Cache removes). Chats stay in your account.",
                new[] { Roaming(@"Telegram Desktop\Telegram.exe") },
                new[] { new CleanRule(Path.Combine(telegram, "user_data")) }, new[] { "Telegram" });

        var vlc = Roaming("vlc");
        if (Directory.Exists(vlc))
            yield return App("VLC Media Player", "vlc.art", "Album Art Cache", "Cover pictures VLC downloaded. It gets them again when needed.",
                new[] { ProgramFiles(@"VideoLAN\VLC\vlc.exe"), ProgramFilesX86(@"VideoLAN\VLC\vlc.exe") },
                new[] { new CleanRule(Path.Combine(vlc, "art")), new CleanRule(Path.Combine(vlc, "art_cache")) }, new[] { "vlc" });

        var whatsApp = Roaming("WhatsApp");
        if (Directory.Exists(whatsApp))
            yield return App("WhatsApp", "whatsapp.cache", "Cache Files", "Cached pictures, scripts and GPU shaders. Chats and sign-in are kept.",
                new[] { Local(@"WhatsApp\WhatsApp.exe") },
                new[]
                {
                    new CleanRule(Path.Combine(whatsApp, @"Cache\Cache_Data")), new CleanRule(Path.Combine(whatsApp, "Code Cache")),
                    new CleanRule(Path.Combine(whatsApp, "GPUCache")),
                }, new[] { "WhatsApp" });

        var zoom = Roaming("Zoom");
        if (Directory.Exists(zoom))
        {
            var files = new[] { Roaming(@"Zoom\bin\Zoom.exe") };
            yield return App("Zoom", "zoom.cache", "Internet Cache", "Cached pictures and GPU shaders. You stay signed in.", files,
                new[] { new CleanRule(Path.Combine(zoom, @"data\Cache")), new CleanRule(Path.Combine(zoom, @"data\GPUCache")) }, new[] { "Zoom" });
            yield return App("Zoom", "zoom.logs", "Log Files", "Zoom's own log files.", files,
                new[] { new CleanRule(Path.Combine(zoom, "logs")), new CleanRule(Path.Combine(zoom, @"data\logs")) }, new[] { "Zoom" });
        }
    }

    /// <summary>Steam's folder from its own registry setting, else the usual place; null when Steam isn't there.</summary>
    private static string? SteamFolder()
    {
        string? path = null;
        try
        {
            // CurrentUser is null off Windows (the tests).
            using var key = Microsoft.Win32.Registry.CurrentUser?.OpenSubKey(@"Software\Valve\Steam");
            path = (key?.GetValue("SteamPath") as string)?.Replace('/', '\\');
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException or PlatformNotSupportedException) { }
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) path = ProgramFilesX86("Steam");
        return File.Exists(Path.Combine(path, "steam.exe")) ? path : null;
    }

    /// <summary>The Downloads folder, wherever the user moved it.</summary>
    private static string DownloadsFolder()
    {
        try
        {
            if (SHGetKnownFolderPath(new Guid("374DE290-123F-4565-9164-39C4925E467B"), 0, IntPtr.Zero, out var path) == 0 && Path.IsPathFullyQualified(path))
                return path;
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPStruct)] Guid id,
        uint flags, IntPtr token, out string path);

    /// <summary>The kinds of files the Downloads items sort by, by extension.</summary>
    internal static readonly IReadOnlyDictionary<string, string[]> DownloadKinds = new Dictionary<string, string[]>
    {
        ["Programs"] = new[] { ".appx", ".appxbundle", ".bat", ".cmd", ".exe", ".jar", ".msi", ".msix", ".msixbundle", ".ps1", ".vbs" },
        ["Compressed Files"] = new[] { ".7z", ".bz2", ".cab", ".gz", ".img", ".iso", ".rar", ".tar", ".tgz", ".xz", ".zip", ".zst" },
        ["Pictures"] = new[] { ".avif", ".bmp", ".cr2", ".dng", ".gif", ".heic", ".heif", ".ico", ".jfif", ".jpeg", ".jpg", ".nef", ".png", ".svg", ".tif", ".tiff", ".webp" },
        ["Audio and Video"] = new[] { ".3gp", ".aac", ".avi", ".flac", ".flv", ".m4a", ".m4v", ".mkv", ".mov", ".mp3", ".mp4", ".mpeg", ".mpg", ".ogg", ".opus", ".wav", ".webm", ".wma", ".wmv" },
        ["Documents"] = new[] { ".csv", ".doc", ".docx", ".epub", ".md", ".odp", ".ods", ".odt", ".oxps", ".pdf", ".ppt", ".pptx", ".rtf", ".txt", ".xls", ".xlsx", ".xps" },
    };

    // Downloads still in progress, and the folder's own settings file, are never offered.
    private static readonly HashSet<string> Unfinished = new(StringComparer.OrdinalIgnoreCase) { ".crdownload", ".download", ".opdownload", ".part", ".partial", ".tmp" };

    /// <summary>Which Downloads item a file belongs to: a kind above, "Other Files", or null for a file that is never offered.</summary>
    internal static string? DownloadKind(string file)
    {
        var name = Path.GetFileName(file);
        if (name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) return null;
        var ext = Path.GetExtension(name);
        if (Unfinished.Contains(ext)) return null;
        foreach (var (kind, list) in DownloadKinds)
            if (list.Contains(ext, StringComparer.OrdinalIgnoreCase)) return kind;
        return "Other Files";
    }

    /// <summary>
    /// The files in the Downloads folder by kind (Ted, 2026-10-12). Only files directly in Downloads, never its subfolders, so programs
    /// unpacked there keep working. These remove files people may still want, so each one asks first.
    /// </summary>
    private static IEnumerable<CleanTarget> DownloadsTargets()
    {
        var folder = DownloadsFolder();
        foreach (var kind in DownloadKinds.Keys.Append("Other Files"))
        {
            var k = kind;
            yield return new CleanTarget
            {
                Id = "downloads." + new string(k.ToLowerInvariant().Where(char.IsLetter).ToArray()), Group = AdvancedGroup, Advanced = true,
                Name = $"Downloads ({k})",
                Description = k == "Other Files"
                    ? "Every other file directly in your Downloads folder. Subfolders and unfinished downloads are left alone."
                    : $"{k} directly in your Downloads folder. Subfolders and unfinished downloads are left alone.",
                Warning = "These are files you downloaded yourself and they don't go to the Recycle Bin. Look through your Downloads folder first and move anything you want to keep.",
                Rules = new[] { new CleanRule(folder, "*", Recursive: false, Filter: f => DownloadKind(f) == k) },
            };
        }
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
        yield return new CleanTarget
        {
            Id = "adv.etl", Group = g, Advanced = true, RequiresAdmin = true,
            Name = "Windows Event Trace Logs",
            Description = "Trace logs (.etl) more than 7 days old that Windows' diagnostics and background services write. Logs in use are skipped.",
            Rules = new[]
            {
                new CleanRule(Path.Combine(Environment.SystemDirectory, @"LogFiles\WMI"), "*.etl", MinAge: OneWeek),
                new CleanRule(ProgramData(@"Microsoft\Diagnosis\ETLLogs"), "*.etl", MinAge: OneWeek),
            },
        };
        yield return new CleanTarget
        {
            Id = "adv.driverlogs", Group = g, Advanced = true, RequiresAdmin = true,
            Name = "Driver Installation Log Files",
            Description = @"The logs Windows writes while installing drivers and devices (setupapi logs in C:\Windows\INF). Windows starts new ones.",
            Rules = new[] { new CleanRule(WinDir("INF"), "setupapi*.log", Recursive: false) },
        };
        yield return new CleanTarget
        {
            Id = "adv.iislogs", Group = g, Advanced = true, RequiresAdmin = true,
            Name = "IIS Log Files",
            Description = "Web server logs more than 7 days old from Internet Information Services, if it is turned on.",
            Rules = new[] { new CleanRule(Path.Combine(SystemDrive, @"inetpub\logs\LogFiles"), "*.log", MinAge: OneWeek) },
        };
        yield return new CleanTarget
        {
            Id = "adv.inetcookies", Group = g, Advanced = true,
            Name = "Windows Internet Cookies",
            Description = "Cookies kept by Windows' own web engine and older programs that use it (Internet Explorer's cookies). Those programs may sign you out.",
            Rules = new[] { new CleanRule(Local(@"Microsoft\Windows\INetCookies")) },
        };
        yield return new CleanTarget
        {
            Id = "adv.jumplists", Group = g, Advanced = true,
            Name = "Taskbar Jump Lists",
            Description = "The recent files shown when you right-click a program on the taskbar or in Start.",
            Warning = "Files you pinned in those lists are removed too. The files themselves aren't touched.",
            Rules = new[]
            {
                new CleanRule(Roaming(@"Microsoft\Windows\Recent\AutomaticDestinations"), "*.automaticDestinations-ms", Recursive: false),
                new CleanRule(Roaming(@"Microsoft\Windows\Recent\CustomDestinations"), "*.customDestinations-ms", Recursive: false),
            },
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
            "Deletes older System Restore points, Point-in-Time Restore snapshots and other shadow copies on the Windows drive. The newest one is always kept.",
            admin: true,
            warning: "You won't be able to go back to the deleted restore points or snapshots, or bring back older versions of files from them.");
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
        // History lists kept in the registry: each is backed up to a .reg file first, like the Run box.
        yield return Task("task.explorermru", "Other Explorer Lists",
            "File Explorer's other history: recently opened files, the Open and Save As lists, typed folder paths and searches. It is backed up to a .reg file first.");
        yield return Task("task.typedurls", "Recently Typed URLs", "Addresses typed into Internet Explorer and Windows' address bars. It is backed up to a .reg file first.");
        yield return Task("task.userassist", "User Assist History",
            "Windows' count of which programs you open most, used for Start's Most Used list. It is backed up to a .reg file first.");
        yield return Task("task.menuorder", "Menu Order Cache", "The order you dragged Start and Favorites menu items into on older Windows. It is backed up to a .reg file first.");
        yield return Task("task.shellbags", "Window Size and Location Cache",
            "The size, place and view File Explorer remembers for every folder you opened, including folders that are gone. It is backed up to a .reg file first.",
            warning: "Every folder goes back to the standard view, size and sort order.");
        yield return Task("task.regedit", "Registry Editor Last Key", "The key Registry Editor opens at. It is backed up to a .reg file first.");
        yield return Task("task.7zip", "7-Zip History", "The folders and archives 7-Zip remembers. It is backed up to a .reg file first.");
        yield return Task("task.envpath", "Environment Path",
            "Removes folders that no longer exist from the PATH setting programs are found by (yours, and the system's when running as administrator). It is backed up to a .reg file first. Folders on network or removable drives are kept.",
            warning: "A program that expects one of those folders back later won't be found from a command prompt until it is reinstalled or the backup restored.");
        yield return Task("task.wipefree", "Wipe Free Space",
            "Overwrites the free space on the Windows drive with Windows' own cipher /w, so deleted files can't be recovered. Your files aren't touched. This can take hours.",
            admin: true,
            warning: "The drive fills up while it runs, so other programs may report low space until it finishes. On an SSD it adds wear and isn't needed, because the drive already erases deleted data itself.");
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
