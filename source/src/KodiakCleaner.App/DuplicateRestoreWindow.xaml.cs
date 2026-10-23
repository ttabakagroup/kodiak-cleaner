using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using KodiakCleaner.App.ViewModels;
using KodiakCleaner.Core.Tools;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App;

/// <summary>A moved duplicate with a tick box.</summary>
public sealed class DuplicateMoveRow : ObservableObject
{
    public DuplicateMoveRow(DuplicateMoveBatch batch, DuplicateMove move)
    {
        Batch = batch;
        Move = move;
    }

    public DuplicateMoveBatch Batch { get; }
    public DuplicateMove Move { get; }
    public string Name => Path.GetFileName(Move.From);
    public string WasIn => Path.GetDirectoryName(Move.From) ?? "";
    public string Kept => Move.Kept;
    public string Size => ByteSize.Format(Move.Size);
    public DateTime MovedOn => Batch.MovedOn;
    public bool CanRestore => Move.RestoredOn is null;
    public string Status => CanRestore ? "Moved" : "Restored";

    public bool Matches(string search) =>
        string.IsNullOrWhiteSpace(search) ||
        Move.From.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
        Move.Kept.Contains(search, StringComparison.CurrentCultureIgnoreCase);

    private bool _isChecked;
    public bool IsChecked { get => _isChecked; set => Set(ref _isChecked, value && CanRestore); }

    internal void Restored()
    {
        IsChecked = false;
        OnPropertyChanged(nameof(CanRestore));
        OnPropertyChanged(nameof(Status));
    }
}

public partial class DuplicateRestoreWindow : Window
{
    private readonly List<DuplicateMoveRow> _rows = new();
    private readonly ICollectionView _view;
    private string _folder;
    private bool _restored;

    private DuplicateRestoreWindow(string folder)
    {
        _folder = folder;
        InitializeComponent();
        _view = CollectionViewSource.GetDefaultView(_rows);
        _view.Filter = o => o is DuplicateMoveRow r && r.Matches(Search.Text);
        DependencyPropertyDescriptor.FromProperty(Views.SearchBox.TextProperty, typeof(Views.SearchBox))
            .AddValueChanged(Search, (_, _) => _view.Refresh());
        Grid.ItemsSource = _view;
        Load();
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
    }

    /// <summary>Shows the moved duplicates in <paramref name="folder"/>; returns true when a file was put back.</summary>
    public static bool Show(Window? owner, string folder)
    {
        var w = new DuplicateRestoreWindow(folder) { Owner = owner ?? Application.Current.MainWindow };
        w.ShowDialog();
        return w._restored;
    }

    private void Load()
    {
        _rows.Clear();
        foreach (var batch in DuplicateMover.List(_folder))
            _rows.AddRange(batch.Moves.OrderBy(m => Path.GetFileName(m.From), StringComparer.CurrentCultureIgnoreCase).ThenBy(m => m.From, StringComparer.OrdinalIgnoreCase).Select(m => new DuplicateMoveRow(batch, m)));
        _view.Refresh();
        AllBox.IsChecked = false;
        Empty.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var moved = _rows.Count(r => r.CanRestore);
        Subheading.Text = $"Duplicates moved to {_folder}: {TargetRun.Plural(moved, "file")} still there, newest move first and A to Z. " +
                          $"Each move has its own dated folder with {DuplicateMover.ReportName}, which lists the copy that stayed and where each duplicate was. " +
                          "Restore puts a file back in its old folder; if another file is there now, it's left alone.";
    }

    /// <summary>Ticked rows, or the highlighted rows (Ctrl-click, Shift-click or drag) when none are ticked.</summary>
    private List<DuplicateMoveRow> Picked()
    {
        var ticked = _rows.Where(r => r.IsChecked).ToList();
        return (ticked.Count > 0 ? ticked : Grid.SelectedItems.OfType<DuplicateMoveRow>().ToList()).Where(r => r.CanRestore).ToList();
    }

    private void All_Click(object sender, RoutedEventArgs e)
    {
        var on = AllBox.IsChecked == true;
        foreach (var r in _view.Cast<DuplicateMoveRow>()) r.IsChecked = on;
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        var picked = Picked();
        if (picked.Count == 0) return;
        var what = picked.Count == 1 ? $"'{picked[0].Name}'" : $"{picked.Count} files";
        if (!Dialogs.Confirm($"Put {what} back where {(picked.Count == 1 ? "it was" : "they were")}?", "Restore Selected")) return;
        var problems = new List<string>();
        foreach (var r in picked)
        {
            if (DuplicateMover.Restore(r.Batch, r.Move, DateTime.Now) is { } problem) problems.Add($"{r.Move.From}: {problem}");
            else
            {
                r.Restored();
                _restored = true;
            }
        }
        var done = picked.Count - problems.Count;
        if (done > 0) ActivityLog.Write("Duplicate Finder", $"Restored {done} moved duplicate(s) from {_folder}");
        if (problems.Count > 0) Dialogs.Error($"{problems.Count} of {picked.Count} file(s) weren't restored:\n\n" + string.Join("\n", problems.Take(20)));
        else Dialogs.Info(picked.Count == 1 ? "Restored." : $"Restored all {picked.Count}.", "Restore Selected");
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (Directory.Exists(_folder)) Elevation.OpenFolder(_folder);
        else Dialogs.Info($"{_folder} doesn't exist yet. It's made the first time duplicates are moved there.", "Open Folder");
    }

    private void OtherFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Choose a Folder with Moved Duplicates" };
        if (Directory.Exists(_folder)) dialog.InitialDirectory = _folder;
        if (dialog.ShowDialog(this) != true) return;
        _folder = dialog.FolderName;
        Load();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
