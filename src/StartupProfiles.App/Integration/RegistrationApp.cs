using System.Windows;
using StartupProfiles.App.Themes;
using StartupProfiles.Core.Integration;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.App.Integration;

/// <summary>
/// The one-shot registration host: parses a <c>register</c> invocation, shows Startup Profiles' own
/// trusted confirmation window, applies the user's choice, and exits. It never starts the loopback API,
/// takes the tray, or contends for the single-instance mutex - it is a short-lived dialog process. When a
/// full instance is already running, the write is routed through it (see <see cref="RunningInstance"/>).
/// </summary>
internal static class RegistrationApp
{
    public static void Run(string[] args)
    {
        if (!RegistrationLaunch.TryParse(args, out var request, out var error))
        {
            ShowError(error);
            return;
        }

        var (choices, registrar) = Compose();
        if (choices.Count == 0)
        {
            ShowError("No startup profiles are configured yet. Create one in Startup Profiles first.");
            return;
        }

        var app = new Application { ShutdownMode = ShutdownMode.OnLastWindowClose };
        new ThemeManager(app).Apply(ThemeManager.Parse(new ConfigStore().GetValue("theme")));

        var viewModel = new RegistrationViewModel(request, choices, registrar);
        var window = new RegistrationWindow(viewModel);
        viewModel.CloseRequested += () => window.Dispatcher.Invoke(window.Close);
        app.Run(window);
    }

    private static (IReadOnlyList<ProfileChoice> Choices, IProfileRegistrar Registrar) Compose()
    {
        if (RunningInstance.Discover() is { } instance)
            return (instance.GetProfiles(), instance.Registrar);

        // No live instance: this process is the sole writer, so a direct store write is safe.
        var store = new ProfileStore();
        var choices = store.GetAll().Select(p => new ProfileChoice(p.Id, p.Name)).ToList();
        return (choices, new ProfileRegistrar(store));
    }

    private static void ShowError(string message) =>
        MessageBox.Show(message, "Startup Profiles", MessageBoxButton.OK, MessageBoxImage.Warning);
}
