using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StartupProfiles.App.Api;
using StartupProfiles.App.Config;
using StartupProfiles.App.Integration;
using StartupProfiles.App.Interaction;
using StartupProfiles.App.Launcher;
using StartupProfiles.App.Maintenance;
using StartupProfiles.App.Themes;
using StartupProfiles.App.Tray;
using StartupProfiles.Core.Confirmations;
using StartupProfiles.Core.Execution;
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

        // Single instance: a second launch just exits.
        using var mutex = new Mutex(initiallyOwned: true, "StartupProfiles.Local.SingleInstance", out var isNew);
        if (!isNew) return;

        var host = BuildHost(options);
        host.StartAsync().GetAwaiter().GetResult();
        RuntimeInfo.Write(options.Port);

        try
        {
            if (options.Headless)
                host.WaitForShutdownAsync().GetAwaiter().GetResult();
            else
                RunUi(host);
        }
        finally
        {
            host.StopAsync().GetAwaiter().GetResult();
            RuntimeInfo.Delete();
        }
    }

    private static void RunUi(WebApplication host)
    {
        var profiles = host.Services.GetRequiredService<IProfileStore>();
        var baseStore = host.Services.GetRequiredService<IBaseStore>();
        var executor = host.Services.GetRequiredService<ProfileExecutor>();
        var config = host.Services.GetRequiredService<IConfigStore>();
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
            new ConfigWindow(new ConfigViewModel(profiles, baseStore, executor, prompts),
                ThemeManager.Parse(config.GetValue("theme")), ApplyTheme).Show();

        void OpenLauncher() =>
            new LauncherWindow(new LauncherViewModel(profiles, executor), OpenConfig).Show();

        using var tray = new TrayIcon(profiles, executor, OpenConfig, OpenLauncher, app.Shutdown);

        // Show the login selector at startup; the app then lives in the tray.
        OpenLauncher();

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
        builder.Services.AddSingleton<IConfigStore>(_ => new ConfigStore());
        builder.Services.AddSingleton<IHistoryStore>(_ => new HistoryStore());
        builder.Services.AddSingleton(_ => WindowsRuntime.CreateActionRegistry());
        builder.Services.AddSingleton(sp => new ProfileRunner(
            sp.GetRequiredService<Core.Actions.ActionHandlerRegistry>(), new TaskDelayer()));
        builder.Services.AddSingleton<ProfileExecutor>();
        builder.Services.AddSingleton<ConfirmationService>();
        builder.Services.AddSingleton<Core.Integration.IProfileRegistrar>(sp =>
            new Core.Integration.ProfileRegistrar(sp.GetRequiredService<IProfileStore>()));

        var app = builder.Build();
        app.Urls.Add($"http://127.0.0.1:{options.Port}");
        app.MapStartupProfilesApi();
        return app;
    }
}
