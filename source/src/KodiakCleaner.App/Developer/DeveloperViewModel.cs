using System.Collections.ObjectModel;
using System.Windows.Input;
using KodiakCleaner.App.ViewModels;

namespace KodiakCleaner.App.Developer;

/// <summary>
/// Settings › Developer, in development builds only (release builds leave this folder out entirely).
/// Preview the app as a Free or Supporter user and check how this build is set up.
/// </summary>
public sealed class DeveloperViewModel : ObservableObject
{
    public DeveloperViewModel(ICommand resetSettings)
    {
        ResetSettingsCommand = resetSettings;
        // Just Free and Supporter (Ted, 0.8.10): no preview behaves like Free, so it shows as Free.
        var current = Settings.Current.DevPreview == PlanPreview.Supporter ? PlanPreview.Supporter : PlanPreview.Free;
        foreach (var (label, value) in new[] { ("Free", PlanPreview.Free), ("Supporter", PlanPreview.Supporter) })
            Previews.Add(new Choice<PlanPreview>(label, value, current == value, SetPreview));
        ResetCommand = new RelayCommand(Reset);
        Supporter.Changed += () => OnPropertyChanged(string.Empty);
    }

    public ObservableCollection<Choice<PlanPreview>> Previews { get; } = new();

    public string PreviewName
    {
        get => Settings.Current.DevPreviewName;
        set
        {
            Settings.Current.DevPreviewName = value;
            Settings.Current.Save();
            Supporter.Refresh();
        }
    }

    public string InstallType => Integration.IsInstalled ? "Installed (MSI, Program Files)" : "Portable copy";
    public string BuildType => $"{AppInfo.Name} {AppInfo.VersionName}, development build";
    public string SigningKey => Supporter.IsTestKey ? "Test key (release builds refuse to build with it)" : "Ted's key";
    public string CodeState => Supporter.License is { } l ? $"Valid (ID {l.Id})" : Supporter.CodeStatus.ToString();
    public string CurrentPreview => Supporter.Preview == PlanPreview.None ? "Free" : $"{Supporter.Preview} · Dev";
    public string PlanNow => $"{Supporter.PlanName} ({Supporter.HowUnlocked})";
    public string PerksNow => Supporter.IsSupporter
        ? $"Classic and Glacier themes, Supporter Badge ({(Settings.Current.ShowSupporterBadge ? "shown" : "hidden")}), support link hidden"
        : "None";

    public ICommand ResetCommand { get; }
    /// <summary>Reset to Default: the same reset as Settings > Application (Ted, 0.8.55).</summary>
    public ICommand ResetSettingsCommand { get; }

    private void SetPreview(PlanPreview preview)
    {
        // Free is the same as no preview, so a real supporter code entered later still counts.
        Settings.Current.DevPreview = preview == PlanPreview.Free ? PlanPreview.None : preview;
        Settings.Current.Save();
        Supporter.Refresh();
        ThemeManager.Apply();
    }

    /// <summary>Clears the preview, the preview name and any saved supporter code.</summary>
    private void Reset()
    {
        Settings.Current.DevPreview = PlanPreview.None;
        Settings.Current.DevPreviewName = "";
        Settings.Current.SupporterCode = "";
        Settings.Current.Save();
        foreach (var p in Previews) p.IsSelected = p.Value == PlanPreview.Free;
        Supporter.Refresh();
        ThemeManager.Apply();
    }
}
