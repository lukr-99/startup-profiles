using System.Globalization;
using System.Windows.Data;

namespace StartupProfiles.App.Ui;

/// <summary>Flips a bool, e.g. to disable a button while something is busy.</summary>
public sealed class NotConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}
