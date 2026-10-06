using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using KodiakCleaner.App.ViewModels;

namespace KodiakCleaner.App.Views;

public partial class SettingsView : UserControl
{
    private bool _indexed;

    public SettingsView()
    {
        InitializeComponent();
        var sections = (CollectionViewSource)Resources["GroupedSections"];
        sections.Filter += (_, e) => e.Accepted = e.Item is not SettingsSection s || Vm is not { } vm || s.Matches(vm.SearchQuery);
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is SettingsViewModel old) old.PropertyChanged -= VmChanged;
            if (e.NewValue is SettingsViewModel vm) vm.PropertyChanged += VmChanged;
        };
    }

    private SettingsViewModel? Vm => DataContext as SettingsViewModel;

    private void VmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SettingsViewModel.SearchQuery) || Vm is not { } vm) return;
        if (!_indexed) Index(vm);
        var view = ((CollectionViewSource)Resources["GroupedSections"]).View;
        view.Refresh();
        vm.NoMatches = view.IsEmpty;
    }

    /// <summary>
    /// The first search reads the labels on every settings page (built off-screen from the page templates, so the index
    /// always matches what the pages show). Document pages (Terms, Privacy, Why It Was Made) match by title only.
    /// </summary>
    private void Index(SettingsViewModel vm)
    {
        _indexed = true;
        foreach (var section in vm.Sections)
        {
            if (section.Context is DocumentViewModel || Resources[section.Key + "Page"] is not DataTemplate template) continue;
            try
            {
                var labels = new List<string>();
                Collect(template.LoadContent(), labels);
                // Dropdown choices come from bindings, which an off-screen page doesn't have: add the update intervals by hand.
                if (section.Key == "updates") labels.AddRange(vm.UpdateIntervals.Select(i => i.Label));
                section.Labels = labels;
            }
            catch (Exception e) when (e is System.Windows.Markup.XamlParseException or InvalidOperationException) { }
        }
        foreach (var s in vm.Sections) s.SearchHint = string.Join(" · ", KodiakCleaner.Core.Util.SettingsSearch.Hints(vm.SearchQuery, s.Labels));
    }

    private static void Collect(object node, List<string> labels)
    {
        switch (node)
        {
            case TextBlock t:
                var text = t.Inlines.Count > 0 ? string.Concat(t.Inlines.OfType<Run>().Select(r => r.Text)) : t.Text;
                if (text.Any(char.IsLetter)) labels.Add(text.Trim());
                return;
            case ContentControl { Content: string s } when s.Any(char.IsLetter):
                labels.Add(s.Trim());
                break;
        }
        if (node is DependencyObject d)
            foreach (var child in LogicalTreeHelper.GetChildren(d)) Collect(child, labels);
    }

    /// <summary>Enter opens the first page that matches; Esc clears the search.</summary>
    private void Search_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Vm is not { } vm) return;
        if (e.Key == Key.Escape) { vm.SearchQuery = ""; e.Handled = true; }
        else if (e.Key == Key.Enter && vm.Sections.FirstOrDefault(s => s.Matches(vm.SearchQuery)) is { } first)
        {
            first.IsSelected = true;
            e.Handled = true;
        }
    }

    /// <summary>Arrow keys pick the accent swatch they move to, like any radio group.</summary>
    private void SwatchFocused(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.OriginalSource is RadioButton swatch &&
            (Keyboard.IsKeyDown(Key.Left) || Keyboard.IsKeyDown(Key.Right) || Keyboard.IsKeyDown(Key.Up) || Keyboard.IsKeyDown(Key.Down)))
            swatch.IsChecked = true;
    }
}
