using System.Globalization;
using System.Windows.Data;
using StartupProfiles.Core.Models;

namespace StartupProfiles.App.Ui;

/// <summary>
/// Multi-binding of an action's Type, Target, and Arguments to its shell icon, so the action table's icon
/// updates as the row is edited.
/// </summary>
public sealed class ActionIconConverter : IMultiValueConverter
{
    public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture) =>
        values is [ActionType type, var target, var arguments]
            ? ShellIcons.Get(ActionIconSource.For(type, target as string, arguments as string))
            : null;

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
