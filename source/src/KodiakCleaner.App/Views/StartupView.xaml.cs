using System.Windows.Controls;
using System.Windows.Input;

namespace KodiakCleaner.App.Views;

public partial class StartupView : UserControl
{
    public StartupView() => InitializeComponent();

    /// <summary>Ctrl-click and Shift-click highlight several rows; the right-click menu and the buttons act on all of them.</summary>
    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ViewModels.StartupViewModel vm && sender is DataGrid grid)
            vm.Highlighted = grid.SelectedItems.OfType<ViewModels.StartupItemViewModel>().ToList();
    }

    private void Grid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) => GridRightClick.SelectRowUnderMouse(e);
}
