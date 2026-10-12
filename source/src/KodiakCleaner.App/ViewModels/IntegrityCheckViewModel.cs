using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using KodiakCleaner.Core.Tools;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App.ViewModels;

/// <summary>One check on the Integrity Check page: its result chip, a one-line summary and progress while it runs.</summary>
public sealed class IntegrityCard : ObservableObject
{
    public IntegrityCard(string title, string description)
    {
        Title = title;
        Description = description;
    }

    public string Title { get; }
    /// <summary>What the check looks at, under its title.</summary>
    public string Description { get; }

    private IntegrityResult _result;
    public IntegrityResult Result
    {
        get => _result;
        set
        {
            if (!Set(ref _result, value)) return;
            OnPropertyChanged(nameof(ResultName));
            OnPropertyChanged(nameof(Tone));
        }
    }

    public string ResultName => IntegrityResultText.Name(Result);
    public string Tone => IntegrityResultText.Tone(Result);

    private string _summary = "";
    public string Summary
    {
        get => _summary;
        set
        {
            if (Set(ref _summary, value)) OnPropertyChanged(nameof(HasSummary));
        }
    }
    public bool HasSummary => _summary.Length > 0;

    private string _lastRun = "";
    /// <summary>"Checked 3:45 PM" or "Repaired Oct 12, 3:45 PM".</summary>
    public string LastRun
    {
        get => _lastRun;
        set
        {
            if (Set(ref _lastRun, value)) OnPropertyChanged(nameof(HasLastRun));
        }
    }
    public bool HasLastRun => _lastRun.Length > 0;

    private bool _isRunning;
    public bool IsRunning { get => _isRunning; set => Set(ref _isRunning, value); }

    private double _percent;
    public double Percent { get => _percent; set => Set(ref _percent, value); }

    private bool _hasPercent;
    /// <summary>A real percentage from Windows' tool; without one the bar just moves.</summary>
    public bool HasPercent { get => _hasPercent; set => Set(ref _hasPercent, value); }

    public void Start(string summary)
    {
        IsRunning = true;
        Percent = 0;
        HasPercent = false;
        Summary = summary;
    }

    public void Finish(IntegrityResult result, string summary, string verb)
    {
        IsRunning = false;
        Result = result;
        Summary = summary;
        LastRun = $"{verb} {DateTime.Now:g}";
    }
}

/// <summary>A drive in the Drive File System list.</summary>
public sealed class FileSystemRow : ObservableObject
{
    public FileSystemRow(OptimizeDrive drive, bool windowsDrive)
    {
        Drive = drive;
        IsWindowsDrive = windowsDrive;
        CanCheck = IntegrityCheck.CanCheck(drive.Format);
        _isChecked = CanCheck && drive.Kind != MediaKind.FlashDrive;
        _status = CanCheck ? "Not Checked" : drive.Format.Equals("ReFS", StringComparison.OrdinalIgnoreCase) ? "Checks Itself (ReFS)" : "Not Supported";
    }

    public OptimizeDrive Drive { get; }
    public bool IsWindowsDrive { get; }
    public bool CanCheck { get; }
    public string Display => Drive.Display + (IsWindowsDrive ? "  (Windows)" : "");
    public string Size => ByteSize.Format(Drive.Size);

    private bool _isChecked;
    public bool IsChecked { get => _isChecked; set => Set(ref _isChecked, value && CanCheck); }

    private IntegrityResult _result;
    public IntegrityResult Result
    {
        get => _result;
        set
        {
            if (Set(ref _result, value)) OnPropertyChanged(nameof(Tone));
        }
    }
    public string Tone => IntegrityResultText.Tone(Result);

    private string _status;
    public string Status { get => _status; set => Set(ref _status, value); }
}

/// <summary>
/// Integrity Check (Ted, 2026-10-12): checks and repairs Windows' system files (sfc) and the component store they are repaired
/// from (DISM), checks and repairs drives' file systems (chkdsk), reads each drive's own health report and the drive errors
/// Windows logged, and runs Windows' Memory Diagnostic. Everything uses Windows' own tools; one run at a time.
/// </summary>
public sealed class IntegrityCheckViewModel : ObservableObject, ILoadOnFirstVisit
{
    private const int HistoryDays = 30;
    private CancellationTokenSource? _cts;
    private readonly List<string> _output = new();

