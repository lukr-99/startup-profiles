using System.Windows;
using StartupProfiles.App.Themes;

namespace StartupProfiles.App.Integration;

/// <summary>Startup Profiles' own trusted confirmation window for an external app's registration request.</summary>
public partial class RegistrationWindow : Window
{
    public RegistrationWindow(RegistrationViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ThemeManager.ApplyTitleBar(this);
    }
}
