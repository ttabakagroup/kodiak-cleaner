using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;

namespace KodiakCleaner.App.ViewModels;

/// <summary>
/// A running count of the ticked rows in a list, so a table's Select All box and its counts don't re-read every row on each tick
/// (Ted, 2026-10-24: re-counting tens of thousands of rows per tick froze Duplicate Finder, and Select All on a long list did the
/// same everywhere). It follows the list's adds and removes and each row's tick, and keeps the totals up to date as they happen.
/// Ticking many rows at once goes through <see cref="Bulk"/>, which tells the page once at the end instead of once per row.
/// </summary>
public sealed class CheckTally<T> where T : class, INotifyPropertyChanged
{
    private readonly ObservableCollection<T> _list;
    private readonly Func<T, bool> _isChecked;
    private readonly string _property;
    private readonly Action _changed;
    private readonly Dictionary<T, bool> _rows = new(ReferenceEqualityComparer.Instance);
    private int _bulk;
    private bool _pending;

    /// <param name="list">The rows.</param>
    /// <param name="isChecked">Whether a row is ticked.</param>
    /// <param name="property">The name of the row's tick property.</param>
    /// <param name="changed">Called after the totals change (once per <see cref="Bulk"/>), usually to raise AllChecked.</param>
    public CheckTally(ObservableCollection<T> list, Func<T, bool> isChecked, string property, Action changed)
    {
        _list = list;
        _isChecked = isChecked;
        _property = property;
        _changed = changed;
        list.CollectionChanged += ListChanged;
        Recount(notify: false); // the page reads the totals when it's ready; telling it from here could ask for them mid-setup
    }

    /// <summary>How many rows there are.</summary>
    public int Count => _rows.Count;

    /// <summary>How many rows are ticked.</summary>
    public int Checked { get; private set; }

    /// <summary>For a Select All box: true when every row is ticked, false when none is, otherwise null.</summary>
    public bool? State => Count > 0 && Checked == Count ? true : Checked > 0 ? null : false;

    /// <summary>Runs a change to many rows (Select All, Clear, Keep Newest…) and tells the page once at the end.</summary>
    public void Bulk(Action change)
    {
        _bulk++;
        try { change(); }
        finally
        {
            if (--_bulk == 0 && _pending)
            {
                Notify();
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    private void ListChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Replace:
                foreach (T row in e.OldItems ?? Array.Empty<T>()) Untrack(row);
                foreach (T row in e.NewItems ?? Array.Empty<T>()) Track(row);
                Notify();
                break;
            case NotifyCollectionChangedAction.Reset:
                Recount(notify: true);
                break;
        }
    }

    private void Recount(bool notify)
    {
        foreach (var row in _rows.Keys.ToList()) Untrack(row);
        foreach (var row in _list) Track(row);
        if (notify) Notify();
    }

    private void Track(T row)
    {
        if (_rows.ContainsKey(row)) return;
        var on = _isChecked(row);
        _rows[row] = on;
        if (on) Checked++;
        row.PropertyChanged += RowChanged;
    }

    private void Untrack(T row)
    {
        if (!_rows.Remove(row, out var on)) return;
        if (on) Checked--;
        row.PropertyChanged -= RowChanged;
    }

    private void RowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != _property || sender is not T row || !_rows.TryGetValue(row, out var was)) return;
        var on = _isChecked(row);
        if (on == was) return;
        _rows[row] = on;
        Checked += on ? 1 : -1;
        Notify();
    }

    private void Notify()
    {
        if (_bulk > 0) { _pending = true; return; }
        _pending = false;
        _changed();
    }
}
