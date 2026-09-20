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

        // Never taller than the screen: on a short display the destination list scrolls instead of the
        // window running off the bottom, which is where the buttons live.
        MaxHeight = SystemParameters.WorkArea.Height;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ThemeManager.ApplyTitleBar(this);
    }
}
