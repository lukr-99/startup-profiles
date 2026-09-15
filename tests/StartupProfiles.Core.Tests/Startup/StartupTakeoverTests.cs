using StartupProfiles.Core.Models;
using StartupProfiles.Core.Startup;
using StartupProfiles.Core.Storage;
using StartupProfiles.Core.Tests.Integration;

namespace StartupProfiles.Core.Tests.Startup;

public sealed class StartupTakeoverTests
{
    private const string OwnValueName = "StartupProfiles";

    private readonly FakeProfileStore _profiles = new([.. DefaultProfiles.Create()]);
    private readonly FakeStartupTakeoverStore _record = new();

    private static StartupEntry Entry(
        string key,
        bool enabled = true,
        bool canToggle = true,
        bool launchable = true,
        StartupEntrySource source = StartupEntrySource.UserRunKey) => new()
        {
            Source = source,
            Key = key,
            Name = key,
            IsEnabled = enabled,
            CanToggle = canToggle,
            Launch = launchable ? Launch(key) : null,
        };

    private static ProfileAction Launch(string key) =>
        new() { Type = ActionType.LaunchApp, Target = $@"C:\Apps\{key}.exe", Arguments = "--tray" };

    private StartupTakeover Takeover(FakeStartupAppCatalog catalog) => new(catalog, _profiles, _record, OwnValueName);

    private List<string> EverythingTargets() =>
        _profiles.Find(DefaultProfiles.EverythingId)!.Actions.Select(a => a.Target).ToList();

    [Fact]
    public void TakeOver_MovesEnabledAppsIntoEverything_AndSwitchesThemOff()
    {
        var catalog = new FakeStartupAppCatalog(Entry("Steam"), Entry("Discord"));

        var result = Takeover(catalog).TakeOver();

        Assert.Equal(["Steam", "Discord"], result.Adopted.Select(e => e.Key));
        Assert.Equal([@"C:\Apps\Steam.exe", @"C:\Apps\Discord.exe"], EverythingTargets());
        Assert.False(catalog.Get("Steam").IsEnabled);
        Assert.False(catalog.Get("Discord").IsEnabled);
        Assert.Equal(["Steam", "Discord"], _record.Entries.Select(e => e.Key));
    }

    [Fact]
    public void TakeOver_OnlyTouchesEverything()
    {
        var catalog = new FakeStartupAppCatalog(Entry("Steam"));

        Takeover(catalog).TakeOver();

        Assert.All(_profiles.GetAll().Where(p => p.Id != DefaultProfiles.EverythingId), p => Assert.Empty(p.Actions));
    }

    [Fact]
    public void TakeOver_NeverTouchesOwnEntry()
    {
        var catalog = new FakeStartupAppCatalog(Entry(OwnValueName), Entry("Steam"));

        var result = Takeover(catalog).TakeOver();

        Assert.True(catalog.Get(OwnValueName).IsEnabled);
        Assert.DoesNotContain(result.Adopted, e => e.Key == OwnValueName);
        Assert.DoesNotContain(result.LeftEnabled, e => e.Key == OwnValueName);
        Assert.Equal([@"C:\Apps\Steam.exe"], EverythingTargets());
    }

    [Fact]
    public void TakeOver_IgnoresAppsAlreadyOff()
    {
        var catalog = new FakeStartupAppCatalog(Entry("Spotify", enabled: false));

        var result = Takeover(catalog).TakeOver();

        Assert.Empty(result.Adopted);
        Assert.Empty(result.LeftEnabled);
        Assert.Empty(EverythingTargets());
        Assert.Empty(_record.Entries);
    }

