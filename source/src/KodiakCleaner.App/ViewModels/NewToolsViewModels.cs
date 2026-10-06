using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Input;
using KodiakCleaner.Core.Tools;
using KodiakCleaner.Core.Util;
using Microsoft.Win32;

namespace KodiakCleaner.App.ViewModels;

// ---------------------------------------------------------------- Large Files

public sealed class LargeFileItem : ObservableObject
{
    public LargeFileItem(LargeFile file) => File = file;
    public LargeFile File { get; }
    public string Path => File.Path;
    public string Name => System.IO.Path.GetFileName(File.Path);
    public string Folder => System.IO.Path.GetDirectoryName(File.Path) ?? "";
    public string Size => ByteSize.Format(File.Size);
    public long Bytes => File.Size;
    public string Modified => File.Modified.ToString("d");

    private bool _isChecked;
    public bool IsChecked { get => _isChecked; set => Set(ref _isChecked, value); }
}

/// <summary>Tools › Large Files: the biggest files in a folder. Removed files go to the Recycle Bin after a confirm.</summary>
public sealed class LargeFilesViewModel : ObservableObject
{
    private CancellationTokenSource? _cts;

    public LargeFilesViewModel()
    {
        _root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var (label, mb) in new[] { ("50 MB", 50L), ("100 MB", 100L), ("500 MB", 500L), ("1 GB", 1024L) })
            MinSizes.Add(new Choice<long>(label, mb * 1024 * 1024, mb == 100, v => _minBytes = v));
        BrowseCommand = new RelayCommand(Browse, () => !IsBusy);
        SearchCommand = new RelayCommand(SearchAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(Root));
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy);
        RemoveCommand = new RelayCommand(() => RemoveAsync(Found.Where(f => f.IsChecked).ToList()), () => !IsBusy && Found.Any(f => f.IsChecked));
        RemoveRowCommand = new RelayCommand(() => RemoveAsync(Selected is { } s ? new List<LargeFileItem> { s } : new()), () => !IsBusy && Selected is not null);
        ShowRowCommand = new RelayCommand(() => { if (Selected is { } s) Elevation.ShowInExplorer(s.Path); }, () => Selected is not null);
    }

    public ObservableCollection<LargeFileItem> Found { get; } = new();
    public ObservableCollection<Choice<long>> MinSizes { get; } = new();
    private long _minBytes = 100L * 1024 * 1024;
    private const int Limit = 500;

    public ICommand BrowseCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand RemoveRowCommand { get; }
    public ICommand ShowRowCommand { get; }

    private LargeFileItem? _selected;
    public LargeFileItem? Selected { get => _selected; set => Set(ref _selected, value); }

    private string _root;
    public string Root { get => _root; set => Set(ref _root, value); }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }

    private string _summary = "Finds the biggest files in a folder. Windows, program folders, and hidden or system files are skipped.";
    public string Summary { get => _summary; private set => Set(ref _summary, value); }

    public bool? AllChecked
    {
        get => Found.Count > 0 && Found.All(f => f.IsChecked) ? true : Found.Any(f => f.IsChecked) ? null : false;
        set
        {
            var on = value != false;
            foreach (var f in Found) f.IsChecked = on;
        }
    }

    private static IEnumerable<string> ProtectedFolders()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    }

    private void Browse()
    {
        var dialog = new OpenFolderDialog { Title = "Choose a Folder to Search" };
        if (dialog.ShowDialog() == true) Root = dialog.FolderName;
    }

    private void Show(IEnumerable<LargeFile> files)
    {
        Found.Clear();
        foreach (var f in files)
        {
            var item = new LargeFileItem(f);
            item.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(LargeFileItem.IsChecked)) OnPropertyChanged(nameof(AllChecked)); };
            Found.Add(item);
        }
        OnPropertyChanged(nameof(AllChecked));
    }

    private async Task SearchAsync()
    {
        if (!Directory.Exists(Root))
        {
            Summary = "That folder doesn't exist.";
            return;
        }
        IsBusy = true;
        Show(Array.Empty<LargeFile>());
        _cts = new CancellationTokenSource();
        Summary = $"Searching {Root}…";
        try
        {
            var (root, min, token) = (Root, _minBytes, _cts.Token);
            var list = await Task.Run(() => LargeFileFinder.Find(root, min, Limit, ProtectedFolders(), token));
            Show(list);
            Summary = list.Count == 0
                ? "No files that big were found."
                : $"Found {list.Count:N0} file(s) taking {ByteSize.Format(list.Sum(f => f.Size))}" +
                  (list.Count == Limit ? $" (the {Limit} biggest are shown)." : ".") + " Tick any you no longer need.";
        }
        catch (OperationCanceledException) { Summary = "Search cancelled."; }
        finally { IsBusy = false; }
    }

    private async Task RemoveAsync(List<LargeFileItem> chosen)
    {
        if (chosen.Count == 0) return;
        var what = chosen.Count == 1 ? $"'{chosen[0].Name}'" : $"{chosen.Count} files";
        if (!Dialogs.Confirm($"Move {what} ({ByteSize.Format(chosen.Sum(f => f.Bytes))}) to the Recycle Bin?")) return;
        IsBusy = true;
        int moved = 0, skipped = 0;
        try
        {
            await Task.Run(() =>
            {
                foreach (var f in chosen)
                {
                    try
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(f.Path,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                        moved++;
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException or OperationCanceledException) { skipped++; }
                }
            });
            foreach (var f in chosen.Where(f => !System.IO.File.Exists(f.Path))) Found.Remove(f);
            OnPropertyChanged(nameof(AllChecked));
            Summary = $"Moved {moved} file(s) to the Recycle Bin." + (skipped > 0 ? $" {skipped} were skipped (in use or protected)." : "");
        }
        finally { IsBusy = false; }
    }
}

