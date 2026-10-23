using System.Collections.ObjectModel;
using System.Windows.Input;

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

        // Glyphs from Segoe Fluent Icons / Segoe MDL2 Assets.
        MainPages.Add(new NavPage("Cleaner", "", Cleaner, OnNavigated));
        MainPages.Add(new NavPage("Registry", "", Registry, OnNavigated));
        MainPages.Add(new NavPage("Startup", "", Startup, OnNavigated));
        MainPages.Add(new NavPage("Programs", "", Programs, OnNavigated));
        // Tools in alphabetical order (Ted's choice).
        ToolPages.Add(new NavPage("Boot Timer", "\uE916", BootTimer, OnNavigated));
        ToolPages.Add(new NavPage("Browser Extensions", "\uEA86", Extensions, OnNavigated));
        ToolPages.Add(new NavPage("Defrag and TRIM", "\uEC4A", Optimize, OnNavigated));
        ToolPages.Add(new NavPage("Disk Analyzer", "\uEB05", DiskAnalyzer, OnNavigated));
        ToolPages.Add(new NavPage("Drive Wiper", "\uEDA2", Wiper, OnNavigated, badge: "\uE74D"));
        ToolPages.Add(new NavPage("Driver Cleaner", "\uE950", DriverCleaner, OnNavigated));
        ToolPages.Add(new NavPage("Duplicate Finder", "", Duplicates, OnNavigated));
        ToolPages.Add(new NavPage("Empty Folders", "\uE8B7", EmptyFolders, OnNavigated));
        ToolPages.Add(new NavPage("Integrity Check", "\uE9D9", Integrity, OnNavigated));
        ToolPages.Add(new NavPage("Large Files", "\uE8A5", LargeFiles, OnNavigated));
        ToolPages.Add(new NavPage("Mass Rename", "\uE8AC", MassRename, OnNavigated));
        ToolPages.Add(new NavPage("Scheduled Tasks", "\uE823", ScheduledTasks, OnNavigated));
        ToolPages.Add(new NavPage("System Restore", "", Restore, OnNavigated));
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
            OnPropertyChanged(nameof(SidebarLocked)); // re-sizes the sidebar (MainWindow.ApplySidebar)
        };
        SettingsPage.ShowRequested += () => FooterPages[0].IsSelected = true;

        RestartAsAdminCommand = new RelayCommand(Elevation.RestartElevated, () => !IsAdministrator);
        HomeCommand = new RelayCommand(() => MainPages[0].IsSelected = true);
        MainPages[0].IsSelected = true;

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

    public ObservableCollection<NavPage> MainPages { get; } = new();
    public ObservableCollection<NavPage> ToolPages { get; } = new();
    public ObservableCollection<NavPage> FooterPages { get; } = new();
    private IEnumerable<NavPage> AllPages => MainPages.Concat(ToolPages).Concat(FooterPages);

    private object? _currentContent;
    public object? CurrentContent { get => _currentContent; private set => Set(ref _currentContent, value); }

    public ICommand RestartAsAdminCommand { get; }
    /// <summary>The logo and name in the sidebar go back to the Cleaner.</summary>
    public ICommand HomeCommand { get; }

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
            OnPropertyChanged(nameof(ShowPaperclip));
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
    public bool IsNotAdministrator => !Elevation.IsAdministrator;
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
        SettingsPage.About.ShowChangelog = true;
    }

    /// <summary>Opens a page by short name (used by jump list tasks: --page=registry). Settings pages work too (--page=feedback).</summary>
    public void Navigate(string key)
    {
        key = key.ToLowerInvariant();
        if (SettingsPage.Sections.Any(s => s.Key == key))
        {
            SettingsPage.Open(key);
            key = "settings";
        }
        object? target = key switch
        {
            "cleaner" => Cleaner,
            "registry" => Registry,
            "startup" => Startup,
            "programs" => Programs,
            "duplicates" => Duplicates,
            "disk" => DiskAnalyzer,
            "drivers" => DriverCleaner,
            "emptyfolders" => EmptyFolders,
            "integrity" => Integrity,
            "largefiles" => LargeFiles,
            "rename" => MassRename,
            "tasks" => ScheduledTasks,
            "sysinfo" => SystemInfo,
            "extensions" => Extensions,
            "restore" => Restore,
            "wiper" => Wiper,
            "boottimer" => BootTimer,
            "optimize" => Optimize,
            "settings" => SettingsPage,
            "about" => SettingsPage.About,
            _ => null,
        };
        var page = AllPages.FirstOrDefault(p => p.Content == target);
        if (page is not null) page.IsSelected = true;
    }

    private void OnNavigated(NavPage page)
    {
        foreach (var p in AllPages)
            if (p != page) p.IsSelected = false;
        CurrentContent = page.Content;
        UpdateCornerTask();
        if (ToolPages.Contains(page)) ToolsExpanded = true; // opened from a jump list or link: show where it lives
        // Lists are read when their page is first shown, not at startup.
        (page.Content as ILoadOnFirstVisit)?.EnsureLoaded();
    }
}
