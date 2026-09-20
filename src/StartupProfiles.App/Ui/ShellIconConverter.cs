using System.Globalization;
using System.Windows.Data;

namespace StartupProfiles.App.Ui;

/// <summary>
/// Binds an <see cref="ActionIconSource"/> parsing name (a file path, <c>shell:AppsFolder\...</c>, or a
/// ".ext") to its shell icon, for the places that already know which item stands for what they show.
/// </summary>
public sealed class ShellIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ShellIcons.Get(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
