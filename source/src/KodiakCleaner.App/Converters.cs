using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace KodiakCleaner.App;

/// <summary>true → false.</summary>
public sealed class NotConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;
}

/// <summary>true → Collapsed, false → Visible.</summary>
public sealed class NotVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Panel width → how many same-size tiles fit side by side (ConverterParameter: the narrowest a tile may be), 1 to 3.</summary>
public sealed class TileColumnsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double width && double.TryParse(parameter as string, NumberStyles.Float, CultureInfo.InvariantCulture, out var min) && min > 0
            ? Math.Clamp((int)(width / min), 1, 3)
            : 3;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
