using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KodiakCleaner.App.Views;

public partial class StartupView : UserControl
{
    public StartupView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is ViewModels.StartupViewModel old) old.PropertyChanged -= Vm_PropertyChanged;
            if (e.NewValue is ViewModels.StartupViewModel vm) vm.PropertyChanged += Vm_PropertyChanged;
            ShowOrHideTasks();
        };
    }

    private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModels.StartupViewModel.ShowTasks)) ShowOrHideTasks();
    }

    /// <summary>
    /// Show Scheduled Tasks off (Ted, 0.8.102): the tasks card and the divider are hidden and the startup list spans all three
    /// rows, so the row sizes (and where the divider was dragged to) are left alone for when it's turned back on.
    /// </summary>
    private void ShowOrHideTasks()
    {
        bool show = DataContext is not ViewModels.StartupViewModel vm || vm.ShowTasks;
        TasksCard.Visibility = TasksSplitter.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetRowSpan(StartupCard, show ? 1 : 3);
    }

    /// <summary>Ctrl-click and Shift-click highlight several rows; the right-click menu and the buttons act on all of them.</summary>
    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ViewModels.StartupViewModel vm && sender is DataGrid grid)
            vm.Highlighted = grid.SelectedItems.OfType<ViewModels.StartupItemViewModel>().ToList();
    }

    /// <summary>The Scheduled Tasks That Run at Startup card highlights several tasks the same way.</summary>
    private void Tasks_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ViewModels.StartupViewModel vm && sender is DataGrid grid)
            vm.HighlightedTasks = grid.SelectedItems.OfType<ViewModels.ScheduledTaskItem>().ToList();
    }

    private void Grid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) => GridRightClick.SelectRowUnderMouse(e);
}