// ---------------------------------------------------------------- System Information

/// <summary>
/// System Information (next to Settings and About since 0.8.46, Ted): read-only facts about this PC. Summary is the quick
/// look; Advanced (Ted, 0.8.46) adds serial numbers, network addresses, memory, cache, BIOS, graphics, a few Windows
/// settings and temperatures, read in the background the first time it is opened. Copy to Clipboard copies the view
/// shown; Export Report saves both views to the Reports folder as Text, HTML or CSV. Nothing is sent anywhere.
/// </summary>
public sealed class SystemInfoViewModel : ObservableObject, ILoadOnFirstVisit
{
    public SystemInfoViewModel()
    {
        RefreshCommand = new RelayCommand(Refresh, () => !IsBusy);
        CopyCommand = new RelayCommand(() =>
        {
            if (ClipboardHelper.CopyOrExplain(SystemReport.ToText(CurrentFacts, $"{AppInfo.Name} {AppInfo.VersionName}: System Information")))
                Status = "Copied. Paste it wherever you need it; nothing was sent anywhere.";
        });
        ExportCommand = new RelayCommand(Export, () => !IsBusy);
        OpenReportCommand = new RelayCommand(() =>
        {
            if (LastReport is not null && File.Exists(LastReport)) Elevation.ShowInExplorer(LastReport);
        });
        OpenReportsFolderCommand = new RelayCommand(() => Elevation.OpenFolder(Settings.Data.Reports));
    }

