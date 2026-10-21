using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using StartupProfiles.App.Launcher;
using StartupProfiles.App.Themes;
using StartupProfiles.Core.Models;

namespace StartupProfiles.App.Tests;

/// <summary>Measures the real launcher window, so a template that eats width (and orphans a tile) fails here.</summary>
public sealed class LauncherWindowLayoutTests
{
    // The expected rows come from LauncherLayout for this machine's work area, so the test holds on a
    // narrow CI screen too. A template that eats width puts one tile fewer per row and adds a row.
    [Fact]
    public void Seven_profiles_fill_the_rows_the_layout_plans() => RunSta(() =>
    {
        using var services = AppTestServices.Create();
        services.Profiles.Save(new Profile { Id = "deadlock", Name = "Deadlock", Icon = "games" });
        var window = Create(services);
        var columns = LauncherLayout.Columns(7, SystemParameters.WorkArea.Width);

        var rows = RowTops(window);

        Assert.Equal(7, services.Profiles.GetAll().Count);
        Assert.Equal((int)Math.Ceiling(7 / (double)columns), rows.Count);
    });

    [Fact]
    public void A_long_status_does_not_widen_the_window() => RunSta(() =>
    {
        using var services = AppTestServices.Create();
        var window = Create(services);
        var before = window.ActualWidth;
        var viewModel = (LauncherViewModel)window.DataContext;

        typeof(LauncherViewModel).GetProperty(nameof(LauncherViewModel.Status))!
            .SetValue(viewModel, new string('x', 400));
        window.UpdateLayout();

        Assert.Equal(before, window.ActualWidth, 0.5);
    });

    private static LauncherWindow Create(AppTestServices services)
    {
        if (Application.Current is null)
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            new ThemeManager(app).Apply(StartupProfiles.App.Themes.ThemeMode.Light);
        }

        var window = new LauncherWindow(new LauncherViewModel(services.Profiles, services.Executor), () => { });
        window.ShowActivated = false;
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { body(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new Xunit.Sdk.XunitException(failure.ToString());
    }

    /// <summary>The distinct vertical positions of the tiles: one entry per row.</summary>
    private static List<double> RowTops(LauncherWindow window)
    {
        var tiles = (ListBox)window.FindName("Tiles");
        return tiles.Items.Cast<object>()
            .Select(item => (ListBoxItem)tiles.ItemContainerGenerator.ContainerFromItem(item))
            .Select(container => Math.Round(container.TranslatePoint(new Point(0, 0), tiles).Y))
            .Distinct()
            .ToList();
    }
}
