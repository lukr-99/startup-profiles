using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using StartupProfiles.App.Themes;

namespace StartupProfiles.App.Launcher;

public partial class LauncherWindow : Window
{
    private readonly LauncherViewModel _viewModel;
    private readonly DispatcherTimer _countdown = new() { Interval = TimeSpan.FromSeconds(1) };

    public LauncherWindow(LauncherViewModel viewModel, Action openConfig)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.CloseRequested += () => Dispatcher.Invoke(Close);
        viewModel.OpenConfigRequested += openConfig;

        _countdown.Tick += (_, _) =>
        {
            _viewModel.Tick();
            if (!_viewModel.IsCountingDown) _countdown.Stop();
        };
        if (viewModel.IsCountingDown) _countdown.Start();
        Closed += (_, _) => _countdown.Stop();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ThemeManager.ApplyTitleBar(this);
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        // Keyboard focus on the highlighted tile, so the arrow keys move from there.
        if (_viewModel.Selected is { } selected && Tiles.ItemContainerGenerator.ContainerFromItem(selected) is ListBoxItem item)
            item.Focus();
    }

    // Any click stops a countdown: the user is choosing.
    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);
        _viewModel.CancelCountdown();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        _viewModel.CancelCountdown();

        switch (e.Key)
        {
            case Key.Escape:
                Close();
                e.Handled = true;
                return;
            case Key.Enter:
                _viewModel.RunSelectedCommand.Execute(null);
                e.Handled = true;
                return;
        }

        if (ToIndex(e.Key) is int index && index < _viewModel.Profiles.Count)
        {
            _viewModel.RunCommand.Execute(_viewModel.Profiles[index]);
            e.Handled = true;
        }
    }

    private void OnTileClicked(object sender, MouseButtonEventArgs e)
    {
        for (var node = e.OriginalSource as DependencyObject; node is not null; node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
        {
            if (node is ListBoxItem { DataContext: ProfileTile tile })
            {
                _viewModel.RunCommand.Execute(tile);
                return;
            }
        }
    }

    private static int? ToIndex(Key key) => key switch
    {
        >= Key.D1 and <= Key.D9 => key - Key.D1,
        >= Key.NumPad1 and <= Key.NumPad9 => key - Key.NumPad1,
        _ => null,
    };
}
