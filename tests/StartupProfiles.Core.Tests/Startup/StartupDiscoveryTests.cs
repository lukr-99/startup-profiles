using StartupProfiles.Core.Library;
using StartupProfiles.Core.Models;
using StartupProfiles.Core.Startup;
using StartupProfiles.Core.Storage;
using StartupProfiles.Core.Tests.Integration;

namespace StartupProfiles.Core.Tests.Startup;

public sealed class StartupDiscoveryTests : IDisposable
{
    private const string OwnValueName = "StartupProfiles";

    private readonly string _libraryFile = Path.Combine(Path.GetTempPath(), $"sp-disc-{Guid.NewGuid():N}.json");
    private readonly string _baseFile = Path.Combine(Path.GetTempPath(), $"sp-discb-{Guid.NewGuid():N}.json");
    private readonly FakeProfileStore _profiles = new([.. DefaultProfiles.Create()]);
    private readonly FakeStartupTakeoverStore _record = new();
    private readonly FakeStartupSeenStore _seen = new();
    private readonly BaseStore _base;
    private readonly LibraryService _library;

    public StartupDiscoveryTests()
    {
        _base = new BaseStore(_baseFile);
        _library = new LibraryService(new LibraryStore(_libraryFile), _profiles, _base);
    }

    public void Dispose()
    {
        File.Delete(_libraryFile);
        File.Delete(_baseFile);
    }

    private static StartupEntry Entry(string key, bool enabled = true, bool canToggle = true, bool launchable = true) => new()
    {
        Source = StartupEntrySource.UserRunKey,
        Key = key,
        Name = key,
        IsEnabled = enabled,
        CanToggle = canToggle,
        Launch = launchable ? new ProfileAction { Type = ActionType.LaunchApp, Target = $@"C:\Apps\{key}.exe" } : null,
    };

    private StartupDiscovery Discovery(FakeStartupAppCatalog catalog) => new(
        catalog, _seen, new StartupTakeover(catalog, _profiles, _record, OwnValueName), _profiles, _base, _library, OwnValueName);

    [Fact]
    public void TheFirstLook_RecordsWhatIsThere_AndOffersNothing()
    {
        var catalog = new FakeStartupAppCatalog(Entry("Steam"), Entry("Discord"));

        Assert.Empty(Discovery(catalog).FindNew());
        Assert.Equal(2, _seen.Keys!.Count);
    }

    [Fact]
    public void ALaterLook_OffersOnlyAppsThatAppearedSince()
    {
        var catalog = new FakeStartupAppCatalog(Entry("Steam"));
        Discovery(catalog).FindNew();

        var later = new FakeStartupAppCatalog(Entry("Steam"), Entry("Spotify"));

        Assert.Equal(["Spotify"], Discovery(later).FindNew().Select(e => e.Key));
    }

    [Fact]
    public void AppsThatAreOff_Locked_Unlaunchable_OrItself_AreNotOffered()
    {
        _seen.Keys = [];
        var catalog = new FakeStartupAppCatalog(
            Entry("Off", enabled: false), Entry("Locked", canToggle: false), Entry("Odd", launchable: false), Entry(OwnValueName));

        Assert.Empty(Discovery(catalog).FindNew());
    }

    [Fact]
    public void PlacingInAProfile_AddsALinkedStep_SwitchesItOff_AndRecordsIt()
    {
        _seen.Keys = [];
        var catalog = new FakeStartupAppCatalog(Entry("Spotify"));
        var discovery = Discovery(catalog);
        var entry = Assert.Single(discovery.FindNew());

        Assert.True(discovery.Place(entry, StartupPlacement.Profile("chill")));

        var step = Assert.Single(_profiles.Find("chill")!.Actions);
        Assert.NotNull(step.LibraryItemId);
        Assert.Equal(@"C:\Apps\Spotify.exe", _library.Find(step.LibraryItemId!)!.Target);
        Assert.False(catalog.Get("Spotify").IsEnabled);
        Assert.Equal(["Spotify"], _record.Entries.Select(e => e.Key));
        Assert.Empty(discovery.FindNew());
    }

    [Fact]
    public void PlacingInTheBase_AddsItThereOnce()
    {
        _seen.Keys = [];
        var catalog = new FakeStartupAppCatalog(Entry("Spotify"));
        var discovery = Discovery(catalog);
        var entry = catalog.Get("Spotify");

        discovery.Place(entry, StartupPlacement.Base);
        discovery.Place(entry, StartupPlacement.Base);

        Assert.Single(_base.Load().Actions);
    }

    [Fact]
    public void LibraryOnly_KeepsIt_AndLeavesWindowsAlone()
    {
        _seen.Keys = [];
        var catalog = new FakeStartupAppCatalog(Entry("Spotify"));
        var discovery = Discovery(catalog);

        discovery.Place(catalog.Get("Spotify"), StartupPlacement.LibraryOnly);

        Assert.Contains(_library.GetAll(), i => i.Name == "Spotify");
        Assert.True(catalog.Get("Spotify").IsEnabled);
        Assert.Empty(_record.Entries);
        Assert.Empty(discovery.FindNew());
    }

    [Fact]
    public void LeavingItToWindows_ChangesNothing_ButIsNotAskedAgain()
    {
        _seen.Keys = [];
        var catalog = new FakeStartupAppCatalog(Entry("Spotify"));
        var discovery = Discovery(catalog);

        discovery.Place(catalog.Get("Spotify"), StartupPlacement.LeaveInWindows);

        Assert.Empty(_library.GetAll());
        Assert.True(catalog.Get("Spotify").IsEnabled);
        Assert.Empty(discovery.FindNew());
    }

    [Fact]
    public void WhenWindowsRefuses_PlaceSaysSo_AndTheStepIsStillAdded()
    {
        _seen.Keys = [];
        var catalog = new FakeStartupAppCatalog(Entry("Spotify"));
        catalog.FailingKeys.Add("Spotify");

        var switchedOff = Discovery(catalog).Place(catalog.Get("Spotify"), StartupPlacement.Profile("chill"));

        Assert.False(switchedOff);
        Assert.Single(_profiles.Find("chill")!.Actions);
        Assert.True(catalog.Get("Spotify").IsEnabled);
    }

    [Fact]
    public void TheSuggestion_FollowsWhetherStartupAppsWereTakenOver()
    {
        var catalog = new FakeStartupAppCatalog(Entry("Steam"));
        var discovery = Discovery(catalog);

        Assert.Equal(StartupPlacement.LeaveInWindows, discovery.SuggestedPlacement);

        new StartupTakeover(catalog, _profiles, _record, OwnValueName).TakeOver();

        Assert.Equal(StartupPlacement.Profile(DefaultProfiles.EverythingId), discovery.SuggestedPlacement);
    }
}
