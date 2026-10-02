using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StartupProfiles.App.Api;
using StartupProfiles.App.Config;
using StartupProfiles.App.Discovery;
using StartupProfiles.App.Integration;
using StartupProfiles.App.Interaction;
using StartupProfiles.App.Launcher;
using StartupProfiles.App.Maintenance;
using StartupProfiles.App.Themes;
using StartupProfiles.App.Tray;
using StartupProfiles.Core.Confirmations;
using StartupProfiles.Core.Execution;
using StartupProfiles.Core.Startup;
using StartupProfiles.Core.Storage;
using StartupProfiles.Windows;
using ThemeMode = StartupProfiles.App.Themes.ThemeMode;

namespace StartupProfiles.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Installer maintenance commands (--register-login, --register-protocol, ...) run headlessly and
        // exit; they must not take the mutex or start the API.
        if (MaintenanceCommands.TryRun(args)) return;

        // A `register` / startupprofiles:// invocation is a one-shot confirmation dialog: it must not take
        // the single-instance mutex or start the API, so it can run alongside a full instance.
        if (RegistrationLaunch.IsRegistrationInvocation(args))
        {
            RegistrationApp.Run(args);
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) Diagnostics.CrashLog.Write("AppDomain", ex);
            else Diagnostics.CrashLog.Write("AppDomain", e.ExceptionObject?.ToString() ?? "unknown");
        };

        var options = LaunchOptions.Parse(args);

        // Single instance: a later launch (e.g. from the Start Menu while the app sits in the tray) asks the
        // running instance to show its launcher, then exits.
        using var instance = new SingleInstance();
        if (!instance.IsFirst)
        {
            instance.SignalFirst();
            return;
        }

        var host = BuildHost(options);
        host.StartAsync().GetAwaiter().GetResult();
        RuntimeInfo.Write(options.Port);

        try
        {
            if (options.Headless)
                host.WaitForShutdownAsync().GetAwaiter().GetResult();
            else
                RunUi(host, instance);
        }
        finally
        {
            host.StopAsync().GetAwaiter().GetResult();
            RuntimeInfo.Delete();
        }
    }

    private static void RunUi(WebApplication host, SingleInstance instance)
    {
        var profiles = host.Services.GetRequiredService<IProfileStore>();
        var baseStore = host.Services.GetRequiredService<IBaseStore>();
        var library = host.Services.GetRequiredService<Core.Library.LibraryService>();
        var executor = host.Services.GetRequiredService<ProfileExecutor>();
        var config = host.Services.GetRequiredService<IConfigStore>();
        var history = host.Services.GetRequiredService<IHistoryStore>();
        var prompts = new UserPrompts();

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) =>
        {
            Diagnostics.CrashLog.Write("Dispatcher", e.Exception);
            e.Handled = true; // Keep the app alive; the exception is logged to error.log for diagnosis.
        };
        var theme = new ThemeManager(app);
        theme.Apply(ThemeManager.Parse(config.GetValue("theme")));

        void ApplyTheme(ThemeMode mode)
        {
            theme.Apply(mode);
            config.SetValue("theme", mode.ToString());
        }

        void OpenConfig() =>
            new ConfigWindow(new ConfigViewModel(profiles, baseStore, library, new WindowsStartupAppCatalog(), executor, prompts,
                    new DebouncedSaveScheduler(), config),
                ThemeManager.Parse(config.GetValue("theme")), ApplyTheme).Show();

        // At most one launcher: reopening it (tray, or launching the app again) brings the open one forward.
        // Only the one shown at login may start a profile by itself after a countdown.
        LauncherWindow? launcher = null;
        void OpenLauncher() => ShowLauncher(atLogin: false);
        void ShowLauncher(bool atLogin)
        {
            if (launcher is not null)
            {
                if (launcher.WindowState == WindowState.Minimized) launcher.WindowState = WindowState.Normal;
                launcher.Activate();
                return;
            }

            launcher = new LauncherWindow(new LauncherViewModel(profiles, executor, history, atLogin: atLogin), OpenConfig);
            launcher.Closed += (_, _) => launcher = null;
            launcher.Show();
        }

        using var tray = new TrayIcon(profiles, executor, OpenConfig, OpenLauncher, app.Shutdown);

        // Show the login selector at startup; the app then lives in the tray. Launching the app again while
        // it runs shows the launcher.
        ShowLauncher(atLogin: true);

        // Look for apps that set themselves to start with Windows since last time, off the UI thread, and offer
        // them once the launcher is out of the way.
        if (!string.Equals(config.GetValue(AppSettingKeys.DiscoverStartupApps), "false", StringComparison.OrdinalIgnoreCase))
            app.Dispatcher.BeginInvoke(async () =>
            {
                var catalog = new WindowsStartupAppCatalog();
                var discovery = new StartupDiscovery(
                    catalog,
                    new StartupSeenStore(),
                    new StartupTakeover(catalog, profiles, new StartupTakeoverStore(), WindowsStartupRegistration.DefaultValueName),
                    profiles,
                    baseStore,
                    library,
                    WindowsStartupRegistration.DefaultValueName);

                IReadOnlyList<StartupEntry> found;
                try { found = await Task.Run(discovery.FindNew); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    Diagnostics.CrashLog.Write("StartupDiscovery", ex);
                    return;
                }
                if (found.Count == 0) return;

                void Offer() => new NewStartupAppsWindow(new NewStartupAppsViewModel(found, profiles.GetAll(), discovery)).Show();
                if (launcher is null) Offer();
                else launcher.Closed += (_, _) => Offer();
            });
        instance.Listen(() => app.Dispatcher.BeginInvoke(OpenLauncher));

        app.Run();
    }

    private static WebApplication BuildHost(LaunchOptions options)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = options.RawArgs,
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        builder.Services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        builder.Services.AddSingleton<IProfileStore>(_ => new ProfileStore());
        builder.Services.AddSingleton<IBaseStore>(_ => new BaseStore());
        builder.Services.AddSingleton<ILibraryStore>(_ => new LibraryStore());
        builder.Services.AddSingleton<Core.Library.LibraryService>();
        builder.Services.AddSingleton<IConfigStore>(_ => new ConfigStore());
        builder.Services.AddSingleton<IHistoryStore>(_ => new HistoryStore());
        builder.Services.AddSingleton(_ => WindowsRuntime.CreateActionRegistry());
        builder.Services.AddSingleton(sp => new ProfileRunner(
            sp.GetRequiredService<Core.Actions.ActionHandlerRegistry>(), new TaskDelayer()));
        builder.Services.AddSingleton<ProfileExecutor>();
        builder.Services.AddSingleton<ConfirmationService>();
        builder.Services.AddSingleton<Core.Integration.IProfileRegistrar>(sp =>
            new Core.Integration.ProfileRegistrar(
                sp.GetRequiredService<IProfileStore>(),
                sp.GetRequiredService<IBaseStore>(),
                sp.GetRequiredService<Core.Library.LibraryService>()));

        var app = builder.Build();
        app.Urls.Add($"http://127.0.0.1:{options.Port}");
        app.MapStartupProfilesApi();
        return app;
    }
}
