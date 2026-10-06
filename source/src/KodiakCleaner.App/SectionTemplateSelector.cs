using System.Windows;
using System.Windows.Controls;
using KodiakCleaner.App.ViewModels;

namespace KodiakCleaner.App;

/// <summary>Shows a Settings page: the "&lt;key&gt;Page" template from the view's resources, or the page itself when it is a control.</summary>
public sealed class SectionTemplateSelector : DataTemplateSelector
{
    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        if (item is not SettingsSection section || container is not FrameworkElement element) return null;
        var key = section.Context is UIElement ? "ElementPage" : section.Key + "Page";
        return element.TryFindResource(key) as DataTemplate;
    }
}
