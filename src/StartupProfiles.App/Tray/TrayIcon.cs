using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using StartupProfiles.Core.Execution;
using StartupProfiles.Core.Models;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.App.Tray;

/// <summary>
/// System-tray presence (a WinForms <see cref="NotifyIcon"/> living on the WPF UI thread): re-run a
/// profile after login, open the config window, open the data folder, or quit.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly IProfileStore _profiles;
    private readonly ProfileExecutor _executor;
    private readonly Action _openConfig;
    private readonly Action _quit;

    public TrayIcon(IProfileStore profiles, ProfileExecutor executor, Action openConfig, Action quit)
    {
        _profiles = profiles;
        _executor = executor;
        _openConfig = openConfig;
        _quit = quit;

        var menu = new ContextMenuStrip();
        menu.Opening += (_, _) => Rebuild(menu);

        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Startup Profiles",
            Visible = true,
            ContextMenuStrip = menu,
        };

        Rebuild(menu);
    }

    private void Rebuild(ContextMenuStrip menu)
    {
        menu.Items.Clear();
        menu.Items.Add(new ToolStripMenuItem("Run a profile") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());

        foreach (var profile in _profiles.GetAll())
            menu.Items.Add(new ToolStripMenuItem(profile.Name, null, (_, _) => Run(profile)));

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Configuration...", null, (_, _) => _openConfig()));
        menu.Items.Add(new ToolStripMenuItem("Open data folder", null, (_, _) => OpenDataFolder()));
        menu.Items.Add(new ToolStripMenuItem($"Version {Version()}") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => _quit()));
    }

    private void Run(Profile profile)
    {
        _icon.ShowBalloonTip(2000, "Startup Profiles", $"Running '{profile.Name}'.", ToolTipIcon.Info);
        _ = Task.Run(() => _executor.RunAndRecordAsync(profile));
    }

    private static void OpenDataFolder()
    {
        try { Process.Start(new ProcessStartInfo(StartupProfilesPaths.DataDirectory) { UseShellExecute = true }); }
        catch (System.ComponentModel.Win32Exception) { /* ignore */ }
    }

    private static string Version()
    {
        var version = typeof(TrayIcon).Assembly.GetName().Version;
        return version is null ? "unknown" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
