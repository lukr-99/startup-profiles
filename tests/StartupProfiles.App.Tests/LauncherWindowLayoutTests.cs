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
    [Fact]
    public void Seven_profiles_share_one_row() => RunSta(() =>
    {
        using var services = AppTestServices.Create();
        services.Profiles.Save(new Profile { Id = "deadlock", Name = "Deadlock", Icon = "games" });
        var window = Create(services);

        var rows = RowTops(window);

        Assert.Equal(7, services.Profiles.GetAll().Count);
        Assert.Single(rows);
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
