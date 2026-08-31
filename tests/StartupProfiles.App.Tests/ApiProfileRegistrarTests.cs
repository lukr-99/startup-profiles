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

        var outcome = await Task.Run(() => registrar.Apply(Request(), ["dev"]));

        Assert.Equal(["dev"], outcome.AddedTo);
        var dev = await api.Client.GetFromJsonAsync<Profile>("/api/profiles/dev", TestApi.Json);
        var action = Assert.Single(dev!.Actions);
        Assert.Equal(ActionType.LaunchApp, action.Type);
        Assert.Equal(@"C:\Apps\example.exe", action.Target);
    }

    [Fact]
    public async Task Apply_UnreachableInstance_ThrowsRegistrationException()
    {
        var registrar = new ApiProfileRegistrar(new Uri("http://127.0.0.1:1/"));

        await Assert.ThrowsAsync<RegistrationException>(() => Task.Run(() => registrar.Apply(Request(), ["dev"])));
    }
}