    public IntegrityCheckViewModel()
    {
        CheckEverythingCommand = new RelayCommand(CheckEverythingAsync, () => !IsBusy);
        StopCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy && CanStop);
        OpenReportsCommand = new RelayCommand(() => Elevation.OpenFolder(ReportsFolder));
        RestartCommand = new RelayCommand(RestartNow, () => !IsBusy);

        CheckHistoryCommand = new RelayCommand(() => Run(CheckHistoryAsync), () => !IsBusy);
        CheckDisksCommand = new RelayCommand(() => Run(CheckDisksAsync), () => !IsBusy);
        RefreshDrivesCommand = new RelayCommand(RefreshDrivesAsync, () => !IsBusy);
        CheckFileSystemCommand = new RelayCommand(() => Run(() => FileSystemAsync(repair: false)), () => !IsBusy && Drives.Any(d => d.IsChecked));
        RepairFileSystemCommand = new RelayCommand(RepairFileSystemAsync, () => !IsBusy && Drives.Any(d => d.IsChecked));
        TestMemoryCommand = new RelayCommand(TestMemory, () => !IsBusy);
        CheckMemoryCommand = new RelayCommand(() => Run(CheckMemoryAsync), () => !IsBusy);
        CheckComponentStoreCommand = new RelayCommand(() => Run(() => ComponentStoreAsync(repair: false)), () => !IsBusy);
        RepairComponentStoreCommand = new RelayCommand(RepairComponentStoreAsync, () => !IsBusy);
        CheckSystemFilesCommand = new RelayCommand(() => Run(() => SystemFilesAsync(repair: false)), () => !IsBusy);
        RepairSystemFilesCommand = new RelayCommand(RepairSystemFilesAsync, () => !IsBusy);
    }

    private bool _loaded;
    public void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _ = RefreshDrivesAsync();
    }

    private static string ReportsFolder => Path.Combine(Settings.Data.Reports, "Integrity Check");
    public bool IsAdministrator => Elevation.IsAdministrator;

    // Cards, A to Z as on the page.
    public IntegrityCard ComponentStore { get; } = new("Windows Component Store",
        "Where Windows keeps clean copies of its own files. Windows System Files repairs come from here, so repair this first when they can't be fixed. Repair may download clean copies from Windows Update.");
    public IntegrityCard DiskHealthCard { get; } = new("Drive Health",
        "Each drive keeps its own health report: whether it expects to fail, its temperature, how worn it is and errors it couldn't correct. Read-only.");
    public IntegrityCard FileSystem { get; } = new("Drive File System",
        "Looks for file system errors on the ticked drives while you keep working. Repair fixes them; the Windows drive is repaired at the next restart, other drives right away.");
    public IntegrityCard History { get; } = new("Drive Error History",
        "Reads the drive and file system errors Windows logged in the last 30 days: bad blocks, drives that stopped answering, damaged file systems. Read-only.");
    public IntegrityCard Memory { get; } = new("Memory Test",
        "Faulty memory causes random crashes and damaged files. Windows' Memory Diagnostic tests it during a restart; Check Last Result shows what it found.");
    public IntegrityCard SystemFiles { get; } = new("Windows System Files",
        "Checks every protected Windows file against Windows' own record. Repair puts back a clean copy of any that are damaged or changed. Your files and programs aren't touched.");

    public ObservableCollection<FileSystemRow> Drives { get; } = new();
    public ObservableCollection<DiskHealth> Disks { get; } = new();
    public ObservableCollection<DriveEventGroup> Events { get; } = new();
    public bool HasDisks => Disks.Count > 0;
    public bool HasEvents => Events.Count > 0;

    public ICommand CheckEverythingCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand OpenReportsCommand { get; }
    public ICommand RestartCommand { get; }
    public ICommand CheckHistoryCommand { get; }
    public ICommand CheckDisksCommand { get; }
    public ICommand RefreshDrivesCommand { get; }
    public ICommand CheckFileSystemCommand { get; }
    public ICommand RepairFileSystemCommand { get; }
    public ICommand TestMemoryCommand { get; }
    public ICommand CheckMemoryCommand { get; }
    public ICommand CheckComponentStoreCommand { get; }
    public ICommand RepairComponentStoreCommand { get; }
    public ICommand CheckSystemFilesCommand { get; }
    public ICommand RepairSystemFilesCommand { get; }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }

    private bool _canStop;
    /// <summary>Checks can be stopped; repairs can't (stopping Windows halfway through a repair is worse than waiting).</summary>
    public bool CanStop { get => _canStop; private set => Set(ref _canStop, value); }

    private string _status = "";
    public string Status { get => _status; private set => Set(ref _status, value); }

    private bool _restartNeeded;
    /// <summary>Shows the Restart Now banner: a repair is waiting for the next start.</summary>
    public bool RestartNeeded { get => _restartNeeded; private set => Set(ref _restartNeeded, value); }

    private bool _lookForBadSectors;
    /// <summary>Repair also reads every sector (chkdsk /r). Can take hours on big hard drives.</summary>
    public bool LookForBadSectors { get => _lookForBadSectors; set => Set(ref _lookForBadSectors, value); }

    private string _detailsTitle = "Details";
    public string DetailsTitle { get => _detailsTitle; private set => Set(ref _detailsTitle, value); }

    private string _details = "Windows' own messages from each check show here.";
    public string Details { get => _details; private set => Set(ref _details, value); }

    public bool? AllDrivesChecked
    {
        get => Drives.Any(d => d.CanCheck) && Drives.Where(d => d.CanCheck).All(d => d.IsChecked) ? true : Drives.Any(d => d.IsChecked) ? null : false;
        set
        {
            bool on = value ?? false;
            foreach (var d in Drives) d.IsChecked = on;
        }
    }

    // ---------- Running ----------

    /// <summary>Runs one check (or several in a row) with the page marked busy.</summary>
    private async Task Run(Func<Task> work, bool canStop = true)
    {
        if (IsBusy) return;
        IsBusy = true;
        CanStop = canStop;
        _cts = new CancellationTokenSource();
        try { await work(); }
        catch (OperationCanceledException) { Status = "Stopped."; }
        finally
        {
            IsBusy = false;
            CanStop = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void BeginDetails(string title)
    {
        lock (_output) _output.Clear();
        DetailsTitle = "Details: " + title;
        Details = "";
    }

    private void AddDetail(string line)
    {
        lock (_output)
        {
            // Progress lines repeat with a new percent; keep only the newest of a run of them.
            if (_output.Count > 0 && IntegrityCheck.PercentOf(line) is not null && IntegrityCheck.PercentOf(_output[^1]) is not null)
                _output[^1] = line;
            else _output.Add(line);
            if (_output.Count > 400) _output.RemoveRange(0, _output.Count - 400);
        }
        Application.Current?.Dispatcher.BeginInvoke(() => { lock (_output) Details = string.Join(Environment.NewLine, _output); });
    }

    private bool CheckAdmin(string what)
    {
        if (Elevation.IsAdministrator) return true;
        Dialogs.Error($"{what} uses Windows' own tool, which needs administrator rights. Use Restart as Administrator, then try again.");
        return false;
    }

    /// <summary>Runs a Windows tool for a card, with progress and Details, and saves its output under Reports.</summary>
    private async Task<ToolRun> RunTool(IntegrityCard card, string exe, string[] args, string what, string? input = null, bool fresh = true)
    {
        if (fresh) BeginDetails(card.Title);
        AddDetail($"> {exe} {string.Join(" ", args)}");
        var pct = new Progress<int>(v =>
        {
            card.HasPercent = true;
            card.Percent = v;
            Status = $"{card.Title}: {what}… {v}%";
        });
        Status = $"{card.Title}: {what}…";
        return await IntegrityCheck.RunAsync(exe, args, new LineReport(AddDetail), pct,
            _cts?.Token ?? CancellationToken.None, input);
    }

    private sealed class LineReport(Action<string> report) : IProgress<string>
    {
        public void Report(string value) => report(value);
    }

    private void Save(IntegrityCard card, string what, ToolRun run, IntegrityResult result)
    {
        try { IntegrityCheck.SaveReport(Settings.Data.Reports, card.Title, what, run.Lines, result, DateTime.Now); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        ActivityLog.Write("Integrity Check", $"{card.Title}: {what}, {IntegrityResultText.Name(result)} (exit code {run.ExitCode})");
        if (result == IntegrityResult.RestartNeeded) RestartNeeded = true;
    }

    // ---------- Check Everything ----------

    /// <summary>Every read-only check in a sensible order; the ones that need administrator rights say so when it's missing.</summary>
    private Task CheckEverythingAsync() => Run(async () =>
    {
        await CheckHistoryAsync();
        await CheckDisksAsync();
        await CheckMemoryAsync();
        if (!Elevation.IsAdministrator)
        {
            foreach (var c in new[] { FileSystem, ComponentStore, SystemFiles })
                c.Summary = "Needs administrator rights. Use Restart as Administrator, then check again.";
            Status = "Done. Three checks need administrator rights; restart Kodiak Cleaner as administrator to run them.";
            return;
        }
        await FileSystemAsync(repair: false);
        await ComponentStoreAsync(repair: false);
        await SystemFilesAsync(repair: false);
        var cards = new[] { ComponentStore, DiskHealthCard, FileSystem, History, Memory, SystemFiles };
        int problems = cards.Count(c => c.Result is IntegrityResult.ProblemsFound or IntegrityResult.Attention);
        Status = problems == 0 ? "Done. No problems found." : $"Done. {problems} {(problems == 1 ? "check found something" : "checks found something")} worth a look.";
    });

    // ---------- Drive Error History ----------

    private async Task CheckHistoryAsync()
    {
        History.Start($"Reading the drive problems Windows logged in the last {HistoryDays} days…");
        Status = "Reading Windows' System log…";
        var xml = await Task.Run(() => IntegrityCheck.QueryEvents(IntegrityCheck.DriveEventQueryArgs(HistoryDays)));
        Events.Clear();
        if (xml is null)
        {
            History.Finish(IntegrityResult.CouldNotCheck, "Windows' System log couldn't be read. Try again as administrator.", "Checked");
        }
        else
        {
            foreach (var g in IntegrityCheck.ParseDriveEvents(xml)) Events.Add(g);
            int total = Events.Sum(e => e.Count);
            bool serious = Events.Any(e => e.Source is "NTFS" || (e.Source == "Disk" && e.EventId is 7 or 51 or 52 or 154));
            History.Finish(total == 0 ? IntegrityResult.Healthy : serious ? IntegrityResult.ProblemsFound : IntegrityResult.Attention,
                total == 0 ? $"No drive problems logged in the last {HistoryDays} days."
                    : $"{total} drive {(total == 1 ? "problem" : "problems")} logged in the last {HistoryDays} days. Unplugging a USB drive without ejecting it shows up here too.",
                "Checked");
        }
        OnPropertyChanged(nameof(HasEvents));
        Status = "";
    }

    // ---------- Drive Health ----------

    private async Task CheckDisksAsync()
    {
        DiskHealthCard.Start("Asking each drive for its own health report…");
        Status = "Reading drive health…";
        var disks = await Task.Run(IntegrityCheck.ReadDiskHealth);
        Disks.Clear();
        foreach (var d in disks) Disks.Add(d);
        OnPropertyChanged(nameof(HasDisks));
        if (disks.Count == 0)
            DiskHealthCard.Finish(IntegrityResult.CouldNotCheck, "Windows didn't share any drive health on this PC.", "Checked");
        else
        {
            var worst = disks.Select(d => d.Result).Contains(IntegrityResult.ProblemsFound) ? IntegrityResult.ProblemsFound
                : disks.Any(d => d.Result == IntegrityResult.Attention) ? IntegrityResult.Attention
                : disks.All(d => d.Result == IntegrityResult.CouldNotCheck) ? IntegrityResult.CouldNotCheck : IntegrityResult.Healthy;
            var extra = Elevation.IsAdministrator ? "" : " Temperature, wear and errors show when Kodiak Cleaner runs as administrator.";
            DiskHealthCard.Finish(worst, worst switch
            {
                IntegrityResult.ProblemsFound => "A drive reports it is failing. Back up its files now.",
                IntegrityResult.Attention => "A drive is worth watching. See the list below.",
                IntegrityResult.Healthy => $"All {disks.Count} {(disks.Count == 1 ? "drive reports" : "drives report")} healthy.",
                _ => "The drives didn't share their health.",
            } + extra, "Checked");
        }
        Status = "";
    }

    // ---------- Drive File System ----------

    private async Task RefreshDrivesAsync()
    {
        var drives = await Task.Run(DriveOptimizer.ListDrives);
        var windows = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        Drives.Clear();
        foreach (var d in drives.OrderBy(d => d.Root, StringComparer.OrdinalIgnoreCase))
        {
            var row = new FileSystemRow(d, d.Root.Equals(windows, StringComparison.OrdinalIgnoreCase));
            row.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(FileSystemRow.IsChecked)) OnPropertyChanged(nameof(AllDrivesChecked)); };
            Drives.Add(row);
        }
        OnPropertyChanged(nameof(AllDrivesChecked));
    }

    private async Task RepairFileSystemAsync()
    {
        if (!CheckAdmin("Drive File System")) return;
        var rows = Drives.Where(d => d.IsChecked).ToList();
        if (rows.Count == 0) return;
        var names = string.Join(", ", rows.Select(r => r.Drive.Letter + ":"));
        var now = rows.Where(r => !r.IsWindowsDrive).ToList();
        var message = $"Kodiak Cleaner will ask Windows to find and fix file system errors on {names}.";
        if (rows.Any(r => r.IsWindowsDrive))
            message += " The Windows drive is in use, so its repair runs the next time the PC restarts, before Windows starts.";
        if (now.Count > 0)
            message += $" {string.Join(", ", now.Select(r => r.Drive.Letter + ":"))} {(now.Count == 1 ? "is" : "are")} locked while it's repaired, so close any files open on {(now.Count == 1 ? "it" : "them")} first.";
        var caution = LookForBadSectors
            ? "Look for Bad Sectors reads every part of the drive. On a large hard drive this can take several hours, and it can't be stopped once started."
            : "A repair can't be stopped once started. Files Windows can't save are moved to a found.000 folder on the drive.";
        if (!Dialogs.Warn(message, "Repair Drive File System", caution)) return;
        await Run(() => FileSystemAsync(repair: true), canStop: false);
    }

    private async Task FileSystemAsync(bool repair)
    {
        if (!repair && !CheckAdminQuiet(FileSystem)) return;
        var rows = Drives.Where(d => d.IsChecked && d.CanCheck).ToList();
        if (rows.Count == 0) return;
        FileSystem.Start(repair ? "Repairing…" : "Checking…");
        var results = new List<IntegrityResult>();
        int n = 0;
        foreach (var row in rows)
        {
            n++;
            var what = repair ? (LookForBadSectors ? "Repair and Look for Bad Sectors" : "Repair") : "Check";
            row.Status = repair ? "Repairing…" : "Checking…";
            FileSystem.Summary = $"{(repair ? "Repairing" : "Checking")} {row.Drive.Letter}: (drive {n} of {rows.Count})…";
            var args = IntegrityCheck.ChkdskArgs(row.Drive.Letter, row.Drive.Format, repair, LookForBadSectors, row.IsWindowsDrive);
            try
            {
                // chkdsk asks "schedule this volume to be checked the next time the system restarts? (Y/N)" for a drive in use.
                var run = await RunTool(FileSystem, "chkdsk.exe", args, $"{what} {row.Drive.Letter}:", repair ? "Y\r\nY\r\n" : null, fresh: n == 1);
                bool scheduled = repair && IntegrityCheck.IsScheduledAtRestart(row.Drive.Letter);
                var result = IntegrityCheck.ReadChkdsk(run.ExitCode, repair, scheduled);
                row.Result = result;
                row.Status = result switch
                {
                    IntegrityResult.Healthy => "No Problems",
                    IntegrityResult.Repaired => "Repaired",
                    IntegrityResult.RestartNeeded => "Repairs at Next Restart",
                    IntegrityResult.ProblemsFound => repair ? "Problems Remain" : "Problems Found, Repair Recommended",
                    _ => $"Couldn't Check (Code {run.ExitCode})",
                };
                Save(FileSystem, $"{what} {row.Drive.Letter}:", run, result);
                results.Add(result);
            }
            catch (OperationCanceledException)
            {
                row.Status = "Stopped";
                throw;
            }
            catch (Exception e) when (e is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                row.Status = "Couldn't Check";
                row.Result = IntegrityResult.CouldNotCheck;
                results.Add(IntegrityResult.CouldNotCheck);
                AddDetail(e.Message);
            }
        }
        var overall = results.Contains(IntegrityResult.ProblemsFound) ? IntegrityResult.ProblemsFound
            : results.Contains(IntegrityResult.RestartNeeded) ? IntegrityResult.RestartNeeded
            : results.Contains(IntegrityResult.CouldNotCheck) ? IntegrityResult.CouldNotCheck
            : results.Contains(IntegrityResult.Repaired) ? IntegrityResult.Repaired : IntegrityResult.Healthy;
        FileSystem.Finish(overall, overall switch
        {
            IntegrityResult.ProblemsFound => repair ? "Some problems couldn't be fixed. Back up the drive's files; it may be failing."
                : "Problems found. Tick the drive and choose Repair.",
            IntegrityResult.RestartNeeded => "The Windows drive is repaired the next time the PC restarts.",
            IntegrityResult.CouldNotCheck => "A drive couldn't be checked. See Details.",
            IntegrityResult.Repaired => "Errors were found and fixed.",
            _ => $"No file system errors on {rows.Count} {(rows.Count == 1 ? "drive" : "drives")}.",
        }, repair ? "Repaired" : "Checked");
        Status = "";
    }

    /// <summary>For Check Everything and single checks: mark the card instead of showing a message.</summary>
    private bool CheckAdminQuiet(IntegrityCard card)
    {
        if (Elevation.IsAdministrator) return true;
        card.Finish(IntegrityResult.NotChecked, "Needs administrator rights. Use Restart as Administrator, then check again.", "Tried");
        return false;
    }

    // ---------- Memory Test ----------

    private void TestMemory()
    {
        if (!Dialogs.Confirm("Windows' Memory Diagnostic tests this PC's memory during a restart, before Windows starts (10 to 20 minutes, " +
                             "longer with a lot of memory). It asks whether to restart now or at the next start. Save your work first. " +
                             "Afterwards, come back here and choose Check Last Result.", "Test Memory")) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(IntegrityCheck.Sys("mdsched.exe")) { UseShellExecute = true });
            ActivityLog.Write("Integrity Check", "Memory Test: opened Windows' Memory Diagnostic");
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            if (e.NativeErrorCode != 1223) Dialogs.Error("Windows' Memory Diagnostic couldn't be opened: " + e.Message); // 1223 = UAC prompt declined
        }
    }

    private async Task CheckMemoryAsync()
    {
        Memory.Start("Looking for the last Memory Diagnostic result…");
        var xml = await Task.Run(() => IntegrityCheck.QueryEvents(IntegrityCheck.MemoryQueryArgs));
        var last = xml is null ? null : IntegrityCheck.ParseMemoryResult(xml);
        if (xml is null) Memory.Finish(IntegrityResult.CouldNotCheck, "Windows' System log couldn't be read.", "Checked");
        else if (last is null) Memory.Finish(IntegrityResult.NotChecked, "No memory test has run on this PC yet. Choose Test Memory to run one.", "Checked");
        else if (last.ErrorsFound)
            Memory.Finish(IntegrityResult.ProblemsFound, $"The memory test on {last.When:D} found errors. A memory module may be failing; test again, then try one module at a time.", "Checked");
        else Memory.Finish(IntegrityResult.Healthy, $"The memory test on {last.When:D} found no errors.", "Checked");
    }

    // ---------- Windows Component Store ----------

    private async Task RepairComponentStoreAsync()
    {
        if (!CheckAdmin("Windows Component Store")) return;
        if (!Dialogs.Warn("Kodiak Cleaner will ask Windows to repair the store it keeps clean copies of its own files in (System Files repairs come from here). " +
                          "This takes 10 to 30 minutes.", "Repair Windows Component Store",
                "Windows may download clean copies from Windows Update, so stay online. A repair can't be stopped once started.")) return;
        await Run(() => ComponentStoreAsync(repair: true), canStop: false);
    }

    private async Task ComponentStoreAsync(bool repair)
    {
        if (!repair && !CheckAdminQuiet(ComponentStore)) return;
        var what = repair ? "Repair" : "Check";
        ComponentStore.Start(repair ? "Repairing. This can take 10 to 30 minutes…" : "Checking. This can take 5 to 15 minutes…");
        ToolRun run;
        try { run = await RunTool(ComponentStore, "dism.exe", IntegrityCheck.DismArgs(repair), what); }
        catch (Exception e) when (e is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            AddDetail(e.Message);
            ComponentStore.Finish(IntegrityResult.CouldNotCheck, "Windows' dism.exe couldn't be started on this PC. See Details.", repair ? "Tried" : "Checked");
            return;
        }
        var result = IntegrityCheck.ReadDism(run.ExitCode, run.Lines, repair);
        Save(ComponentStore, what, run, result);
        ComponentStore.Finish(result, result switch
        {
            IntegrityResult.Healthy => repair ? "Repair finished. Nothing was left to fix." : "No damage found.",
            IntegrityResult.Repaired => "Damage was found and repaired. Repair Windows System Files next.",
            IntegrityResult.RestartNeeded => "Restart the PC to finish the repair.",
            IntegrityResult.ProblemsFound when repair => "Windows couldn't get clean copies to repair with. Check the internet connection and try again. " +
                                                         (IntegrityCheck.DismError(run.Lines) ?? ""),
            IntegrityResult.ProblemsFound => "Damage found. Choose Repair.",
            IntegrityResult.Finished => "Finished. See Details for Windows' message.",
            _ => $"Windows' tool stopped with code {run.ExitCode}. See Details.",
        }, repair ? "Repaired" : "Checked");
        Status = "";
    }

    // ---------- Windows System Files ----------

    private async Task RepairSystemFilesAsync()
    {
        if (!CheckAdmin("Windows System Files")) return;
        if (!Dialogs.Confirm("Kodiak Cleaner will ask Windows to check every protected Windows file and put back a clean copy of any that are damaged " +
                             "or changed. This takes 10 to 30 minutes and can't be stopped once started. Your own files, settings and programs aren't touched.",
                "Repair Windows System Files")) return;
        await Run(async () =>
        {
            await SystemFilesAsync(repair: true);
            if (SystemFiles.Result != IntegrityResult.ProblemsFound) return;
            // Windows repairs system files from the component store, so a damaged store has to be fixed first.
            if (!Dialogs.Confirm(
                    "Windows couldn't fix some files because its own clean copies are damaged too. Repair the Windows Component Store " +
                    "(Windows may download clean copies from Windows Update), then repair System Files again?", "Repair Further")) return;
            await ComponentStoreAsync(repair: true);
            if (ComponentStore.Result is IntegrityResult.Healthy or IntegrityResult.Repaired) await SystemFilesAsync(repair: true);
        }, canStop: false);
    }

    private async Task SystemFilesAsync(bool repair)
    {
        if (!repair && !CheckAdminQuiet(SystemFiles)) return;
        var what = repair ? "Repair" : "Check";
        SystemFiles.Start(repair ? "Repairing. This can take 10 to 30 minutes…" : "Checking. This can take 5 to 15 minutes…");
        ToolRun run;
        try { run = await RunTool(SystemFiles, "sfc.exe", IntegrityCheck.SfcArgs(repair), what); }
        catch (Exception e) when (e is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            AddDetail(e.Message);
            SystemFiles.Finish(IntegrityResult.CouldNotCheck, "Windows' sfc.exe couldn't be started on this PC. See Details.", repair ? "Tried" : "Checked");
            return;
        }
        var result = IntegrityCheck.ReadSfc(run.Lines, repair);
        Save(SystemFiles, what, run, result);
        SystemFiles.Finish(result, result switch
        {
            IntegrityResult.Healthy => "No damaged or changed Windows files.",
            IntegrityResult.Repaired => "Damaged files were found and repaired.",
            IntegrityResult.RestartNeeded => "Restart the PC to finish the repair, then check again.",
            IntegrityResult.ProblemsFound when repair => "Some files couldn't be repaired. Repair the Windows Component Store, then repair System Files again.",
            IntegrityResult.ProblemsFound => "Damaged or changed files found. Choose Repair.",
            IntegrityResult.Finished => "Finished. See Details for Windows' message.",
            _ => "Windows couldn't run the check. Restart the PC and try again.",
        }, repair ? "Repaired" : "Checked");
        Status = "";
    }

    // ---------- Restart ----------

    private void RestartNow()
    {
        if (!Dialogs.Confirm("Restart the PC now to finish the repairs? Save your work first.", "Restart Now")) return;
        ActivityLog.Write("Integrity Check", "Restarted to finish repairs");
        DriverTools.Restart();
    }
}
