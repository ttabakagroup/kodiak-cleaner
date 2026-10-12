using System.Collections.ObjectModel;
using System.IO;
using System.IO.Enumeration;
using System.Windows.Input;
using System.Windows.Threading;
using KodiakCleaner.Core.Tools;
using KodiakCleaner.Core.Util;
using Microsoft.Win32;

namespace KodiakCleaner.App.ViewModels;

/// <summary>A dropdown choice; ToString is the label so the closed dropdown shows it too.</summary>
public sealed record LabeledChoice<T>(T Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>One file in the Mass Rename preview: its name now and the name it would get.</summary>
public sealed class RenameRow : ObservableObject
{
    public RenameRow(RenameFile file) => File = file;
    public RenameFile File { get; set; }
    public string Path => File.Path;
    public string Name => File.Name;
    /// <summary>The folder below the chosen one, for Include Subfolders ("" for the chosen folder itself).</summary>
    public string Subfolder { get; set; } = "";

    private bool _isChecked = true;
    public bool IsChecked { get => _isChecked; set => Set(ref _isChecked, value); }

    private string _newName = "";
    public string NewName { get => _newName; set => Set(ref _newName, value); }

    private RenameState _state;
    public RenameState State
    {
        get => _state;
        set { if (Set(ref _state, value)) { OnPropertyChanged(nameof(IsProblem)); OnPropertyChanged(nameof(Status)); } }
    }

    private string _note = "";
    public string Note { get => _note; set { if (Set(ref _note, value)) OnPropertyChanged(nameof(Status)); } }

    public bool IsProblem => State == RenameState.Problem;
    public string Status => State switch
    {
        RenameState.Ready => Note.Length > 0 ? Note : "Ready",
        _ => Note,
    };
}

/// <summary>
/// Tools › Mass Rename (Ted, 2026-10-12): renames a set of files in a folder by a pattern, with Find and Replace, letter case,
/// numbering and extension options. The table shows every new name before anything changes, problems (a name Windows won't
/// take, two files getting the same name, a name already used) are flagged and skipped, and Undo Last Rename puts the names
/// back from a journal in Backups\Mass Rename. Windows, program folders and AppData are never offered.
/// </summary>
public sealed class MassRenameViewModel : ObservableObject
{
    public const int Limit = 20000;
    private readonly DispatcherTimer _refresh;
    private CancellationTokenSource? _cts;
    private Dictionary<string, HashSet<string>> _existing = new(StringComparer.OrdinalIgnoreCase);
    private bool _takenRead;

    public MassRenameViewModel()
    {
        var saved = Settings.Current.MassRename;
        _pattern = saved.Pattern;
        _find = saved.Find;
        _replace = saved.Replace;
        _matchCase = saved.MatchCase;
        _useRegex = saved.UseRegex;
        _case = saved.Case;
        _extension = saved.Extension;
        _newExtension = saved.NewExtension;
        _tidySpaces = saved.TidySpaces;
        _start = saved.Start;
        _step = saved.Step;
        _digits = saved.Digits;
        _order = saved.Order;
        _folder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

        _refresh = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _refresh.Tick += (_, _) => { _refresh.Stop(); UpdatePreview(); };

        BrowseCommand = new RelayCommand(Browse, () => !IsBusy);
        LoadCommand = new RelayCommand(LoadAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(Folder));
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy);
        RenameCommand = new RelayCommand(RenameAsync, () => !IsBusy && ReadyCount > 0);
        UndoCommand = new RelayCommand(UndoAsync, () => !IsBusy && CanUndo);
        ResetCommand = new RelayCommand(ResetRules, () => !IsBusy);
        ExampleCommand = new RelayCommand<string>(p => Pattern = p);
        ShowRowCommand = new RelayCommand(() => { if (Selected is { } s) Elevation.ShowInExplorer(s.Path); }, () => Selected is not null);
        CheckRowsCommand = new RelayCommand(() => SetChecked(true), () => Selected is not null);
        UncheckRowsCommand = new RelayCommand(() => SetChecked(false), () => Selected is not null);
    }

    public ObservableCollection<RenameRow> Rows { get; } = new();

    // Choices, A to Z.
    public IReadOnlyList<LabeledChoice<RenameCase>> CaseChoices { get; } = new[]
    {
        new LabeledChoice<RenameCase>(RenameCase.AsIs, "As Is"),
        new LabeledChoice<RenameCase>(RenameCase.Lower, "Lower Case"),
        new LabeledChoice<RenameCase>(RenameCase.Sentence, "Sentence Case"),
        new LabeledChoice<RenameCase>(RenameCase.Title, "Title Case"),
        new LabeledChoice<RenameCase>(RenameCase.Upper, "Upper Case"),
    };
    public IReadOnlyList<LabeledChoice<RenameExtension>> ExtensionChoices { get; } = new[]
    {
        new LabeledChoice<RenameExtension>(RenameExtension.AsIs, "As Is"),
        new LabeledChoice<RenameExtension>(RenameExtension.ChangeTo, "Change To"),
        new LabeledChoice<RenameExtension>(RenameExtension.Lower, "Lower Case"),
        new LabeledChoice<RenameExtension>(RenameExtension.Upper, "Upper Case"),
    };
    public IReadOnlyList<LabeledChoice<RenameOrder>> OrderChoices { get; } = new[]
    {
        new LabeledChoice<RenameOrder>(RenameOrder.DateCreated, "Date Created"),
        new LabeledChoice<RenameOrder>(RenameOrder.DateModified, "Date Modified"),
        new LabeledChoice<RenameOrder>(RenameOrder.Name, "Name"),
        new LabeledChoice<RenameOrder>(RenameOrder.Size, "Size"),
    };

    /// <summary>Ready-made patterns, A to Z by name.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Examples { get; } = new[]
    {
        new KeyValuePair<string, string>("Folder Name and Number", "{folder} {num:3}"),
        new KeyValuePair<string, string>("Number First", "{num:2} - {name}"),
        new KeyValuePair<string, string>("Original Name", "{name}"),
        new KeyValuePair<string, string>("Photos by Date Taken", "{taken} {num:3}"),
    };

    public string TokenHelp { get; } = string.Join("\n", MassRenamer.Tokens.Select(t => $"{t.Token}  {t.Meaning}"));

    public ICommand BrowseCommand { get; }
    public ICommand LoadCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand RenameCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand ExampleCommand { get; }
    public ICommand ShowRowCommand { get; }
    public ICommand CheckRowsCommand { get; }
    public ICommand UncheckRowsCommand { get; }

    private RenameRow? _selected;
    public RenameRow? Selected { get => _selected; set => Set(ref _selected, value); }

    /// <summary>Highlighted rows (the table allows Ctrl and Shift-click); the right-click menu acts on all of them.</summary>
    public IList<RenameRow> Highlighted { get; set; } = Array.Empty<RenameRow>();

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }

    private string _summary = "Choose a folder and select Load Files. Nothing is renamed until you select Rename, and every new name is shown here first.";
    public string Summary { get => _summary; private set => Set(ref _summary, value); }

    private string _folder;
    public string Folder { get => _folder; set => Set(ref _folder, value); }

    private string _filter = "";
    /// <summary>Which files to load, like "*.jpg; *.png" (empty = every file).</summary>
    public string Filter { get => _filter; set => Set(ref _filter, value); }

    private bool _includeSubfolders;
    public bool IncludeSubfolders { get => _includeSubfolders; set => Set(ref _includeSubfolders, value); }

    private string _pattern;
    public string Pattern { get => _pattern; set { if (Set(ref _pattern, value ?? "")) RulesChanged(); } }
    private string _find;
    public string Find { get => _find; set { if (Set(ref _find, value ?? "")) RulesChanged(); } }
    private string _replace;
    public string Replace { get => _replace; set { if (Set(ref _replace, value ?? "")) RulesChanged(); } }
    private bool _matchCase;
    public bool MatchCase { get => _matchCase; set { if (Set(ref _matchCase, value)) RulesChanged(); } }
    private bool _useRegex;
    public bool UseRegex { get => _useRegex; set { if (Set(ref _useRegex, value)) RulesChanged(); } }
    private RenameCase _case;
    public RenameCase Case { get => _case; set { if (Set(ref _case, value)) RulesChanged(); } }
    private RenameExtension _extension;
    public RenameExtension Extension
    {
        get => _extension;
        set { if (Set(ref _extension, value)) { OnPropertyChanged(nameof(ChangingExtension)); RulesChanged(); } }
    }
    public bool ChangingExtension => Extension == RenameExtension.ChangeTo;
    private string _newExtension;
    public string NewExtension { get => _newExtension; set { if (Set(ref _newExtension, value ?? "")) RulesChanged(); } }
    private bool _tidySpaces;
    public bool TidySpaces { get => _tidySpaces; set { if (Set(ref _tidySpaces, value)) RulesChanged(); } }
    private int _start;
    public int Start { get => _start; set { if (Set(ref _start, value)) RulesChanged(); } }
    private int _step;
    public int Step { get => _step; set { if (Set(ref _step, value)) RulesChanged(); } }
    private int _digits;
    public int Digits { get => _digits; set { if (Set(ref _digits, Math.Clamp(value, 1, 12))) RulesChanged(); } }
    private RenameOrder _order;
    public RenameOrder Order { get => _order; set { if (Set(ref _order, value)) RulesChanged(); } }

    public bool? AllChecked
    {
        get => Rows.Count > 0 && Rows.All(r => r.IsChecked) ? true : Rows.Any(r => r.IsChecked) ? null : false;
        set
        {
            var on = value != false;
            foreach (var r in Rows) r.IsChecked = on;
        }
    }

    private int _readyCount;
    public int ReadyCount { get => _readyCount; private set => Set(ref _readyCount, value); }

    public bool ShowSubfolder => IncludeSubfolders && Rows.Any(r => r.Subfolder.Length > 0);

    private static string JournalPath => Path.Combine(Settings.Data.Backups, "Mass Rename", "last-rename.json");
    public bool CanUndo => File.Exists(JournalPath);

    public RenameRules Rules => new()
    {
        Pattern = Pattern, Find = Find, Replace = Replace, MatchCase = MatchCase, UseRegex = UseRegex, Case = Case,
        Extension = Extension, NewExtension = NewExtension, TidySpaces = TidySpaces, Start = Start, Step = Step,
        Digits = Digits, Order = Order,
    };

    private void RulesChanged()
    {
        _refresh.Stop();
        _refresh.Start();
    }

    private void SetChecked(bool on)
    {
        var rows = Highlighted.Count > 0 ? Highlighted : Selected is { } s ? new[] { s } : Array.Empty<RenameRow>();
        foreach (var r in rows.ToList()) r.IsChecked = on;
    }

    private void ResetRules()
    {
        var d = new RenameRules();
        Pattern = d.Pattern; Find = d.Find; Replace = d.Replace; MatchCase = d.MatchCase; UseRegex = d.UseRegex; Case = d.Case;
        Extension = d.Extension; NewExtension = d.NewExtension; TidySpaces = d.TidySpaces; Start = d.Start; Step = d.Step;
        Digits = d.Digits; Order = d.Order;
    }

    /// <summary>Folders whose file names programs and Windows rely on: never offered for renaming.</summary>
    private static IEnumerable<string> ProtectedFolders()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData");
        yield return AppContext.BaseDirectory;
        yield return Settings.Data.Root;
    }

    private static string? WhyNotAllowed(string folder)
    {
        string full;
        try { full = Path.GetFullPath(folder); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return "That isn't a folder path."; }
        if (!Directory.Exists(full)) return "That folder doesn't exist.";
        if (Path.GetPathRoot(full) is { } root && string.Equals(Path.TrimEndingDirectorySeparator(root), Path.TrimEndingDirectorySeparator(full), StringComparison.OrdinalIgnoreCase))
            return "Choose a folder, not a whole drive.";
        foreach (var p in ProtectedFolders().Where(p => !string.IsNullOrEmpty(p)))
            if (PathSafety.IsInside(full, p) || string.Equals(Path.TrimEndingDirectorySeparator(full), Path.TrimEndingDirectorySeparator(p), StringComparison.OrdinalIgnoreCase))
                return "Windows, program folders, AppData and Kodiak Cleaner's own folders need their file names, so they can't be renamed here.";
        return null;
    }

    private void Browse()
    {
        var dialog = new OpenFolderDialog { Title = "Choose a Folder to Rename Files In" };
        if (Directory.Exists(Folder)) dialog.InitialDirectory = Folder;
        if (dialog.ShowDialog() != true) return;
        Folder = dialog.FolderName;
        _ = LoadAsync();
    }

    private static IReadOnlyList<string> FilterPatterns(string filter) =>
        filter.Split(new[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.Contains('*') || p.Contains('?') ? p : p.StartsWith('.') ? "*" + p : "*." + p)
            .DefaultIfEmpty("*").Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private async Task LoadAsync()
    {
        if (WhyNotAllowed(Folder) is { } why)
        {
            Summary = why;
            return;
        }
        IsBusy = true;
        _cts = new CancellationTokenSource();
        Summary = $"Reading {Folder}…";
        try
        {
            var (root, deep, patterns, token) = (Path.GetFullPath(Folder), IncludeSubfolders, FilterPatterns(Filter), _cts.Token);
            var guarded = ProtectedFolders().Where(p => !string.IsNullOrEmpty(p)).ToList();
            var (files, existing, more) = await Task.Run(() => Read(root, deep, patterns, guarded, token));
            _existing = existing;
            _takenRead = false;
            Rows.Clear();
            foreach (var f in files)
            {
                var row = new RenameRow(f) { Subfolder = Path.GetRelativePath(root, f.Folder) is var rel && rel != "." ? rel : "" };
                row.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName != nameof(RenameRow.IsChecked)) return;
                    OnPropertyChanged(nameof(AllChecked));
                    RulesChanged(); // numbering only counts ticked files
                };
                Rows.Add(row);
            }
            OnPropertyChanged(nameof(AllChecked));
            OnPropertyChanged(nameof(ShowSubfolder));
            UpdatePreview();
            if (more) Summary = $"Only the first {Limit:N0} files are shown. Choose a smaller folder or a filter. " + Summary;
        }
        catch (OperationCanceledException) { Summary = "Loading cancelled."; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Summary = "That folder couldn't be read: " + e.Message; }
        finally { IsBusy = false; }
    }

    private static (List<RenameFile> Files, Dictionary<string, HashSet<string>> Existing, bool More) Read(
        string root, bool deep, IReadOnlyList<string> patterns, List<string> guarded, CancellationToken token)
    {
        var files = new List<RenameFile>();
        var existing = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var folders = new Stack<string>();
        folders.Push(root);
        var more = false;
        while (folders.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            var folder = folders.Pop();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            existing[folder] = names;
            DirectoryInfo dir = new(folder);
            IEnumerable<FileSystemInfo> entries;
            try { entries = dir.EnumerateFileSystemInfos("*", new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = 0 }).ToList(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { continue; }
            foreach (var entry in entries)
            {
                names.Add(entry.Name);
                var hidden = (entry.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0;
                if (entry is DirectoryInfo sub)
                {
                    // Linked folders lead elsewhere; hidden and protected folders are left alone.
                    if (deep && !hidden && (sub.Attributes & FileAttributes.ReparsePoint) == 0 &&
                        !guarded.Any(g => PathSafety.IsInside(sub.FullName, g) || string.Equals(sub.FullName, Path.TrimEndingDirectorySeparator(g), StringComparison.OrdinalIgnoreCase)))
                        folders.Push(sub.FullName);
                    continue;
                }
                if (hidden || entry is not FileInfo file) continue;
                if (!patterns.Any(p => FileSystemName.MatchesSimpleExpression(p, file.Name))) continue;
                if (files.Count >= Limit) { more = true; continue; }
                files.Add(new RenameFile(file.FullName, file.Length, file.LastWriteTime, file.CreationTime));
            }
        }
        return (files, existing, more);
    }

    private void UpdatePreview()
    {
        if (Rows.Count == 0)
        {
            ReadyCount = 0;
            return;
        }
        if (MassRenamer.UsesTaken(Pattern) && !_takenRead)
        {
            _takenRead = true;
            _ = ReadTakenAsync();
        }
        var skipped = new HashSet<string>(Rows.Where(r => !r.IsChecked).Select(r => r.Path), StringComparer.OrdinalIgnoreCase);
        var plan = MassRenamer.Plan(Rows.Select(r => r.File), Rules, _existing, skipped);
        var byPath = plan.ToDictionary(p => p.File.Path, StringComparer.OrdinalIgnoreCase);
        foreach (var r in Rows)
        {
            var p = byPath[r.Path];
            r.NewName = p.NewName;
            r.State = p.State;
            r.Note = p.Note;
        }
        ReadyCount = plan.Count(p => p.State == RenameState.Ready);
        var problems = plan.Count(p => p.State == RenameState.Problem);
        Summary = $"{Rows.Count:N0} file(s) loaded. {ReadyCount:N0} will be renamed" +
                  (problems > 0 ? $", {problems:N0} can't be (see Status) and will keep their names." : ".");
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>Reads each picture's Date Taken in the background the first time a pattern uses {taken}.</summary>
    private async Task ReadTakenAsync()
    {
        var files = Rows.Select(r => r.File).ToList();
        var dates = await Task.Run(() => files.Select(f => PictureDates.Taken(f.Path)).ToList());
        for (int i = 0; i < files.Count && i < Rows.Count; i++)
            if (ReferenceEquals(Rows[i].File, files[i])) Rows[i].File = files[i] with { Taken = dates[i] };
        UpdatePreview();
    }

    private async Task RenameAsync()
    {
        _refresh.Stop();
        UpdatePreview();
        var ready = Rows.Where(r => r.IsChecked && r.State == RenameState.Ready).ToList();
        if (ready.Count == 0) return;
        var problems = Rows.Count(r => r.IsChecked && r.State == RenameState.Problem);
        var message = $"Rename {ready.Count:N0} file(s)?" + (problems > 0 ? $" {problems:N0} with a problem will keep their names." : "") +
                      " You can put the names back with Undo Last Rename.";
        if (!Dialogs.Confirm(message, "Rename Files")) return;

        Settings.Current.MassRename = Rules;
        Settings.Current.Save();
        IsBusy = true;
        Summary = $"Renaming {ready.Count:N0} file(s)…";
        try
        {
            var moves = ready.Select(r => new RenameMove(r.Path, Path.Combine(r.File.Folder, r.NewName))).ToList();
            var journal = JournalPath;
            var outcome = await Task.Run(() => MassRenamer.Rename(moves, journal));
            ActivityLog.Write("Mass Rename", $"Renamed {outcome.Done.Count} file(s) in {Folder}" +
                                             (outcome.Failed.Count > 0 ? $", {outcome.Failed.Count} couldn't be renamed" : ""));
            await ReloadAfter();
            Summary = Report("Renamed", outcome);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanUndo));
        }
    }

    private async Task UndoAsync()
    {
        var journal = RenameJournal.Load(JournalPath);
        if (journal is null || journal.Moves.Count == 0)
        {
            RenameJournal.Delete(JournalPath);
            OnPropertyChanged(nameof(CanUndo));
            Summary = "There's no rename to undo.";
            return;
        }
        var folders = journal.Moves.Select(m => Path.GetDirectoryName(m.From)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var where = folders.Count == 1 ? $" in {folders[0]}" : "";
        if (!Dialogs.Confirm($"Put back the old names of {journal.Moves.Count:N0} file(s) renamed {journal.When:g}{where}?", "Undo Last Rename")) return;
        IsBusy = true;
        try
        {
            var path = JournalPath;
            var outcome = await Task.Run(() => MassRenamer.Undo(journal, path));
            ActivityLog.Write("Mass Rename", $"Undo put back {outcome.Done.Count} name(s)" +
                                             (outcome.Failed.Count > 0 ? $", {outcome.Failed.Count} couldn't be put back" : ""));
            await ReloadAfter();
            Summary = Report("Put back", outcome);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanUndo));
        }
    }

    private async Task ReloadAfter()
    {
        if (Rows.Count == 0 || WhyNotAllowed(Folder) is not null) return;
        IsBusy = false;
        await LoadAsync();
        IsBusy = true;
    }

    private static string Report(string verb, RenameOutcome outcome)
    {
        var text = $"{verb} {outcome.Done.Count:N0} file name(s).";
        if (outcome.Failed.Count == 0) return text;
        var first = outcome.Failed[0];
        return text + $" {outcome.Failed.Count:N0} couldn't be changed, for example {Path.GetFileName(first.Path)}: {first.Reason}";
    }
}
