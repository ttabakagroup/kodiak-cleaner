using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace KodiakCleaner.App.Views;

/// <summary>
/// Drag to select (Ted, 0.8.90), switched on for every table and list by the DataGrid and ItemsControl styles in Controls.xaml:
/// <list type="bullet">
/// <item>Drag from one row to another highlights every row in between (the table does this itself once it allows several rows).
/// Dragging from the empty space under the rows draws a selection box, as in File Explorer. Ctrl or Shift keeps what was
/// already highlighted.</item>
/// <item>Press a tick box and drag up or down: every tick box the mouse passes takes the same tick (all on or all off).</item>
/// <item>Space ticks or unticks every highlighted row at once.</item>
/// </list>
/// </summary>
public static class ListSelect
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(ListSelect), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject d) => (bool)d.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject d, bool value) => d.SetValue(IsEnabledProperty, value);

    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(Drag), typeof(ListSelect), new PropertyMetadata(null));

    private sealed class Drag
    {
        public CheckBox? Start;        // tick drag: the box pressed first
        public bool Ticking;           // tick drag under way (the mouse left the first box's row)
        public bool Target;            // the tick every box passed over takes
        public Point Anchor;           // selection box: where it started, in the table's coordinates
        public BandAdorner? Band;
        public HashSet<object>? Kept;  // selection box: rows highlighted before it started (Ctrl or Shift held)
    }

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ItemsControl list) return;
        if ((bool)e.NewValue)
        {
            list.PreviewMouseLeftButtonDown += Down;
            list.PreviewMouseMove += Move;
            list.PreviewMouseLeftButtonUp += Up;
            list.LostMouseCapture += Lost;
            if (list is DataGrid) list.PreviewKeyDown += Key;
        }
        else
        {
            list.PreviewMouseLeftButtonDown -= Down;
            list.PreviewMouseMove -= Move;
            list.PreviewMouseLeftButtonUp -= Up;
            list.LostMouseCapture -= Lost;
            list.PreviewKeyDown -= Key;
        }
    }

    private static Drag? StateOf(ItemsControl list) => (Drag?)list.GetValue(StateProperty);

    private static void Down(object sender, MouseButtonEventArgs e)
    {
        var list = (ItemsControl)sender;
        Finish(list);
        if (e.ClickCount > 1 || e.OriginalSource is not DependencyObject source) return;
        if (Ancestor<ScrollBar>(source, list) is not null || Ancestor<DataGridColumnHeadersPresenter>(source, list) is not null) return;

        if (Ancestor<CheckBox>(source, list) is { } box && IsListTickBox(box, list))
        {
            list.SetValue(StateProperty, new Drag { Start = box });
            return;   // the box is pressed as usual; a drag only starts once the mouse reaches another row
        }

        if (list is DataGrid { SelectionMode: DataGridSelectionMode.Extended } grid && Ancestor<DataGridRow>(source, grid) is null &&
            Ancestor<ButtonBase>(source, grid) is null && Ancestor<TextBoxBase>(source, grid) is null && grid.Items.Count > 0 &&
            AdornerLayer.GetAdornerLayer(grid) is { } layer)
        {
            var keep = (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != 0;
            var drag = new Drag { Anchor = e.GetPosition(grid), Kept = keep ? grid.SelectedItems.Cast<object>().ToHashSet() : new HashSet<object>() };
            if (!keep) grid.UnselectAll();
            drag.Band = new BandAdorner(grid, drag.Anchor);
            layer.Add(drag.Band);
            list.SetValue(StateProperty, drag);
            grid.Focus();
            grid.CaptureMouse();
            e.Handled = true;
        }
    }

    private static void Move(object sender, MouseEventArgs e)
    {
        var list = (ItemsControl)sender;
        if (StateOf(list) is not { } drag) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            Finish(list);
            return;
        }

        if (drag.Band is { } band && list is DataGrid grid)
        {
            var at = e.GetPosition(grid);
            band.To = at;
            var box = new Rect(drag.Anchor, at);
            foreach (var row in Descendants<DataGridRow>(grid))
            {
                if (row.Item is not { } item || !row.IsVisible) continue;
                var bounds = row.TransformToAncestor(grid).TransformBounds(new Rect(row.RenderSize));
                var want = drag.Kept!.Contains(item) || bounds.IntersectsWith(box);
                if (row.IsSelected != want) row.IsSelected = want;
            }
            e.Handled = true;
            return;
        }

        if (drag.Start is not { } start) return;
        var target = TickBoxAt(list, e.GetPosition(list), start);
        if (!drag.Ticking)
        {
            if (target is null || target == start) return;
            // The mouse reached another row: tick (or untick) the first box and take the mouse from it, so letting go doesn't click it again.
            drag.Ticking = true;
            drag.Target = start.IsChecked != true;
            Press(start, drag.Target);
            list.CaptureMouse();
        }
        if (target is not null) Press(target, drag.Target);
        e.Handled = true;
    }

    private static void Up(object sender, MouseButtonEventArgs e)
    {
        var list = (ItemsControl)sender;
        if (StateOf(list) is { } drag && (drag.Ticking || drag.Band is not null)) e.Handled = true;
        Finish(list);
    }

    private static void Lost(object sender, MouseEventArgs e)
    {
        // Only the list itself losing the mouse ends a drag (a tick box inside it loses the mouse when the drag starts).
        var list = (ItemsControl)sender;
        if (e.OriginalSource == list && StateOf(list) is { Band: not null } or { Ticking: true }) Finish(list);
    }

    private static void Finish(ItemsControl list)
    {
        if (StateOf(list) is not { } drag) return;
        list.ClearValue(StateProperty);
        if (drag.Band is { } band) AdornerLayer.GetAdornerLayer(list)?.Remove(band);
        if ((drag.Ticking || drag.Band is not null) && list.IsMouseCaptured) list.ReleaseMouseCapture();
    }

    /// <summary>Space ticks every highlighted row, or unticks them all when they are all ticked already.</summary>
    private static void Key(object sender, KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Space || Keyboard.Modifiers != ModifierKeys.None || sender is not DataGrid grid ||
            grid.SelectedItems.Count < 2 || e.OriginalSource is TextBoxBase) return;
        if (TickProperties(grid) is not var (tick, canTick)) return;
        // Rows whose box is greyed out (the newest restore point, a drive that can't be wiped) are left as they are.
        var rows = grid.SelectedItems.Cast<object>().Where(r => canTick?.GetValue(r) is not false).ToList();
        if (rows.Count == 0) return;
        var on = !rows.All(r => tick.GetValue(r) is true);
        foreach (var row in rows) tick.SetValue(row, on);
        e.Handled = true;
    }

    /// <summary>
    /// The item properties a table's tick boxes are bound to (IsChecked, and IsEnabled when some boxes are greyed out), read from
    /// a row that is on screen.
    /// </summary>
    private static (PropertyInfo Tick, PropertyInfo? CanTick)? TickProperties(DataGrid grid)
    {
        foreach (var row in Descendants<DataGridRow>(grid))
        {
            if (row.Item is not { } item) continue;
            foreach (var box in Descendants<CheckBox>(row))
            {
                if (Ancestor<DataGridColumnHeader>(box, grid) is not null || box.Style == grid.TryFindResource("ToggleSwitch")) continue;
                if (BoundBool(item, box, ToggleButton.IsCheckedProperty, writable: true) is not { } tick) continue;
                return (tick, BoundBool(item, box, UIElement.IsEnabledProperty, writable: false));
            }
            return null;
        }
        return null;
    }

    private static PropertyInfo? BoundBool(object item, DependencyObject box, DependencyProperty property, bool writable)
    {
        if (BindingOperations.GetBinding(box, property)?.Path?.Path is not { Length: > 0 } path || path.Contains('.') || path.Contains('[')) return null;
        return item.GetType().GetProperty(path) is { } p && (p.PropertyType == typeof(bool) || p.PropertyType == typeof(bool?)) && (!writable || p.CanWrite)
            ? p : null;
    }

    /// <summary>A tick box in a row of this list (not a header's Select All box, a switch, or a box in a list inside a row).</summary>
    private static bool IsListTickBox(CheckBox box, ItemsControl list)
    {
        if (!box.IsEnabled || box.IsThreeState || box.Style == list.TryFindResource("ToggleSwitch")) return false;
        for (DependencyObject? d = VisualTreeHelper.GetParent(box); d is not null; d = VisualTreeHelper.GetParent(d))
        {
            if (d is DataGridColumnHeader) return false;
            if (d is ItemsControl owner && GetIsEnabled(owner)) return owner == list;
        }
        return false;
    }

    /// <summary>The tick box in the row under the mouse: in a table the one in the same column as the first box, in a list the row's box.</summary>
    private static CheckBox? TickBoxAt(ItemsControl list, Point at, CheckBox start)
    {
        if (list.InputHitTest(at) is not DependencyObject hit) return null;
        if (list is DataGrid)
        {
            if (Ancestor<DataGridRow>(hit, list) is not { } row || Ancestor<DataGridCell>(start, list) is not { } cell) return null;
            return cell.Column.GetCellContent(row) is { } content ? Descendants<CheckBox>(content).Prepend(content as CheckBox)
                .FirstOrDefault(b => b is not null && IsListTickBox(b, list)) : null;
        }
        if (ItemsControl.ContainerFromElement(list, hit) is not DependencyObject container) return null;
        return Descendants<CheckBox>(container).FirstOrDefault(b => IsListTickBox(b, list));
    }

    private static readonly MethodInfo ClickMethod =
        typeof(ButtonBase).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!;

    /// <summary>Clicks the box if it doesn't already show the tick, so its binding, Click handler and command all run as for a real click.</summary>
    private static void Press(CheckBox box, bool on)
    {
        if ((box.IsChecked == true) != on && box.IsEnabled) ClickMethod.Invoke(box, null);
    }

    private static T? Ancestor<T>(DependencyObject from, DependencyObject stop) where T : DependencyObject
    {
        for (var d = from; d is not null && d != stop; d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            if (d is T found) return found;
        return null;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) yield return t;
            foreach (var deeper in Descendants<T>(child)) yield return deeper;
        }
    }

    /// <summary>The selection box: a see-through rectangle in the accent color.</summary>
    private sealed class BandAdorner : Adorner
    {
        private readonly Point _from;
        private Point _to;

        public BandAdorner(UIElement table, Point from) : base(table)
        {
            _from = _to = from;
            IsHitTestVisible = false;
        }

        public Point To
        {
            set { _to = value; InvalidateVisual(); }
        }

        protected override void OnRender(DrawingContext dc)
        {
            var accent = (AdornedElement as FrameworkElement)?.TryFindResource("Accent") as SolidColorBrush ?? Brushes.SteelBlue;
            var fill = new SolidColorBrush(accent.Color) { Opacity = 0.18 };
            var rect = new Rect(_from, _to);
            rect.Intersect(new Rect(AdornedElement.RenderSize));
            if (!rect.IsEmpty) dc.DrawRectangle(fill, new Pen(accent, 1), rect);
        }
    }
}
