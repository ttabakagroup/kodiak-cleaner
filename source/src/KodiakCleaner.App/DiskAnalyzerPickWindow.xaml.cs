using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using KodiakCleaner.App.ViewModels;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App;

/// <summary>A quick pick in the Disk Analyzer picker's Folder choice.</summary>
public sealed record FolderPlace(string Name, string Path);

/// <summary>A drive in the Disk Analyzer picker, with how full it is.</summary>
public sealed class DriveChoice : ObservableObject
{
    public DriveChoice(string root, string label, string type, string format, long total, long free, bool local)
    {
        Root = root;
        Letter = root.TrimEnd('\\');
        bool named = !string.IsNullOrWhiteSpace(label);
        Title = $"{(named ? label : type)} ({Letter})";
        Type = type;
        Detail = string.Join(" · ", new[] { named ? type : "", format }.Where(s => s.Length > 0));
        IsLocal = local;
        if (total > 0)
        {
            Used = 100.0 * (total - free) / total;
            Space = $"{ByteSize.Format(free)} free of {ByteSize.Format(total)}";
            UsedText = $"{Used:0}% Used";
        }
        else
        {
            Space = "Not ready";
            UsedText = "";
        }
    }

    public string Root { get; }
    public string Letter { get; }
    public string Title { get; }
    public string Type { get; }
    /// <summary>The type when the drive has its own name, and the file system ("Local Disk · NTFS").</summary>
    public string Detail { get; }
    public bool IsLocal { get; }
    public double Used { get; }
    public string Space { get; }
    public string UsedText { get; }
    public bool IsReady => UsedText.Length > 0;
    public string Glyph => Type == "Network Drive" ? "" : Type == "USB Drive" ? "" : "";

    private bool _isChecked;
    public bool IsChecked { get => _isChecked; set => Set(ref _isChecked, value); }
}

/// <summary>
/// "Choose What to Analyze" (Ted, 2026-10-12; a dropdown since 2026-10-22): All Drives (every local drive at once), Individual Drives
/// (tick one or more, network drives too) or Folder. The window has a fixed size and the drive cards scroll, however many drives there are. Drives are read in the background, each given a few seconds, so a sleeping disk never freezes the window.
/// </summary>
public partial class DiskAnalyzerPickWindow : Window
{
    private static readonly TimeSpan DriveWait = TimeSpan.FromSeconds(5);

    public DiskAnalyzerPickWindow(DiskTarget? last)
    {
        InitializeComponent();
        Drives.ItemsSource = DriveList;
        Places.ItemsSource = UserFolders();
        FolderBox.Text = last is { Roots.Count: 1 } t && !IsDriveRoot(t.Roots[0].Path) ? t.Roots[0].Path
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _last = last;
        if (last is { Roots.Count: 1 } one && !IsDriveRoot(one.Roots[0].Path)) Choose(FolderMode);
        else if (last is not null && last.Description == KodiakCleaner.Core.Tools.DiskAnalyzer.AllDrivesName) Choose(AllMode);
        else Choose(DrivesMode);
        Loaded += async (_, _) =>
        {
            ThemeManager.ApplyTitleBar(this);
            await LoadDrivesAsync();
        };
        Update();
    }

    private readonly DiskTarget? _last;

    public ObservableCollection<DriveChoice> DriveList { get; } = new();

    /// <summary>What was picked, once Analyze is clicked.</summary>
    public DiskTarget? Target { get; private set; }

    private static bool IsDriveRoot(string path) => Path.GetPathRoot(path) == path;

    private async Task LoadDrivesAsync()
    {
        DriveInfo[] all;
        try { all = DriveInfo.GetDrives(); }
        catch (IOException) { all = Array.Empty<DriveInfo>(); }

        var reads = all.Where(d => d.DriveType is DriveType.Fixed or DriveType.Removable or DriveType.Network).Select(async d =>
        {
            try { return await Task.Run(() => Read(d)).WaitAsync(DriveWait); }
            catch (TimeoutException) { return new DriveChoice(d.Name, "", TypeOf(d), "", 0, 0, d.DriveType != DriveType.Network); }
        });
        var drives = await Task.WhenAll(reads);
        var lastRoots = _last?.Roots.Select(r => r.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var d in drives.OrderBy(d => d.Letter, StringComparer.OrdinalIgnoreCase))
        {
            d.IsChecked = d.IsReady && (lastRoots?.Contains(d.Root) ?? d.Root.StartsWith(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:", StringComparison.OrdinalIgnoreCase));
            d.PropertyChanged += (_, _) => Update();
            DriveList.Add(d);
        }
        Loading.Visibility = Visibility.Collapsed;

        Update();
    }


    private static DriveChoice Read(DriveInfo d)
    {
        bool ready = d.IsReady;
        return new DriveChoice(d.Name, ready ? d.VolumeLabel : "", TypeOf(d), ready ? d.DriveFormat : "", ready ? d.TotalSize : 0, ready ? d.TotalFreeSpace : 0,
            d.DriveType != DriveType.Network);
    }

    private static string TypeOf(DriveInfo d) => d.DriveType switch
    {
        DriveType.Network => "Network Drive",
        DriveType.Removable => "USB Drive",
        _ => "Local Disk",
    };

    /// <summary>The Windows user folders as quick picks, A to Z.</summary>
    private static List<FolderPlace> UserFolders()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var list = new List<FolderPlace>
        {
            new("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)),
            new("Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
            new("Downloads", Path.Combine(profile, "Downloads")),
            new("Music", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)),
            new("My User Folder", profile),
            new("Pictures", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)),
            new("Videos", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)),
        };
        return list.Where(p => !string.IsNullOrEmpty(p.Path) && Directory.Exists(p.Path)).ToList();
    }

