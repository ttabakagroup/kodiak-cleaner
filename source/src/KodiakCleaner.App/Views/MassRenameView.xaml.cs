using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KodiakCleaner.App.ViewModels;

namespace KodiakCleaner.App.Views;

public partial class MassRenameView : UserControl
{
    public MassRenameView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is INotifyPropertyChanged old) old.PropertyChanged -= OnViewModelChanged;
            if (e.NewValue is INotifyPropertyChanged now) now.PropertyChanged += OnViewModelChanged;
            ShowSubfolderColumn();
        };
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MassRenameViewModel.ShowSubfolder)) ShowSubfolderColumn();
    }

    // Table columns aren't in the visual tree, so the Subfolder column is shown from here, only when a subfolder was read.
    private void ShowSubfolderColumn() =>
        SubfolderColumn.Visibility = DataContext is MassRenameViewModel { ShowSubfolder: true } ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Puts the clicked part ({name}, {num}...) into the pattern where the cursor is.</summary>
    private void InsertToken(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string token }) return;
        var at = Math.Clamp(PatternBox.CaretIndex, 0, PatternBox.Text.Length);
        if (PatternBox.SelectionLength > 0)
        {
            at = PatternBox.SelectionStart;
            PatternBox.Text = PatternBox.Text.Remove(at, PatternBox.SelectionLength);
        }
        PatternBox.Text = PatternBox.Text.Insert(at, token);
        PatternBox.Focus();
        PatternBox.CaretIndex = at + token.Length;
    }

    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MassRenameViewModel vm) vm.Highlighted = Grid.SelectedItems.OfType<RenameRow>().ToList();
    }

    private void Grid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) => GridRightClick.SelectRowUnderMouse(e);
}
