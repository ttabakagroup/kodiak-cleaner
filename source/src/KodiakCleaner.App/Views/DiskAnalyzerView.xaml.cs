using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KodiakCleaner.App.ViewModels;

namespace KodiakCleaner.App.Views;

public partial class DiskAnalyzerView
{
    public DiskAnalyzerView() => InitializeComponent();

    private DiskAnalyzerViewModel? Vm => DataContext as DiskAnalyzerViewModel;

    private void Legend_MouseEnter(object sender, MouseEventArgs e)
    {
        if (Vm is { } vm && sender is FrameworkElement { DataContext: KindShare k }) vm.HighlightKind = k.Kind;
    }

    private void Legend_MouseLeave(object sender, MouseEventArgs e)
    {
        if (Vm is { } vm) vm.HighlightKind = null;
    }

    private void Grid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) => GridRightClick.SelectRowUnderMouse(e);

    private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Vm is not { } vm || vm.SelectedEntry is not { } entry) return;
        if (entry.Folder is not null) vm.OpenCommand.Execute(entry);
        else if (vm.ShowInExplorerCommand.CanExecute(null)) vm.ShowInExplorerCommand.Execute(null);
    }

    // The mouse's back button and Backspace go up one folder, like File Explorer.
    private void View_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.XButton1 || Vm is not { } vm || !vm.UpCommand.CanExecute(null)) return;
        vm.UpCommand.Execute(null);
        e.Handled = true;
    }

    private void View_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Back || e.OriginalSource is TextBox || Vm is not { } vm || !vm.UpCommand.CanExecute(null)) return;
        vm.UpCommand.Execute(null);
        e.Handled = true;
    }
}
