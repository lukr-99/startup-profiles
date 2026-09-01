using System.Windows;
using System.Windows.Controls;
using ThemeMode = StartupProfiles.App.Themes.ThemeMode;

namespace StartupProfiles.App.Config;

public partial class ConfigWindow : Window
{
    private readonly Action<ThemeMode> _applyTheme;
    private bool _ready;

    public ConfigWindow(ConfigViewModel viewModel, ThemeMode current, Action<ThemeMode> applyTheme)
    {
        InitializeComponent();
        DataContext = viewModel;
        _applyTheme = applyTheme;
        ThemeBox.SelectedIndex = (int)current;
        var version = typeof(ConfigWindow).Assembly.GetName().Version;
        VersionText.Text = version is null ? "Startup Profiles" : $"Startup Profiles {version.Major}.{version.Minor}.{version.Build}";
        _ready = true;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Themes.ThemeManager.ApplyTitleBar(this);
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_ready) _applyTheme((ThemeMode)ThemeBox.SelectedIndex);
    }
}
