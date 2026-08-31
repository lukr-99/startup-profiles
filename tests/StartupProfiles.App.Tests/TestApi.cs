using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StartupProfiles.App.Api;
using StartupProfiles.Core.Actions;
using StartupProfiles.Core.Confirmations;
using StartupProfiles.Core.Execution;
using StartupProfiles.Core.Integration;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.App.Tests;

/// <summary>
/// Boots the loopback API on an ephemeral port over temp-file stores and exposes an <see cref="HttpClient"/>,
/// so the endpoints are exercised end to end (routing + JSON) without launching real processes.
/// </summary>
internal sealed class TestApi : IAsyncDisposable
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly WebApplication _app;
    private readonly string _profilesFile;
    private readonly string _historyFile;

    public HttpClient Client { get; }

    private TestApi(WebApplication app, HttpClient client, string profilesFile, string historyFile)
    {
        _app = app;
        Client = client;
        _profilesFile = profilesFile;
        _historyFile = historyFile;
    }

    public static async Task<TestApi> StartAsync()
    {
        var profilesFile = Path.Combine(Path.GetTempPath(), $"sp-api-{Guid.NewGuid():N}.json");
        var historyFile = Path.Combine(Path.GetTempPath(), $"sp-hist-{Guid.NewGuid():N}.json");

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });
        builder.Services.AddSingleton<IProfileStore>(_ => new ProfileStore(profilesFile));
        builder.Services.AddSingleton<IHistoryStore>(_ => new HistoryStore(historyFile));
        builder.Services.AddSingleton(_ => ActionHandlerRegistry.CreateDefault(new SystemProcessLauncher()));
        builder.Services.AddSingleton(sp => new ProfileRunner(sp.GetRequiredService<ActionHandlerRegistry>(), new TaskDelayer()));
        builder.Services.AddSingleton<ProfileExecutor>();
        builder.Services.AddSingleton<ConfirmationService>();
        builder.Services.AddSingleton<IProfileRegistrar>(sp => new ProfileRegistrar(sp.GetRequiredService<IProfileStore>()));

        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.MapStartupProfilesApi();
        await app.StartAsync();

        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        var client = new HttpClient { BaseAddress = new Uri(address) };
        return new TestApi(app, client, profilesFile, historyFile);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
        File.Delete(_profilesFile);
        File.Delete(_historyFile);
    }
}
