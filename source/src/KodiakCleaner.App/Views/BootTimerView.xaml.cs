using System.Windows.Controls;
using System.Windows.Input;

namespace KodiakCleaner.App.Views;

public partial class BootTimerView : UserControl
{
    public BootTimerView() => InitializeComponent();

    private void Grid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) => GridRightClick.SelectRowUnderMouse(e);
}
