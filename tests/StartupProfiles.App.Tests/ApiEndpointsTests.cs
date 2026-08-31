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
    public async Task Register_FirstCall_RequiresConfirmationAndAddsNothing()
    {
        await using var api = await TestApi.StartAsync();
        var body = new { appId = "com.example.app", name = "Example App", target = @"C:\Apps\example.exe", profileIds = new[] { "dev" } };

        var response = await api.Client.PostAsJsonAsync("/api/register", body);
        var preview = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(preview.GetProperty("required").GetBoolean());
        Assert.False(string.IsNullOrEmpty(preview.GetProperty("confirmToken").GetString()));

        var dev = await api.Client.GetFromJsonAsync<Profile>("/api/profiles/dev", TestApi.Json);
        Assert.Empty(dev!.Actions);
    }

    [Fact]
    public async Task Register_WithConfirmationToken_AddsLaunchActionToProfile()
    {
        await using var api = await TestApi.StartAsync();
        var body = new { appId = "com.example.app", name = "Example App", target = @"C:\Apps\example.exe", arguments = "--fast", profileIds = new[] { "dev" } };

        var first = await api.Client.PostAsJsonAsync("/api/register", body);
        var token = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("confirmToken").GetString();

        var confirmed = await api.Client.PostAsJsonAsync($"/api/register?confirmToken={token}", body);
        var result = await confirmed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.GetProperty("ok").GetBoolean());

        var dev = await api.Client.GetFromJsonAsync<Profile>("/api/profiles/dev", TestApi.Json);
        var action = Assert.Single(dev!.Actions);
        Assert.Equal(ActionType.LaunchApp, action.Type);
        Assert.Equal(@"C:\Apps\example.exe", action.Target);
        Assert.Equal("--fast", action.Arguments);
    }

    [Fact]
    public async Task Register_TokenIsBoundToTheChosenProfiles()
    {
        await using var api = await TestApi.StartAsync();
        var forDev = new { appId = "com.example.app", name = "Example App", target = @"C:\Apps\example.exe", profileIds = new[] { "dev" } };

        var first = await api.Client.PostAsJsonAsync("/api/register", forDev);
        var token = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("confirmToken").GetString();

        // Replaying the token against a different profile set must not consume it - it re-previews instead.
        var forGames = new { appId = "com.example.app", name = "Example App", target = @"C:\Apps\example.exe", profileIds = new[] { "games" } };
        var replayed = await api.Client.PostAsJsonAsync($"/api/register?confirmToken={token}", forGames);
        var body = await replayed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("required").GetBoolean());

        var games = await api.Client.GetFromJsonAsync<Profile>("/api/profiles/games", TestApi.Json);
        Assert.Empty(games!.Actions);
    }

    [Fact]
    public async Task Register_MissingTarget_IsBadRequest()
    {
        await using var api = await TestApi.StartAsync();
        var body = new { appId = "com.example.app", name = "Example App", profileIds = new[] { "dev" } };

        var response = await api.Client.PostAsJsonAsync("/api/register", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_NoProfiles_IsBadRequest()
    {
        await using var api = await TestApi.StartAsync();
        var body = new { appId = "com.example.app", name = "Example App", target = @"C:\Apps\example.exe" };

        var response = await api.Client.PostAsJsonAsync("/api/register", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
