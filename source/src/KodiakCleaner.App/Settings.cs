using System.IO;
using KodiakCleaner.Core.Cleaning;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App;

/// <summary>Light or dark: Match Device (default), Light or Dark.</summary>
public enum AppTheme
{
    System,
    Light,
    Dark,
}

/// <summary>Color scheme. Honey is the default and free; Classic, Glacier and Lagoon are Supporter themes.</summary>
public enum Palette
{
    Honey,
    /// <summary>Neutral greys and blue, close to the first versions. Free.</summary>
    Classic,
    Glacier,
    Lagoon,
}

/// <summary>Developer page only: preview the app as a Free or Supporter user without a code.</summary>
public enum PlanPreview
{
    None,
    Free,
    Supporter,
}

/// <summary>
/// All preferences, stored in KodiakCleaner.ini next to KodiakCleaner.exe. The MSI grants normal users write access to
/// that one file, so the program files themselves stay protected. Backups and logs live in <see cref="Data"/>.
/// </summary>
public sealed class Settings
{
    public static Settings Current { get; private set; } = new();

    public static string Folder => AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    public static string FilePath => Path.Combine(Folder, "KodiakCleaner.ini");

    /// <summary>
    /// Backups and logs: Documents\Kodiak Cleaner for an installed copy (AppData\Local\Kodiak Cleaner if Documents can't be
    /// written), the folder beside KodiakCleaner.exe for a portable copy (Ted, 0.8.36).
    /// </summary>
    public static DataFolders Data { get; private set; } = new(Folder);

