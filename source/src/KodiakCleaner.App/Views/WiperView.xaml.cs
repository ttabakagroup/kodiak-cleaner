using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace KodiakCleaner.App.Views;

public partial class WiperView : UserControl
{
    private static readonly DependencyPropertyDescriptor RowHeight =
        DependencyPropertyDescriptor.FromProperty(RowDefinition.HeightProperty, typeof(RowDefinition));

    public WiperView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is ViewModels.WiperViewModel old) old.PropertyChanged -= Vm_PropertyChanged;
            if (e.NewValue is ViewModels.WiperViewModel vm) vm.PropertyChanged += Vm_PropertyChanged;
            ShowOrHideCard();
        };
        // Watched only while the page is shown, so a page that's left isn't kept alive by the row.
        Loaded += (_, _) => { RowHeight.AddValueChanged(CardRow, CardRow_HeightChanged); FitResults(); };
        Unloaded += (_, _) => RowHeight.RemoveValueChanged(CardRow, CardRow_HeightChanged);
        ShowOrHideCard();
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModels.WiperViewModel.ShowWipeCard)) ShowOrHideCard();
    }

    /// <summary>
    /// The divider between the progress card and the settings (Ted, 0.8.115) only shows with the card. Without it the settings
    /// span all three rows, so the row sizes (and where the divider was dragged to) are left alone for the next wipe or check.
    /// </summary>
    private void ShowOrHideCard()
    {
        bool show = DataContext is ViewModels.WiperViewModel { ShowWipeCard: true };
        CardSplitter.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetRow(SettingsArea, show ? 2 : 0);
        Grid.SetRowSpan(SettingsArea, show ? 1 : 3);
    }

    private void CardRow_HeightChanged(object? sender, EventArgs e) => FitResults();

    /// <summary>
    /// The drive results keep to 250 px while the card sizes itself; once the divider is dragged (or a dragged size is put back),
    /// they fill the room the card was given, and a double-click on the divider brings the 250 px back.
    /// </summary>
    private void FitResults() => HealthScroll.MaxHeight = CardRow.Height.IsAuto ? 250 : double.PositiveInfinity;

    // ---------------------------------------------------------------- Step 1's drive list (Ted, 0.8.116)

    /// <summary>
    /// The mouse wheel over the drive list scrolls the page, unless the list is longer than its ten-or-so rows and still has
    /// further to go in that direction; then the list scrolls, and once it reaches its end the page carries on.
    /// </summary>
    private void DiskGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled) return;
        var inner = Find<ScrollViewer>(DiskGrid);
        bool listCanScroll = inner is not null && (e.Delta > 0 ? inner.VerticalOffset > 0 : inner.VerticalOffset < inner.ScrollableHeight);
        if (listCanScroll) return;
        e.Handled = true;
        SettingsArea.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = MouseWheelEvent, Source = DiskGrid });
    }

    private WipeDiskRowPress? _press;
    private sealed record WipeDiskRowPress(ViewModels.WipeDiskRow Row, Point At);

    /// <summary>Remembers the row a plain click starts on; tick boxes, the details button and drags keep their own handling.</summary>
    private void DiskGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _press = null;
        if (e.ClickCount > 1 || Keyboard.Modifiers != ModifierKeys.None || e.OriginalSource is not DependencyObject source) return;
        if (Up<ButtonBase>(source) is not null || Up<DataGridColumnHeader>(source) is not null || Up<ScrollBar>(source) is not null) return;
        if (Up<DataGridRow>(source) is { Item: ViewModels.WipeDiskRow row }) _press = new WipeDiskRowPress(row, e.GetPosition(DiskGrid));
    }

    /// <summary>
    /// Clicking anywhere on a drive's row ticks or unticks it, like clicking its tick box (Ted, 0.8.116). A drag (which highlights
    /// or ticks several rows) and drives that can't be wiped are left alone; the box itself (a ButtonBase) ticks on its own.
    /// </summary>
    private void DiskGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_press is not { } press) return;
        _press = null;
        var at = e.GetPosition(DiskGrid);
        if (Math.Abs(at.X - press.At.X) >= SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(at.Y - press.At.Y) >= SystemParameters.MinimumVerticalDragDistance) return;
        if (e.OriginalSource is not DependencyObject source || Up<DataGridRow>(source)?.Item != press.Row) return;
        if (press.Row.CanWipe) press.Row.IsChecked = !press.Row.IsChecked;
    }

    private static T? Up<T>(DependencyObject? d) where T : DependencyObject
    {
        for (; d is not null; d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            if (d is T found) return found;
        return null;
    }

    private static T? Find<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;
            if (Find<T>(child) is { } deeper) return deeper;
        }
        return null;
    }
}
