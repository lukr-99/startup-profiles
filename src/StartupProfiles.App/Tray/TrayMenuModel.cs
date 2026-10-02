using StartupProfiles.App.Themes;
using StartupProfiles.App.Ui;
using StartupProfiles.Core.Models;

namespace StartupProfiles.App.Tray;

/// <summary>
/// What the tray shows for the app's current state: the menu, built fresh on every change, and the tooltip.
/// Pure, so it is unit-tested; <see cref="TrayIcon"/> draws it with DotNetLib's <c>TrayMenuBuilder</c>.
/// </summary>
public static class TrayMenuModel
{
    /// <param name="profiles">Every profile, in launcher order.</param>
    /// <param name="activeProfileId">The profile that ran last (checked in the menu), or null.</param>
    /// <param name="theme">The theme setting (checked in the Theme submenu).</param>
    /// <param name="version">The running version, shown at the bottom.</param>
    /// <param name="availableUpdate">A newer version ready to install, or null.</param>
    /// <param name="checkingForUpdates">True while a check runs (the item is greyed out).</param>
    public static IReadOnlyList<TrayMenuEntry> Build(
        IReadOnlyList<Profile> profiles,
        string? activeProfileId,
        ThemeMode theme,
        string version,
        string? availableUpdate = null,
        bool checkingForUpdates = false)
    {
        List<TrayMenuEntry> menu =
        [
            new("Open launcher", TrayCommand.OpenLauncher, IsDefault: true),
            TrayMenuEntry.Separator,
        ];

        if (profiles.Count > 0)
        {
            menu.Add(new("Run a profile"));
            menu.AddRange(profiles.Select(p => new TrayMenuEntry(
                $"{ProfileGlyph.For(p.Icon, p.Name)}  {p.Name}",
                TrayCommand.RunProfile,
                p.Id,
                IsChecked: p.Id == activeProfileId)));
            menu.Add(TrayMenuEntry.Separator);
        }

        menu.Add(new("Configuration...", TrayCommand.OpenConfiguration));
        menu.Add(new("Theme")
        {
            Children =
            [
                new("Same as Windows", TrayCommand.SetTheme, nameof(ThemeMode.System), IsChecked: theme == ThemeMode.System),
                new("Light", TrayCommand.SetTheme, nameof(ThemeMode.Light), IsChecked: theme == ThemeMode.Light),
                new("Dark", TrayCommand.SetTheme, nameof(ThemeMode.Dark), IsChecked: theme == ThemeMode.Dark),
            ],
        });
        menu.Add(availableUpdate is not null
            ? new($"Install version {availableUpdate}", TrayCommand.InstallUpdate)
            : new(checkingForUpdates ? "Checking for updates..." : "Check for updates", TrayCommand.CheckForUpdates,
                IsEnabled: !checkingForUpdates));
        menu.Add(new("Open data folder", TrayCommand.OpenDataFolder));
        menu.Add(new($"Version {version}"));
        menu.Add(TrayMenuEntry.Separator);
        menu.Add(new("Exit", TrayCommand.Exit));
        return menu;
    }

    /// <summary>The tooltip: the app name, the active profile, and a waiting update.</summary>
    public static string ToolTip(string? activeProfileName, string? availableUpdate) =>
        "Startup Profiles" +
        (activeProfileName is null ? "" : $" · {activeProfileName}") +
        (availableUpdate is null ? "" : $"{Environment.NewLine}Update {availableUpdate} available");
}
