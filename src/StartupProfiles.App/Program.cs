using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StartupProfiles.App.Api;
using StartupProfiles.App.Config;
using StartupProfiles.App.Interaction;
using StartupProfiles.App.Launcher;
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
        var executor = host.Services.GetRequiredService<ProfileExecutor>();
        var config = host.Services.GetRequiredService<IConfigStore>();
        var prompts = new UserPrompts();

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var theme = new ThemeManager(app);
        theme.Apply(ThemeManager.Parse(config.GetValue("theme")));

        void ApplyTheme(ThemeMode mode)
        {
            theme.Apply(mode);
            config.SetValue("theme", mode.ToString());
        }

        void OpenConfig() =>
            new ConfigWindow(new ConfigViewModel(profiles, executor, prompts),
                ThemeManager.Parse(config.GetValue("theme")), ApplyTheme).Show();

        using var tray = new TrayIcon(profiles, executor, OpenConfig, app.Shutdown);

        // Show the login selector at startup; the app then lives in the tray.
        new LauncherWindow(new LauncherViewModel(profiles, executor), OpenConfig).Show();

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
