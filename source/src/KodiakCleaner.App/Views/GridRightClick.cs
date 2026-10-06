using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KodiakCleaner.App.Views;

/// <summary>Right-click selects the row under the mouse first, so a table's right-click menu acts on that row.</summary>
public static class GridRightClick
{
    public static void SelectRowUnderMouse(MouseButtonEventArgs e)
    {
        for (var d = e.OriginalSource as DependencyObject; d is not null;
             d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
        {
            if (d is DataGridRow row)
            {
                row.IsSelected = true;
                row.Focus();
                return;
            }
        }
        // Empty space: clear the selection so the menu can't act on whichever row was selected before.
        for (var d = e.OriginalSource as DependencyObject; d is not null;
             d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
        {
            if (d is DataGrid grid)
            {
                grid.SelectedItem = null;
                return;
            }
        }
    }
}
