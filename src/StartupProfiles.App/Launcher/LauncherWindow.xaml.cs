using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using StartupProfiles.App.Themes;

namespace StartupProfiles.App.Launcher;

public partial class LauncherWindow : Window
{
    public static readonly DependencyProperty TileListWidthProperty =
        DependencyProperty.Register(nameof(TileListWidth), typeof(double), typeof(LauncherWindow), new PropertyMetadata(double.NaN));

    public static readonly DependencyProperty FooterWidthProperty =
        DependencyProperty.Register(nameof(FooterWidth), typeof(double), typeof(LauncherWindow), new PropertyMetadata(double.NaN));

    private readonly LauncherViewModel _viewModel;
    private readonly DispatcherTimer _countdown = new() { Interval = TimeSpan.FromSeconds(1) };

    /// <summary>Exact width of the tile panel: columns x tile slot, set from <see cref="LauncherLayout"/>.</summary>
    public double TileListWidth { get => (double)GetValue(TileListWidthProperty); private set => SetValue(TileListWidthProperty, value); }

    /// <summary>Width of the footer row, so a long status message trims instead of widening the window.</summary>
    public double FooterWidth { get => (double)GetValue(FooterWidthProperty); private set => SetValue(FooterWidthProperty, value); }

    public LauncherWindow(LauncherViewModel viewModel, Action openConfig)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.CloseRequested += () => Dispatcher.BeginInvoke(Close);
        viewModel.OpenConfigRequested += openConfig;

        _countdown.Tick += (_, _) =>
        {
            _viewModel.Tick();
            if (!_viewModel.IsCountingDown) _countdown.Stop();
        };
        if (viewModel.IsCountingDown) _countdown.Start();
        Closed += (_, _) => _countdown.Stop();

        viewModel.Profiles.CollectionChanged += (_, _) => Dispatcher.BeginInvoke(ApplyLayout);
        SizeChanged += (_, _) => PlaceAtBottomCentre();
        SystemParameters.StaticPropertyChanged += OnSystemParameterChanged;
        Closed += (_, _) => SystemParameters.StaticPropertyChanged -= OnSystemParameterChanged;
        ApplyLayout();
    }

    private void OnSystemParameterChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.WorkArea)) Dispatcher.BeginInvoke(ApplyLayout);
    }

    // Width follows the profile count; height follows the content (SizeToContent).
    private void ApplyLayout()
    {
        var work = SystemParameters.WorkArea;
        var columns = LauncherLayout.Columns(_viewModel.Profiles.Count, work.Width);
        TileListWidth = LauncherLayout.ListWidth(columns);
        FooterWidth = LauncherLayout.FooterWidth(columns);
        MaxWidth = work.Width;
        MaxHeight = work.Height;
        PlaceAtBottomCentre();
    }

    private void PlaceAtBottomCentre()
    {
        var work = SystemParameters.WorkArea;
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        (Left, Top) = LauncherLayout.Position(ActualWidth, ActualHeight, work.Left, work.Top, work.Width, work.Height);
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
