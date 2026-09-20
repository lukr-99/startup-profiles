using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using StartupProfiles.Core.Integration;

namespace StartupProfiles.App.Integration;

/// <summary>
/// A reachable, already-running Startup Profiles host discovered via endpoint.json. Registration routes
/// through it so the running app (which caches profiles in memory) stays the single writer of profiles.json.
/// </summary>
public sealed class RunningInstance
{
    private readonly Uri _baseAddress;

    private RunningInstance(Uri baseAddress) => _baseAddress = baseAddress;

    public IProfileRegistrar Registrar => new ApiProfileRegistrar(_baseAddress);

    /// <summary>Returns the running instance if endpoint.json points at a live host, else null.</summary>
    public static RunningInstance? Discover()
    {
        if (RuntimeInfo.ReadUrl() is not { } url || !Uri.TryCreate(url, UriKind.Absolute, out var baseAddress))
            return null;

        try
        {
            using var http = new HttpClient { BaseAddress = baseAddress, Timeout = TimeSpan.FromSeconds(2) };
            var health = http.GetAsync("/api/health").GetAwaiter().GetResult();
            return health.IsSuccessStatusCode ? new RunningInstance(baseAddress) : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Stale endpoint.json (the app is not actually running); fall back to a direct write.
            return null;
        }
    }

    /// <summary>Fetches the destinations the window offers - the base, then every profile - from the running instance.</summary>
    public IReadOnlyList<RegistrationChoice> GetChoices()
    {
        using var http = new HttpClient { BaseAddress = _baseAddress, Timeout = TimeSpan.FromSeconds(5) };
        var health = http.GetFromJsonAsync<HealthDto>("/api/health").GetAwaiter().GetResult();
        var summaries = http.GetFromJsonAsync<List<ProfileSummaryDto>>("/api/profiles").GetAwaiter().GetResult() ?? [];

        return
        [
            RegistrationChoice.ForBase(health?.BaseActions ?? 0),
            .. summaries.Select(s => RegistrationChoice.ForProfile(s.Id, s.Name, s.Icon, s.ActionCount)),
        ];
    }

    private sealed record HealthDto(int BaseActions);

    private sealed record ProfileSummaryDto(string Id, string Name, string? Icon, int ActionCount);
}
