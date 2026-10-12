using System.Collections.ObjectModel;
using System.IO;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App.ViewModels;

/// <summary>One place a tool can search: a whole drive (with its free space) or a folder.</summary>
public sealed class SearchPlace : ObservableObject
{
    public SearchPlace(string path, string title, string detail, double used, bool isDrive, bool isChecked)
    {
        Path = path;
        Title = title;
        Detail = detail;
        Used = used;
        IsDrive = isDrive;
        _isChecked = isChecked;
    }

    public string Path { get; }
    public string Title { get; }
    /// <summary>"1.21 TB free of 3.64 TB" for a drive; the full path for a folder.</summary>
    public string Detail { get; }
    /// <summary>How full the drive is, 0 to 1, for its bar.</summary>
    public double Used { get; }
    public bool IsDrive { get; }
    public bool IsFolder => !IsDrive;
    public string Glyph => IsDrive ? "" : "";

    private bool _isChecked;
    public bool IsChecked { get => _isChecked; set { if (Set(ref _isChecked, value)) Changed?.Invoke(); } }
    internal Action? Changed { get; set; }
}

/// <summary>
/// The drives and folders a tool searches (Ted, 0.8.65): every drive that is ready, each with its free space and a bar, plus
/// folders the person adds. Any mix can be ticked; All Drives ticks every drive at once.
/// </summary>
public sealed class SearchPlaces : ObservableObject
{
    public ObservableCollection<SearchPlace> Drives { get; } = new();
    public ObservableCollection<SearchPlace> Folders { get; } = new();

    /// <summary>Raised when what is ticked changes, so a Search button can update.</summary>
    public event Action? Changed;

    public SearchPlaces(IEnumerable<string> folders, Func<string, bool>? tickFolder = null)
    {
        foreach (var d in ReadyDrives()) Add(Drives, d);
        foreach (var f in folders.Where(Directory.Exists)) AddFolder(f, tickFolder?.Invoke(f) ?? true);
    }

    public IEnumerable<SearchPlace> All => Drives.Concat(Folders);
    public IReadOnlyList<string> Ticked => All.Where(p => p.IsChecked).Select(p => p.Path).ToList();
    public bool AnyTicked => All.Any(p => p.IsChecked);
    public bool HasFolders => Folders.Count > 0;

    /// <summary>The All Drives box: true when every drive is ticked, false when none, null when some.</summary>
    public bool? AllDrives
    {
        get => Drives.Count == 0 || Drives.All(d => !d.IsChecked) ? false : Drives.All(d => d.IsChecked) ? true : null;
        set
        {
            var on = value != false && AllDrives != true;
            foreach (var d in Drives) d.IsChecked = on;
        }
    }

    public void AddFolder(string path, bool isChecked = true)
    {
        var existing = Folders.FirstOrDefault(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) { existing.IsChecked = true; return; }
        Add(Folders, new SearchPlace(path, System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(path)) is { Length: > 0 } n ? n : path,
            path, 0, isDrive: false, isChecked));
    }

    private void Add(ObservableCollection<SearchPlace> list, SearchPlace place)
    {
        place.Changed = () =>
        {
            OnPropertyChanged(nameof(AllDrives));
            Changed?.Invoke();
        };
        list.Add(place);
        OnPropertyChanged(nameof(HasFolders));
        Changed?.Invoke();
    }

    /// <summary>Fixed and removable drives that are ready, A to Z. A drive that doesn't answer is left out.</summary>
    private static IEnumerable<SearchPlace> ReadyDrives()
    {
        DriveInfo[] drives;
        try { drives = DriveInfo.GetDrives(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { yield break; }
        foreach (var d in drives.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
        {
            SearchPlace? place = null;
            try
            {
                if (d.DriveType is not (DriveType.Fixed or DriveType.Removable) || !d.IsReady) continue;
                var letter = d.Name.TrimEnd('\\');
                var label = string.IsNullOrWhiteSpace(d.VolumeLabel) ? (d.DriveType == DriveType.Removable ? "Removable Drive" : "Local Disk") : d.VolumeLabel;
                var used = d.TotalSize > 0 ? 1 - (double)d.TotalFreeSpace / d.TotalSize : 0;
                place = new SearchPlace(d.Name, $"{label} ({letter})",
                    $"{ByteSize.Format(d.TotalFreeSpace)} free of {ByteSize.Format(d.TotalSize)}", used, isDrive: true, isChecked: false);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            if (place is not null) yield return place;
        }
    }
}
