using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StartupProfiles.App.Ui;

/// <summary>
/// Visible when the value is true; pass ConverterParameter "invert" to flip it. An empty list or a zero count
/// also counts as false, so a chip row or a counter can hide itself.
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = value switch
        {
            bool flag => flag,
            int count => count > 0,
            System.Collections.ICollection list => list.Count > 0,
            _ => value is not null,
        };
        if (string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase)) visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
