using StartupProfiles.Core.Library;
using StartupProfiles.Core.Models;
using StartupProfiles.Core.Storage;
using StartupProfiles.Core.Tests.Integration;

namespace StartupProfiles.Core.Tests.Library;

public sealed class LibraryServiceTests : IDisposable
{
    private readonly string _libraryFile = Path.Combine(Path.GetTempPath(), $"sp-lib-{Guid.NewGuid():N}.json");
    private readonly string _baseFile = Path.Combine(Path.GetTempPath(), $"sp-libb-{Guid.NewGuid():N}.json");
    private readonly FakeProfileStore _profiles = new([.. DefaultProfiles.Create()]);
    private readonly BaseStore _base;
    private readonly LibraryService _library;

    public LibraryServiceTests()
    {
        _base = new BaseStore(_baseFile);
        _library = new LibraryService(new LibraryStore(_libraryFile), _profiles, _base);
    }

    private static ProfileAction Launch(string target, string? arguments = null) =>
        new() { Type = ActionType.LaunchApp, Target = target, Arguments = arguments };

    [Fact]
    public void Add_GivesEachItemAUniqueIdFromItsName()
    {
        var first = _library.Add(new LibraryItem { Name = "Steam Client", Target = "a.exe" });
        var second = _library.Add(new LibraryItem { Name = "Steam client!", Target = "b.exe" });
        var unnamed = _library.Add(new LibraryItem { Name = "  ", Target = "c.exe" });

        Assert.Equal("steam-client", first.Id);
        Assert.Equal("steam-client-2", second.Id);
        Assert.Equal("item", unnamed.Id);
    }

    [Fact]
    public void FindOrAdd_ReusesTheItemThatStartsTheSameThing()
    {
        var steam = _library.FindOrAdd("Steam", Launch(@"C:\Steam\steam.exe", "-silent"));

        var again = _library.FindOrAdd("Other name", Launch(@"c:\steam\STEAM.exe", "-SILENT"));
        var different = _library.FindOrAdd("Steam", Launch(@"C:\Steam\steam.exe", "-bigpicture"));

        Assert.Equal(steam.Id, again.Id);
        Assert.NotEqual(steam.Id, different.Id);
        Assert.Equal(2, _library.GetAll().Count);
    }

    [Fact]
    public void UsedBy_ListsTheBaseFirst_ThenProfiles()
    {
        var steam = _library.Add(new LibraryItem { Name = "Steam", Target = "steam.exe" });
        var link = steam.ApplyTo(Launch("steam.exe"));
        _profiles.Save(_profiles.Find("games")! with { Actions = [link] });
        _base.Save(new StartupBase { Actions = [link] });

        Assert.Equal(["Base", "Games"], _library.UsedBy(steam.Id));
    }

    [Fact]
    public void Remove_TurnsLinksIntoStandaloneCopiesOfTheItem()
    {
        var steam = _library.Add(new LibraryItem { Name = "Steam", Target = @"D:\Steam\steam.exe", Arguments = "-silent" });
        var staleLink = Launch(@"C:\old\steam.exe") with { LibraryItemId = steam.Id, Delay = TimeSpan.FromSeconds(2) };
        _profiles.Save(_profiles.Find("games")! with { Actions = [staleLink, Launch("discord.exe")] });
        _base.Save(new StartupBase { Actions = [staleLink] });

        _library.Remove(steam.Id);

        Assert.Empty(_library.GetAll());
        var kept = _profiles.Find("games")!.Actions[0];
        Assert.Null(kept.LibraryItemId);
        Assert.Equal(@"D:\Steam\steam.exe", kept.Target);
        Assert.Equal("-silent", kept.Arguments);
        Assert.Equal(TimeSpan.FromSeconds(2), kept.Delay);
        Assert.Equal("discord.exe", _profiles.Find("games")!.Actions[1].Target);
        Assert.Null(Assert.Single(_base.Load().Actions).LibraryItemId);
    }

    public void Dispose()
    {
        File.Delete(_libraryFile);
        File.Delete(_baseFile);
    }
}