    /// <summary>Picks <see cref="Data"/>, creates its folders and moves backups from the old single Backups folder into them.</summary>
    public static void PrepareDataFolders()
    {
        Data = PickDataFolders();
        ActivityLog.Folder = Data.Logs;
        ActivityLog.Enabled = Current.KeepActivityLog;
        foreach (var dir in Data.All)
        {
            try { Directory.CreateDirectory(dir); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        int moved = 0;
        try { moved = Data.MoveFromOldLayout(Path.Combine(Folder, "Backups")); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        if (moved > 0) ActivityLog.Write("Backups", $"Moved {moved} backup file(s) from {Path.Combine(Folder, "Backups")} to {Data.Backups}");
    }

    private static DataFolders PickDataFolders()
    {
        if (!Integration.IsInstalled) return new DataFolders(Folder);
        foreach (var place in new[] { Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.LocalApplicationData })
        {
            var root = Environment.GetFolderPath(place);
            if (root.Length == 0) continue;
            var data = DataFolders.Installed(root);
            try
            {
                // Controlled folder access in Windows Security can block Documents; then AppData\Local is used.
                Directory.CreateDirectory(data.Root);
                var probe = Path.Combine(data.Root, Path.GetRandomFileName());
                File.WriteAllText(probe, "");
                File.Delete(probe);
                return data;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return new DataFolders(Folder);
    }

    /// <summary>Set when KodiakCleaner.ini could not be written; the Settings page shows a warning.</summary>
    public static string? SaveError { get; private set; }

    // [General]
    public bool CleanOnStartup { get; set; }
    public bool RecycleBinMenu { get; set; }
    public bool ShowWhatsNewAfterUpdate { get; set; } = true;
    /// <summary>Pop up when new startup programs appear, with Keep, Disable or Delete for each (Ted, 0.8.53).</summary>
    public bool NotifyNewStartup { get; set; } = true;
    /// <summary>How many times the window has opened (command-line and scheduled runs don't count). Kept by Reset.</summary>
    public int LaunchCount { get; set; }
    /// <summary>The donation pop-up at startup (Ted, 0.8.55). It can only be turned off after <see cref="Donation.LaunchesBeforeOptOut"/> launches.</summary>
    public bool ShowDonationPrompt { get; set; } = true;
    public string? LastSeenVersion { get; set; }
    /// <summary>Whether the Tools group in the sidebar is open. Open by default since 0.8.11 (Ted), saved under a new key
    /// so copies that saved the old closed default open too.</summary>
    public bool ToolsExpanded { get; set; } = true;
    /// <summary>Sidebar: default width 310; the paperclip locks it there, unlocked it can be dragged (0.8.11).</summary>
    public const int DefaultSidebarWidth = 310, MinSidebarWidth = 240, MaxSidebarWidth = 480;
    public int SidebarWidth { get; set; } = DefaultSidebarWidth;
    public bool SidebarLocked { get; set; } = true;
    public bool SidebarCollapsed { get; set; }
    /// <summary>
    /// Where the page dividers were dragged to (Ted, 0.8.99): "startup=420|260;duplicates=310|900", each the two areas' sizes.
    /// Empty until a divider is moved; Reset to Default empties it.
    /// </summary>
    public string PaneSizes { get; set; } = "";
    /// <summary>Off: the sidebar always keeps the default width and the paperclip is hidden (Settings › General, 0.8.12).</summary>
    public bool SidebarAdjustable { get; set; } = true;
    /// <summary>Off hides the paperclip button; the sidebar keeps whatever width and lock it had.</summary>
    public bool ShowSidebarLock { get; set; } = true;
    /// <summary>Sidebar Favorites (Ted, 2026-10-23): page keys in the order they show; null until changed, which means
    /// <see cref="KodiakCleaner.Core.Util.SidebarFavorites.Default"/>. Reset to Default puts the default back.</summary>
    public List<string>? Favorites { get; set; }
    /// <summary>Settings › General › Favorites Order: A to Z instead of the order set by dragging or Move Up and Move Down.</summary>
    public bool FavoritesAToZ { get; set; }
    /// <summary>Tools in the order set by dragging (Ted, 2026-10-23); null until changed, which means A to Z. Reset Tools Order clears it.</summary>
    public List<string>? ToolsOrder { get; set; }
    /// <summary>Settings' Sort By dropdown: A to Z instead of the Default order (General, then the most used).</summary>
    public bool SettingsAToZ { get; set; }
    public bool ShowBetaNotice { get; set; } = true;

    // [Appearance]
    public AppTheme Theme { get; set; } = AppTheme.System;
    public Palette Palette { get; set; } = Palette.Honey;
    /// <summary>Optional accent color name (Windows, Blue, ...); "" keeps the color scheme's own accent.</summary>
    /// <summary>Logo dance and moving busy bars; off stops every animation (Ted, 0.8.10).</summary>
    public bool Animations { get; set; } = true;
    public string Accent { get; set; } = "";

    // [Cleaner]
    public List<string>? CheckedTargets { get; set; }
    /// <summary>Extensions Kodiak Cleaner asked a browser to remove ("Browser|id"), until the browser has done it (0.8.65).</summary>
    public List<string> RemovedExtensions { get; set; } = new();
    public bool ConfirmBeforeCleaning { get; set; } = true;
    /// <summary>How the Cleaner list is ordered (0.8.21).</summary>
    public CleanSort CleanerSort { get; set; } = CleanSort.Default;
    /// <summary>When each item was last cleaned and how much it freed ([Cleaner History]).</summary>
    public CleanerHistory History { get; set; } = new();
    /// <summary>Scheduled cleanings ([Schedule id] sections), started by Windows Task Scheduler.</summary>
    public List<CleaningSchedule> Schedules { get; set; } = new();

    // [Updates]
    /// <summary>Check for updates on the schedule below and say when one is available (Ted, 0.8.51: checking alone never installs).</summary>
    public bool AutoUpdate { get; set; }
    /// <summary>Also download a found update and install it when Kodiak Cleaner closes (Ted, 0.8.51). Only used while AutoUpdate is on.</summary>
    public bool AutoInstall { get; set; }
    /// <summary>How often automatic updates check (Ted, 0.8.44); On Startup was the only behavior in 0.8.43.</summary>
    public KodiakCleaner.Core.Updates.UpdateInterval UpdateInterval { get; set; } = KodiakCleaner.Core.Updates.UpdateInterval.OnStartup;
    public DateTime? LastUpdateCheckUtc { get; set; }

    // [System Information]
    /// <summary>The format Export Report saves in (Ted, 0.8.46): Text, HTML or CSV, remembered between runs.</summary>
    public KodiakCleaner.Core.Tools.ReportFormat ReportFormat { get; set; } = KodiakCleaner.Core.Tools.ReportFormat.Html;

    // [Duplicate Finder]
    /// <summary>Remove Selected moves duplicates to <see cref="DuplicatesMoveFolder"/> instead of the Recycle Bin (Ted, 2026-10-23).</summary>
    public bool DuplicatesMoveToFolder { get; set; }
    /// <summary>Where moved duplicates go; "" means the user folder's Kodiak Cleaner Duplicates.</summary>
    public string DuplicatesMoveFolder { get; set; } = "";

    // [Mass Rename]
    /// <summary>The rules from the last rename, so a pattern used before is ready again (Ted, 2026-10-12).</summary>
    public KodiakCleaner.Core.Tools.RenameRules MassRename { get; set; } = new();

    // [Advanced]
    public bool TempOlderThan24h { get; set; } = true;
    public bool RecycleBinOlderThan24h { get; set; }
    public bool PromptRegistryBackup { get; set; } = true;
    public bool MinimizeToTray { get; set; }
    public bool CloseToTray { get; set; }
    public bool SkipUac { get; set; }
    public bool AlwaysAdmin { get; set; }
    public bool JumpList { get; set; } = true;
    /// <summary>Writes the monthly activity log in Logs (0.8.36).</summary>
    public bool KeepActivityLog { get; set; } = true;

    // [Supporter]
    public string SupporterCode { get; set; } = "";
    public bool ShowSupporterBadge { get; set; } = true;

    // [Developer] (development builds only; release builds ignore these)
    public PlanPreview DevPreview { get; set; }
    public string DevPreviewName { get; set; } = "";

    public TimeSpan? TempMinAge => TempOlderThan24h ? TimeSpan.FromHours(24) : null;
    public TimeSpan? RecycleBinMinAge => RecycleBinOlderThan24h ? TimeSpan.FromHours(24) : null;

    public static void Load()
    {
        var s = new Settings();
        try
        {
            if (File.Exists(FilePath)) s.Read(IniFile.Parse(File.ReadAllText(FilePath)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        Current = s;
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(FilePath, Write().Serialize(
                "Kodiak Cleaner settings. Edit with Kodiak Cleaner closed; unknown or invalid values fall back to defaults."));
            SaveError = null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            SaveError = $"Settings can't be saved because {FilePath} is read-only ({e.Message}). " +
                        "Reinstall Kodiak Cleaner, or move the portable version to a folder you can write to.";
        }
    }

    /// <summary>
    /// Restores every preference to its default but keeps the update bookkeeping and the supporter code. The launch count starts
    /// again, so the donation reminder is back until 10 more starts (Ted, 0.8.60); Supporters still never see it.
    /// </summary>
    public static void Reset()
    {
        Current = new Settings
        {
            LastSeenVersion = Current.LastSeenVersion,
            LastUpdateCheckUtc = Current.LastUpdateCheckUtc,
            SupporterCode = Current.SupporterCode,
            // Kept: the schedules have Windows tasks that point at them, and the history is a record, not a preference.
            History = Current.History,
            Schedules = Current.Schedules,
        };
        Current.Save();
    }

    private void Read(IniFile ini)
    {
        CleanOnStartup = ini.GetBool("General", "CleanOnStartup", CleanOnStartup);
        RecycleBinMenu = ini.GetBool("General", "RecycleBinMenu", RecycleBinMenu);
        ShowWhatsNewAfterUpdate = ini.GetBool("General", "ShowWhatsNewAfterUpdate", ShowWhatsNewAfterUpdate);
        NotifyNewStartup = ini.GetBool("General", "NotifyNewStartup", NotifyNewStartup);
        LaunchCount = Math.Max(0, ini.GetInt("General", "LaunchCount", LaunchCount));
        ShowDonationPrompt = ini.GetBool("General", "ShowDonationPrompt", ShowDonationPrompt);
        LastSeenVersion = ini.Get("General", "LastSeenVersion");
        ToolsExpanded = ini.GetBool("General", "ToolsOpen", ToolsExpanded);
        SidebarWidth = Math.Clamp(ini.GetInt("General", "SidebarWidth", SidebarWidth), MinSidebarWidth, MaxSidebarWidth);
        PaneSizes = ini.Get("Layout", "Panes") ?? "";
        SidebarLocked = ini.GetBool("General", "SidebarLocked", SidebarLocked);
        SidebarCollapsed = ini.GetBool("General", "SidebarCollapsed", SidebarCollapsed);
        SidebarAdjustable = ini.GetBool("General", "SidebarAdjustable", SidebarAdjustable);
        ShowSidebarLock = ini.GetBool("General", "ShowSidebarLock", ShowSidebarLock);
        Favorites = ini.Get("General", "Favorites")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        FavoritesAToZ = string.Equals(ini.Get("General", "FavoritesOrder"), "AToZ", StringComparison.OrdinalIgnoreCase);
        ToolsOrder = ini.Get("General", "ToolsOrder")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        SettingsAToZ = string.Equals(ini.Get("General", "SettingsSort"), "AToZ", StringComparison.OrdinalIgnoreCase);
        ShowBetaNotice = ini.GetBool("General", "ShowBetaNotice", ShowBetaNotice);

        Theme = Enum.TryParse<AppTheme>(ini.Get("Appearance", "Theme"), ignoreCase: true, out var t) ? t : Theme;
        Palette = Enum.TryParse<Palette>(ini.Get("Appearance", "Palette"), ignoreCase: true, out var pal) ? pal : Palette;
        Accent = AccentColors.Migrate(ini.Get("Appearance", "Accent") ?? Accent);
        Animations = ini.GetBool("Appearance", "Animations", Animations);

        RemovedExtensions = (ini.Get("Extensions", "Removed") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var targets = ini.Get("Cleaner", "CheckedTargets");
        CheckedTargets = targets is null ? null : targets.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        ConfirmBeforeCleaning = ini.GetBool("Cleaner", "ConfirmBeforeCleaning", ConfirmBeforeCleaning);
        CleanerSort = Enum.TryParse<CleanSort>(ini.Get("Cleaner", "SortBy"), ignoreCase: true, out var sort) ? sort : CleanerSort;
        History = CleanerHistory.Read(ini);
        Schedules = CleaningSchedule.ReadAll(ini).ToList();

        AutoUpdate = ini.GetBool("Updates", "AutoUpdate", AutoUpdate);
        // Before 0.8.51 automatic updates always downloaded and installed, so anyone who had them on keeps that.
        AutoInstall = ini.GetBool("Updates", "AutoInstall", AutoUpdate);
        UpdateInterval = Enum.TryParse<KodiakCleaner.Core.Updates.UpdateInterval>(ini.Get("Updates", "Interval"), ignoreCase: true, out var every) ? every : UpdateInterval;
        LastUpdateCheckUtc = DateTime.TryParse(ini.Get("Updates", "LastCheckUtc"), System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var d) ? d : null;

        ReportFormat = Enum.TryParse<KodiakCleaner.Core.Tools.ReportFormat>(ini.Get("System Information", "ReportFormat"), ignoreCase: true, out var format) ? format : ReportFormat;
        DuplicatesMoveToFolder = ini.GetBool("Duplicate Finder", "MoveToFolder", DuplicatesMoveToFolder);
        DuplicatesMoveFolder = ini.GetString("Duplicate Finder", "MoveFolder", DuplicatesMoveFolder);
        MassRename = ReadMassRename(ini);

        TempOlderThan24h = ini.GetBool("Advanced", "TempOlderThan24h", TempOlderThan24h);
        RecycleBinOlderThan24h = ini.GetBool("Advanced", "RecycleBinOlderThan24h", RecycleBinOlderThan24h);
        PromptRegistryBackup = ini.GetBool("Advanced", "PromptRegistryBackup", PromptRegistryBackup);
        MinimizeToTray = ini.GetBool("Advanced", "MinimizeToTray", MinimizeToTray);
        CloseToTray = ini.GetBool("Advanced", "CloseToTray", CloseToTray);
        SkipUac = ini.GetBool("Advanced", "SkipUac", SkipUac);
        AlwaysAdmin = ini.GetBool("Advanced", "AlwaysAdmin", AlwaysAdmin);
        JumpList = ini.GetBool("Advanced", "JumpList", JumpList);
        KeepActivityLog = ini.GetBool("Advanced", "KeepActivityLog", KeepActivityLog);

        SupporterCode = ini.GetString("Supporter", "Code", SupporterCode);
        ShowSupporterBadge = ini.GetBool("Supporter", "ShowBadge", ShowSupporterBadge);

        if (AppInfo.HasDeveloperTools)
        {
            DevPreview = Enum.TryParse<PlanPreview>(ini.Get("Developer", "Preview"), ignoreCase: true, out var pv) ? pv : DevPreview;
            DevPreviewName = ini.GetString("Developer", "PreviewName", DevPreviewName);
        }
    }

    private IniFile Write()
    {
        var ini = new IniFile();
        ini.Set("General", "CleanOnStartup", CleanOnStartup);
        ini.Set("General", "RecycleBinMenu", RecycleBinMenu);
        ini.Set("General", "ShowWhatsNewAfterUpdate", ShowWhatsNewAfterUpdate);
        ini.Set("General", "NotifyNewStartup", NotifyNewStartup);
        ini.Set("General", "LaunchCount", LaunchCount);
        ini.Set("General", "ShowDonationPrompt", ShowDonationPrompt);
        ini.Set("General", "LastSeenVersion", LastSeenVersion ?? "");
        ini.Set("General", "ToolsOpen", ToolsExpanded);
        ini.Set("General", "SidebarWidth", SidebarWidth);
        ini.Set("Layout", "Panes", PaneSizes);
        ini.Set("General", "SidebarLocked", SidebarLocked);
        ini.Set("General", "SidebarCollapsed", SidebarCollapsed);
        ini.Set("General", "SidebarAdjustable", SidebarAdjustable);
        ini.Set("General", "ShowSidebarLock", ShowSidebarLock);
        if (Favorites is not null) ini.Set("General", "Favorites", string.Join(",", Favorites));
        ini.Set("General", "FavoritesOrder", FavoritesAToZ ? "AToZ" : "Default");
        if (ToolsOrder is not null) ini.Set("General", "ToolsOrder", string.Join(",", ToolsOrder));
        ini.Set("General", "SettingsSort", SettingsAToZ ? "AToZ" : "Default");
        ini.Set("General", "ShowBetaNotice", ShowBetaNotice);

        ini.Set("Appearance", "Theme", Theme.ToString());
        ini.Set("Appearance", "Palette", Palette.ToString());
        ini.Set("Appearance", "Accent", Accent);
        ini.Set("Appearance", "Animations", Animations);

        if (CheckedTargets is not null) ini.Set("Cleaner", "CheckedTargets", string.Join(",", CheckedTargets));
        if (RemovedExtensions.Count > 0) ini.Set("Extensions", "Removed", string.Join(",", RemovedExtensions));
        ini.Set("Cleaner", "ConfirmBeforeCleaning", ConfirmBeforeCleaning);
        ini.Set("Cleaner", "SortBy", CleanerSort.ToString()); // "Sort" (0.8.21) meant A to Z for everyone, so it isn't read

        ini.Set("Updates", "AutoUpdate", AutoUpdate);
        ini.Set("Updates", "AutoInstall", AutoInstall);
        ini.Set("Updates", "Interval", UpdateInterval.ToString());
        ini.Set("Updates", "LastCheckUtc", LastUpdateCheckUtc?.ToString("o") ?? "");

        ini.Set("System Information", "ReportFormat", ReportFormat.ToString());
        ini.Set("Duplicate Finder", "MoveToFolder", DuplicatesMoveToFolder);
        if (DuplicatesMoveFolder.Length > 0) ini.Set("Duplicate Finder", "MoveFolder", DuplicatesMoveFolder);
        WriteMassRename(ini, MassRename);

        ini.Set("Advanced", "TempOlderThan24h", TempOlderThan24h);
        ini.Set("Advanced", "RecycleBinOlderThan24h", RecycleBinOlderThan24h);
        ini.Set("Advanced", "PromptRegistryBackup", PromptRegistryBackup);
        ini.Set("Advanced", "MinimizeToTray", MinimizeToTray);
        ini.Set("Advanced", "CloseToTray", CloseToTray);
        ini.Set("Advanced", "SkipUac", SkipUac);
        ini.Set("Advanced", "AlwaysAdmin", AlwaysAdmin);
        ini.Set("Advanced", "JumpList", JumpList);
        ini.Set("Advanced", "KeepActivityLog", KeepActivityLog);

        ini.Set("Supporter", "Code", SupporterCode);
        ini.Set("Supporter", "ShowBadge", ShowSupporterBadge);

        History.Write(ini);
        foreach (var schedule in Schedules) schedule.Write(ini);

        if (AppInfo.HasDeveloperTools && (DevPreview != PlanPreview.None || DevPreviewName.Length > 0))
        {
            ini.Set("Developer", "Preview", DevPreview.ToString());
            ini.Set("Developer", "PreviewName", DevPreviewName);
        }
        return ini;
    }

    private static KodiakCleaner.Core.Tools.RenameRules ReadMassRename(IniFile ini)
    {
        const string S = "Mass Rename";
        var r = new KodiakCleaner.Core.Tools.RenameRules();
        r.Pattern = Unquote(ini.Get(S, "Pattern")) ?? r.Pattern;
        r.Find = Unquote(ini.Get(S, "Find")) ?? r.Find;
        r.Replace = Unquote(ini.Get(S, "Replace")) ?? r.Replace;
        r.MatchCase = ini.GetBool(S, "MatchCase", r.MatchCase);
        r.UseRegex = ini.GetBool(S, "RegularExpression", r.UseRegex);
        r.Case = Enum.TryParse<KodiakCleaner.Core.Tools.RenameCase>(ini.Get(S, "Case"), true, out var c) ? c : r.Case;
        r.Extension = Enum.TryParse<KodiakCleaner.Core.Tools.RenameExtension>(ini.Get(S, "Extension"), true, out var e) ? e : r.Extension;
        r.NewExtension = Unquote(ini.Get(S, "NewExtension")) ?? r.NewExtension;
        r.TidySpaces = ini.GetBool(S, "TidySpaces", r.TidySpaces);
        r.Start = ini.GetInt(S, "Start", r.Start);
        r.Step = ini.GetInt(S, "Step", r.Step);
        r.Digits = Math.Clamp(ini.GetInt(S, "Digits", r.Digits), 1, 12);
        r.Order = Enum.TryParse<KodiakCleaner.Core.Tools.RenameOrder>(ini.Get(S, "Order"), true, out var o) ? o : r.Order;
        return r;
    }

    private static void WriteMassRename(IniFile ini, KodiakCleaner.Core.Tools.RenameRules r)
    {
        const string S = "Mass Rename";
        ini.Set(S, "Pattern", Quote(r.Pattern));
        ini.Set(S, "Find", Quote(r.Find));
        ini.Set(S, "Replace", Quote(r.Replace));
        ini.Set(S, "MatchCase", r.MatchCase);
        ini.Set(S, "RegularExpression", r.UseRegex);
        ini.Set(S, "Case", r.Case.ToString());
        ini.Set(S, "Extension", r.Extension.ToString());
        ini.Set(S, "NewExtension", Quote(r.NewExtension));
        ini.Set(S, "TidySpaces", r.TidySpaces);
        ini.Set(S, "Start", r.Start);
        ini.Set(S, "Step", r.Step);
        ini.Set(S, "Digits", r.Digits);
        ini.Set(S, "Order", r.Order.ToString());
    }

    // Quoted so a Find or Replace of spaces survives (the ini trims values).
    private static string Quote(string value) => "\"" + value + "\"";
    private static string? Unquote(string? value) =>
        value is { Length: >= 2 } && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;
}
