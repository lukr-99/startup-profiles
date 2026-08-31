using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StartupProfiles.App.Api;
using StartupProfiles.App.Tray;
using StartupProfiles.Core.Confirmations;
using StartupProfiles.Core.Execution;
using StartupProfiles.Core.Storage;
using StartupProfiles.Windows;

namespace StartupProfiles.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var options = LaunchOptions.Parse(args);

        // Single instance: a second launch just exits (the login selector UI arrives in a later milestone).
        using var mutex = new Mutex(initiallyOwned: true, "StartupProfiles.Local.SingleInstance", out var isNew);
        if (!isNew) return;

        var app = BuildApp(options);
        app.StartAsync().GetAwaiter().GetResult();
        RuntimeInfo.Write(options.Port);

        try
        {
            if (options.Headless)
            {
                app.WaitForShutdownAsync().GetAwaiter().GetResult();
            }
            else
            {
                System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.SystemAware);
                System.Windows.Forms.Application.EnableVisualStyles();
                System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
                using var tray = new TrayApplicationContext(
                    app.Services.GetRequiredService<IProfileStore>(),
                    app.Services.GetRequiredService<ProfileExecutor>());
                System.Windows.Forms.Application.Run(tray);
            }
        }
        finally
        {
            app.StopAsync().GetAwaiter().GetResult();
            RuntimeInfo.Delete();
        }
    }

    private static WebApplication BuildApp(LaunchOptions options)
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

        var app = builder.Build();
        app.Urls.Add($"http://127.0.0.1:{options.Port}");
        app.MapStartupProfilesApi();
        return app;
    }
}
