using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using KodiakCleaner.Core.About;
using KodiakCleaner.Core.Updates;
using KodiakCleaner.Core.Appearance;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App.ViewModels;

/// <summary>One page in the Settings sidebar.</summary>
public sealed class SettingsSection : ObservableObject
{
    private readonly Action<SettingsSection> _onSelected;

    public SettingsSection(string key, string title, string glyph, string group, string description, object context, Action<SettingsSection> onSelected)
    {
        Key = key;
        Title = title;
        Glyph = glyph;
        Group = group;
        Description = description;
        Context = context;
        _onSelected = onSelected;
    }

    /// <summary>Short name used by --page=settings/about and to pick the page's template.</summary>
    public string Key { get; }
    public string Title { get; }
    public string Glyph { get; }
    /// <summary>Sidebar heading this page sits under ("" = the app's own pages at the top).</summary>
    public string Group { get; }
    /// <summary>One line shown under the page title.</summary>
    public string Description { get; }
    /// <summary>The page's view model (or, for the Developer page, the page itself).</summary>
    public object Context { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!Set(ref _isSelected, value)) return;
            if (value) _onSelected(this);
        }
    }

    /// <summary>Labels on the page (titles, switches, buttons) for the Settings search; filled by SettingsView the first time someone searches.</summary>
    public IReadOnlyList<string> Labels { get; set; } = [];

    public bool Matches(string query) => SettingsSearch.Matches(query, [Title, Description, Group, .. Labels]);

    /// <summary>While searching, the labels on this page that match, shown in small text under its name.</summary>
    private string _searchHint = "";
    public string SearchHint { get => _searchHint; set => Set(ref _searchHint, value); }
}

