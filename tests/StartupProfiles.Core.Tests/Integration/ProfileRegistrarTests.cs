using StartupProfiles.Core.Integration;
using StartupProfiles.Core.Library;
using StartupProfiles.Core.Models;

namespace StartupProfiles.Core.Tests.Integration;

public sealed class ProfileRegistrarTests
{
    private readonly FakeProfileStore _profiles;
    private readonly FakeBaseStore _base = new();
    private readonly FakeLibraryStore _libraryStore = new();
    private readonly ProfileRegistrar _registrar;
    private readonly LibraryService _library;

    public ProfileRegistrarTests()
    {
        _profiles = new FakeProfileStore(
            new Profile { Id = "dev", Name = "Dev" },
            new Profile { Id = "games", Name = "Games" });
        _library = new LibraryService(_libraryStore, _profiles, _base);
        _registrar = new ProfileRegistrar(_profiles, _base, _library);
    }

    private static RegistrationRequest Request(string target = @"C:\Apps\example.exe", string? args = null) => new()
    {
        AppId = "com.example.app",
        Name = "Example App",
        Target = target,
        Arguments = args,
    };

    [Fact]
    public void Apply_AddsLaunchActionToChosenProfile_LinkedToItsLibraryItem()
    {
        var outcome = _registrar.Apply(Request(args: "--fast"), RegistrationTargets.For(["dev"]));

        Assert.Equal(["dev"], outcome.AddedTo);
        Assert.True(outcome.ChangedAnything);
        var action = Assert.Single(_profiles.Find("dev")!.Actions);
        Assert.Equal(ActionType.LaunchApp, action.Type);
        Assert.Equal(@"C:\Apps\example.exe", action.Target);
        Assert.Equal("--fast", action.Arguments);

        var item = Assert.Single(_library.GetAll());
        Assert.Equal("Example App", item.Name);
        Assert.Equal(item.Id, action.LibraryItemId);
        Assert.Equal(item.Id, outcome.LibraryItemId);
        Assert.True(outcome.AddedToLibrary);
    }

    [Fact]
    public void Apply_OnlyTouchesChosenProfiles()
    {
        _registrar.Apply(Request(), RegistrationTargets.For(["dev"]));

        Assert.Single(_profiles.Find("dev")!.Actions);
        Assert.Empty(_profiles.Find("games")!.Actions);
        Assert.Empty(_base.Load().Actions);
    }

    [Fact]
    public void Apply_WithNoDestination_OnlyRecognizesTheAppInTheLibrary()
    {
        var outcome = _registrar.Apply(Request(), RegistrationTargets.LibraryOnly);

        var item = Assert.Single(_library.GetAll());
        Assert.Equal(item.Id, outcome.LibraryItemId);
        Assert.True(outcome.AddedToLibrary);
        Assert.True(outcome.ChangedAnything);
        Assert.Empty(outcome.AddedTo);
        Assert.Empty(_base.Load().Actions);
        Assert.Empty(_profiles.Find("dev")!.Actions);
    }

    [Fact]
    public void Apply_ReusesTheLibraryItemThatAlreadyStartsTheSameThing()
    {
        var first = _registrar.Apply(Request(), RegistrationTargets.LibraryOnly);

        var second = _registrar.Apply(Request(), RegistrationTargets.For(["dev"]));

        Assert.Equal(first.LibraryItemId, second.LibraryItemId);
        Assert.False(second.AddedToLibrary);
        Assert.Single(_library.GetAll());
    }

    [Fact]
    public void Apply_AddsToBase_WhenTheBaseIsChosen()
    {
        var outcome = _registrar.Apply(Request(), new RegistrationTargets { IncludeBase = true });

        Assert.True(outcome.AddedToBase);
        Assert.False(outcome.AlreadyInBase);
        var action = Assert.Single(_base.Load().Actions);
        Assert.Equal(@"C:\Apps\example.exe", action.Target);
        Assert.Equal(outcome.LibraryItemId, action.LibraryItemId);
    }

    [Fact]
    public void Apply_BaseIsIdempotentForSameTarget()
    {
        _registrar.Apply(Request(), new RegistrationTargets { IncludeBase = true });

        var outcome = _registrar.Apply(Request(), new RegistrationTargets { IncludeBase = true });

        Assert.False(outcome.AddedToBase);
        Assert.True(outcome.AlreadyInBase);
        Assert.Single(_base.Load().Actions);
    }

    [Fact]
    public void Apply_SkipsProfilesThatAlreadyStartItFromTheBase()
    {
        // "dev" includes the base by default, so adding it there too would launch the app twice.
        var outcome = _registrar.Apply(Request(), RegistrationTargets.For(["dev", "games"], includeBase: true));

        Assert.True(outcome.AddedToBase);
        Assert.Equal(["dev", "games"], outcome.AlreadyPresentIn);
        Assert.Empty(outcome.AddedTo);
        Assert.Empty(_profiles.Find("dev")!.Actions);
    }

    [Fact]
    public void Apply_AddsToAProfileThatExcludesTheBase()
    {
        _profiles.Save(_profiles.Find("games")! with { IncludeBase = false });

        var outcome = _registrar.Apply(Request(), RegistrationTargets.For(["games"], includeBase: true));

        Assert.Equal(["games"], outcome.AddedTo);
        Assert.Single(_profiles.Find("games")!.Actions);
        Assert.Single(_base.Load().Actions);
    }

    [Fact]
    public void Apply_IsIdempotentForSameTarget()
    {
        _profiles.Save(_profiles.Find("dev")! with
        {
            Actions = [new ProfileAction { Type = ActionType.LaunchApp, Target = @"C:\Apps\EXAMPLE.exe" }],
        });

        var outcome = _registrar.Apply(Request(), RegistrationTargets.For(["dev"]));

        Assert.Empty(outcome.AddedTo);
        Assert.Equal(["dev"], outcome.AlreadyPresentIn);
        Assert.Single(_profiles.Find("dev")!.Actions);
    }

    [Fact]
    public void Apply_DeduplicatesRepeatedProfileIds()
    {
        var outcome = _registrar.Apply(Request(), RegistrationTargets.For(["dev", "dev"]));

        Assert.Equal(["dev"], outcome.AddedTo);
        Assert.Single(_profiles.Find("dev")!.Actions);
    }

    [Fact]
    public void Apply_UnknownProfile_IsReportedNotCreated()
    {
        var outcome = _registrar.Apply(Request(), RegistrationTargets.For(["nope"]));

        Assert.Equal(["nope"], outcome.UnknownProfileIds);
        Assert.Empty(outcome.AddedTo);
        Assert.Null(_profiles.Find("nope"));
    }
}
