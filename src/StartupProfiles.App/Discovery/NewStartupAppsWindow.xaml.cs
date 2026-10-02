using System.Windows;
using StartupProfiles.App.Themes;

namespace StartupProfiles.App.Discovery;

/// <summary>Offers the startup apps found since last time (see <see cref="NewStartupAppsViewModel"/>).</summary>
public partial class NewStartupAppsWindow : Window
{
    public NewStartupAppsWindow(NewStartupAppsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += () => Dispatcher.Invoke(Close);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ThemeManager.ApplyTitleBar(this);
    }
}
