using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

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
}
