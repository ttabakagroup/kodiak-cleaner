using System.Collections;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace KodiakCleaner.App.Views;

/// <summary>
/// Drag to select (Ted, 0.8.90; outline, drag from anywhere on the card and whole-row highlight 0.8.92), switched on for every table and list by the DataGrid and
/// ItemsControl styles in Controls.xaml:
/// <list type="bullet">
/// <item>Press anywhere on a list's card (a row, the empty space under the rows, the card's edge) and drag: an outline in the
/// accent color follows the mouse, and every row it covers is highlighted (tables) or ticked (lists of tick boxes). Dragging past
/// the top or bottom scrolls. Ctrl or Shift keeps what was already highlighted.</item>
/// <item>Starting on a tick box ticks every covered row's box, or unticks them when that first box was ticked.</item>
/// <item>Tick-box lists highlight the whole row: the row under the mouse, and every ticked row, as tables do. Clicking anywhere on
/// such a row ticks or unticks it.</item>
/// <item>Space ticks or unticks every highlighted table row at once.</item>
/// </list>
/// </summary>
public static class ListSelect
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(ListSelect), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject d) => (bool)d.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject d, bool value) => d.SetValue(IsEnabledProperty, value);

    /// <summary>On a card (or a list without one): the lists on it and the drag under way.</summary>
    private sealed class Surface
    {
        public required FrameworkElement Element;
        public readonly List<ItemsControl> Lists = new();
        public Drag? Drag;
    }

    private static readonly DependencyProperty SurfaceProperty = DependencyProperty.RegisterAttached(
        "Surface", typeof(Surface), typeof(ListSelect), new PropertyMetadata(null));

    private static readonly DependencyProperty RowsProperty = DependencyProperty.RegisterAttached(
        "Rows", typeof(RowAdorner), typeof(ListSelect), new PropertyMetadata(null));

    private enum Mode { Highlight, TickTable, TickList }

    private sealed class Drag
    {
        public required ItemsControl List;
        public required Mode Mode;
        public Point Start;                          // in the surface's coordinates
        public bool Began;
        public CheckBox? StartBox;                   // the tick box (or the row's box) pressed first
        public bool Target = true;                   // the tick every covered row takes
        public HashSet<object> Kept = new();         // rows highlighted before (Ctrl or Shift held)
        public int AnchorIndex;                      // tables: row the drag started on (-1 above the rows, Count below them)
        public double AnchorOffset;                  // ... and how far into (or past) that row
        public FrameworkElement? AnchorSpace;        // lists: the scrolled content the start point sits in
        public Point AnchorLocal;
        public readonly Dictionary<object, bool> Original = new();     // table rows ticked by this drag, with their tick before
        public readonly Dictionary<CheckBox, bool> OriginalBoxes = new();
        public PropertyInfo? Tick, CanTick;
        public ScrollViewer? Scroller;
        public BandAdorner? Band;
        public DispatcherTimer? Timer;
    }

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ItemsControl list) return;
        list.Loaded -= Loaded;
        list.Unloaded -= Unloaded;
        list.PreviewKeyDown -= Key;
        if ((bool)e.NewValue)
        {
            list.Loaded += Loaded;
            list.Unloaded += Unloaded;
            if (list is DataGrid) list.PreviewKeyDown += Key;
            if (list.IsLoaded) Loaded(list, new RoutedEventArgs());
        }
        else Unloaded(list, new RoutedEventArgs());
    }

    // ---------------------------------------------------------------- where a drag can start

    private static void Loaded(object sender, RoutedEventArgs e)
    {
        var list = (ItemsControl)sender;
        var element = CardOf(list) ?? (FrameworkElement)list;
        if (element.GetValue(SurfaceProperty) is not Surface surface)
        {
            surface = new Surface { Element = element };
            element.SetValue(SurfaceProperty, surface);
            element.PreviewMouseLeftButtonDown += Down;
            element.PreviewMouseMove += Move;
            element.PreviewMouseLeftButtonUp += Up;
            element.LostMouseCapture += Lost;
        }
        if (!surface.Lists.Contains(list)) surface.Lists.Add(list);
        list.SetValue(SurfaceProperty, surface);
        if (list is not DataGrid) RowAdorner.Attach(list);
    }

    private static void Unloaded(object sender, RoutedEventArgs e)
    {
        var list = (ItemsControl)sender;
        if (list.GetValue(SurfaceProperty) is Surface surface)
        {
            if (surface.Drag?.List == list) Finish(surface);
            surface.Lists.Remove(list);
            if (surface.Lists.Count == 0)
            {
                surface.Element.PreviewMouseLeftButtonDown -= Down;
                surface.Element.PreviewMouseMove -= Move;
                surface.Element.PreviewMouseLeftButtonUp -= Up;
                surface.Element.LostMouseCapture -= Lost;
                surface.Element.ClearValue(SurfaceProperty);
            }
        }
        list.ClearValue(SurfaceProperty);
        RowAdorner.Detach(list);
    }

    /// <summary>The card (a Border with the Card style) a list sits on, so a drag can start anywhere on it.</summary>
    private static FrameworkElement? CardOf(ItemsControl list)
    {
        var card = list.TryFindResource("Card");
        for (var d = VisualTreeHelper.GetParent(list); d is not null; d = VisualTreeHelper.GetParent(d))
        {
            if (d is Border b && card is not null && b.Style == card) return b;
            if (d is Window or UserControl) return null;
        }
        return null;
    }

    private static Surface? SurfaceOf(object sender) => (sender as DependencyObject)?.GetValue(SurfaceProperty) as Surface;

    private static void Down(object sender, MouseButtonEventArgs e)
    {
        if (SurfaceOf(sender) is not { } surface) return;
        Finish(surface);
        if (e.ClickCount > 1 || e.OriginalSource is not DependencyObject source) return;
        var root = surface.Element;
        if (Ancestor<ScrollBar>(source, root) is not null || Ancestor<Thumb>(source, root) is not null ||
            Ancestor<DataGridColumnHeader>(source, root) is not null || Ancestor<DataGridColumnHeadersPresenter>(source, root) is not null ||
            Ancestor<TextBoxBase>(source, root) is not null || Ancestor<ComboBox>(source, root) is not null ||
            Ancestor<Slider>(source, root) is not null || Ancestor<Hyperlink>(source, root) is not null) return;

        var start = e.GetPosition(root);
        var keep = (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != 0;
        Drag? drag = null;
        if (Ancestor<CheckBox>(source, root) is { } box && OwnerOf(box) is { } boxList && surface.Lists.Contains(boxList))
        {
            drag = new Drag { List = boxList, Mode = boxList is DataGrid ? Mode.TickTable : Mode.TickList, StartBox = box };
        }
        else if (Ancestor<ButtonBase>(source, root) is not null) return;   // buttons, switches and links keep their own clicks
        else
        {
            var list = surface.Lists.Where(l => l.IsVisible && IsAncestorOf(l, source)).OrderByDescending(Depth).FirstOrDefault()
                       ?? Nearest(surface, start);
            if (list is DataGrid { SelectionMode: DataGridSelectionMode.Extended } grid && grid.Items.Count > 0)
            {
                drag = new Drag { List = grid, Mode = Mode.Highlight };
                if (Ancestor<DataGridRow>(source, grid) is null && !keep && IsAncestorOf(grid, source)) grid.UnselectAll();
            }
            else if (list is not null and not DataGrid && TickBoxes(list).Any())
                drag = new Drag { List = list, Mode = Mode.TickList, StartBox = RowBoxAt(list, e.GetPosition(list)) };
        }
        if (drag is null) return;
        drag.Start = start;
        if (drag.List is DataGrid g && keep) drag.Kept = g.SelectedItems.Cast<object>().ToHashSet();
        surface.Drag = drag;
    }

    private static int Depth(DependencyObject d)
    {
        var n = 0;
        for (var p = VisualTreeHelper.GetParent(d); p is not null; p = VisualTreeHelper.GetParent(p)) n++;
        return n;
    }

    /// <summary>The list nearest the point, for a drag started on the card around it.</summary>
    private static ItemsControl? Nearest(Surface surface, Point at) => surface.Lists
        .Where(l => l.IsVisible && (l is DataGrid || TickBoxes(l).Any()))
        .OrderBy(l => Distance(BoundsIn(l, surface.Element), at)).FirstOrDefault();

    private static double Distance(Rect r, Point p)
    {
        var dx = Math.Max(Math.Max(r.Left - p.X, 0), p.X - r.Right);
        var dy = Math.Max(Math.Max(r.Top - p.Y, 0), p.Y - r.Bottom);
        return dx * dx + dy * dy;
    }

    // ---------------------------------------------------------------- dragging

    private static void Move(object sender, MouseEventArgs e)
    {
        if (SurfaceOf(sender) is not { Drag: { } drag } surface) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            Finish(surface);
            return;
        }
        if (!drag.Began)
        {
            var at = e.GetPosition(surface.Element);
            if (Math.Abs(at.X - drag.Start.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(at.Y - drag.Start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            Begin(surface, drag);
        }
        Update(surface, drag);
        e.Handled = true;
    }

    private static void Begin(Surface surface, Drag drag)
    {
        drag.Began = true;
        var list = drag.List;
        if (drag.StartBox is { } box) drag.Target = box.IsChecked != true;
        if (list is DataGrid grid)
        {
            drag.Scroller = Descendants<ScrollViewer>(grid).FirstOrDefault();
            (drag.AnchorIndex, drag.AnchorOffset) = AnchorAt(grid, surface.Element, drag.Start.Y);
            if (drag.Mode == Mode.TickTable && TickProperties(grid) is var (tick, canTick))
                (drag.Tick, drag.CanTick) = (tick, canTick);
            else if (drag.Mode == Mode.TickTable) drag.Mode = Mode.Highlight;
        }
        else
        {
            drag.Scroller = Ancestor<ScrollViewer>(list, surface.Element) ?? Descendants<ScrollViewer>(list).FirstOrDefault();
            drag.AnchorSpace = drag.Scroller?.Content as FrameworkElement is { } content && IsAncestorOf(content, list) ? content : surface.Element;
            drag.AnchorLocal = surface.Element.TranslatePoint(drag.Start, drag.AnchorSpace);
        }
        if (AdornerLayer.GetAdornerLayer(surface.Element) is { } layer)
        {
            drag.Band = new BandAdorner(surface.Element);
            layer.Add(drag.Band);
        }
        // Taking the mouse from a pressed tick box means letting go doesn't click it again.
        list.CaptureMouse();
        if (drag.Mode == Mode.Highlight) list.Focus();
        drag.Timer = new DispatcherTimer(TimeSpan.FromMilliseconds(40), DispatcherPriority.Input, (_, _) => AutoScroll(surface), list.Dispatcher);
        drag.Timer.Start();
    }

    /// <summary>Dragging past the top or bottom of the rows scrolls them, a line at a time.</summary>
    private static void AutoScroll(Surface surface)
    {
        if (surface.Drag is not { Began: true, Scroller: { } scroller } drag) return;
        if (Mouse.LeftButton != MouseButtonState.Pressed)
        {
            Finish(surface);
            return;
        }
        var view = Viewport(scroller, surface.Element);
        var at = Mouse.GetPosition(surface.Element);
        if (at.Y < view.Top && scroller.VerticalOffset > 0) scroller.LineUp();
        else if (at.Y > view.Bottom && scroller.VerticalOffset < scroller.ScrollableHeight) scroller.LineDown();
        else return;
        scroller.UpdateLayout();
        Update(surface, drag);
    }

    private static void Update(Surface surface, Drag drag)
    {
        var root = surface.Element;
        var at = Mouse.GetPosition(root);
        var anchor = drag.List is DataGrid grid ? new Point(drag.Start.X, AnchorY(grid, root, drag)) : drag.AnchorSpace!.TranslatePoint(drag.AnchorLocal, root);
        drag.Band?.Show(anchor, at);
        var box = new Rect(anchor, at);

        if (drag.List is DataGrid table)
        {
            var current = AnchorAt(table, root, at.Y).Index;
            var (lo, hi) = (Math.Min(drag.AnchorIndex, current), Math.Max(drag.AnchorIndex, current));
            var covered = new List<object>();
            for (var i = Math.Max(lo, 0); i <= Math.Min(hi, table.Items.Count - 1); i++) covered.Add(table.Items[i]);
            if (drag.Mode == Mode.TickTable) TickRows(drag, covered);
            else Highlight(table, drag.Kept.Concat(covered));
            return;
        }

        // Lists of tick boxes: every row the outline covers takes the tick; rows it leaves go back to how they were.
        foreach (var list in surface.Lists.Where(l => l is not DataGrid && l.IsVisible))
            foreach (var tickBox in TickBoxes(list))
            {
                var row = RowOf(list, tickBox);
                if (!row.IsVisible) continue;
                var bounds = BoundsIn(row, root);
                if (bounds.IntersectsWith(box))
                {
                    if (!drag.OriginalBoxes.ContainsKey(tickBox)) drag.OriginalBoxes[tickBox] = tickBox.IsChecked == true;
                    Press(tickBox, drag.Target);
                }
                else if (drag.OriginalBoxes.Remove(tickBox, out var was)) Press(tickBox, was);
            }
    }

    private static void TickRows(Drag drag, List<object> covered)
    {
        var inside = covered.ToHashSet();
        foreach (var (item, was) in drag.Original.Where(o => !inside.Contains(o.Key)).ToList())
        {
            drag.Tick!.SetValue(item, was);
            drag.Original.Remove(item);
        }
        foreach (var item in covered.Where(i => drag.CanTick?.GetValue(i) is not false))
        {
            if (!drag.Original.ContainsKey(item)) drag.Original[item] = drag.Tick!.GetValue(item) is true;
            if (drag.Tick!.GetValue(item) is not bool on || on != drag.Target) drag.Tick.SetValue(item, drag.Target);
        }
    }

    private static readonly MethodInfo BeginUpdate = typeof(MultiSelector).GetMethod("BeginUpdateSelectedItems", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly MethodInfo EndUpdate = typeof(MultiSelector).GetMethod("EndUpdateSelectedItems", BindingFlags.Instance | BindingFlags.NonPublic)!;

    /// <summary>Highlights exactly these rows, as one change (one SelectionChanged).</summary>
    private static void Highlight(DataGrid grid, IEnumerable<object> rows)
    {
        var want = rows.ToHashSet();
        var have = grid.SelectedItems.Cast<object>().ToList();
        if (have.Count == want.Count && have.All(want.Contains)) return;
        BeginUpdate.Invoke(grid, null);
        try
        {
            foreach (var item in have.Where(i => !want.Contains(i))) grid.SelectedItems.Remove(item);
            var had = have.ToHashSet();
            foreach (var item in want.Where(i => !had.Contains(i))) grid.SelectedItems.Add(item);
        }
        finally { EndUpdate.Invoke(grid, null); }
    }

    private static void Up(object sender, MouseButtonEventArgs e)
    {
        if (SurfaceOf(sender) is not { Drag: { } drag } surface) return;
        // A plain click on a tick-box row's empty part ticks it too: the whole row is the target (Ted, 0.8.92).
        if (!drag.Began && drag.Mode == Mode.TickList && drag.StartBox is { } box && e.OriginalSource is DependencyObject source &&
            Ancestor<CheckBox>(source, surface.Element) is null && RowRect(drag.List, box).Contains(e.GetPosition(drag.List)))
        {
            Press(box, box.IsChecked != true);
            e.Handled = true;
        }
        if (drag.Began) e.Handled = drag.Mode != Mode.Highlight;
        Finish(surface);
    }

    private static void Lost(object sender, MouseEventArgs e)
    {
        if (SurfaceOf(sender) is { Drag: { Began: true } drag } surface && e.OriginalSource == drag.List) Finish(surface);
    }

    private static void Finish(Surface surface)
    {
        if (surface.Drag is not { } drag) return;
        surface.Drag = null;
        drag.Timer?.Stop();
        if (drag.Band is { } band) AdornerLayer.GetAdornerLayer(surface.Element)?.Remove(band);
        if (drag.Began && drag.List.IsMouseCaptured) drag.List.ReleaseMouseCapture();
    }

    // ---------------------------------------------------------------- tables

    /// <summary>
    /// The table row at a height (in the surface's coordinates), and how far into it: -1 above the first row, Count below the last.
    /// Between rows (a group heading) it is the nearer row; above or below the rows on screen, the first or last one on screen.
    /// </summary>
    private static (int Index, double Offset) AnchorAt(DataGrid grid, FrameworkElement root, double y)
    {
        var rows = ShownRows(grid, root);
        if (rows.Count == 0) return (grid.Items.Count, 0);
        var (first, last) = (rows[0], rows[^1]);
        if (y < first.Bounds.Top) return first.Index == 0 ? (-1, y - first.Bounds.Top) : (first.Index, 0);
        if (y >= last.Bounds.Bottom) return last.Index == grid.Items.Count - 1 ? (grid.Items.Count, y - last.Bounds.Bottom) : (last.Index, last.Bounds.Height);
        var nearest = rows.OrderBy(r => y < r.Bounds.Top ? r.Bounds.Top - y : y >= r.Bounds.Bottom ? y - r.Bounds.Bottom : 0).First();
        return (nearest.Index, Math.Clamp(y - nearest.Bounds.Top, 0, nearest.Bounds.Height));
    }

    /// <summary>Where the start of the drag is now (the rows may have scrolled): far off the card when its row has scrolled away.</summary>
    private static double AnchorY(DataGrid grid, FrameworkElement root, Drag drag)
    {
        var rows = ShownRows(grid, root);
        if (rows.Count == 0) return drag.Start.Y;
        var count = grid.Items.Count;
        var refIndex = Math.Clamp(drag.AnchorIndex, 0, count - 1);
        if (rows.FirstOrDefault(r => r.Index == refIndex) is { Row: not null } shown)
            return drag.AnchorIndex >= count ? shown.Bounds.Bottom + drag.AnchorOffset : shown.Bounds.Top + drag.AnchorOffset;
        return refIndex < rows[0].Index ? -10000 : 10000;
    }

    private sealed record ShownRow(DataGridRow Row, int Index, Rect Bounds);

    /// <summary>The table's rows on screen, top to bottom, in the surface's coordinates.</summary>
    private static List<ShownRow> ShownRows(DataGrid grid, FrameworkElement root)
    {
        var view = Descendants<ScrollViewer>(grid).FirstOrDefault() is { } scroller ? Viewport(scroller, root) : BoundsIn(grid, root);
        var rows = new List<ShownRow>();
        foreach (var row in Descendants<DataGridRow>(grid))
        {
            if (!row.IsVisible || row.Item == CollectionView.NewItemPlaceholder) continue;
            var index = grid.ItemContainerGenerator.IndexFromContainer(row);
            if (index < 0) continue;
            var bounds = BoundsIn(row, root);
            if (bounds.Bottom > view.Top && bounds.Top < view.Bottom) rows.Add(new ShownRow(row, index, bounds));
        }
        return rows.OrderBy(r => r.Bounds.Top).ToList();
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

    // ---------------------------------------------------------------- lists of tick boxes

    /// <summary>The list whose row this tick box is in (not a header's Select All box, a switch, or a greyed-out box).</summary>
    private static ItemsControl? OwnerOf(CheckBox box)
    {
        if (!box.IsEnabled || box.IsThreeState || box.Style == box.TryFindResource("ToggleSwitch")) return null;
        for (DependencyObject? d = VisualTreeHelper.GetParent(box); d is not null; d = VisualTreeHelper.GetParent(d))
        {
            if (d is DataGridColumnHeader) return null;
            if (d is ItemsControl owner && GetIsEnabled(owner)) return owner;
        }
        return null;
    }

    private static IEnumerable<CheckBox> TickBoxes(ItemsControl list) => Descendants<CheckBox>(list).Where(b => OwnerOf(b) == list);

    /// <summary>A row of a list: the item's container, or the box itself.</summary>
    private static FrameworkElement RowOf(ItemsControl list, CheckBox box) =>
        ItemsControl.ContainerFromElement(list, box) as FrameworkElement ?? box;

    /// <summary>The tick box of the row at a point (in the list's coordinates), counting the row's full width.</summary>
    private static CheckBox? RowBoxAt(ItemsControl list, Point at) =>
        TickBoxes(list).FirstOrDefault(b => b.IsVisible && RowRect(list, b).Contains(at));

    /// <summary>A tick-box row across the full width of its list, in the list's coordinates.</summary>
    private static Rect RowRect(ItemsControl list, CheckBox box) =>
        BoundsIn(RowOf(list, box), list) is { IsEmpty: false } r ? new Rect(0, r.Top, list.ActualWidth, r.Height) : Rect.Empty;

    private static readonly MethodInfo ClickMethod =
        typeof(ButtonBase).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!;

    /// <summary>Clicks the box if it doesn't already show the tick, so its binding, Click handler and command all run as for a real click.</summary>
    private static void Press(CheckBox box, bool on)
    {
        if ((box.IsChecked == true) != on && box.IsEnabled) ClickMethod.Invoke(box, null);
    }

    // ---------------------------------------------------------------- helpers

    private static Rect BoundsIn(FrameworkElement element, Visual root)
    {
        try { return element.TransformToVisual(root).TransformBounds(new Rect(element.RenderSize)); }
        catch (InvalidOperationException) { return Rect.Empty; }
    }

    /// <summary>The part of a scroller that shows the rows (below a table's column headings), in the surface's coordinates.</summary>
    private static Rect Viewport(ScrollViewer scroller, FrameworkElement root) =>
        scroller.Template?.FindName("PART_ScrollContentPresenter", scroller) is FrameworkElement presenter ? BoundsIn(presenter, root) : BoundsIn(scroller, root);

    private static bool IsAncestorOf(DependencyObject ancestor, DependencyObject d)
    {
        for (var p = d; p is not null; p = p is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(p) : LogicalTreeHelper.GetParent(p))
            if (p == ancestor) return true;
        return false;
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

    /// <summary>A brush from the theme dictionary (null if it is missing, and nothing is drawn).</summary>
    private static SolidColorBrush? Themed(FrameworkElement element, string key) => element.TryFindResource(key) as SolidColorBrush;

    /// <summary>The selection outline: a see-through rectangle with an accent-colored edge, kept inside the card.</summary>
    private sealed class BandAdorner : Adorner
    {
        private Rect _rect = Rect.Empty;

        public BandAdorner(UIElement surface) : base(surface) => IsHitTestVisible = false;

        public void Show(Point from, Point to)
        {
            _rect = new Rect(from, to);
            _rect.Intersect(new Rect(AdornedElement.RenderSize));
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (_rect.IsEmpty) return;
            var surface = (FrameworkElement)AdornedElement;
            if (Themed(surface, "Accent") is not { } accent) return;
            var fill = new SolidColorBrush(accent.Color) { Opacity = 0.16 };
            dc.DrawRoundedRectangle(fill, new Pen(accent, 1.5), _rect, 3, 3);
        }
    }

    /// <summary>
    /// Whole-row highlight for lists of tick boxes, matching tables: the row under the mouse (RowHover) and every ticked row
    /// (AccentTint), across the full width of the list, kept inside the part that scrolls.
    /// </summary>
    private sealed class RowAdorner : Adorner
    {
        private readonly ItemsControl _list;
        private ScrollViewer? _scroller;

        private RowAdorner(ItemsControl list) : base(list)
        {
            _list = list;
            IsHitTestVisible = false;
        }

        public static void Attach(ItemsControl list)
        {
            if (list.GetValue(RowsProperty) is RowAdorner || AdornerLayer.GetAdornerLayer(list) is not { } layer) return;
            var rows = new RowAdorner(list);
            layer.Add(rows);
            list.SetValue(RowsProperty, rows);
            list.MouseMove += rows.Changed;
            list.MouseLeave += rows.Changed;
            list.SizeChanged += rows.Changed;
            list.IsVisibleChanged += rows.VisibleChanged;
            list.AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler(rows.Changed));
            list.AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler(rows.Changed));
            list.ItemContainerGenerator.ItemsChanged += rows.Changed;
            rows._scroller = Ancestor<ScrollViewer>(list, Window.GetWindow(list) ?? (DependencyObject)list);
            if (rows._scroller is not null) rows._scroller.ScrollChanged += rows.Changed;
        }

        public static void Detach(ItemsControl list)
        {
            if (list.GetValue(RowsProperty) is not RowAdorner rows) return;
            list.MouseMove -= rows.Changed;
            list.MouseLeave -= rows.Changed;
            list.SizeChanged -= rows.Changed;
            list.IsVisibleChanged -= rows.VisibleChanged;
            list.RemoveHandler(ToggleButton.CheckedEvent, new RoutedEventHandler(rows.Changed));
            list.RemoveHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler(rows.Changed));
            list.ItemContainerGenerator.ItemsChanged -= rows.Changed;
            if (rows._scroller is not null) rows._scroller.ScrollChanged -= rows.Changed;
            AdornerLayer.GetAdornerLayer(list)?.Remove(rows);
            list.ClearValue(RowsProperty);
        }

        private void Changed(object? sender, EventArgs e) => InvalidateVisual();

        private void VisibleChanged(object sender, DependencyPropertyChangedEventArgs e) => InvalidateVisual();

        protected override void OnRender(DrawingContext dc)
        {
            if (!_list.IsVisible) return;   // a page that isn't shown keeps its list (and this layer) alive
            var boxes = TickBoxes(_list).Where(b => b.IsVisible && b.ReadLocalValue(StyleProperty) == DependencyProperty.UnsetValue).ToList();
            if (boxes.Count == 0) return;
            if (_scroller is not null) dc.PushClip(new RectangleGeometry(Viewport(_scroller, _list)));
            var hover = Themed(_list, "RowHover");
            var ticked = Themed(_list, "AccentTint");
            var mouse = Mouse.GetPosition(_list);
            foreach (var box in boxes)
            {
                var row = RowRect(_list, box);
                if (row.IsEmpty) continue;
                var over = _list.IsMouseOver && row.Contains(mouse);
                if (box.IsChecked == true) dc.DrawRoundedRectangle(ticked, null, row, 6, 6);
                if (over) dc.DrawRoundedRectangle(hover, null, row, 6, 6);
            }
            if (_scroller is not null) dc.Pop();
        }
    }
}
