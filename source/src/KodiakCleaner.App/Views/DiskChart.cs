using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using KodiakCleaner.App.ViewModels;
using KodiakCleaner.Core.Appearance;
using KodiakCleaner.Core.Tools;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App.Views;

/// <summary>
/// Disk Analyzer colors follow the chosen scheme and accent (Ted, 2026-10-12): every kind of file gets the current accent's hue,
/// turned a little further round the color wheel per kind, in the scheme's button shade, so the map looks like part of Honey,
/// Classic or Glacier in light or dark, and the button label color always reads on it (WCAG AA). Other is the accent, nearly grey.
/// </summary>
public static class DiskColors
{
    // Degrees turned from the accent, by FileKind: Archives, Documents, Music, Other, Pictures, Programs, Videos.
    // The kinds that are usually biggest (Videos, Pictures, Music) sit closest to the accent itself.
    private static readonly double[] Turns = { -48, 48, -24, 0, 24, 80, 0 };
    private static (Brush Fill, Brush Label)[]? _cache;

    static DiskColors() => ThemeManager.Applied += () =>
    {
        _cache = null;
        Changed?.Invoke();
    };

    /// <summary>Raised after the scheme, mode or accent changes, so charts redraw.</summary>
    public static event Action? Changed;

    public static Brush Fill(FileKind kind) => Get()[(int)kind].Fill;
    public static Brush Label(FileKind kind) => Get()[(int)kind].Label;

    private static (Brush Fill, Brush Label)[] Get()
    {
        if (_cache is not null) return _cache;
        var res = Application.Current.Resources;
        Brush primary = res["Primary"] as Brush ?? Brushes.Transparent, onPrimary = res["OnPrimary"] as Brush ?? Brushes.Transparent;
        if (res["Accent"] is not SolidColorBrush accent || onPrimary is not SolidColorBrush label)
            return _cache = FileKinds.All.Select(_ => (primary, onPrimary)).ToArray();
        var accentHex = Hex(accent.Color);
        var labelHex = Hex(label.Color);
        return _cache = FileKinds.All.Select(k =>
        {
            var shade = AccentShades.ChartShade(accentHex, Turns[(int)k], k == FileKind.Other ? 0.2 : 1, labelHex);
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(shade));
            brush.Freeze();
            return ((Brush)brush, (Brush)label);
        }).ToArray();
    }

    private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}

