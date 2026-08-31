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
        _ready = true;
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_ready) _applyTheme((ThemeMode)ThemeBox.SelectedIndex);
    }
}
