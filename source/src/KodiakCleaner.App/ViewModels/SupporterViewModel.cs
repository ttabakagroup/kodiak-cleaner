using System.Windows.Input;
using KodiakCleaner.Core.Supporter;

namespace KodiakCleaner.App.ViewModels;

/// <summary>Settings › Supporter: Free and Supporter side by side, the Ko-fi link and the supporter code box.</summary>
public sealed class SupporterViewModel : ObservableObject
{
    public SupporterViewModel()
    {
        KoFiCommand = new RelayCommand(() => Shell.OpenUrl(AppInfo.KoFiUrl));
        ApplyCodeCommand = new RelayCommand(ApplyCode, () => !string.IsNullOrWhiteSpace(Code));
        RemoveCodeCommand = new RelayCommand(RemoveCode, () => Supporter.License is not null);
        Supporter.Changed += Refresh;
    }

    public bool IsSupporter => Supporter.IsSupporter;
    public bool IsFree => !Supporter.IsSupporter;
    public string PlanName => Supporter.PlanName;
    public string Thanks => Supporter.SupporterName is { } name ? $"Thank you, {name}!" : "Thank you for supporting Kodiak Cleaner!";
    public bool HasCode => Supporter.License is not null;

    /// <summary>Shown while the app checks codes with the test key (Ted's own key hasn't been made yet).</summary>
    public bool IsTestKey => Supporter.IsTestKey;

    public bool ShowBadge
    {
        get => Settings.Current.ShowSupporterBadge;
        set
        {
            Settings.Current.ShowSupporterBadge = value;
            Settings.Current.Save();
            Supporter.Refresh();
        }
    }

    private string _code = "";
    public string Code { get => _code; set => Set(ref _code, value); }

    private string _status = "";
    public string Status { get => _status; private set => Set(ref _status, value); }

    public ICommand KoFiCommand { get; }
    public ICommand ApplyCodeCommand { get; }
    public ICommand RemoveCodeCommand { get; }

    private void ApplyCode()
    {
        var result = Supporter.Apply(Code);
        Status = SupporterCode.Describe(result);
        if (result == CodeStatus.Valid) Code = "";
    }

    private void RemoveCode()
    {
        if (!Dialogs.Confirm("Remove your supporter code from this PC? You can enter it again at any time.")) return;
        Supporter.RemoveCode();
        Status = "Your supporter code was removed from this PC.";
    }

    private void Refresh() => OnPropertyChanged(string.Empty);
}
