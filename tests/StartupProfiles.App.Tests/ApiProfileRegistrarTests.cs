using System.Net.Http.Json;
using StartupProfiles.App.Integration;
using StartupProfiles.Core.Integration;
using StartupProfiles.Core.Models;

namespace StartupProfiles.App.Tests;

public sealed class ApiProfileRegistrarTests
{
    private static RegistrationRequest Request() => new()
    {
        AppId = "com.example.app",
        Name = "Example App",
        Target = @"C:\Apps\example.exe",
        Arguments = "--fast",
    };

    [Fact]
    public async Task Apply_RunsTheTwoPhaseConfirm_AndAddsTheAction()
    {
        await using var api = await TestApi.StartAsync();
        var registrar = new ApiProfileRegistrar(api.Client.BaseAddress!);

        var outcome = await Task.Run(() => registrar.Apply(Request(), RegistrationTargets.For(["dev"])));

        Assert.Equal(["dev"], outcome.AddedTo);
        Assert.True(outcome.AddedToLibrary);
        var dev = await api.Client.GetFromJsonAsync<Profile>("/api/profiles/dev", TestApi.Json);
        var action = Assert.Single(dev!.Actions);
        Assert.Equal(ActionType.LaunchApp, action.Type);
        Assert.Equal(@"C:\Apps\example.exe", action.Target);
        Assert.Equal(outcome.LibraryItemId, action.LibraryItemId);
    }

    [Fact]
    public async Task Apply_WithTheBaseChosen_AddsToTheBase()
    {
        await using var api = await TestApi.StartAsync();
        var registrar = new ApiProfileRegistrar(api.Client.BaseAddress!);

        var outcome = await Task.Run(() => registrar.Apply(Request(), new RegistrationTargets { IncludeBase = true }));

        Assert.True(outcome.AddedToBase);
        var baseValue = await api.Client.GetFromJsonAsync<StartupBase>("/api/base", TestApi.Json);
        var action = Assert.Single(baseValue!.Actions);
        Assert.Equal(@"C:\Apps\example.exe", action.Target);
    }

    [Fact]
    public async Task Apply_LibraryOnly_RecognizesTheAppWithoutTouchingAProfile()
    {
        await using var api = await TestApi.StartAsync();
        var registrar = new ApiProfileRegistrar(api.Client.BaseAddress!);

        var outcome = await Task.Run(() => registrar.Apply(Request(), RegistrationTargets.LibraryOnly));

        Assert.True(outcome.AddedToLibrary);
        Assert.Empty(outcome.AddedTo);
        var dev = await api.Client.GetFromJsonAsync<Profile>("/api/profiles/dev", TestApi.Json);
        Assert.Empty(dev!.Actions);
    }

    [Fact]
    public async Task Apply_UnreachableInstance_ThrowsRegistrationException()
    {
        var registrar = new ApiProfileRegistrar(new Uri("http://127.0.0.1:1/"));

        await Assert.ThrowsAsync<RegistrationException>(() =>
            Task.Run(() => registrar.Apply(Request(), RegistrationTargets.For(["dev"]))));
    }
}
