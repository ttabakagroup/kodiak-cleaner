namespace KodiakCleaner.App.ViewModels;

/// <summary>One entry in the sidebar.</summary>
public sealed class NavPage : ObservableObject
{
    private readonly Action<NavPage> _onSelected;

    public NavPage(string title, string glyph, object content, Action<NavPage> onSelected, string badge = "", string key = "")
    {
        Key = key;
        Title = title;
        Glyph = glyph;
        Badge = badge;
        Content = content;
        _onSelected = onSelected;
    }

    /// <summary>The page's --page= name, saved in the Favorites list ("" for Settings, System Information and About).</summary>
    public string Key { get; }
    public bool CanPin => Key.Length > 0;

    private bool _isFavorite;
    /// <summary>Pinned to Favorites at the top of the sidebar.</summary>
    public bool IsFavorite { get => _isFavorite; set => Set(ref _isFavorite, value); }

    private bool _isDragging;
    /// <summary>Being dragged in the sidebar: the row is outlined so it's clear what is moving.</summary>
    public bool IsDragging { get => _isDragging; set => Set(ref _isDragging, value); }

    public string Title { get; }
    public string Glyph { get; }
    /// <summary>A small second icon drawn over the bottom right of <see cref="Glyph"/> (Drive Wiper: a drive with a bin).</summary>
    public string Badge { get; }
    public bool HasBadge => Badge.Length > 0;
    public object Content { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!Set(ref _isSelected, value)) return;
            if (value) _onSelected(this);
        }
    }
}

/// <summary>One option in a group of radio-style choices (light or dark, wipe passes, ...).</summary>
public sealed class Choice<T> : ObservableObject
{
    private readonly Action<T> _onSelected;

    public Choice(string label, T value, bool isSelected, Action<T> onSelected)
    {
        Label = label;
        Value = value;
        _isSelected = isSelected;
        _onSelected = onSelected;
    }

    public string Label { get; }
    public T Value { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!Set(ref _isSelected, value)) return;
            if (value) _onSelected(Value);
        }
    }

    private bool _isRecommended;
    /// <summary>Shows a Recommended tag on the choice (Drive Wiper's format for the ticked drive).</summary>
    public bool IsRecommended { get => _isRecommended; set => Set(ref _isRecommended, value); }
}

/// <summary>A color scheme tile on the Appearance page. Classic, Glacier and Lagoon show a lock for non-supporters (visible, not hidden).</summary>
public sealed class PaletteChoice : ObservableObject
{
    private readonly Action<PaletteChoice> _onPicked;

    public PaletteChoice(Palette palette, string name, string description, bool locked, Action<PaletteChoice> onPicked)
    {
        Palette = palette;
        Name = name;
        Description = description;
        IsLocked = locked;
        _onPicked = onPicked;
        PickCommand = new RelayCommand(() => _onPicked(this));
    }

    public Palette Palette { get; }
    public string Name { get; }
    public string Description { get; }
    public bool IsLocked { get; }
    public System.Windows.Input.ICommand PickCommand { get; }

    public bool IsSelected => !IsLocked && ThemeManager.ActivePalette == Palette;

    public void Refresh() => OnPropertyChanged(nameof(IsSelected));
}

/// <summary>An accent color swatch on the Appearance page. The empty name is the color scheme's own accent.</summary>
public sealed class AccentChoice : ObservableObject
{
    private readonly Action<AccentChoice> _onPicked;

    public AccentChoice(string name, string label, Action<AccentChoice> onPicked)
    {
        Name = name;
        Label = label;
        _onPicked = onPicked;
    }

    public string Name { get; }
    public string Label { get; }

    public System.Windows.Media.Brush? Brush => Name == "" ? ThemeManager.SchemeAccent
        : ThemeManager.SchemeColors is { } scheme ? AccentColors.BrushOf(Name, scheme, ThemeManager.IsDark) : null;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!Set(ref _isSelected, value)) return;
            if (value) _onPicked(this);
        }
    }

    /// <summary>Updates the swatch after the scheme, light or dark mode or the Windows accent changed.</summary>
    public void Refresh(string current)
    {
        _isSelected = current == Name;
        OnPropertyChanged(nameof(IsSelected));
        OnPropertyChanged(nameof(Brush));
    }
}

/// <summary>A page that reads its list the first time it is shown (MainViewModel calls this on navigation), not at startup.</summary>
public interface ILoadOnFirstVisit
{
    void EnsureLoaded();
}
