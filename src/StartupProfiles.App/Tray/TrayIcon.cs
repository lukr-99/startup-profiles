using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using DotNetLib.Tray;
using StartupProfiles.App.Themes;
using StartupProfiles.App.Updates;
using StartupProfiles.Core.Execution;
using StartupProfiles.Core.Storage;
using ThemeMode = StartupProfiles.App.Themes.ThemeMode;

namespace StartupProfiles.App.Tray;

/// <summary>
/// The notification area icon, on DotNetLib's tray kit (<see cref="TrayIconHost"/>, <see cref="TrayMenuBuilder"/>),
/// so the menu follows the app theme with no drawing code here. A left click opens the launcher. The menu's
/// contents come from <see cref="TrayMenuModel"/>; <see cref="Refresh"/> builds it again after anything changes.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private static readonly Uri IconUri = new("pack://application:,,,/StartupProfiles;component/Assets/app.ico");

    private readonly TrayIconHost _host;
    private readonly IProfileStore _profiles;
    private readonly IHistoryStore _history;
    private readonly ProfileExecutor _executor;
    private readonly UpdateCoordinator _updates;
    private readonly Func<ThemeMode> _theme;
    private readonly Action<ThemeMode> _setTheme;
    private readonly Action _openConfig;
    private readonly Action _openLauncher;
    private readonly Action _quit;

    public TrayIcon(
        IProfileStore profiles,
        IHistoryStore history,
        ProfileExecutor executor,
        UpdateCoordinator updates,
        Func<ThemeMode> theme,
        Action<ThemeMode> setTheme,
        Action openConfig,
        Action openLauncher,
        Action quit)
    {
        _profiles = profiles;
        _history = history;
        _executor = executor;
        _updates = updates;
        _theme = theme;
        _setTheme = setTheme;
        _openConfig = openConfig;
        _openLauncher = openLauncher;
        _quit = quit;

        _host = new TrayIconHost("Startup Profiles", leftClick: openLauncher);
        if (LoadIcon() is { } icon) _host.SetIcon(icon);
        _updates.PropertyChanged += OnUpdatesChanged;
        Refresh();
    }

    /// <summary>Builds the menu and tooltip again from the current profiles, history, theme, and update state.</summary>
    public void Refresh()
    {
        var profiles = _profiles.GetAll();
        var activeId = _history.GetRecent(20).FirstOrDefault(r => r.ProfileId != ProfileExecutor.BaseRunId)?.ProfileId;
        var entries = TrayMenuModel.Build(profiles, activeId, _theme(), _updates.CurrentVersion,
            _updates.AvailableVersion, _updates.IsBusy);

        var builder = new TrayMenuBuilder();
        foreach (var entry in entries) Add(builder, entry);
        _host.SetMenu(builder.Build());
        _host.SetToolTip(TrayMenuModel.ToolTip(profiles.FirstOrDefault(p => p.Id == activeId)?.Name, _updates.AvailableVersion));
    }

    /// <summary>Shows a balloon from the tray.</summary>
    public void Notify(string title, string message) => _host.Notify(title, message);

    public void Dispose()
    {
        _updates.PropertyChanged -= OnUpdatesChanged;
        _host.Dispose();
    }

    private void Add(TrayMenuBuilder builder, TrayMenuEntry entry)
    {
        if (entry.IsSeparator) builder.Separator();
        else if (entry.Children.Count > 0) builder.Submenu(entry.Header!, sub => { foreach (var child in entry.Children) Add(sub, child); });
        else if (entry.Command == TrayCommand.None) builder.Label(entry.Header!);
        else builder.Item(entry.Header!, () => Execute(entry), entry.IsChecked, entry.IsEnabled, entry.IsDefault);
    }

    private void Execute(TrayMenuEntry entry)
    {
        switch (entry.Command)
        {
            case TrayCommand.OpenLauncher: _openLauncher(); break;
            case TrayCommand.OpenConfiguration: _openConfig(); break;
            case TrayCommand.RunProfile: _ = RunAsync(entry.Argument!); break;
            case TrayCommand.SetTheme: _setTheme(ThemeManager.Parse(entry.Argument)); break;
            case TrayCommand.CheckForUpdates: _ = CheckForUpdatesAsync(); break;
            case TrayCommand.InstallUpdate: _ = _updates.InstallAsync(); break;
            case TrayCommand.OpenDataFolder: OpenDataFolder(); break;
            case TrayCommand.Exit: _quit(); break;
        }

        // A checkable item flips its own mark on click; build again so marks match the state.
        Refresh();
    }

    private async Task RunAsync(string profileId)
    {
        if (_profiles.Find(profileId) is not { } profile) return;
        _host.Notify("Startup Profiles", $"Starting {profile.Name}.");
        await Task.Run(() => _executor.RunAndRecordAsync(profile)).ConfigureAwait(true);
        Refresh();
    }

    private async Task CheckForUpdatesAsync()
    {
        await _updates.CheckAsync().ConfigureAwait(true);
        if (_updates.Status is { } status) _host.Notify("Startup Profiles", status);
    }

    private void OnUpdatesChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(UpdateCoordinator.AvailableVersion) or nameof(UpdateCoordinator.IsBusy))
            Application.Current?.Dispatcher.BeginInvoke(Refresh);
    }

    private static byte[]? LoadIcon()
    {
        try
        {
            using var stream = Application.GetResourceStream(IconUri)?.Stream;
            if (stream is null) return null;
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return copy.ToArray();
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static void OpenDataFolder()
    {
        try { Process.Start(new ProcessStartInfo(StartupProfilesPaths.DataDirectory) { UseShellExecute = true }); }
        catch (Win32Exception) { /* No shell to open it with; nothing to do. */ }
    }
}
