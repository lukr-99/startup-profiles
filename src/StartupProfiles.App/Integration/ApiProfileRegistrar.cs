using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using StartupProfiles.App.Api;
using StartupProfiles.Core.Integration;

namespace StartupProfiles.App.Integration;

/// <summary>
/// An <see cref="IProfileRegistrar"/> that applies through a running Startup Profiles instance's loopback
/// API rather than writing profiles.json directly. This matters because the running app caches the
/// profile list in memory: a second process writing the file would be overwritten on the app's next save.
/// Routing the write through the owner keeps a single writer. Runs the two-phase confirm on the caller's
/// behalf (this transport is Startup Profiles talking to itself, not an untrusted external app).
/// </summary>
public sealed class ApiProfileRegistrar : IProfileRegistrar
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly Uri _baseAddress;

    public ApiProfileRegistrar(Uri baseAddress) => _baseAddress = baseAddress;

    public RegistrationOutcome Apply(RegistrationRequest request, RegistrationTargets targets)
    {
        var body = new RegisterRequestBody(
            request.AppId, request.Name, request.Target, request.Arguments, request.Icon,
            request.Publisher, request.SuggestedProfile, request.SupportsMinimized,
            [.. targets.ProfileIds], targets.IncludeBase);

        try
        {
            using var http = new HttpClient { BaseAddress = _baseAddress, Timeout = TimeSpan.FromSeconds(15) };

            var preview = Post(http, "/api/register", body).GetProperty("confirmToken").GetString();
            if (string.IsNullOrEmpty(preview))
                throw new RegistrationException("The running app did not return a confirmation token.");

            var confirmed = Post(http, $"/api/register?confirmToken={preview}", body);
            return confirmed.GetProperty("outcome").Deserialize<RegistrationOutcome>(Json)
                ?? throw new RegistrationException("The running app returned no outcome.");
        }
        catch (HttpRequestException ex)
        {
            throw new RegistrationException("Could not reach the running Startup Profiles instance.", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new RegistrationException("The running Startup Profiles instance did not respond in time.", ex);
        }
    }

    private static JsonElement Post(HttpClient http, string path, RegisterRequestBody body)
    {
        var response = http.PostAsJsonAsync(path, body, Json).GetAwaiter().GetResult();
        var json = response.Content.ReadFromJsonAsync<JsonElement>(Json).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            var error = json.TryGetProperty("error", out var e) ? e.GetString() : response.ReasonPhrase;
            throw new RegistrationException(error ?? "The registration request was rejected.");
        }
        return json;
    }
}
