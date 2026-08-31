using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StartupProfiles.App.Ui;

/// <summary>
/// Visible when the value is non-null; pass ConverterParameter "invert" to flip it. Used to toggle the
/// editor panel and its empty-state placeholder in the config window.
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = value is not null;
        if (string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase)) visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
