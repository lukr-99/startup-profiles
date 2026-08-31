using System.Windows;

namespace StartupProfiles.App.Integration;

/// <summary>Startup Profiles' own trusted confirmation window for an external app's registration request.</summary>
public partial class RegistrationWindow : Window
{
    public RegistrationWindow(RegistrationViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
