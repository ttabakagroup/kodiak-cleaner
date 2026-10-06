using System.Windows;
using System.Windows.Input;
using KodiakCleaner.App.ViewModels;
using KodiakCleaner.Core.About;

namespace KodiakCleaner.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        LogoDance.Attach(Logo);
        DataContext = _vm = new MainViewModel();
        ApplySidebar();
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.SidebarCollapsed) or nameof(MainViewModel.SidebarLocked)) ApplySidebar();
        };
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
        Loaded += async (_, _) =>
        {
            if (BetaNoticeWindow.ShouldShow) BetaNoticeWindow.ShowFor(this);
            ShowWhatsNewIfUpdated();
            await UpdateService.AutoCheckAsync(msg => _vm.Notice = msg, atStartup: true);
            // Intervals shorter than a session (Every Hour, Every 4 Hours) are checked while Kodiak Cleaner stays open.
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
            timer.Tick += async (_, _) => await UpdateService.AutoCheckAsync(msg => _vm.Notice = msg, atStartup: false);
            timer.Start();
        };
    }

    // The sidebar logo dances on hover (LogoDance.Attach) and when the Home button is clicked.
    private void Logo_Click(object sender, RoutedEventArgs e) => LogoDance.Play(Logo);

    private const double CollapsedSidebarWidth = 84;

    /// <summary>Sizes the sidebar: icons only when collapsed, the default width when the paperclip is locked or
    /// Adjustable Sidebar Width is off, otherwise the saved width with a draggable edge.</summary>
    private void ApplySidebar()
    {
        var s = Settings.Current;
        var adjustable = !s.SidebarCollapsed && !s.SidebarLocked && s.SidebarAdjustable;
        double width = s.SidebarCollapsed ? CollapsedSidebarWidth : adjustable ? s.SidebarWidth : Settings.DefaultSidebarWidth;
        SidebarColumn.MinWidth = adjustable ? Settings.MinSidebarWidth : 0;
        SidebarColumn.MaxWidth = adjustable ? Settings.MaxSidebarWidth : double.PositiveInfinity;
        SidebarColumn.Width = new GridLength(width);
        SidebarEdge.Visibility = adjustable ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SidebarEdge_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        Settings.Current.SidebarWidth = (int)Math.Round(Math.Clamp(SidebarColumn.ActualWidth, Settings.MinSidebarWidth, Settings.MaxSidebarWidth));
        Settings.Current.Save();
    }

    /// <summary>Command-line / jump list arguments, e.g. --page=registry.</summary>
    public void HandleArgs(IEnumerable<string> args)
    {
        foreach (var a in args)
        {
            if (a.StartsWith(Integration.PageArgPrefix, StringComparison.OrdinalIgnoreCase))
                _vm.Navigate(a[Integration.PageArgPrefix.Length..]);
            else if (a.Equals(Integration.AutoCleanArg, StringComparison.OrdinalIgnoreCase))
                Task.Run(AutoClean.Run);
            else if (a.StartsWith(ScheduledCleaning.Arg, StringComparison.OrdinalIgnoreCase))
            {
                var id = a[ScheduledCleaning.Arg.Length..];
                Task.Run(() => ScheduledCleaning.Run(id));
            }
        }
    }

    private void DismissNotice_Click(object sender, RoutedEventArgs e) => _vm.Notice = "";

    public void ShowFullChangelog()
    {
        _vm.ShowChangelog();
        Activate();
    }

    /// <summary>Opens a Settings page by key ("terms", "about", ...).</summary>
    public void OpenSettings(string key)
    {
        _vm.Navigate(key);
        Activate();
    }

    /// <summary>
    /// First launch: Getting Started. First launch after an update: What's New, listing every change since
    /// the version the person had before.
    /// </summary>
    private void ShowWhatsNewIfUpdated()
    {
        var s = Settings.Current;
        var current = AppInfo.Version;
        Version? previous = Version.TryParse(s.LastSeenVersion, out var v) ? Changelog.Normalize(v) : null;

        if (previous is null)
            WelcomeWindow.ShowFor(this);
        else if (previous < current && s.ShowWhatsNewAfterUpdate)
        {
            var releases = Changelog.Since(AppInfo.Releases, previous, current);
            if (releases.Count > 0) WhatsNewWindow.ShowFor(releases, previous);
        }

        if (previous is null || previous != current)
        {
            s.LastSeenVersion = current.ToString(3);
            s.Save();
        }
    }
}
