using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace KodiakCleaner.App.Views;

/// <summary>
/// Gives every button, check box, switch, text box and drop-down a name screen readers can say (WCAG 2.1 4.1.2, audit 2026-10-24).
/// WPF only names a control from plain text content, so a button holding an icon and a label in a panel, an icon-only button, a
/// switch with its label beside it or a text box under a heading was read as just "button", "check box" or "edit". Registered
/// once at startup (<see cref="Register"/>), it fills in a name at each control's first layout, only where none is set, from the first of:
/// the label text inside the control; a label beside it (a Settings row's title, the text in front of a box; its caption becomes
/// the help text); for a table's tick box, "Select" and the row's first text; the tooltip; a text box's example hint (Tag).
/// An explicit <c>AutomationProperties.Name</c> or <c>LabeledBy</c> always wins, so set one where these guesses aren't right.
/// </summary>
public static class AccessibleNames
{
    private static bool _registered;

    /// <summary>Set once a control has been looked at, so later layouts skip it.</summary>
    private static readonly DependencyProperty CheckedProperty = DependencyProperty.RegisterAttached(
        "Checked", typeof(bool), typeof(AccessibleNames), new PropertyMetadata(false));

    public static void Register()
    {
        if (_registered) return;
        _registered = true;
        // SizeChanged, not Loaded: WPF raises Loaded only on elements with a Loaded handler of their own, so a class handler for
        // it never runs. SizeChanged comes with every control's first layout, when its label and content are in place.
        foreach (var type in new[] { typeof(ButtonBase), typeof(TextBox), typeof(PasswordBox), typeof(ComboBox), typeof(Slider) })
            EventManager.RegisterClassHandler(type, FrameworkElement.SizeChangedEvent, new SizeChangedEventHandler(OnFirstLayout));
    }

    private static void OnFirstLayout(object sender, SizeChangedEventArgs e)
    {
        if (sender is not Control control || (bool)control.GetValue(CheckedProperty)) return;
        control.SetValue(CheckedProperty, true);
        if (HasName(control)) return;
        try { Name(control); }
        catch (InvalidOperationException) { }  // a control torn down mid-layout: left as it was
    }

    private static bool HasName(Control c) =>
        !string.IsNullOrWhiteSpace(AutomationProperties.GetName(c)) || AutomationProperties.GetLabeledBy(c) is not null
        || BindingOperations.IsDataBound(c, AutomationProperties.NameProperty);

    private static void Name(Control c)
    {
        // Content with readable text of its own: WPF already names a plain string or a lone TextBlock; a panel of icon + label it doesn't.
        if (c is ContentControl cc)
        {
            if (cc.Content is string s) { if (IsReadable(s)) return; }
            else if (cc.Content is TextBlock tb && IsReadable(tb.Text)) return;
            else if (cc.Content is DependencyObject content && TextsIn(content) is { Count: > 0 } texts)
            {
                BindToTexts(c, texts);
                return;
            }
        }

        if (c is CheckBox && FindParent<DataGridCell>(c) is { } cell)
        {
            NameInCell(c, cell);
            // Rows are reused as the table scrolls, so the name follows the row's item.
            c.DataContextChanged += (_, _) => c.Dispatcher.BeginInvoke(() => NameInCell(c, cell), System.Windows.Threading.DispatcherPriority.Loaded);
            return;
        }

        // ComboBox items carry text but the box itself doesn't; a label beside it says what it picks.
        if (c is not ButtonBase || c is ToggleButton { Content: null })
        {
            if (LabelBeside(c) is { } label)
            {
                // Bound to the label's text rather than LabeledBy: text inside a template (every Settings page) has no automation peer
                // for LabeledBy to read, so the name came out empty.
                BindingOperations.SetBinding(c, AutomationProperties.NameProperty, new Binding(nameof(TextBlock.Text)) { Source = label.Title });
                if (label.Caption is { } caption && string.IsNullOrEmpty(AutomationProperties.GetHelpText(c)))
                    BindingOperations.SetBinding(c, AutomationProperties.HelpTextProperty, new Binding(nameof(TextBlock.Text)) { Source = caption });
                return;
            }
        }

        if (c.ToolTip is string || BindingOperations.IsDataBound(c, FrameworkElement.ToolTipProperty))
        {
            BindingOperations.SetBinding(c, AutomationProperties.NameProperty, new Binding(nameof(c.ToolTip)) { Source = c });
            return;
        }

        if (c is TextBox { Tag: string hint } && IsReadable(hint)) AutomationProperties.SetName(c, hint);
    }

    /// <summary>The name follows the label texts, so a button whose label changes ("Scan", "Stop") is read with its new label.
    /// Icon glyphs among them (a bound glyph is only known once bound) are left out by <see cref="JoinTexts"/>.</summary>
    private static void BindToTexts(Control c, IReadOnlyList<TextBlock> texts)
    {
        var all = new MultiBinding { Converter = JoinTexts.Instance };
        foreach (var t in texts) all.Bindings.Add(new Binding(nameof(TextBlock.Text)) { Source = t });
        BindingOperations.SetBinding(c, AutomationProperties.NameProperty, all);
    }

