using StartupProfiles.Core.Integration;
using StartupProfiles.Core.Models;

namespace StartupProfiles.Core.Tests.Integration;

public sealed class ProfileRegistrarTests
{
    private static RegistrationRequest Request(string target = @"C:\Apps\example.exe", string? args = null) => new()
    {
        AppId = "com.example.app",
        Name = "Example App",
        Target = target,
        Arguments = args,
    };

    [Fact]
    public void Apply_AddsLaunchActionToChosenProfile()
    {
        var store = new FakeProfileStore(new Profile { Id = "dev", Name = "Dev" });
        var registrar = new ProfileRegistrar(store);

        var outcome = registrar.Apply(Request(args: "--fast"), ["dev"]);

        Assert.Equal(["dev"], outcome.AddedTo);
        Assert.True(outcome.ChangedAnything);
        var action = Assert.Single(store.Find("dev")!.Actions);
        Assert.Equal(ActionType.LaunchApp, action.Type);
        Assert.Equal(@"C:\Apps\example.exe", action.Target);
        Assert.Equal("--fast", action.Arguments);
    }

    [Fact]
    public void Apply_OnlyTouchesChosenProfiles()
    {
        var store = new FakeProfileStore(
            new Profile { Id = "dev", Name = "Dev" },
            new Profile { Id = "games", Name = "Games" });
        var registrar = new ProfileRegistrar(store);

        registrar.Apply(Request(), ["dev"]);

        Assert.Single(store.Find("dev")!.Actions);
        Assert.Empty(store.Find("games")!.Actions);
    }

    [Fact]
    public void Apply_IsIdempotentForSameTarget()
    {
        var store = new FakeProfileStore(new Profile
        {
            Id = "dev",
            Name = "Dev",
            Actions = [new ProfileAction { Type = ActionType.LaunchApp, Target = @"C:\Apps\EXAMPLE.exe" }],
        });
        var registrar = new ProfileRegistrar(store);

        var outcome = registrar.Apply(Request(), ["dev"]);

        Assert.Empty(outcome.AddedTo);
        Assert.Equal(["dev"], outcome.AlreadyPresentIn);
        Assert.Single(store.Find("dev")!.Actions);
    }

    [Fact]
    public void Apply_DeduplicatesRepeatedProfileIds()
    {
        var store = new FakeProfileStore(new Profile { Id = "dev", Name = "Dev" });
        var registrar = new ProfileRegistrar(store);

        var outcome = registrar.Apply(Request(), ["dev", "dev"]);

        Assert.Equal(["dev"], outcome.AddedTo);
        Assert.Single(store.Find("dev")!.Actions);
    }

    [Fact]
    public void Apply_UnknownProfile_IsReportedNotCreated()
    {
        var store = new FakeProfileStore(new Profile { Id = "dev", Name = "Dev" });
        var registrar = new ProfileRegistrar(store);

        var outcome = registrar.Apply(Request(), ["nope"]);

        Assert.Equal(["nope"], outcome.UnknownProfileIds);
        Assert.Empty(outcome.AddedTo);
        Assert.Null(store.Find("nope"));
    }
}
