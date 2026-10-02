using StartupProfiles.App.Discovery;
using StartupProfiles.Core.Models;
using StartupProfiles.Core.Startup;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.App.Tests;

public sealed class NewStartupAppsViewModelTests : IDisposable
{
    private readonly string _seenFile = Path.Combine(Path.GetTempPath(), $"sp-seen-{Guid.NewGuid():N}.json");
    private readonly string _takeoverFile = Path.Combine(Path.GetTempPath(), $"sp-take-{Guid.NewGuid():N}.json");
    private readonly AppTestServices _services = AppTestServices.Create();

    public void Dispose()
    {
        File.Delete(_seenFile);
        File.Delete(_takeoverFile);
        _services.Dispose();
    }

    private static StartupEntry Entry(string key) => new()
    {
        Source = StartupEntrySource.UserRunKey,
        Key = key,
        Name = key,
        IsEnabled = true,
        CanToggle = true,
        Launch = new ProfileAction { Type = ActionType.LaunchApp, Target = $@"C:\Apps\{key}.exe" },
    };

    private (NewStartupAppsViewModel ViewModel, StartupDiscovery Discovery) Open(bool tookOver = false)
    {
        var catalog = _services.StartupApps;
        var takeover = new StartupTakeover(catalog, _services.Profiles, new StartupTakeoverStore(_takeoverFile), "StartupProfiles");
        if (tookOver) takeover.SwitchOff(Entry("Old"));

        var discovery = new StartupDiscovery(catalog, new StartupSeenStore(_seenFile), takeover, _services.Profiles,
            _services.Base, _services.Library, "StartupProfiles");
        discovery.FindNew(); // the first look only records what is there
        catalog.Entries.Add(Entry("Spotify"));
        catalog.Entries.Add(Entry("Zoom"));

        return (new NewStartupAppsViewModel(discovery.FindNew(), _services.Profiles.GetAll(), discovery), discovery);
    }

    [Fact]
    public void ListsTheNewApps_WithEveryProfileAndTheOtherChoices()
    {
        var (viewModel, _) = Open();

        Assert.Equal(["Spotify", "Zoom"], viewModel.Apps.Select(a => a.App.Name));
        Assert.Equal("2 apps now start with Windows", viewModel.Heading);
        var labels = viewModel.Apps[0].Options.Select(o => o.Label).ToList();
        Assert.Contains("Start with Work", labels);
        Assert.Contains("Start with Base (every profile)", labels);
        Assert.Contains("Keep in Created for later", labels);
        Assert.Equal("Leave it to Windows", labels[^1]);
    }

    [Fact]
    public void Defaults_ToLeavingItToWindows_UnlessStartupAppsWereTakenOver()
    {
        Assert.Equal("Leave it to Windows", Open().ViewModel.Apps[0].Selected.Label);
    }

    [Fact]
    public void Defaults_ToEverything_AfterATakeover()
    {
        Assert.Equal("Start with Everything", Open(tookOver: true).ViewModel.Apps[0].Selected.Label);
    }

    [Fact]
    public void Apply_PlacesEachApp_AsChosen_AndCloses()
    {
        var (viewModel, discovery) = Open();
        var closed = false;
        viewModel.CloseRequested += () => closed = true;
        viewModel.Apps[0].Selected = viewModel.Apps[0].Options.First(o => o.Label == "Start with Games");

        viewModel.ApplyCommand.Execute(null);

        Assert.True(closed);
        Assert.Single(_services.Profiles.Find("games")!.Actions);
        Assert.False(_services.StartupApps.Entries.Single(e => e.Key == "Spotify").IsEnabled);
        Assert.True(_services.StartupApps.Entries.Single(e => e.Key == "Zoom").IsEnabled);
        Assert.Empty(discovery.FindNew());
    }

    [Fact]
    public void AskMeLater_DecidesNothing_SoTheyAreOfferedAgain()
    {
        var (viewModel, discovery) = Open();

        viewModel.LaterCommand.Execute(null);

        Assert.Equal(2, discovery.FindNew().Count);
    }
}
