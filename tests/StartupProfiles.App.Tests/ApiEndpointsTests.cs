using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StartupProfiles.Core.Models;

namespace StartupProfiles.App.Tests;

public sealed class ApiEndpointsTests
{
    [Fact]
    public async Task Health_ReportsStatusAndProfileCount()
    {
        await using var api = await TestApi.StartAsync();

        var health = await api.Client.GetFromJsonAsync<JsonElement>("/api/health");

        Assert.Equal("ok", health.GetProperty("status").GetString());
        Assert.Equal(6, health.GetProperty("profiles").GetInt32());
    }

    [Fact]
    public async Task Profiles_List_ReturnsSeededSummaries()
    {
        await using var api = await TestApi.StartAsync();

        var list = await api.Client.GetFromJsonAsync<JsonElement>("/api/profiles");

        Assert.Equal(6, list.GetArrayLength());
        Assert.True(list[0].TryGetProperty("actionCount", out _));
    }

    [Fact]
    public async Task Profiles_CreateThenGet_RoundTrips()
    {
        await using var api = await TestApi.StartAsync();
        var profile = new Profile
        {
            Id = "test",
            Name = "Test",
            Actions = [new ProfileAction { Type = ActionType.OpenUrl, Target = "https://example.com" }],
        };

        var created = await api.Client.PostAsJsonAsync("/api/profiles", profile, TestApi.Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var fetched = await api.Client.GetFromJsonAsync<Profile>("/api/profiles/test", TestApi.Json);

        Assert.NotNull(fetched);
        Assert.Equal("Test", fetched!.Name);
        Assert.Equal(ActionType.OpenUrl, Assert.Single(fetched.Actions).Type);
    }

    [Fact]
    public async Task Profiles_CreateDuplicate_Conflicts()
    {
        await using var api = await TestApi.StartAsync();
        var profile = new Profile { Id = "work", Name = "Work" };

        var response = await api.Client.PostAsJsonAsync("/api/profiles", profile, TestApi.Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Profiles_Delete_RequiresConfirmationToken()
    {
        await using var api = await TestApi.StartAsync();

        var first = await api.Client.DeleteAsync("/api/profiles/work");
        var body = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("required").GetBoolean());
        var token = body.GetProperty("confirmToken").GetString();

        var confirmed = await api.Client.DeleteAsync($"/api/profiles/work?confirmToken={token}");
        var result = await confirmed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.GetProperty("ok").GetBoolean());

        var afterDelete = await api.Client.GetAsync("/api/profiles/work");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [Fact]
    public async Task Run_ExecutesProfile_AndRecordsHistory()
    {
        await using var api = await TestApi.StartAsync();
        var profile = new Profile
        {
            Id = "runme",
            Name = "Run Me",
            Actions = [new ProfileAction { Type = ActionType.Delay, Delay = TimeSpan.Zero }],
        };
        await api.Client.PostAsJsonAsync("/api/profiles", profile, TestApi.Json);

        var run = await api.Client.PostAsync("/api/profiles/runme/run", content: null);
        var runBody = await run.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(runBody.GetProperty("succeeded").GetBoolean());

        var history = await api.Client.GetFromJsonAsync<JsonElement>("/api/history");
        Assert.True(history.GetArrayLength() >= 1);
        Assert.Equal("runme", history[0].GetProperty("profileId").GetString());
    }

    [Fact]
    public async Task Register_ReturnsNotImplemented()
    {
        await using var api = await TestApi.StartAsync();

        var response = await api.Client.PostAsync("/api/register", content: null);

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }
}
