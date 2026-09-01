using System.Windows;
using System.Windows.Input;
using StartupProfiles.App.Themes;

namespace StartupProfiles.App.Launcher;

public partial class LauncherWindow : Window
{
    private readonly LauncherViewModel _viewModel;

    public LauncherWindow(LauncherViewModel viewModel, Action openConfig)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.CloseRequested += () => Dispatcher.Invoke(Close);
        viewModel.OpenConfigRequested += openConfig;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ThemeManager.ApplyTitleBar(this);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            Close();
            return;
        }

        if (ToIndex(e.Key) is int index && index < _viewModel.Profiles.Count)
            _viewModel.RunCommand.Execute(_viewModel.Profiles[index]);
    }

    private static int? ToIndex(Key key) => key switch
    {
        >= Key.D1 and <= Key.D9 => key - Key.D1,
        >= Key.NumPad1 and <= Key.NumPad9 => key - Key.NumPad1,
        _ => null,
    };
}