/// <summary>
/// The Disk Analyzer's map: every folder and file in the open folder as a box sized by the space it takes and colored by the kind
/// of file it holds most. Folders big enough show the folders inside them too. Click a folder to look inside; hover for details.
/// </summary>
public sealed class TreemapView : FrameworkElement
{
    private const double Gap = 3, HeaderHeight = 24;
    private const int MaxDepth = 3;

    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(nameof(Items), typeof(IEnumerable), typeof(TreemapView),
        new FrameworkPropertyMetadata(null, (d, e) => ((TreemapView)d).ItemsChanged(e)));
    public static readonly DependencyProperty SelectedProperty = DependencyProperty.Register(nameof(Selected), typeof(DiskEntry), typeof(TreemapView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty HighlightKindProperty = DependencyProperty.Register(nameof(HighlightKind), typeof(FileKind?), typeof(TreemapView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty OpenCommandProperty = DependencyProperty.Register(nameof(OpenCommand), typeof(ICommand), typeof(TreemapView));

    public IEnumerable? Items { get => (IEnumerable?)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
    public DiskEntry? Selected { get => (DiskEntry?)GetValue(SelectedProperty); set => SetValue(SelectedProperty, value); }
    public FileKind? HighlightKind { get => (FileKind?)GetValue(HighlightKindProperty); set => SetValue(HighlightKindProperty, value); }
    /// <summary>Runs with a folder's <see cref="DiskEntry"/> when it's clicked.</summary>
    public ICommand? OpenCommand { get => (ICommand?)GetValue(OpenCommandProperty); set => SetValue(OpenCommandProperty, value); }

    private sealed record Tile(Rect Box, DiskEntry Entry, int Level, bool HasInner);

    private readonly List<Tile> _tiles = new();
    private Size _laidOutFor;
    private Tile? _hover;
    private readonly ToolTip _tip = new() { Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse };

    public TreemapView()
    {
        ClipToBounds = true;
        Focusable = false;
        ToolTip = _tip;
        ToolTipService.SetInitialShowDelay(this, 350);
        ToolTipService.SetBetweenShowDelay(this, 0);
        ToolTipService.SetShowDuration(this, 60000);
        Cursor = Cursors.Hand;
        _tip.Opened += (_, _) => { if (_hover is null) _tip.IsOpen = false; };
        Loaded += (_, _) => DiskColors.Changed += InvalidateVisual;
        Unloaded += (_, _) => DiskColors.Changed -= InvalidateVisual;
    }

    private void ItemsChanged(DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyCollectionChanged old) old.CollectionChanged -= Relayout;
        if (e.NewValue is INotifyCollectionChanged now) now.CollectionChanged += Relayout;
        Relayout(null, null);
    }

    private void Relayout(object? sender, NotifyCollectionChangedEventArgs? e)
    {
        _laidOutFor = Size.Empty;
        _hover = null;
        _tip.IsOpen = false;
        InvalidateVisual();
        if (Motion.Instance.Enabled && IsLoaded)
            BeginAnimation(OpacityProperty, new DoubleAnimation(0.35, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = new QuadraticEase() });
    }

    private void Layout(Size size)
    {
        _tiles.Clear();
        _laidOutFor = size;
        var items = Items?.OfType<DiskEntry>().Where(i => i.Bytes > 0).ToList() ?? new();
        AddTiles(items, new Rect(size), 1);
    }

    /// <summary>Lays out boxes in an area; a folder box with room shows its own folders inside, down to <see cref="MaxDepth"/>.</summary>
    private void AddTiles(IReadOnlyList<DiskEntry> items, Rect area, int level)
    {
        foreach (var t in Treemap.Layout(items.Select(i => (double)i.Bytes).ToList(), area.X, area.Y, area.Width, area.Height))
        {
            var entry = items[t.Index];
            var box = Shrink(new Rect(t.X, t.Y, t.Width, t.Height), level == 1 ? Gap / 2 : 1);
            if (box.Width < 2 || box.Height < 2) continue;
            // A folder holding just one lump of files stays one box: a lone "3 Files" box inside it would say nothing new.
            var inner = level < MaxDepth && box.Width >= 90 && box.Height >= 64 && entry.HasInside ? entry.Inner : null;
            bool hasInner = inner is { Count: > 0 };
            _tiles.Add(new Tile(box, entry, level, hasInner));
            if (hasInner)
                AddTiles(inner!, new Rect(box.X + 4, box.Y + HeaderHeight, Math.Max(0, box.Width - 8), Math.Max(0, box.Height - HeaderHeight - 4)), level + 1);
        }
    }

    private static Rect Shrink(Rect r, double by) =>
        new(r.X + by, r.Y + by, Math.Max(0, r.Width - 2 * by), Math.Max(0, r.Height - 2 * by));

    protected override void OnRender(DrawingContext dc)
    {
        var size = new Size(ActualWidth, ActualHeight);
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(size)); // the whole area takes the mouse
        if (size != _laidOutFor) Layout(size);

        var surface = Find("Surface");
        var text = Find("Text");
        var muted = Find("Muted");
        var ring = Find("FocusRing");
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var font = new Typeface(System.Windows.Documents.TextElement.GetFontFamily(this), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var bold = new Typeface(System.Windows.Documents.TextElement.GetFontFamily(this), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        var highlight = HighlightKind;

        foreach (var tile in _tiles)
        {
            var e = tile.Entry;
            var fill = DiskColors.Fill(e.Kind);
            var label = DiskColors.Label(e.Kind);
            bool dim = highlight is { } k && (tile.HasInner ? e.Folder?.KindBytes[(int)k] == 0 : e.Kind != k);
            if (dim) dc.PushOpacity(0.22);
            double radius = Radius(tile);

            if (tile.HasInner)
            {
                // A folder showing what's inside: a soft tint of its color, a header, then its own boxes.
                dc.DrawRoundedRectangle(surface, null, tile.Box, radius, radius);
                dc.PushOpacity(0.18);
                dc.DrawRoundedRectangle(fill, null, tile.Box, radius, radius);
                dc.Pop();
                dc.DrawRoundedRectangle(null, new Pen(fill, 1.5), tile.Box, radius, radius);
                var head = Text(e.Name, bold, 12, text, tile.Box.Width - 16, dpi);
                dc.DrawText(head, new Point(tile.Box.X + 8, tile.Box.Y + 4));
                var sizeText = Text(e.Size, font, 12, muted, tile.Box.Width - 24 - head.Width, dpi);
                if (head.Width + 30 < tile.Box.Width) dc.DrawText(sizeText, new Point(tile.Box.X + 16 + head.Width, tile.Box.Y + 4));
            }
            else
            {
                dc.DrawRoundedRectangle(fill, null, tile.Box, radius, radius);
                if (tile.Box.Width >= 46 && tile.Box.Height >= 22)
                {
                    double fontSize = tile.Level == 1 ? 12 : 11;
                    var name = Text(e.Name, tile.Level == 1 ? bold : font, fontSize, label, tile.Box.Width - 12, dpi);
                    dc.DrawText(name, new Point(tile.Box.X + 6, tile.Box.Y + 4));
                    if (tile.Box.Height >= 40)
                    {
                        dc.PushOpacity(0.85);
                        dc.DrawText(Text(e.Size, font, fontSize, label, tile.Box.Width - 12, dpi), new Point(tile.Box.X + 6, tile.Box.Y + 5 + name.Height));
                        dc.Pop();
                    }
                }
            }
            if (dim) dc.Pop();
        }

        if (Selected is { } selected && _tiles.FirstOrDefault(t => ReferenceEquals(t.Entry, selected)) is { } chosen)
            dc.DrawRoundedRectangle(null, new Pen(text, 2.5), Shrink(chosen.Box, -1), Radius(chosen) + 1, Radius(chosen) + 1);
        if (_hover is { } hover)
            dc.DrawRoundedRectangle(null, new Pen(ring, 2), Shrink(hover.Box, -1), Radius(hover) + 1, Radius(hover) + 1);
    }

    private static double Radius(Tile t) => t.Level == 1 ? 6 : 3;

    private Brush Find(string key) => TryFindResource(key) as Brush ?? Brushes.Transparent;

    private static FormattedText Text(string s, Typeface face, double size, Brush brush, double maxWidth, double dpi) => new(
        s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, size, brush, dpi)
    {
        MaxTextWidth = Math.Max(1, maxWidth),
        MaxLineCount = 1,
        Trimming = TextTrimming.CharacterEllipsis,
    };

    /// <summary>The smallest box under the point: a folder's inner box wins over the folder itself, except on its header.</summary>
    private Tile? HitTest(Point p)
    {
        Tile? found = null;
        foreach (var t in _tiles)
            if (t.Box.Contains(p) && (found is null || t.Level > found.Level)) found = t;
        return found;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hit = HitTest(e.GetPosition(this));
        if (ReferenceEquals(hit, _hover)) return;
        _hover = hit;
        InvalidateVisual();
        if (hit is null)
        {
            _tip.IsOpen = false;
            return;
        }
        _tip.Content = TipFor(hit.Entry);
        Cursor = hit.Entry.Folder is not null ? Cursors.Hand : Cursors.Arrow;
        if (_tip.IsOpen)
        {
            _tip.IsOpen = false;
            _tip.IsOpen = true; // follow the mouse to the new box
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = null;
        _tip.IsOpen = false;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (HitTest(e.GetPosition(this)) is not { } hit) return;
        Selected = hit.Entry;
        if (hit.Entry.Folder is not null && OpenCommand?.CanExecute(hit.Entry) == true)
        {
            _tip.IsOpen = false;
            OpenCommand.Execute(hit.Entry);
        }
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        if (HitTest(e.GetPosition(this)) is { } hit) Selected = hit.Entry; // the right-click menu acts on it
        _tip.IsOpen = false;
    }

    private static object TipFor(DiskEntry e)
    {
        var panel = new StackPanel { MaxWidth = 420 };
        panel.Children.Add(new TextBlock { Text = e.Name, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        foreach (var line in e.Details) panel.Children.Add(new TextBlock { Text = line, TextWrapping = TextWrapping.Wrap });
        return panel;
    }
}

/// <summary>A bar split by kind of file (Videos, Pictures and so on) for the open folder. Hovering a part highlights that kind in the map.</summary>
public sealed class KindBarView : FrameworkElement
{
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(nameof(Items), typeof(IEnumerable), typeof(KindBarView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, (d, e) =>
        {
            var bar = (KindBarView)d;
            if (e.OldValue is INotifyCollectionChanged old) old.CollectionChanged -= bar.Redraw;
            if (e.NewValue is INotifyCollectionChanged now) now.CollectionChanged += bar.Redraw;
        }));

    private void Redraw(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();
    public static readonly DependencyProperty HighlightKindProperty = DependencyProperty.Register(nameof(HighlightKind), typeof(FileKind?), typeof(KindBarView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable? Items { get => (IEnumerable?)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
    public FileKind? HighlightKind { get => (FileKind?)GetValue(HighlightKindProperty); set => SetValue(HighlightKindProperty, value); }

    private readonly List<(Rect Box, KindShare Kind)> _parts = new();

    public KindBarView()
    {
        Height = 14;
        Loaded += (_, _) => DiskColors.Changed += InvalidateVisual;
        Unloaded += (_, _) => DiskColors.Changed -= InvalidateVisual;
    }

    protected override void OnRender(DrawingContext dc)
    {
        _parts.Clear();
        var box = new Rect(0, 0, ActualWidth, ActualHeight);
        double r = box.Height / 2;
        dc.DrawRoundedRectangle(TryFindResource("Line") as Brush, null, box, r, r);
        var kinds = Items?.OfType<KindShare>().Where(k => k.Bytes > 0).ToList() ?? new();
        long total = kinds.Sum(k => k.Bytes);
        if (total <= 0) return;

        dc.PushClip(new RectangleGeometry(box, r, r));
        double x = 0;
        foreach (var k in kinds)
        {
            double w = box.Width * k.Bytes / total;
            var part = new Rect(x, 0, w, box.Height);
            _parts.Add((part, k));
            bool dim = HighlightKind is { } h && h != k.Kind;
            if (dim) dc.PushOpacity(0.25);
            dc.DrawRectangle(DiskColors.Fill(k.Kind), null, new Rect(x, 0, Math.Max(0, w - (x + w < box.Width - 0.5 ? 1.5 : 0)), box.Height));
            if (dim) dc.Pop();
            x += w;
        }
        dc.Pop();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var p = e.GetPosition(this);
        var hit = _parts.FirstOrDefault(part => part.Box.Contains(p)).Kind;
        HighlightKind = hit?.Kind;
        ToolTip = hit is null ? null : $"{hit.Name}: {hit.Size} ({hit.ShareText})";
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        HighlightKind = null;
    }
}
