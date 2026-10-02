using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace StartupProfiles.App.Themes;

/// <summary>
/// Merges the light or dark semantic-token dictionary (plus the shared control styles) into the
/// application resources, and matches the native window title bar to the theme. In
/// <see cref="ThemeMode.System"/> it follows the Windows "apps use light theme" setting, including
/// changes made while the app runs.
/// </summary>
public sealed class ThemeManager
{
    private const string ControlsUri = "pack://application:,,,/StartupProfiles;component/Themes/Controls.xaml";

    private readonly Application _app;
    private readonly SystemThemeWatcher _systemTheme;
    private ThemeMode _mode;

    public ThemeManager(Application app)
    {
        _app = app;
        _systemTheme = new SystemThemeWatcher(() => ResolveSystem() == ThemeMode.Dark);
        _systemTheme.Changed += (_, _) => _app.Dispatcher.BeginInvoke(new Action(OnSystemThemeChanged));
        _app.Dispatcher.ShutdownStarted += (_, _) => _systemTheme.Stop();
    }

    /// <summary>Whether the most recently applied theme resolves to dark (drives the title-bar color).</summary>
    public static bool EffectiveIsDark { get; private set; }

    public void Apply(ThemeMode mode)
    {
        _mode = mode;
        if (mode == ThemeMode.System) _systemTheme.Start();
        else _systemTheme.Stop();

        var effective = mode == ThemeMode.System ? ResolveSystem() : mode;
        EffectiveIsDark = effective == ThemeMode.Dark;

        _app.Resources.MergedDictionaries.Clear();
        _app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/StartupProfiles;component/Themes/{effective}.xaml"),
        });
        _app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(ControlsUri) });

        foreach (Window window in _app.Windows) ApplyTitleBar(window);
    }

    /// <summary>Colors the native title bar of <paramref name="window"/> to match the current theme.</summary>
    public static void ApplyTitleBar(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return; // Not yet created; the window re-applies on SourceInitialized.

        var useDark = EffectiveIsDark ? 1 : 0;
        try { _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int)); }
        catch (DllNotFoundException) { /* Pre-Windows 10 1809: no immersive dark mode. */ }
    }

    private void OnSystemThemeChanged()
    {
        if (_mode == ThemeMode.System) Apply(ThemeMode.System);
    }

    private static ThemeMode ResolveSystem()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int light && light == 0 ? ThemeMode.Dark : ThemeMode.Light;
    }

    public static ThemeMode Parse(string? value) =>
        Enum.TryParse<ThemeMode>(value, ignoreCase: true, out var mode) ? mode : ThemeMode.System;

    private const int DwmwaUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll", SetLastError = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);
}