    [Fact]
    public void TakeOver_LeavesAllUsersAndUnlaunchableEntriesOn_AndOutOfEverything()
    {
        var catalog = new FakeStartupAppCatalog(
            Entry("SecurityHealth", canToggle: false, source: StartupEntrySource.MachineRunKey),
            Entry("FeedProvider", launchable: false, source: StartupEntrySource.PackagedTask));

        var result = Takeover(catalog).TakeOver();

        Assert.Equal(["SecurityHealth", "FeedProvider"], result.LeftEnabled.Select(e => e.Key));
        Assert.True(catalog.Get("SecurityHealth").IsEnabled);
        Assert.True(catalog.Get("FeedProvider").IsEnabled);
        Assert.Empty(EverythingTargets());
        Assert.Empty(_record.Entries);
    }

    [Fact]
    public void TakeOver_DoesNotDuplicateAnExistingEverythingAction()
    {
        _profiles.Save(DefaultProfiles.Everything() with
        {
            Actions = [new ProfileAction { Type = ActionType.LaunchApp, Target = @"c:\apps\STEAM.exe", Arguments = "--TRAY" }],
        });
        var catalog = new FakeStartupAppCatalog(Entry("Steam"));

        Takeover(catalog).TakeOver();

        Assert.Single(EverythingTargets());
    }

    [Fact]
    public void TakeOver_RecreatesEverything_WhenTheUserDeletedIt()
    {
        _profiles.Remove(DefaultProfiles.EverythingId);
        var catalog = new FakeStartupAppCatalog(Entry("Steam"));

        Takeover(catalog).TakeOver();

        var everything = _profiles.Find(DefaultProfiles.EverythingId);
        Assert.NotNull(everything);
        Assert.Equal("Everything", everything!.Name);
        Assert.Single(everything.Actions);
    }

    [Fact]
    public void TakeOver_ReportsSwitchFailures_ButKeepsTheAppInEverything()
    {
        var catalog = new FakeStartupAppCatalog(Entry("Steam"), Entry("Locked"));
        catalog.FailingKeys.Add("Locked");

        var result = Takeover(catalog).TakeOver();

        Assert.Equal(["Steam"], result.Adopted.Select(e => e.Key));
        Assert.Equal(["Locked"], result.Failed.Select(e => e.Key));
        Assert.Contains(@"C:\Apps\Locked.exe", EverythingTargets());
        Assert.True(catalog.Get("Locked").IsEnabled);
    }

    [Fact]
    public void TakeOver_RunAgain_AddsNewAppsToTheExistingRecord()
    {
        var catalog = new FakeStartupAppCatalog(Entry("Steam"));
        Takeover(catalog).TakeOver();

        var later = new FakeStartupAppCatalog(Entry("Steam", enabled: false), Entry("Discord"));
        Takeover(later).TakeOver();

        Assert.Equal(["Steam", "Discord"], _record.Entries.Select(e => e.Key));
        Assert.Equal([@"C:\Apps\Steam.exe", @"C:\Apps\Discord.exe"], EverythingTargets());
    }

    [Fact]
    public void Restore_SwitchesRecordedAppsBackOn_AndClearsTheRecord()
    {
        var catalog = new FakeStartupAppCatalog(Entry("Steam"), Entry("Discord"), Entry("Spotify", enabled: false));
        var takeover = Takeover(catalog);
        takeover.TakeOver();

        var restored = takeover.Restore();

        Assert.Equal(["Steam", "Discord"], restored.Select(e => e.Key));
        Assert.True(catalog.Get("Steam").IsEnabled);
        Assert.True(catalog.Get("Discord").IsEnabled);
        Assert.False(catalog.Get("Spotify").IsEnabled);
        Assert.Empty(_record.Entries);
    }

    [Fact]
    public void Restore_KeepsEntriesThatFailedToSwitchBackOn()
    {
        var catalog = new FakeStartupAppCatalog(Entry("Steam"), Entry("Discord"));
        var takeover = Takeover(catalog);
        takeover.TakeOver();
        catalog.FailingKeys.Add("Discord");

        var restored = takeover.Restore();

        Assert.Equal(["Steam"], restored.Select(e => e.Key));
        Assert.Equal(["Discord"], _record.Entries.Select(e => e.Key));
    }
}
