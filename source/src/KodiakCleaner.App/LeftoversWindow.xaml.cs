using System.Windows;
using KodiakCleaner.App.ViewModels;
using KodiakCleaner.Core.Programs;

namespace KodiakCleaner.App;

/// <summary>A leftover with a tick box.</summary>
public sealed class LeftoverChoice : ObservableObject
{
    public LeftoverChoice(Leftover leftover) => Leftover = leftover;
    public Leftover Leftover { get; }
    public string Path => Leftover.Path;
    public string Where => Leftover.Kind switch
    {
        LeftoverKind.RegistryKey => "Registry key",
        LeftoverKind.Shortcut => $"Shortcut, {Leftover.Where}",
        LeftoverKind.StartupEntry => "Startup entry",
        _ => Leftover.Where,
    };
    private bool _isChecked = true;
    public bool IsChecked { get => _isChecked; set => Set(ref _isChecked, value); }
}

public partial class LeftoversWindow : Window
{
    private readonly List<LeftoverChoice> _choices;

    private LeftoversWindow(string programName, IEnumerable<Leftover> leftovers)
    {
        InitializeComponent();
        Title = $"Leftovers from {programName}";
        Subheading.Text = $"'{programName}' was uninstalled, but these were left behind. Untick anything you want to keep. " +
                          "Folders and shortcuts go to the Recycle Bin, and registry keys and startup entries are backed up to a .reg file first.";
        _choices = leftovers.Select(l => new LeftoverChoice(l)).ToList();
        List.ItemsSource = _choices;
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
    }

    /// <returns>The leftovers to remove; empty when the person keeps everything.</returns>
    public static List<Leftover> Ask(string programName, IReadOnlyList<Leftover> leftovers)
    {
        var w = new LeftoversWindow(programName, leftovers) { Owner = Application.Current.MainWindow };
        return w.ShowDialog() == true ? w._choices.Where(c => c.IsChecked).Select(c => c.Leftover).ToList() : new List<Leftover>();
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e) { foreach (var c in _choices) c.IsChecked = true; }
    private void Clear_Click(object sender, RoutedEventArgs e) { foreach (var c in _choices) c.IsChecked = false; }
    private void Keep_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        // This window is the confirm step: nothing is removed until Remove Selected is clicked.
        DialogResult = _choices.Any(c => c.IsChecked);
    }
}
