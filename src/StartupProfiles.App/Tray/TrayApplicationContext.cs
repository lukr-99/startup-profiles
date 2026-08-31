using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using StartupProfiles.Core.Execution;
using StartupProfiles.Core.Models;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.App.Tray;

/// <summary>
/// System-tray presence for Startup Profiles. Keeps the local server alive and offers quick actions:
/// re-run a profile after login, open the data folder, and exit. The richer config UI arrives in a
/// later milestone.
/// </summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _icon;
    private readonly IProfileStore _profiles;
    private readonly ProfileExecutor _executor;

    public TrayApplicationContext(IProfileStore profiles, ProfileExecutor executor)
    {
        _profiles = profiles;
        _executor = executor;

        var menu = new ContextMenuStrip();
        menu.Opening += (_, _) => RebuildMenu(menu);

        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Startup Profiles",
            Visible = true,
            ContextMenuStrip = menu,
        };

        RebuildMenu(menu);
    }

    private void RebuildMenu(ContextMenuStrip menu)
    {
        menu.Items.Clear();
        menu.Items.Add(new ToolStripMenuItem("Run a profile") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());

        foreach (var profile in _profiles.GetAll())
            menu.Items.Add(new ToolStripMenuItem(profile.Name, null, (_, _) => Run(profile)));

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Open data folder", null, (_, _) => OpenDataFolder()));
        menu.Items.Add(new ToolStripMenuItem($"Version {GetVersion()}") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => Exit()));
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

    private static string GetVersion()
    {
        var version = typeof(TrayApplicationContext).Assembly.GetName().Version;
        return version is null ? "unknown" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private void Exit()
    {
        _icon.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _icon.Dispose();
        base.Dispose(disposing);
    }
}
