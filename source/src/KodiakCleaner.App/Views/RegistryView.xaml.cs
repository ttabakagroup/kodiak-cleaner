using System.Windows.Controls;
using System.Windows.Input;

namespace KodiakCleaner.App.Views;

public partial class RegistryView : UserControl
{
    public RegistryView() => InitializeComponent();

    /// <summary>Ctrl-click and Shift-click highlight several rows; the right-click menu and Fix Selected act on all of them.</summary>
    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ViewModels.RegistryViewModel vm && sender is DataGrid grid)
            vm.Highlighted = grid.SelectedItems.OfType<ViewModels.IssueItem>().ToList();
    }

    private void Grid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) => GridRightClick.SelectRowUnderMouse(e);
}