    private bool _loaded;
    /// <summary>Reads the list the first time the page is shown, so starting Kodiak Cleaner stays quick.</summary>
    public void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        LoadSummary();
    }

    public ObservableCollection<SystemFact> Facts { get; } = new();
    public ObservableCollection<SystemFact> AdvancedFacts { get; } = new();
    public IEnumerable<SystemFact> CurrentFacts => ShowAdvanced ? AdvancedFacts : Facts;

    public ICommand RefreshCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand OpenReportCommand { get; }
    public ICommand OpenReportsFolderCommand { get; }

    private bool _showAdvanced;
    public bool ShowAdvanced
    {
        get => _showAdvanced;
        set
        {
            if (!Set(ref _showAdvanced, value)) return;
            OnPropertyChanged(nameof(ShowSummary));
            OnPropertyChanged(nameof(CurrentFacts));
            Status = "";
            if (value && !_advancedLoaded) _ = LoadAdvancedAsync();
        }
    }
    public bool ShowSummary { get => !ShowAdvanced; set => ShowAdvanced = !value; }

    private bool _busy;
    public bool IsBusy
    {
        get => _busy;
        private set { if (Set(ref _busy, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    private string _status = "";
    public string Status { get => _status; private set => Set(ref _status, value); }

    private string? _lastReport;
    /// <summary>The report Export Report saved last, for Show Report.</summary>
    public string? LastReport { get => _lastReport; private set { Set(ref _lastReport, value); OnPropertyChanged(nameof(HasReport)); } }
    public bool HasReport => LastReport is not null;

    public string ReportsFolder => Settings.Data.Reports;

    public IReadOnlyList<ReportFormatOption> ReportFormats { get; } =
        SystemReport.Formats.Select(f => new ReportFormatOption(f.Format, f.Label)).ToList();

    public ReportFormat ReportFormat
    {
        get => Settings.Current.ReportFormat;
        set
        {
            if (Settings.Current.ReportFormat == value) return;
            Settings.Current.ReportFormat = value;
            try { Settings.Current.Save(); }
            catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException or IOException) { }
            OnPropertyChanged();
        }
    }

    private void Refresh()
    {
        LoadSummary();
        Status = "";
        if (_advancedLoaded || ShowAdvanced) _ = LoadAdvancedAsync();
    }

    private void LoadSummary()
    {
        Facts.Clear();
        foreach (var f in Collect()) Facts.Add(f);
    }

    private bool _advancedLoaded;
    private Task? _advancedTask;

    private Task LoadAdvancedAsync()
    {
        if (_advancedTask is { IsCompleted: false }) return _advancedTask;
        return _advancedTask = LoadAdvancedCore();
    }

    private async Task LoadAdvancedCore()
    {
        IsBusy = true;
        Status = "Reading details. This can take a few seconds.";
        try
        {
            var facts = await Task.Run(SystemDetails.Collect);
            AdvancedFacts.Clear();
            foreach (var f in facts) AdvancedFacts.Add(f);
            _advancedLoaded = true;
            Status = "";
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            ActivityLog.Error("System Information", e);
            Status = "Some details couldn't be read.";
        }
        finally { IsBusy = false; }
    }

    private async void Export()
    {
        if (!_loaded) EnsureLoaded();
        if (!_advancedLoaded) await LoadAdvancedAsync();
        var format = ReportFormat;
        var now = DateTime.Now;
        var title = $"{AppInfo.Name} {AppInfo.VersionName}: System Report for {Environment.MachineName}";
        try
        {
            var content = SystemReport.Create(format, Facts.Concat(AdvancedFacts), title, now, CurrentStyle());
            Directory.CreateDirectory(Settings.Data.Reports);
            var path = Path.Combine(Settings.Data.Reports, SystemReport.FileName(now, format));
            // A byte order mark lets Excel and Notepad read °C and other symbols in CSV and text files.
            File.WriteAllText(path, content, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: format != ReportFormat.Html));
            LastReport = path;
            Status = $"Saved {Path.GetFileName(path)} in Reports.";
            ActivityLog.Write("System Information", $"Exported a {SystemReport.Extension(format).TrimStart('.').ToUpperInvariant()} report: {path}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Status = "";
            Dialogs.Error($"The report couldn't be saved in {Settings.Data.Reports}.\n\n{e.Message}");
        }
    }

    /// <summary>The colors and font in use right now, so an HTML report looks like Kodiak Cleaner.</summary>
    private static ReportStyle CurrentStyle()
    {
        static string Hex(string key) =>
            System.Windows.Application.Current.TryFindResource(key) is System.Windows.Media.SolidColorBrush b
                ? $"#{b.Color.R:X2}{b.Color.G:X2}{b.Color.B:X2}"
                : "";
        var font = System.Windows.Application.Current.TryFindResource("FontBody") is System.Windows.Media.FontFamily f ? f.Source : "";
        return new ReportStyle(Hex("Bg"), Hex("Surface"), Hex("Text"), Hex("Muted"), Hex("Line"), Hex("Accent"), Hex("AccentText"), font);
    }

    private static IEnumerable<SystemFact> Collect()
    {
        string? Reg(string key, string value)
        {
            try { return Registry.LocalMachine.OpenSubKey(key)?.GetValue(value)?.ToString(); }
            catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { return null; }
        }
        const string nt = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

        var build = Reg(nt, "CurrentBuild") ?? Environment.OSVersion.Version.Build.ToString();
        var edition = Reg(nt, "ProductName") ?? "Windows";
        // Windows 11 still reports "Windows 10" in ProductName; build 22000 and later is Windows 11.
        if (int.TryParse(build, out var b) && b >= 22000) edition = edition.Replace("Windows 10", "Windows 11");
        yield return new("Windows", "Edition", edition);
        if (Reg(nt, "DisplayVersion") is { } display) yield return new("Windows", "Version", display);
        yield return new("Windows", "Build", Reg(nt, "UBR") is { } ubr ? $"{build}.{ubr}" : build);
        yield return new("Windows", "Architecture", RuntimeInformation.OSArchitecture.ToString());
        yield return new("Windows", "Up Time", SystemReport.Uptime(TimeSpan.FromMilliseconds(Environment.TickCount64)));

        yield return new("Computer", "Name", Environment.MachineName);
        yield return new("Computer", "Processor", (Reg(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString") ?? "Unknown").Trim());
        yield return new("Computer", "Logical Processors", Environment.ProcessorCount.ToString());
        if (Memory() is var (total, free))
        {
            yield return new("Computer", "Memory", ByteSize.Format(total));
            yield return new("Computer", "Memory Available", ByteSize.Format(free));
        }

        foreach (var d in DriveInfo.GetDrives())
        {
            string? line = null;
            try
            {
                if (d.IsReady)
                    line = $"{ByteSize.Format(d.AvailableFreeSpace)} free of {ByteSize.Format(d.TotalSize)} ({d.DriveFormat}, {d.DriveType})";
            }
            catch (IOException) { }
            if (line is not null) yield return new("Drives", "Drive " + d.Name.TrimEnd('\\').TrimEnd(':'), line);
        }

        yield return new(AppInfo.Name, "Version", AppInfo.VersionName);
        yield return new(AppInfo.Name, "Install Type", Integration.IsInstalled ? "Installed (MSI)" : "Portable");
        yield return new(AppInfo.Name, ".NET", RuntimeInformation.FrameworkDescription);
        yield return new(AppInfo.Name, "Running As", Elevation.IsAdministrator ? "Administrator" : "Standard user");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

    private static (long Total, long Free)? Memory()
    {
        var s = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        return GlobalMemoryStatusEx(ref s) ? ((long)s.TotalPhys, (long)s.AvailPhys) : null;
    }
}

/// <summary>One choice in System Information's report format dropdown.</summary>
public sealed record ReportFormatOption(ReportFormat Format, string Label)
{
    // The themed ComboBox shows the chosen item's text, not DisplayMemberPath.
    public override string ToString() => Label;
}

// ---------------------------------------------------------------- Scheduled Tasks

public sealed class ScheduledTaskItem : ObservableObject
{
    private readonly dynamic _task;

    public ScheduledTaskItem(dynamic task, string folder)
    {
        _task = task;
        Name = (string)task.Name;
        Folder = folder;
        Refresh();
    }

    public string Name { get; }
    public string Folder { get; }
    public bool IsWindowsTask => Folder.StartsWith(@"\Microsoft", StringComparison.OrdinalIgnoreCase);

    private bool _enabled;
    private string _status = "", _nextRun = "", _lastRun = "";
    public string Status { get => _status; private set => Set(ref _status, value); }
    public string NextRun { get => _nextRun; private set => Set(ref _nextRun, value); }
    public string LastRun { get => _lastRun; private set => Set(ref _lastRun, value); }

    private bool _isChecked;
    public bool IsChecked { get => _isChecked; set => Set(ref _isChecked, value); }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (!TrySetEnabled(value))
                Dialogs.Error("Changing this task needs administrator rights. Use Restart as Administrator at the top.");
        }
    }

    /// <returns>false when Windows refused the change (usually administrator rights).</returns>
    public bool TrySetEnabled(bool value)
    {
        if (value == _enabled) return true;
        try
        {
            _task.Enabled = value;
            return true;
        }
        catch (Exception e) when (ScheduledTasksViewModel.IsSchedulerError(e))
        {
            return false;
        }
        finally
        {
            Refresh();
            OnPropertyChanged(nameof(Enabled));
        }
    }

    private void Refresh()
    {
        string When(DateTime t, string none) => t.Year < 2000 ? none : t.ToString("g");
        try
        {
            _enabled = (bool)_task.Enabled;
            Status = ((int)_task.State) switch { 1 => "Disabled", 2 => "Queued", 3 => "Ready", 4 => "Running", _ => "Unknown" };
            NextRun = _enabled ? When((DateTime)_task.NextRunTime, "Not scheduled") : "";
            LastRun = When((DateTime)_task.LastRunTime, "Never");
        }
        catch (Exception e) when (ScheduledTasksViewModel.IsSchedulerError(e))
        {
            Status = "Unknown";
        }
    }
}

/// <summary>
/// Tools › Scheduled Tasks: programs Windows runs on a schedule. Turning a task off is reversible, like Task Scheduler's
/// Disable; Kodiak Cleaner never deletes tasks. Windows' own tasks are hidden unless Show Windows Tasks is on.
/// </summary>
public sealed class ScheduledTasksViewModel : ObservableObject, ILoadOnFirstVisit
{
    private List<ScheduledTaskItem> _all = new();

    public ScheduledTasksViewModel()
    {
        RefreshCommand = new RelayCommand(Refresh);
        EnableSelectedCommand = new RelayCommand(() => SetPicked(true), () => Items.Any(i => i.IsChecked && !i.Enabled));
        DisableSelectedCommand = new RelayCommand(() => SetPicked(false), () => Items.Any(i => i.IsChecked && i.Enabled));
        EnableRowCommand = new RelayCommand(() => SetRows(Selected, true), () => Selected is { Enabled: false });
        DisableRowCommand = new RelayCommand(() => SetRows(Selected, false), () => Selected is { Enabled: true });
    }

    public ObservableCollection<ScheduledTaskItem> Items { get; } = new();
    public ICommand RefreshCommand { get; }
    public ICommand EnableSelectedCommand { get; }
    public ICommand DisableSelectedCommand { get; }
    public ICommand EnableRowCommand { get; }
    public ICommand DisableRowCommand { get; }

    private ScheduledTaskItem? _selected;
    public ScheduledTaskItem? Selected { get => _selected; set => Set(ref _selected, value); }

    private bool _loaded;
    /// <summary>Loads the first time the page is shown (reading every task takes a moment).</summary>
    public void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        Refresh();
    }

    private bool _showWindowsTasks;
    public bool ShowWindowsTasks
    {
        get => _showWindowsTasks;
        set { if (Set(ref _showWindowsTasks, value)) Filter(); }
    }

    private string _summary = "";
    public string Summary { get => _summary; private set => Set(ref _summary, value); }

    public bool? AllChecked
    {
        get => Items.Count > 0 && Items.All(i => i.IsChecked) ? true : Items.Any(i => i.IsChecked) ? null : false;
        set
        {
            var on = value != false;
            foreach (var i in Items) i.IsChecked = on;
        }
    }

    private void Refresh()
    {
        _all = new List<ScheduledTaskItem>();
        try
        {
            var type = Type.GetTypeFromProgID("Schedule.Service") ?? throw new COMException("Task Scheduler isn't available.");
            dynamic service = Activator.CreateInstance(type)!;
            service.Connect();
            Walk(service.GetFolder(@"\"));
        }
        catch (Exception e) when (IsSchedulerError(e))
        {
            _all.Clear();
            Summary = "Scheduled tasks couldn't be read on this PC.";
            Filter(keepSummary: true);
            return;
        }
        _all.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        Filter();
    }

    /// <summary>What Task Scheduler's COM interface throws when it can't (access denied, not implemented, missing).</summary>
    internal static bool IsSchedulerError(Exception e) =>
        e is COMException or UnauthorizedAccessException or InvalidCastException or NotImplementedException
            or NotSupportedException or ArgumentException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException;

    private void Walk(dynamic folder)
    {
        string path = folder.Path;
        try
        {
            foreach (dynamic task in folder.GetTasks(1)) // 1 = include hidden tasks
                _all.Add(new ScheduledTaskItem(task, path));
            foreach (dynamic sub in folder.GetFolders(0))
                Walk(sub);
        }
        catch (Exception e) when (IsSchedulerError(e)) { } // folders this user can't read
    }

    private void Filter(bool keepSummary = false)
    {
        Items.Clear();
        foreach (var t in _all.Where(t => ShowWindowsTasks || !t.IsWindowsTask))
        {
            t.PropertyChanged -= Item_PropertyChanged;
            t.PropertyChanged += Item_PropertyChanged;
            Items.Add(t);
        }
        OnPropertyChanged(nameof(AllChecked));
        if (keepSummary) return;
        var hidden = _all.Count - Items.Count;
        Summary = Items.Count == 0 && hidden == 0 ? "No scheduled tasks found." :
            $"{Items.Count} task(s) shown" + (hidden > 0 ? $"; {hidden} Windows task(s) hidden." : ".") +
            " Turning a task off is reversible; Kodiak Cleaner never deletes tasks.";
    }

    private void Item_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScheduledTaskItem.IsChecked)) OnPropertyChanged(nameof(AllChecked));
    }

    private void SetPicked(bool enabled) => SetRows(Items.Where(i => i.IsChecked).ToList(), enabled);

    private static void SetRows(ScheduledTaskItem? item, bool enabled)
    {
        if (item is not null) SetRows(new List<ScheduledTaskItem> { item }, enabled);
    }

    private static void SetRows(List<ScheduledTaskItem> items, bool enabled)
    {
        var failed = items.Count(i => !i.TrySetEnabled(enabled));
        if (failed > 0)
            Dialogs.Error($"{failed} task(s) need administrator rights to change. Use Restart as Administrator at the top.");
    }
}