    private HashSet<string>? _ticked;

    private void Mode_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // All Drives ticks every local drive for show; going back to Individual Drives brings back the drives that were ticked there.
        if (e.RemovedItems.Contains(DrivesMode)) _ticked = DriveList.Where(d => d.IsChecked).Select(d => d.Root).ToHashSet();
        else if (e.AddedItems.Contains(DrivesMode) && _ticked is { } ticked)
            foreach (var d in DriveList) d.IsChecked = d.IsReady && ticked.Contains(d.Root);
        Update();
    }

    /// <summary>Picks All Drives, Folder or Individual Drives in the dropdown.</summary>
    private void Choose(System.Windows.Controls.ComboBoxItem mode)
    {
        if (!ReferenceEquals(ModeBox.SelectedItem, mode)) ModeBox.SelectedItem = mode;
    }

    private bool IsMode(System.Windows.Controls.ComboBoxItem mode) => ReferenceEquals(ModeBox.SelectedItem, mode);

    private void Update()
    {
        if (!IsInitialized || AnalyzeButton is null) return;
        bool all = IsMode(AllMode), drives = IsMode(DrivesMode), folder = IsMode(FolderMode);
        DrivePanel.Visibility = all || drives ? Visibility.Visible : Visibility.Collapsed;
        FolderPanel.Visibility = folder ? Visibility.Visible : Visibility.Collapsed;
        DriveButtons.Visibility = drives ? Visibility.Visible : Visibility.Collapsed;
        ModeCaption.Text = all ? "Every local drive at once." : drives ? "One or more drives, network drives too." : folder ? "One folder and everything in it." : "";
        Drives.IsEnabled = drives;
        DriveHint.Text = all ? "All of these local drives are scanned side by side. Network drives can be picked under Individual Drives."
            : "Tick the drives to scan. Several are scanned side by side.";
        AnalyzeButton.IsEnabled = all ? DriveList.Any(d => d.IsLocal && d.IsReady)
            : drives ? DriveList.Any(d => d.IsChecked && d.IsReady)
            : Directory.Exists(FolderBox.Text.Trim());
        if (all)
            foreach (var d in DriveList) d.IsChecked = d.IsLocal && d.IsReady;
    }

    private void Drive_Click(object sender, RoutedEventArgs e)
    {
        Choose(DrivesMode);
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var d in DriveList) d.IsChecked = d.IsReady;
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        foreach (var d in DriveList) d.IsChecked = false;
    }

    private void FolderBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (FolderMode is not null && IsLoaded) Choose(FolderMode);
        Update();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Choose a Folder to Analyze" };
        if (Directory.Exists(FolderBox.Text.Trim())) dialog.InitialDirectory = FolderBox.Text.Trim();
        if (dialog.ShowDialog(this) != true) return;
        FolderBox.Text = dialog.FolderName;
        Choose(FolderMode);
    }

    private void Place_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string path }) FolderBox.Text = path;
        Choose(FolderMode);
    }

    private void Analyze_Click(object sender, RoutedEventArgs e)
    {
        if (IsMode(FolderMode))
        {
            var path = Path.GetFullPath(FolderBox.Text.Trim());
            Target = new DiskTarget(new[] { (path, "") }, path);
        }
        else
        {
            bool all = IsMode(AllMode);
            var picked = DriveList.Where(d => d.IsReady && (all ? d.IsLocal : d.IsChecked)).ToList();
            if (picked.Count == 0) return;
            Target = new DiskTarget(picked.Select(d => (d.Root, d.Title)).ToList(),
                all ? KodiakCleaner.Core.Tools.DiskAnalyzer.AllDrivesName : picked.Count == 1 ? picked[0].Title : string.Join(", ", picked.Select(d => d.Letter)));
        }
        DialogResult = true;
    }
}