/// <summary>
/// Options &amp; Settings: the app's own pages (General, Cleaning, Updates, Advanced), then Personalize
/// (Appearance, Supporter), Protect (Backups), Help (Contact &amp; Feedback, Terms of Use, Privacy Policy, Why It Was Made) and, in
/// development builds only, Developer. About has its own sidebar entry under Settings in the main window.
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly Action _cleanerOptionsChanged;
    private UpdateInfo? _available;

    public SettingsViewModel(Action cleanerOptionsChanged)
    {
        _cleanerOptionsChanged = cleanerOptionsChanged;
        About = new AboutViewModel(this);
        Feedback = new FeedbackViewModel();
        SupporterPage = new SupporterViewModel();
        Terms = new DocumentViewModel(AppInfo.ReadResource("terms-of-use.md") ?? "");
        Privacy = new DocumentViewModel(AppInfo.ReadResource("PRIVACY.md") ?? "");
        Story = new DocumentViewModel(AppInfo.ReadResource("why-it-was-made.md") ?? "", bodySize: 17);
        SupportCommand = new RelayCommand(() => Shell.OpenUrl(AppInfo.KoFiUrl));
        OpenSectionCommand = new RelayCommand<string>(Open);
        LockedPaletteCommand = new RelayCommand(() => Open("supporter"));
        OpenBackupsCommand = new RelayCommand(() => Elevation.OpenFolder(Settings.Data.Registry));
        OpenSettingsFolderCommand = new RelayCommand(() => Elevation.ShowInExplorer(Settings.FilePath));
        OpenDataFolderCommand = new RelayCommand(() => Elevation.OpenFolder(Settings.Data.Root));
        ResetCommand = new RelayCommand(Reset);
        BackUpRegistryCommand = new RelayCommand(BackUpRegistryAsync, () => !IsBackingUp);
        BackUpSettingsCommand = new RelayCommand(BackUpSettings);
        CheckForUpdatesCommand = new RelayCommand(CheckForUpdatesAsync, () => !IsCheckingUpdates);
        InstallUpdateCommand = new RelayCommand(InstallUpdateAsync, () => _available is not null && !IsCheckingUpdates);
        BuildChoices();

        Add("general", "General", "\uE713", "", "How Kodiak Cleaner fits into Windows.", this);
        Add("cleaning", "Cleaning", "\uE74D", "", "What the Cleaner asks and keeps.", this);
        Add("updates", "Updates", "\uE895", "", "Get new versions of Kodiak Cleaner.", this);
        Add("advanced", "Advanced", "\uE9F5", "", "Options for people who know what they need.", this);
        Add("appearance", "Appearance", "\uE790", "Personalize", "Color scheme, accent color and light or dark mode.", this);
        Add("supporter", "Supporter", "\uEB51", "Personalize", "Say thanks and unlock a few extras.", SupporterPage);
        Add("backups", "Registry", "\uE8F1", "Protect", "Registry backups: made before every fix, or whenever you choose.", this);
        Add("application", "Application", "\uE74C", "Protect", "Kodiak Cleaner's settings file, backups and activity log.", this);
        Add("feedback", "Contact & Feedback", "\uE8BD", "Help", "Tell us what's working and what isn't.", Feedback);
        Add("terms", "Terms of Use", "\uE8A5", "Help", "The terms for using Kodiak Cleaner.", Terms);
        Add("privacy", "Privacy Policy", "\uE72E", "Help", "What Kodiak Cleaner does with information: everything stays on this PC.", Privacy);
        Add("story", "Why It Was Made", "\uE736", "Help", "The story behind Kodiak Cleaner, in Ted's words.", Story);
#if DEVTOOLS
        // Development builds only; release builds leave this page (and its code) out.
        Add("developer", "Developer", "\uEC7A", "Developer", "Test plans and check how this build is set up.",
            new Developer.DeveloperPage { DataContext = new Developer.DeveloperViewModel() });
#endif
        Sections[0].IsSelected = true;

        Supporter.Changed += () =>
        {
            OnPropertyChanged(nameof(ShowSupportLink));
            BuildChoices();
        };
        ThemeManager.Applied += () =>
        {
            // A scheme change can change the swatch list (Classic hides Blue, its own color).
            if (!Accents.Select(a => a.Name).SequenceEqual(AccentNames)) BuildAccents();
            foreach (var a in Accents) a.Refresh(CurrentAccent);
        };
    }

    /// <summary>Asks the main window to show Settings (used when another page opens a settings page).</summary>
    public event Action? ShowRequested;

    private static Settings S => Settings.Current;

    public ObservableCollection<SettingsSection> Sections { get; } = new();

    /// <summary>The Settings search box above General (Ted, 0.8.45). SettingsView filters the sidebar as it changes.</summary>
    private string _searchQuery = "";
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (!Set(ref _searchQuery, value ?? "")) return;
            foreach (var s in Sections) s.SearchHint = string.Join(" · ", SettingsSearch.Hints(_searchQuery, s.Labels));
        }
    }

    private bool _noMatches;
    /// <summary>Set by SettingsView after filtering: shows "No settings match" under the search box.</summary>
    public bool NoMatches { get => _noMatches; set => Set(ref _noMatches, value); }

    private SettingsSection? _current;
    public SettingsSection? CurrentSection { get => _current; private set => Set(ref _current, value); }

    public AboutViewModel About { get; }
    public FeedbackViewModel Feedback { get; }
    public SupporterViewModel SupporterPage { get; }
    public DocumentViewModel Terms { get; }
    public DocumentViewModel Privacy { get; }
    public DocumentViewModel Story { get; }

    public ICommand SupportCommand { get; }
    public ICommand OpenSectionCommand { get; }
    public ICommand LockedPaletteCommand { get; }

    /// <summary>The quiet "Support Kodiak Cleaner" link is hidden once someone is a supporter.</summary>
    public bool ShowSupportLink => !Supporter.IsSupporter;
    public string SupportLinkText => $"Support {AppInfo.ShortName}";
    public string PrivacyNote => "Everything stays on this PC. Only update checks go online, and only when you ask.";
    public string Footer => "Copyright © 2026 TED TABAKA.";
    public string LicenseLine => "Free and open source under the MIT License.";

    public ObservableCollection<Choice<AppTheme>> Themes { get; } = new();
    public ObservableCollection<PaletteChoice> Palettes { get; } = new();
    public ObservableCollection<AccentChoice> Accents { get; } = new();

    private static string CurrentAccent => AccentColors.Effective(S.Accent, ThemeManager.ActivePalette);

    /// <summary>The scheme's own accent always comes first, then Windows and the swatches.</summary>
    private static IEnumerable<string> AccentNames => AccentColors.NamesFor(ThemeManager.ActivePalette).Prepend("");

    /// <summary>Opens a settings page by key ("about", "feedback", ...).</summary>
    public void Open(string key)
    {
        var section = Sections.FirstOrDefault(s => s.Key == key);
        if (section is null) return;
        section.IsSelected = true;
        ShowRequested?.Invoke();
    }

    private void Add(string key, string title, string glyph, string group, string description, object context) =>
        Sections.Add(new SettingsSection(key, title, glyph, group, description, context, OnSelected));

    private void OnSelected(SettingsSection section)
    {
        foreach (var s in Sections)
            if (s != section) s.IsSelected = false;
        CurrentSection = section;
    }

    public ICommand OpenBackupsCommand { get; }
    public ICommand OpenSettingsFolderCommand { get; }
    public ICommand OpenDataFolderCommand { get; }

    public string DataFolder => Settings.Data.Root;
    public string RegistryBackupFolder => Settings.Data.Registry;
    public string DataFolderNote => (Integration.IsInstalled
            ? "Kept in your Documents folder, so saving them never needs administrator rights. "
            : "This portable copy keeps them beside KodiakCleaner.exe. ") +
        "Backups has a folder for each kind (Event Logs, Programs History, Registry, Settings and Startup History); Logs has the activity log; Reports has the System Information reports you export. " +
        (Integration.IsInstalled ? "Uninstalling Kodiak Cleaner asks whether to delete them." : "Delete the folder to remove them.");
    public ICommand ResetCommand { get; }
    public ICommand BackUpRegistryCommand { get; }
    public ICommand BackUpSettingsCommand { get; }

    private bool _isBackingUp;
    public bool IsBackingUp { get => _isBackingUp; private set => Set(ref _isBackingUp, value); }

    private string _registryBackupStatus = "";
    public string RegistryBackupStatus { get => _registryBackupStatus; private set => Set(ref _registryBackupStatus, value); }

    private string _settingsBackupStatus = "";
    public string SettingsBackupStatus { get => _settingsBackupStatus; private set => Set(ref _settingsBackupStatus, value); }

    /// <summary>Saves every registry area Kodiak Cleaner can change into one .reg file in Backups.</summary>
    private async Task BackUpRegistryAsync()
    {
        IsBackingUp = true;
        RegistryBackupStatus = "Backing up…";
        try
        {
            var path = await Task.Run(() => Core.RegistryTools.RegistryBackup.Save(Core.RegistryTools.RegistryBackup.AreasKodiakCleanerChanges(), "Full"));
            RegistryBackupStatus = $"Saved {System.IO.Path.GetFileName(path)}.";
            ActivityLog.Write("Backups", $"Registry backed up to {path}");
        }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            RegistryBackupStatus = "The backup couldn't be saved: " + e.Message;
        }
        finally { IsBackingUp = false; }
    }

    /// <summary>Copies KodiakCleaner.ini into Backups with the date and time in its name.</summary>
    private void BackUpSettings()
    {
        try
        {
            S.Save();
            System.IO.Directory.CreateDirectory(Settings.Data.Settings);
            var stamp = $"{DateTime.Now:yyyy-MM-dd_HHmmss}";
            var target = System.IO.Path.Combine(Settings.Data.Settings, $"{stamp}_Settings.ini");
            System.IO.File.Copy(Settings.FilePath, target, overwrite: false);
            // The Programs and Startup histories go with it, so a restored setup still knows what it removed.
            var saved = new List<string> { System.IO.Path.GetFileName(target) };
            foreach (var (folder, file, label) in new[]
            {
                (ProgramHistoryStore.Folder, Core.Programs.ProgramHistory.FileName, "Programs History"),
                (StartupHistoryStore.Folder, Core.Startup.StartupHistory.FileName, "Startup History"),
            })
            {
                var history = System.IO.Path.Combine(folder, file);
                if (!System.IO.File.Exists(history)) continue;
                var historyTarget = System.IO.Path.Combine(Settings.Data.Settings, $"{stamp}_{label}.json");
                System.IO.File.Copy(history, historyTarget, overwrite: false);
                saved.Add(System.IO.Path.GetFileName(historyTarget));
            }
            SettingsBackupStatus = $"Saved {(saved.Count == 1 ? saved[0] : string.Join(", ", saved.Take(saved.Count - 1)) + " and " + saved[^1])} in Backups\\Settings.";
            ActivityLog.Write("Backups", SettingsBackupStatus);
        }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException)
        {
            SettingsBackupStatus = "The settings couldn't be backed up: " + e.Message;
        }
    }
    public ICommand CheckForUpdatesCommand { get; }
    public ICommand InstallUpdateCommand { get; }

    public string SettingsFile => Settings.FilePath;
    public string? SaveError => Settings.SaveError;
    public bool HasSaveError => Settings.SaveError is not null;
    public bool IsInstalled => Integration.IsInstalled;
    public bool CanSkipUac => Integration.CanSkipUac;
    public string SkipUacNote => Integration.CanSkipUac
        ? "Starts Kodiak Cleaner as administrator without the Windows permission prompt, using a scheduled task. Turning this on or off needs administrator rights."
        : "Only available when Kodiak Cleaner is installed with the MSI (a portable copy could be swapped out by other programs).";

    // ---- General ----
    public bool CleanOnStartup
    {
        get => S.CleanOnStartup;
        set => Apply(value, v => { Integration.SetCleanOnStartup(v); S.CleanOnStartup = v; });
    }

    public bool RecycleBinMenu
    {
        get => S.RecycleBinMenu;
        set => Apply(value, v => { Integration.SetRecycleBinMenu(v); S.RecycleBinMenu = v; });
    }

    public bool ShowWhatsNewAfterUpdate
    {
        get => S.ShowWhatsNewAfterUpdate;
        set => Apply(value, v => S.ShowWhatsNewAfterUpdate = v);
    }

    public bool IsPreRelease => AppInfo.Channel != ReleaseChannel.Stable;

    public bool ShowBetaNotice
    {
        get => S.ShowBetaNotice;
        set => Apply(value, v => S.ShowBetaNotice = v);
    }

    // ---- Cleaning ----
    public bool ConfirmBeforeCleaning
    {
        get => S.ConfirmBeforeCleaning;
        set => Apply(value, v => S.ConfirmBeforeCleaning = v);
    }

    // ---- Updates ----
    public bool AutoUpdate
    {
        get => S.AutoUpdate;
        set => Apply(value, v => S.AutoUpdate = v);
    }

    /// <summary>Download and install found updates by themselves (Ted, 0.8.51); greyed out while checking is off.</summary>
    public bool AutoInstall
    {
        get => S.AutoInstall;
        set => Apply(value, v => S.AutoInstall = v);
    }

    /// <summary>Check for Updates interval choices (Ted, 0.8.44; a dropdown since 0.8.45, so more can be added).</summary>
    public IReadOnlyList<UpdateIntervalOption> UpdateIntervals { get; } =
        UpdateSchedule.Options.Select(o => new UpdateIntervalOption(o.Interval, o.Label)).ToList();

    public UpdateInterval UpdateInterval
    {
        get => S.UpdateInterval;
        set
        {
            if (S.UpdateInterval == value) return;
            S.UpdateInterval = value;
            try { S.Save(); }
            catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException or System.IO.IOException)
            {
                Dialogs.Error(e.Message);
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(SaveError));
            OnPropertyChanged(nameof(HasSaveError));
        }
    }

    private bool _isCheckingUpdates;
    public bool IsCheckingUpdates { get => _isCheckingUpdates; private set => Set(ref _isCheckingUpdates, value); }

    private string _updateStatus = "";
    public string UpdateStatus { get => _updateStatus; private set => Set(ref _updateStatus, value); }

    public bool UpdateAvailable => _available is not null;

    public string LastChecked => S.LastUpdateCheckUtc is { } t ? $"Last checked {t.ToLocalTime():g}." : "Never checked.";

    // ---- Advanced ----
    public bool TempOlderThan24h
    {
        get => S.TempOlderThan24h;
        set => Apply(value, v => { S.TempOlderThan24h = v; _cleanerOptionsChanged(); });
    }

    public bool RecycleBinOlderThan24h
    {
        get => S.RecycleBinOlderThan24h;
        set => Apply(value, v => { S.RecycleBinOlderThan24h = v; _cleanerOptionsChanged(); });
    }

    public bool PromptRegistryBackup
    {
        get => S.PromptRegistryBackup;
        set => Apply(value, v => S.PromptRegistryBackup = v);
    }

    public bool MinimizeToTray
    {
        get => S.MinimizeToTray;
        set => Apply(value, v => S.MinimizeToTray = v);
    }

    public bool Animations
    {
        get => S.Animations;
        set => Apply(value, v => { S.Animations = v; Motion.Instance.Refresh(); });
    }

    public bool SidebarAdjustable
    {
        get => S.SidebarAdjustable;
        set => Apply(value, v => { S.SidebarAdjustable = v; MainViewModel.NotifySidebarSettingsChanged(); });
    }

    public bool ShowSidebarLock
    {
        get => S.ShowSidebarLock;
        set => Apply(value, v => { S.ShowSidebarLock = v; MainViewModel.NotifySidebarSettingsChanged(); });
    }

    public bool CloseToTray
    {
        get => S.CloseToTray;
        set => Apply(value, v => S.CloseToTray = v);
    }

    /// <summary>Every start asks Windows for administrator rights; with Skip UAC on, installed copies get them without a prompt.</summary>
    public bool AlwaysAdmin
    {
        get => S.AlwaysAdmin;
        set => Apply(value, v => S.AlwaysAdmin = v);
    }

    public bool SkipUac
    {
        get => S.SkipUac;
        set => Apply(value, v =>
        {
            if (!Elevation.IsAdministrator)
                throw new UnauthorizedAccessException("Changing this needs administrator rights. Use Restart as Administrator, then try again.");
            Integration.SetSkipUac(v);
            S.SkipUac = v;
        });
    }

    public bool KeepActivityLog
    {
        get => S.KeepActivityLog;
        set => Apply(value, v => { S.KeepActivityLog = v; Core.Util.ActivityLog.Enabled = v; });
    }

    public bool JumpList
    {
        get => S.JumpList;
        set => Apply(value, v => { Integration.ApplyJumpList(v); S.JumpList = v; });
    }

    /// <summary>Runs the change, saves, and puts the toggle back if Windows refused.</summary>
    private void Apply(bool value, Action<bool> change, [CallerMemberName] string? name = null)
    {
        try
        {
            change(value);
            S.Save();
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException or System.IO.IOException)
        {
            Dialogs.Error(e.Message);
        }
        OnPropertyChanged(name);
        OnPropertyChanged(nameof(SaveError));
        OnPropertyChanged(nameof(HasSaveError));
    }

    private async Task CheckForUpdatesAsync()
    {
        IsCheckingUpdates = true;
        UpdateStatus = "Checking…";
        try
        {
            var result = await UpdateService.CheckAsync();
            _available = result.Available ? result.Info : null;
            UpdateStatus = result.Message + (result.Available && !Integration.IsInstalled ? " Download it from the website." : "");
        }
        finally
        {
            IsCheckingUpdates = false;
            OnPropertyChanged(nameof(UpdateAvailable));
            OnPropertyChanged(nameof(LastChecked));
        }
    }

    private async Task InstallUpdateAsync()
    {
        if (_available is not { } info) return;
        if (!Integration.IsInstalled)
        {
            Shell.OpenUrl(AppInfo.HomepageUrl + "download/");
            return;
        }
        IsCheckingUpdates = true;
        UpdateStatus = $"Downloading Kodiak Cleaner {info.Version.ToString(3)}…";
        try
        {
            await UpdateService.DownloadAsync(info);
            if (Dialogs.Confirm($"Kodiak Cleaner {info.Version.ToString(3)} is downloaded and verified. Close Kodiak Cleaner and install it now?"))
                UpdateService.InstallPendingNow();
            else
                UpdateStatus = "The update will install when you close Kodiak Cleaner.";
        }
        catch (Exception e) when (e is InvalidOperationException or System.Net.Http.HttpRequestException or System.IO.IOException or TaskCanceledException)
        {
            UpdateStatus = "Update failed: " + e.Message;
        }
        finally { IsCheckingUpdates = false; }
    }

    private void BuildChoices()
    {
        Themes.Clear();
        foreach (var (label, theme) in new[] { ("Match Device", AppTheme.System), ("Light", AppTheme.Light), ("Dark", AppTheme.Dark) })
            Themes.Add(new Choice<AppTheme>(label, theme, S.Theme == theme, t =>
            {
                S.Theme = t;
                S.Save();
                ThemeManager.Apply();
            }));

        Palettes.Clear();
        Palettes.Add(new PaletteChoice(Palette.Honey, "Honey", "Warm cream and gold", locked: false, OnPalettePicked));
        Palettes.Add(new PaletteChoice(Palette.Classic, "Classic", "Crisp white and blue", locked: !Supporter.IsSupporter, OnPalettePicked));
        Palettes.Add(new PaletteChoice(Palette.Glacier, "Glacier", "Calm blue-grey", locked: !Supporter.IsSupporter, OnPalettePicked));

        BuildAccents();
    }

    private void BuildAccents()
    {
        Accents.Clear();
        var own = AccentShades.SchemeSwatch(ThemeManager.ActivePalette.ToString());
        foreach (var name in AccentNames)
            Accents.Add(new AccentChoice(name, name switch
            {
                "" => own is null ? "Theme Accent" : $"Theme Accent ({own})",
                "Windows" => "Windows Accent",
                _ => name,
            }, OnAccentPicked));
        foreach (var a in Accents) a.Refresh(CurrentAccent);
    }

    private void OnAccentPicked(AccentChoice choice)
    {
        if (S.Accent == choice.Name) return;
        S.Accent = choice.Name;
        S.Save();
        ThemeManager.Apply();
    }

    private void OnPalettePicked(PaletteChoice choice)
    {
        if (choice.IsLocked)
        {
            Open("supporter");
            return;
        }
        S.Palette = choice.Palette;
        S.Save();
        ThemeManager.Apply();
        foreach (var p in Palettes) p.Refresh();
    }

    private void Reset()
    {
        if (!Dialogs.Confirm("Restore all settings to their defaults? Your cleaner selections are reset too.")) return;
        var wasSkipUac = S.SkipUac;
        Settings.Reset();
        if (wasSkipUac && Elevation.IsAdministrator)
        {
            try { Integration.SetSkipUac(false); } catch (InvalidOperationException) { }
        }
        else if (wasSkipUac)
        {
            S.SkipUac = true; // can't remove the task without admin rights; keep the setting truthful
            S.Save();
        }
        Integration.SyncAll();
        Supporter.Refresh();
        ThemeManager.Apply();
        Motion.Instance.Refresh();
        MainViewModel.NotifySidebarSettingsChanged(); // the sidebar's width, lock and collapse follow the reset
        BuildChoices();
        _cleanerOptionsChanged();
        OnPropertyChanged(string.Empty);
    }
}

/// <summary>One choice in Settings > Updates' Check for Updates dropdown.</summary>
public sealed record UpdateIntervalOption(UpdateInterval Interval, string Label)
{
    // The themed ComboBox shows the chosen item's text, not DisplayMemberPath.
    public override string ToString() => Label;
}
