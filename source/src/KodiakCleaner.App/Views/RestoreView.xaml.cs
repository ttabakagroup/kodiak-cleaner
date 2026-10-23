using System.Windows.Controls;
using System.Windows.Input;

namespace KodiakCleaner.App.Views;

public partial class RestoreView : UserControl
{
    public RestoreView() => InitializeComponent();

    /// <summary>Ctrl-click and Shift-click highlight several restore points; right-click Delete and the Delete button act on them.</summary>
    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ViewModels.RestoreViewModel vm && sender is DataGrid grid)
            vm.Highlighted = grid.SelectedItems.OfType<ViewModels.RestorePointRow>().ToList();
    }

    private void Grid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) => GridRightClick.SelectRowUnderMouse(e);
}
