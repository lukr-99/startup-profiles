using System.Windows;
using Microsoft.Win32;

namespace StartupProfiles.App.Themes;

/// <summary>
/// Merges the light or dark semantic-token dictionary into the application resources. In
/// <see cref="ThemeMode.System"/> it follows the Windows "apps use light theme" setting.
/// </summary>
public sealed class ThemeManager
{
    private readonly Application _app;

    public ThemeManager(Application app) => _app = app;

    public void Apply(ThemeMode mode)
    {
        var effective = mode == ThemeMode.System ? ResolveSystem() : mode;
        var uri = new Uri($"pack://application:,,,/StartupProfiles;component/Themes/{effective}.xaml");

        _app.Resources.MergedDictionaries.Clear();
        _app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = uri });
    }

    private static ThemeMode ResolveSystem()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int light && light == 0 ? ThemeMode.Dark : ThemeMode.Light;
    }

    public static ThemeMode Parse(string? value) =>
        Enum.TryParse<ThemeMode>(value, ignoreCase: true, out var mode) ? mode : ThemeMode.System;
}
