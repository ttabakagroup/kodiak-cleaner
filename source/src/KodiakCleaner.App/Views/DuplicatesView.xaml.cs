using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using KodiakCleaner.App.ViewModels;

namespace KodiakCleaner.App.Views;

public partial class DuplicatesView : UserControl
{
    public DuplicatesView() => InitializeComponent();

    /// <summary>Double-clicking a copy's row (not its tick box) shows it in its folder, like the Show link.</summary>
    private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        for (var d = e.OriginalSource as DependencyObject; d is not null; d = d is Visual ? VisualTreeHelper.GetParent(d) : null)
        {
            if (d is ToggleButton or ButtonBase) return;
            if (d is DataGridRow { Item: DuplicateFileItem f } && DataContext is DuplicatesViewModel vm)
            {
                vm.OpenFolderCommand.Execute(f);
                e.Handled = true;
                return;
            }
        }
    }
}
