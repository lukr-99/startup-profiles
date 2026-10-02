using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using DotNetLib.Tray;

namespace StartupProfiles.App.Themes;

/// <summary>
/// The app's theme, on DotNetLib's tray theme engine (CodePrint's standard for Windows tray apps).
/// <see cref="TrayThemeApplier"/> picks light, dark, or Windows high contrast, follows Windows in
/// <see cref="ThemeMode.System"/> while the app runs, and themes the WPF UI controls the tray menu uses. On top
/// of it this class keeps the app's own look: after every apply it swaps in the matching <c>App.*</c> token
/// dictionary (Light, Dark, or HighContrast.xaml) and colors every window's title bar. The app's control
/// styles (Controls.xaml) are merged last so they win over WPF UI's for the controls they style.
/// </summary>
public sealed class ThemeManager : IDisposable
{
    private const string ThemesRoot = "pack://application:,,,/StartupProfiles;component/Themes/";

    private readonly Application _app;
    private readonly TrayThemeApplier _applier;
    private readonly int _tokensIndex;

    public ThemeManager(Application app)
    {
        _app = app;

        // WPF UI themes and controls, then the kit's Window style, then the app's tokens and control styles.
        TrayResources.Merge(app.Resources);
        _tokensIndex = app.Resources.MergedDictionaries.Count;
        app.Resources.MergedDictionaries.Add(Tokens("Light"));
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(ThemesRoot + "Controls.xaml") });

        _applier = new TrayThemeApplier(app.Resources, TrayThemeApplier.WindowsAppsUseDark);
        _applier.Applied += (_, _) => OnApplied();
    }

    /// <summary>Whether the most recently applied theme resolves to dark (drives the title-bar color).</summary>
    public static bool EffectiveIsDark { get; private set; }

    /// <summary>Raised after every apply, including when Windows changes its theme or contrast.</summary>
    public event Action? Applied;

    /// <summary>The kit's applier, for the kit's own dialogs (<c>prepare: theme.Applier.Attach</c>).</summary>
    public TrayThemeApplier Applier => _applier;

    public void Apply(ThemeMode mode) => _applier.Apply(mode switch
    {
        ThemeMode.Light => TrayThemeMode.Light,
        ThemeMode.Dark => TrayThemeMode.Dark,
        _ => TrayThemeMode.System,
    });

    /// <summary>Colors the native title bar of <paramref name="window"/> to match the current theme.</summary>
    public static void ApplyTitleBar(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return; // Not yet created; the window re-applies on SourceInitialized.

        var useDark = EffectiveIsDark ? 1 : 0;
        try { _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int)); }
        catch (DllNotFoundException) { /* Pre-Windows 10 1809: no immersive dark mode. */ }
    }

    public static ThemeMode Parse(string? value) =>
        Enum.TryParse<ThemeMode>(value, ignoreCase: true, out var mode) ? mode : ThemeMode.System;

    /// <summary>Which token dictionary fits the Windows contrast setting and the dark flag.</summary>
    public static string TokensFor(bool highContrast, bool isDark) =>
        highContrast ? "HighContrast" : isDark ? "Dark" : "Light";

    public void Dispose() => _applier.Dispose();

    private void OnApplied()
    {
        var highContrast = SystemParameters.HighContrast;
        EffectiveIsDark = _applier.IsDark && !highContrast;
        _app.Resources.MergedDictionaries[_tokensIndex] = Tokens(TokensFor(highContrast, _applier.IsDark));

        foreach (Window window in _app.Windows) ApplyTitleBar(window);
        Applied?.Invoke();
    }

    private static ResourceDictionary Tokens(string name) => new() { Source = new Uri($"{ThemesRoot}{name}.xaml") };

    private const int DwmwaUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll", SetLastError = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);
}
