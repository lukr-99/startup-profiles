using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using StartupProfiles.App.Themes;
using StartupProfiles.Core.Execution;
using StartupProfiles.Core.Models;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.App.Tray;

/// <summary>
/// System-tray presence (a WinForms <see cref="NotifyIcon"/> living on the WPF UI thread): re-open the
/// launcher, run a profile, open the config window, open the data folder, or quit. The menu is
/// re-themed each time it opens so it follows the app's current light/dark theme.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly IProfileStore _profiles;
    private readonly ProfileExecutor _executor;
    private readonly Action _openConfig;
    private readonly Action _openLauncher;
    private readonly Action _quit;

    public TrayIcon(IProfileStore profiles, ProfileExecutor executor, Action openConfig, Action openLauncher, Action quit)
    {
        _profiles = profiles;
        _executor = executor;
        _openConfig = openConfig;
        _openLauncher = openLauncher;
        _quit = quit;

        var menu = new ContextMenuStrip { ShowImageMargin = false };
        menu.Opening += (_, _) => Rebuild(menu);

        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Startup Profiles",
            Visible = true,
            ContextMenuStrip = menu,
        };

        Rebuild(menu);
    }

    private void Rebuild(ContextMenuStrip menu)
    {
        // Colors come from the current theme's WPF tokens; the literals are the same values, used only
        // when no theme dictionary is loaded.
        var dark = ThemeManager.EffectiveIsDark;
        var text = Token("App.Text", dark ? Color.FromArgb(0xEE, 0xF0, 0xF4) : Color.FromArgb(0x1B, 0x1D, 0x22));
        var muted = Token("App.Muted", dark ? Color.FromArgb(0x9A, 0xA0, 0xB0) : Color.FromArgb(0x6B, 0x70, 0x80));
        var surface = Token("App.Surface", dark ? Color.FromArgb(0x1C, 0x1F, 0x27) : Color.White);
        var hover = Token("App.Selection", dark ? Color.FromArgb(0x2A, 0x35, 0x50) : Color.FromArgb(0xDC, 0xE6, 0xFF));
        var line = Token("App.Border", dark ? Color.FromArgb(0x33, 0x38, 0x46) : Color.FromArgb(0xD9, 0xDC, 0xE3));

        menu.Renderer = new ThemedRenderer(new ThemedColors(surface, hover, line));
        menu.BackColor = surface;
        menu.ForeColor = text;

        menu.Items.Clear();
        Add(menu, "Open launcher...", text, () => _openLauncher());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Run a profile") { Enabled = false, ForeColor = muted });

        foreach (var profile in _profiles.GetAll())
            Add(menu, profile.Name, text, () => Run(profile));

        menu.Items.Add(new ToolStripSeparator());
        Add(menu, "Configuration...", text, () => _openConfig());
        Add(menu, "Open data folder", text, OpenDataFolder);
        menu.Items.Add(new ToolStripMenuItem($"Version {Version()}") { Enabled = false, ForeColor = muted });
        menu.Items.Add(new ToolStripSeparator());
        Add(menu, "Exit", text, () => _quit());
    }

    private static Color Token(string key, Color fallback) =>
        System.Windows.Application.Current?.TryFindResource(key) is System.Windows.Media.SolidColorBrush brush
            ? Color.FromArgb(brush.Color.A, brush.Color.R, brush.Color.G, brush.Color.B)
            : fallback;

    private static void Add(ContextMenuStrip menu, string text, Color foreColor, Action onClick) =>
        menu.Items.Add(new ToolStripMenuItem(text, null, (_, _) => onClick()) { ForeColor = foreColor });

    private void Run(Profile profile)
    {
        _icon.ShowBalloonTip(2000, "Startup Profiles", $"Running '{profile.Name}'.", ToolTipIcon.Info);
        _ = Task.Run(() => _executor.RunAndRecordAsync(profile));
    }

    private static Icon LoadIcon()
    {
        try
        {
            if (Environment.ProcessPath is { } exe && Icon.ExtractAssociatedIcon(exe) is { } icon) return icon;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException) { /* fall back */ }
        return SystemIcons.Application;
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

    /// <summary>Renders the tray menu in the app's theme colors (WinForms menus are otherwise system-light).</summary>
    private sealed class ThemedRenderer : ToolStripProfessionalRenderer
    {
        public ThemedRenderer(ThemedColors colors) : base(colors) => RoundedEdges = false;

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.ForeColor;
            base.OnRenderItemText(e);
        }
    }

    private sealed class ThemedColors : ProfessionalColorTable
    {
        public ThemedColors(Color surface, Color hover, Color line)
        {
            Surface = surface;
            Hover = hover;
            Line = line;
            UseSystemColors = false;
        }

        private Color Surface { get; }
        private Color Hover { get; }
        private Color Line { get; }

        public override Color ToolStripDropDownBackground => Surface;
        public override Color ImageMarginGradientBegin => Surface;
        public override Color ImageMarginGradientMiddle => Surface;
        public override Color ImageMarginGradientEnd => Surface;
        public override Color MenuItemSelected => Hover;
        public override Color MenuItemSelectedGradientBegin => Hover;
        public override Color MenuItemSelectedGradientEnd => Hover;
        public override Color MenuItemBorder => Hover;
        public override Color MenuItemPressedGradientBegin => Hover;
        public override Color MenuItemPressedGradientEnd => Hover;
        public override Color MenuBorder => Line;
        public override Color SeparatorDark => Line;
        public override Color SeparatorLight => Line;
    }
}
