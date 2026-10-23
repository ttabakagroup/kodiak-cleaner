using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KodiakCleaner.App.Views;

public partial class HashCheckerView : UserControl
{
    public HashCheckerView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is ViewModels.HashCheckerViewModel old) old.Reveal -= Reveal;
            if (e.NewValue is ViewModels.HashCheckerViewModel vm) vm.Reveal += Reveal;
        };
    }

    /// <summary>A file was just dropped or added: highlight it and open its details (path, size, dates and every hash as it arrives).</summary>
    private void Reveal(ViewModels.HashFileRow row)
    {
        FileGrid.SelectedItems.Clear();
        FileGrid.SelectedItem = row;
        FileGrid.ScrollIntoView(row);
    }

    private ViewModels.HashCheckerViewModel? Vm => DataContext as ViewModels.HashCheckerViewModel;

    /// <summary>Ctrl-click, Shift-click and dragging highlight several files; the right-click menu acts on all of them.</summary>
    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Vm is not { } vm || sender is not DataGrid grid) return;
        vm.Highlighted = grid.SelectedItems.OfType<ViewModels.HashFileRow>().ToList();
        // One highlighted file opens its five hashes under its row; picking several closes them, so the list stays short.
        // Set only when it changes: setting it rebuilds the open details, and a button under the mouse would lose its click.
        var mode = grid.SelectedItems.Count == 1 ? DataGridRowDetailsVisibilityMode.VisibleWhenSelected : DataGridRowDetailsVisibilityMode.Collapsed;
        if (grid.RowDetailsVisibilityMode != mode) grid.RowDetailsVisibilityMode = mode;
    }

    private void Grid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) => GridRightClick.SelectRowUnderMouse(e);

    // Files and folders dropped anywhere on the page (the list box too) are added; checksum files become the list.
    // Dragged text is left alone, so it can still be dropped into the list box.
    private void Page_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private async void Page_Drop(object sender, DragEventArgs e)
    {
        if (Vm is not { } vm || e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        e.Handled = true;
        try { await vm.DropAsync(paths); }
        catch (Exception ex) { Dialogs.Error(ex.Message); }
    }
}
