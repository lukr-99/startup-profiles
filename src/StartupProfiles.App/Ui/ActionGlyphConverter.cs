using System.Globalization;
using System.Windows.Data;
using StartupProfiles.App.Config;
using StartupProfiles.Core.Models;

namespace StartupProfiles.App.Ui;

/// <summary>An action type to its fallback icon glyph, for list items whose target has no shell icon.</summary>
public sealed class ActionGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ActionLabels.Glyph(value is ActionType type ? type : ActionType.LaunchApp);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
