using System.Collections.ObjectModel;
using System.Windows.Input;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    public MainViewModel()
    {
        Cleaner = new CleanerViewModel();
        Registry = new RegistryViewModel();
        Startup = new StartupViewModel();
        Programs = new ProgramsViewModel();
        Duplicates = new DuplicatesViewModel();
        DiskAnalyzer = new DiskAnalyzerViewModel();
        DriverCleaner = new DriverCleanerViewModel();
        EmptyFolders = new EmptyFoldersViewModel();
        HashChecker = new HashCheckerViewModel();
        Integrity = new IntegrityCheckViewModel();
        LargeFiles = new LargeFilesViewModel();
        MassRename = new MassRenameViewModel();
        ScheduledTasks = new ScheduledTasksViewModel();
        SystemInfo = new SystemInfoViewModel();
        Extensions = new ExtensionsViewModel();
        Restore = new RestoreViewModel();
        Wiper = new WiperViewModel();
        BootTimer = new BootTimerViewModel(Startup, Navigate);
        Optimize = new OptimizeViewModel();
        SettingsPage = new SettingsViewModel(Cleaner.ReloadTargets);
        // Entering a Supporter code turns the donation reminder off and hides the banner.
        Supporter.Changed += () =>
        {
            if (!Supporter.IsSupporter) return;
            Donation.SupporterTurnsPromptOff();
            ShowDonationBanner = false;
        };

        // Glyphs from Segoe Fluent Icons / Segoe MDL2 Assets. Every page here can be pinned to Favorites (Ted, 2026-10-23);
        // the rest show under Tools, A to Z (Ted's choice). The key is the page's --page= name.
        Pinnable.Add(new NavPage("Cleaner", "\uE74D", Cleaner, OnNavigated, key: "cleaner"));
        Pinnable.Add(new NavPage("Registry", "\uE943", Registry, OnNavigated, key: "registry"));
        Pinnable.Add(new NavPage("Startup", "\uE7E8", Startup, OnNavigated, key: "startup"));
        Pinnable.Add(new NavPage("Programs", "\uE71D", Programs, OnNavigated, key: "programs"));
        Pinnable.Add(new NavPage("Boot Timer", "\uE916", BootTimer, OnNavigated, key: "boottimer"));
        Pinnable.Add(new NavPage("Browser Extensions", "\uEA86", Extensions, OnNavigated, key: "extensions"));
        Pinnable.Add(new NavPage("Defrag and TRIM", "\uEC4A", Optimize, OnNavigated, key: "optimize"));
        Pinnable.Add(new NavPage("Disk Analyzer", "\uEB05", DiskAnalyzer, OnNavigated, key: "disk"));
        Pinnable.Add(new NavPage("Drive Wiper", "\uEDA2", Wiper, OnNavigated, badge: "\uE74D", key: "wiper"));
        Pinnable.Add(new NavPage("Driver Cleaner", "\uE950", DriverCleaner, OnNavigated, key: "drivers"));
        Pinnable.Add(new NavPage("Duplicate Finder", "\uE8C8", Duplicates, OnNavigated, key: "duplicates"));
        Pinnable.Add(new NavPage("Empty Folders", "\uE8B7", EmptyFolders, OnNavigated, key: "emptyfolders"));
        Pinnable.Add(new NavPage("Hash Checker", "\uE928", HashChecker, OnNavigated, key: "hash"));
        Pinnable.Add(new NavPage("Integrity Check", "\uE9D9", Integrity, OnNavigated, key: "integrity"));
        Pinnable.Add(new NavPage("Large Files", "\uE8A5", LargeFiles, OnNavigated, key: "largefiles"));
        Pinnable.Add(new NavPage("Mass Rename", "\uE8AC", MassRename, OnNavigated, key: "rename"));
        Pinnable.Add(new NavPage("Scheduled Tasks", "\uE823", ScheduledTasks, OnNavigated, key: "tasks"));
        Pinnable.Add(new NavPage("System Restore", "\uE777", Restore, OnNavigated, key: "restore"));
        // Settings › Tools lists every one of them with a switch that hides it from the sidebar (Ted, 0.9.1 Beta).
        SettingsPage.SetTools(Pinnable.Select(p => new ToolChoice(p.Key, p.Title, p.Glyph, p.Badge)));
        ArrangeSidebar();
        FooterPages.Add(new NavPage("Settings", "", SettingsPage, OnNavigated));
        // System Information sits with Settings and About (Ted, 0.8.46), not in Tools.
        FooterPages.Add(new NavPage("System Information", "\uE7F4", SystemInfo, OnNavigated));
        FooterPages.Add(new NavPage("About", "\uE946", SettingsPage.About, OnNavigated));
        ToggleSidebarCommand = new RelayCommand(() => SidebarCollapsed = !SidebarCollapsed);
        ToggleSidebarLockCommand = new RelayCommand(() => SidebarLocked = !SidebarLocked);
        SidebarSettingsChanged += () =>
        {
            OnPropertyChanged(nameof(ShowPaperclip));
            OnPropertyChanged(nameof(SidebarCollapsed));
            OnPropertyChanged(nameof(ToolsExpanded));
            OnPropertyChanged(nameof(ToolsVisible));
            OnPropertyChanged(nameof(ShowResetToolsOrder));
            OnPropertyChanged(nameof(SidebarLocked)); // re-sizes the sidebar (MainWindow.ApplySidebar)
            ArrangeSidebar(); // Favorites Order, Restore Default Order and Reset to Default
        };
        SettingsPage.ShowRequested += () => FooterPages[0].IsSelected = true;

        RestartAsAdminCommand = new RelayCommand(() => Elevation.RestartElevated(Elevation.CurrentPage), () => !IsAdministrator);
        HomeCommand = new RelayCommand(OpenHome);
        OpenToolsSettingsCommand = new RelayCommand(() => Navigate("tools"));
        OpenHome();

        BackgroundTasks.Changed += UpdateCornerTask;
        _cornerDelay.Tick += (_, _) => { _cornerDelay.Stop(); UpdateCornerTask(); };
        OpenCornerTaskCommand = new RelayCommand(() =>
        {
            var page = AllPages.FirstOrDefault(p => p.Content == CornerTask?.Owner);
            if (page is not null) page.IsSelected = true;
        });
        HideCornerTaskCommand = new RelayCommand(() =>
        {
            foreach (var t in BackgroundTasks.Running) _hidden.Add(t);
            UpdateCornerTask();
        });

        Supporter.Changed += () =>
        {
            OnPropertyChanged(nameof(ShowBadge));
            OnPropertyChanged(nameof(BadgeText));
        };
    }

    public CleanerViewModel Cleaner { get; }
    public RegistryViewModel Registry { get; }
    public StartupViewModel Startup { get; }
    public ProgramsViewModel Programs { get; }
    public DuplicatesViewModel Duplicates { get; }
    public DiskAnalyzerViewModel DiskAnalyzer { get; }
    public DriverCleanerViewModel DriverCleaner { get; }
    public EmptyFoldersViewModel EmptyFolders { get; }
    public HashCheckerViewModel HashChecker { get; }
    public IntegrityCheckViewModel Integrity { get; }
    public LargeFilesViewModel LargeFiles { get; }
    public MassRenameViewModel MassRename { get; }
    public ScheduledTasksViewModel ScheduledTasks { get; }
    public SystemInfoViewModel SystemInfo { get; }
    public ExtensionsViewModel Extensions { get; }
    public RestoreViewModel Restore { get; }
    public WiperViewModel Wiper { get; }
    public BootTimerViewModel BootTimer { get; }
    public OptimizeViewModel Optimize { get; }
    public SettingsViewModel SettingsPage { get; }

    /// <summary>Every page that can sit in Favorites or Tools.</summary>
    private List<NavPage> Pinnable { get; } = new();
    /// <summary>Pinned at the top of the sidebar; they don't scroll (Ted, 2026-10-23).</summary>
    public ObservableCollection<NavPage> FavoritePages { get; } = new();
    /// <summary>Everything not in Favorites, in the scrolling list: A to Z until tools are dragged into an order of their own.</summary>
    public ObservableCollection<NavPage> ToolPages { get; } = new();
    public ObservableCollection<NavPage> FooterPages { get; } = new();
    private IEnumerable<NavPage> AllPages => Pinnable.Concat(FooterPages);

    /// <summary>With nothing pinned, Favorites says how to pin something (not when collapsed to icons, nor with every tool hidden).</summary>
    public bool ShowFavoritesHint => FavoritePages.Count == 0 && ToolPages.Count > 0 && !SidebarCollapsed;
    /// <summary>Every tool is hidden in Settings › Tools: the sidebar says where to turn them back on.</summary>
    public bool NoToolsShown => FavoritePages.Count == 0 && ToolPages.Count == 0;
    public bool ShowNoToolsHint => NoToolsShown && !SidebarCollapsed;
    public bool FavoritesAToZ => Settings.Current.FavoritesAToZ;
    /// <summary>Reset Tools Order shows under the open Tools list (not when collapsed to icons, nor with every tool hidden).</summary>
    public bool ShowResetToolsOrder => ToolsExpanded && !SidebarCollapsed && !NoToolsShown;

    private List<string> SavedFavorites() =>
        SidebarFavorites.Normalize(Settings.Current.Favorites, Pinnable.Select(p => p.Key).ToList());

    /// <summary>The tools turned off in Settings › Tools.</summary>
    private HashSet<string> HiddenTools() =>
        SidebarFavorites.NormalizeHidden(Settings.Current.HiddenTools, Pinnable.Select(p => p.Key).ToList());

    public bool IsHidden(NavPage page) => page.CanPin && HiddenTools().Contains(page.Key);

    /// <summary>Every page not in <paramref name="favorites"/> in the Tools order, hidden ones included (they keep their place).</summary>
    private List<string> ArrangedTools(ICollection<string> favorites)
    {
        var byKey = Pinnable.ToDictionary(p => p.Key);
        return SidebarFavorites.ArrangeTools(byKey.Keys, favorites, Settings.Current.ToolsOrder, k => byKey[k].Title);
    }

    /// <summary>Fills Favorites and Tools from the saved favorites and the Favorites Order setting, leaving out hidden tools.</summary>
    private void ArrangeSidebar()
    {
        var favorites = SavedFavorites();
        var hidden = HiddenTools();
        var byKey = Pinnable.ToDictionary(p => p.Key);
        Refill(FavoritePages, SidebarFavorites.Arrange(favorites.Where(k => !hidden.Contains(k)), byKey, p => p.Title, Settings.Current.FavoritesAToZ));
        Refill(ToolPages, ArrangedTools(favorites).Where(k => !hidden.Contains(k)).Select(k => byKey[k]).ToList());
        foreach (var p in Pinnable) p.IsFavorite = favorites.Contains(p.Key);
        OnPropertyChanged(nameof(ShowFavoritesHint));
        OnPropertyChanged(nameof(NoToolsShown));
        OnPropertyChanged(nameof(ShowNoToolsHint));
        OnPropertyChanged(nameof(ShowResetToolsOrder));
        OnPropertyChanged(nameof(FavoritesAToZ));
    }

    /// <summary>
    /// The logo, and the page Kodiak Cleaner opens on: the Cleaner, or with the Cleaner hidden the first favorite or tool shown,
    /// or with every tool hidden Settings › Tools, where they're turned back on.
    /// </summary>
    private void OpenHome()
    {
        var hidden = HiddenTools();
        var home = Pinnable.FirstOrDefault(p => p.Key == "cleaner" && !hidden.Contains(p.Key))
                   ?? FavoritePages.FirstOrDefault() ?? ToolPages.FirstOrDefault();
        if (home is not null) home.IsSelected = true;
        else Navigate("tools");
    }

    /// <summary>
    /// Hides a tool from the sidebar or shows it again (Settings › Tools, or right-click › Hide from Sidebar). It stays in the saved
    /// Favorites and Tools order, so it comes back where it was; the jump list follows.
    /// </summary>
    public static void SetToolHidden(string key, bool hide)
    {
        var list = Settings.Current.HiddenTools;
        var had = list.Any(k => k.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (had == hide) return;
        if (hide) list.Add(key);
        else list.RemoveAll(k => k.Equals(key, StringComparison.OrdinalIgnoreCase));
        Settings.Current.Save();
        Integration.RefreshJumpList();
        NotifySidebarSettingsChanged();
    }

    /// <summary>Right-click › Hide from Sidebar: says where it went, so it can be found again.</summary>
    public void Hide(NavPage page)
    {
        SetToolHidden(page.Key, true);
        ShowHiddenNotice(page);
    }

    private string? _hiddenNotice;

    private void ShowHiddenNotice(NavPage page)
    {
        Notice = _hiddenNotice = $"{page.Title} is hidden from the sidebar. Turn it back on in Settings › Tools.";
    }

    /// <summary>Changes the lists in place, so entries that stay keep their place on screen (and their selection).</summary>
    private static void Refill(ObservableCollection<NavPage> list, List<NavPage> wanted)
    {
        for (var i = list.Count - 1; i >= 0; i--)
            if (!wanted.Contains(list[i])) list.RemoveAt(i);
        for (var i = 0; i < wanted.Count; i++)
        {
            var at = list.IndexOf(wanted[i]);
            if (at < 0) list.Insert(i, wanted[i]);
            else if (at != i) list.Move(at, i);
        }
    }

    private void SaveFavorites(List<string> favorites)
    {
        Settings.Current.Favorites = favorites;
        Settings.Current.Save();
        ArrangeSidebar();
    }

    /// <summary>Right-click › Pin to Favorites, or a tool dropped on Favorites (at the place it was dropped).</summary>
    public void Pin(NavPage page, int index = -1)
    {
        // The index is a place among the favorites shown; hidden ones go back where they were.
        var favorites = SavedFavorites();
        var hidden = HiddenTools();
        var shown = favorites.Where(k => !hidden.Contains(k)).ToList();
        if (SidebarFavorites.Pin(shown, page.Key, index)) SaveFavorites(SidebarFavorites.MergeHidden(shown, favorites, hidden));
    }

    /// <summary>Right-click › Unpin from Favorites, or a favorite dropped on Tools (at the place it was dropped, which gives Tools
    /// an order of its own).</summary>
    public void Unpin(NavPage page, int index = -1)
    {
        var favorites = SavedFavorites();
        var before = ArrangedTools(favorites);
        if (!SidebarFavorites.Unpin(favorites, page.Key)) return;
        if (index >= 0)
        {
            var tools = ToolPages.Select(p => p.Key).ToList();
            tools.Insert(Math.Clamp(index, 0, tools.Count), page.Key);
            Settings.Current.ToolsOrder = SidebarFavorites.MergeHidden(tools, before, HiddenTools());
        }
        SaveFavorites(favorites);
    }

    /// <summary>Move Up and Move Down on a tool, or a drag inside Tools (Ted, 2026-10-23): Tools keep that order from then on.</summary>
    public void MoveTool(NavPage page, int index)
    {
        var tools = ToolPages.Select(p => p.Key).ToList();
        if (!SidebarFavorites.Move(tools, page.Key, index)) return;
        Settings.Current.ToolsOrder = SidebarFavorites.MergeHidden(tools, ArrangedTools(SavedFavorites()), HiddenTools());
        Settings.Current.Save();
        ArrangeSidebar();
    }

    public bool CanMoveTool(NavPage page, int by)
    {
        var at = ToolPages.IndexOf(page);
        return at >= 0 && at + by >= 0 && at + by < ToolPages.Count;
    }

    /// <summary>
    /// Reset Tools Order, under the Tools list (Ted, 2026-10-23; 2026-10-24: Favorites too): Favorites go back to the default
    /// (Cleaner, Registry, Startup, Programs, in that order, anything else unpinned), Favorites Order to Default Order, Tools to A to Z.
    /// </summary>
    public ICommand ResetToolsOrderCommand => _resetToolsOrder ??= new RelayCommand(() =>
    {
        if (!Dialogs.Confirm("Put Favorites back to Cleaner, Registry, Startup and Programs, and the tools back in A to Z order? Any other tools you pinned go back to Tools.")) return;
        Settings.Current.Favorites = null;
        Settings.Current.FavoritesAToZ = false;
        Settings.Current.ToolsOrder = null;
        Settings.Current.Save();
        NotifySidebarSettingsChanged(); // the sidebar, and Favorites Order in Settings
    });
    private ICommand? _resetToolsOrder;

    /// <summary>Move Up and Move Down, or a drag inside Favorites. Only in the saved order; A to Z has no order to change.</summary>
    public void MoveFavorite(NavPage page, int index)
    {
        if (Settings.Current.FavoritesAToZ) return;
        var favorites = SavedFavorites();
        var hidden = HiddenTools();
        var shown = favorites.Where(k => !hidden.Contains(k)).ToList();
        if (SidebarFavorites.Move(shown, page.Key, index)) SaveFavorites(SidebarFavorites.MergeHidden(shown, favorites, hidden));
    }

    public bool CanMoveFavorite(NavPage page, int by)
    {
        if (Settings.Current.FavoritesAToZ) return false;
        var at = FavoritePages.IndexOf(page);
        return at >= 0 && at + by >= 0 && at + by < FavoritePages.Count;
    }

    private object? _currentContent;
    public object? CurrentContent { get => _currentContent; private set => Set(ref _currentContent, value); }

    public ICommand RestartAsAdminCommand { get; }
    /// <summary>The logo and name in the sidebar go back to the Cleaner.</summary>
    public ICommand HomeCommand { get; }
    /// <summary>Choose Tools, shown in the sidebar when every tool is hidden: opens Settings › Tools.</summary>
    public ICommand OpenToolsSettingsCommand { get; }

    /// <summary>The Tools group in the sidebar starts open and can be folded away; the choice is remembered in KodiakCleaner.ini.</summary>
    public bool ToolsExpanded
    {
        get => Settings.Current.ToolsExpanded;
        set
        {
            if (Settings.Current.ToolsExpanded == value) return;
            Settings.Current.ToolsExpanded = value;
            Settings.Current.Save();
            OnPropertyChanged();
            OnPropertyChanged(nameof(ToolsVisible));
            OnPropertyChanged(nameof(ShowResetToolsOrder));
        }
    }

    /// <summary>The collapsed sidebar shows every icon, so the tools show even when the group is folded.</summary>
    public bool ToolsVisible => ToolsExpanded || SidebarCollapsed;

    /// <summary>Collapsed: just the logo and the icons. The button at the bottom right of the sidebar switches it.</summary>
    public bool SidebarCollapsed
    {
        get => Settings.Current.SidebarCollapsed;
        set
        {
            if (Settings.Current.SidebarCollapsed == value) return;
            Settings.Current.SidebarCollapsed = value;
            Settings.Current.Save();
            OnPropertyChanged();
            OnPropertyChanged(nameof(ToolsVisible));
            OnPropertyChanged(nameof(ShowResetToolsOrder));
            OnPropertyChanged(nameof(ShowPaperclip));
            OnPropertyChanged(nameof(ShowFavoritesHint));
            OnPropertyChanged(nameof(ShowResetToolsOrder));
        }
    }

    /// <summary>The paperclip: on its side (locked) the sidebar keeps the default width; upright, its edge can be dragged.</summary>
    public bool SidebarLocked
    {
        get => Settings.Current.SidebarLocked;
        set
        {
            if (Settings.Current.SidebarLocked == value) return;
            Settings.Current.SidebarLocked = value;
            if (value) Settings.Current.SidebarWidth = Settings.DefaultSidebarWidth;
            Settings.Current.Save();
            OnPropertyChanged();
        }
    }

    /// <summary>The paperclip shows unless the sidebar is collapsed, fixed (Adjustable Sidebar Width off) or hidden in Settings.</summary>
    public bool ShowPaperclip => !SidebarCollapsed && Settings.Current.SidebarAdjustable && Settings.Current.ShowSidebarLock;

    /// <summary>Raised by Settings › General when Adjustable Sidebar Width or Show the Paperclip changes.</summary>
    public static event Action? SidebarSettingsChanged;
    public static void NotifySidebarSettingsChanged() => SidebarSettingsChanged?.Invoke();

    public ICommand ToggleSidebarCommand { get; }
    public ICommand ToggleSidebarLockCommand { get; }

    public bool IsAdministrator => Elevation.IsAdministrator;

    private AdminNeed? _pageAdminNeed;
    /// <summary>
    /// Running as a standard user: what the open page needs administrator rights for, or runs faster with. Shown at the top
    /// of the page with Restart as Administrator; null (hidden) as administrator and on pages that work the same either way.
    /// </summary>
    public AdminNeed? PageAdminNeed
    {
        get => _pageAdminNeed;
        private set { if (Set(ref _pageAdminNeed, value)) OnPropertyChanged(nameof(HasPageAdminNeed)); }
    }
    public bool HasPageAdminNeed => PageAdminNeed is not null;
    public string VersionName => AppInfo.VersionName;
    public string WindowTitle => AppInfo.Name + (IsAdministrator ? " (Administrator)" : "");
    public bool ShowBadge => Supporter.ShowBadge;
    public string BadgeText => Supporter.BadgeText;

    private string _notice = "";
    /// <summary>One-line message shown under the admin banner (e.g. an update was downloaded).</summary>
    public string Notice { get => _notice; set { Set(ref _notice, value); OnPropertyChanged(nameof(HasNotice)); } }
    public bool HasNotice => !string.IsNullOrEmpty(Notice);

    private bool _showDonationBanner = Donation.ShouldShowBanner;
    /// <summary>The small donation banner on every 10th launch; never for Supporters (Ted, 0.8.55).</summary>
    public bool ShowDonationBanner { get => _showDonationBanner; set => Set(ref _showDonationBanner, value); }

    // ---------- Corner pop-up for work running on another page (Ted, 2026-10-23) ----------

    /// <summary>Tasks this new don't show yet, so a quick read never flashes a pop-up.</summary>
    private static readonly TimeSpan CornerDelay = TimeSpan.FromSeconds(1);
    private readonly System.Windows.Threading.DispatcherTimer _cornerDelay = new() { Interval = CornerDelay };

    private RunningTask? _cornerTask;
    /// <summary>The newest task running on a page that isn't open; null hides the pop-up.</summary>
    public RunningTask? CornerTask
    {
        get => _cornerTask;
        private set
        {
            if (!Set(ref _cornerTask, value)) return;
            OnPropertyChanged(nameof(HasCornerTask));
            OnPropertyChanged(nameof(CornerPageTitle));
            OnPropertyChanged(nameof(CornerPageGlyph));
        }
    }

    public bool HasCornerTask => CornerTask is not null;
    public string CornerPageTitle => PageOf(CornerTask)?.Title ?? "";
    public string CornerPageGlyph => PageOf(CornerTask)?.Glyph is { Length: > 0 } g ? g : "\uE9F5";

    private string _cornerMore = "";
    /// <summary>"1 more task running" when several pages are busy.</summary>
    public string CornerMore { get => _cornerMore; private set { if (Set(ref _cornerMore, value)) OnPropertyChanged(nameof(HasCornerMore)); } }
    public bool HasCornerMore => CornerMore.Length > 0;

    /// <summary>Clicking the pop-up opens the page doing the work.</summary>
    public ICommand OpenCornerTaskCommand { get; }

    /// <summary>The X hides the pop-up for the tasks running now; the next task shows it again.</summary>
    public ICommand HideCornerTaskCommand { get; }
    private readonly HashSet<RunningTask> _hidden = new();

    private NavPage? PageOf(RunningTask? task) => task is null ? null : AllPages.FirstOrDefault(p => p.Content == task.Owner);

    private void UpdateCornerTask()
    {
        var now = DateTime.UtcNow;
        _hidden.IntersectWith(BackgroundTasks.Running);
        var elsewhere = BackgroundTasks.Running.Where(t => t.Owner != CurrentContent && !_hidden.Contains(t)).ToList();
        var shown = elsewhere.Where(t => now - t.Started >= CornerDelay).ToList();
        // A page can run one task inside another (Clean runs Analyze first); the newest says what is happening now.
        CornerTask = shown.LastOrDefault();
        var others = shown.Select(t => t.Owner).Distinct().Count() - 1;
        CornerMore = others <= 0 ? "" : others == 1 ? "1 more task running" : $"{others} more tasks running";
        if (elsewhere.Count > shown.Count && !_cornerDelay.IsEnabled) _cornerDelay.Start();
    }

    public void ShowChangelog()
    {
        Navigate("about");
        SettingsPage.About.ShowHistory = false;
        SettingsPage.About.ShowChangelog = true;
    }

    /// <summary>Each page's short name for --page= (jump lists, the Safe Mode and Boot Timer restarts, Restart as Administrator).</summary>
    private Dictionary<string, object> PageByKey => _pageByKey ??= new()
    {
        ["cleaner"] = Cleaner,
        ["registry"] = Registry,
        ["startup"] = Startup,
        ["programs"] = Programs,
        ["duplicates"] = Duplicates,
        ["disk"] = DiskAnalyzer,
        ["drivers"] = DriverCleaner,
        ["emptyfolders"] = EmptyFolders,
        ["hash"] = HashChecker,
        ["integrity"] = Integrity,
        ["largefiles"] = LargeFiles,
        ["rename"] = MassRename,
        ["tasks"] = ScheduledTasks,
        ["sysinfo"] = SystemInfo,
        ["extensions"] = Extensions,
        ["restore"] = Restore,
        ["wiper"] = Wiper,
        ["boottimer"] = BootTimer,
        ["optimize"] = Optimize,
        ["settings"] = SettingsPage,
        ["about"] = SettingsPage.About,
    };
    private Dictionary<string, object>? _pageByKey;

    /// <summary>Opens a page by short name (used by jump list tasks: --page=registry). Settings pages work too (--page=feedback).
    /// The keys are listed in <see cref="CommandLineHelp.Pages"/> and <see cref="CommandLineHelp.SettingsPages"/> (a test checks).</summary>
    public void Navigate(string key)
    {
        key = key.ToLowerInvariant();
        if (SettingsPage.Sections.Any(s => s.Key == key))
        {
            SettingsPage.Open(key);
            key = "settings";
        }
        var target = PageByKey.GetValueOrDefault(key);
        var page = AllPages.FirstOrDefault(p => p.Content == target);
        if (page is not null) page.IsSelected = true;
    }

    private void OnNavigated(NavPage page)
    {
        foreach (var p in AllPages)
            if (p != page) p.IsSelected = false;
        CurrentContent = page.Content;
        // A hidden tool still opens (a jump list, --page=, a link on another page); a note says where it went.
        if (IsHidden(page)) ShowHiddenNotice(page);
        else if (_hiddenNotice is not null && Notice == _hiddenNotice) Notice = "";
        Elevation.CurrentPage = PageByKey.FirstOrDefault(p => p.Value == page.Content).Key;
        PageAdminNeed = IsAdministrator ? null : AdminNeed.For(page.Content);
        UpdateCornerTask();
        if (ToolPages.Contains(page)) ToolsExpanded = true; // opened from a jump list or link: show where it lives
        // Lists are read when their page is first shown, not at startup.
        (page.Content as ILoadOnFirstVisit)?.EnsureLoaded();
    }
}