    private sealed class JoinTexts : IMultiValueConverter
    {
        public static readonly JoinTexts Instance = new();
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            string.Join(" ", values.OfType<string>().Where(IsReadable).Select(v => v.Trim()));
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture) =>
            throw new NotSupportedException();
    }

    private static void NameInCell(Control box, DataGridCell cell) => AutomationProperties.SetName(box, CellName(cell, box));

    /// <summary>A table cell's tick box: "Select" plus the row's first readable text; a switch column: the column's header.</summary>
    private static string CellName(DataGridCell cell, Control box)
    {
        if (cell.Column?.Header is string header && IsReadable(header)) return header;
        var row = FindParent<DataGridRow>(cell);
        var grid = FindParent<DataGrid>(cell);
        if (row is not null && grid is not null)
            foreach (var column in grid.Columns.OrderBy(col => col.DisplayIndex))
                if (column.GetCellContent(row) is { } part && part != box && TextIn(part) is { Length: > 0 } text)
                    return "Select " + text;
        return "Select";
    }

    private record Label(TextBlock Title, TextBlock? Caption);

    /// <summary>The label next to a control in its panel: the nearest text before it, else the nearest after it (a Settings row
    /// keeps the switch docked right, written before its title). A panel of texts gives the title and, below it, the caption.</summary>
    private static Label? LabelBeside(Control c)
    {
        if (VisualTreeHelper.GetParent(c) is not Panel panel) return null;
        var siblings = panel.Children.OfType<UIElement>().ToList();
        if (panel is Grid) siblings = siblings.Where(s => Grid.GetRow(s) == Grid.GetRow(c)).ToList();
        var at = siblings.IndexOf(c);
        if (at < 0) return null;
        for (var i = at - 1; i >= 0; i--)
        {
            if (siblings[i] is Control) break;  // another control in between: its label isn't ours
            if (LabelIn(siblings[i]) is { } before) return before;
        }
        for (var i = at + 1; i < siblings.Count; i++)
        {
            if (siblings[i] is Control) break;
            if (LabelIn(siblings[i]) is { } after) return after;
        }
        return null;
    }

    private static Label? LabelIn(UIElement element)
    {
        if (element.Visibility == Visibility.Collapsed) return null;
        if (element is TextBlock tb) return IsReadable(tb.Text) ? new Label(tb, null) : null;
        if (element is not Panel p) return null;
        var texts = p.Children.OfType<TextBlock>().Where(t => IsReadable(t.Text)).ToList();
        return texts.Count == 0 ? null : new Label(texts[0], texts.Count > 1 ? texts[1] : null);
    }

    /// <summary>The readable texts inside an element (icon glyphs and hidden parts left out), in order.</summary>
    private static List<TextBlock> TextsIn(DependencyObject root)
    {
        var found = new List<TextBlock>();
        void Walk(DependencyObject d)
        {
            if (d is UIElement { Visibility: Visibility.Collapsed }) return;
            if (d is TextBlock tb)
            {
                // A bound label may still be empty at first layout; it is read once its binding fills it.
                if (!IsIconFont(tb) && (IsReadable(tb.Text) || BindingOperations.IsDataBound(tb, TextBlock.TextProperty))) found.Add(tb);
                return;
            }
            var logical = LogicalTreeHelper.GetChildren(d).OfType<DependencyObject>().ToList();
            foreach (var child in logical) Walk(child);
            // Parts made from a template (a table's template columns) aren't logical children; their visuals are.
            if (logical.Count == 0 && d is Visual)
                for (var i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++) Walk(VisualTreeHelper.GetChild(d, i));
        }
        Walk(root);
        return found;
    }

    private static string TextIn(DependencyObject root) => string.Join(" ", TextsIn(root).Where(t => IsReadable(t.Text)).Select(t => t.Text.Trim()));

    /// <summary>Text drawn in the icon fonts (the Icon style): glyphs, never words.</summary>
    private static bool IsIconFont(TextBlock tb) => tb.FontFamily?.Source is { } font && (font.Contains("Icons") || font.Contains("MDL2"));

    /// <summary>Text with at least one letter or digit outside the icon fonts' private-use glyphs.</summary>
    private static bool IsReadable(string? s) => !string.IsNullOrWhiteSpace(s) && s.Any(ch => char.IsLetterOrDigit(ch) && ch is < '' or > '');

    private static T? FindParent<T>(DependencyObject d) where T : DependencyObject
    {
        for (var p = VisualTreeHelper.GetParent(d); p is not null; p = VisualTreeHelper.GetParent(p))
            if (p is T t) return t;
        return null;
    }
}
