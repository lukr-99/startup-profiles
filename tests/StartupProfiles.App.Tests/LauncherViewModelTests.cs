using StartupProfiles.App.Launcher;
using StartupProfiles.Core.Models;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.App.Tests;

public sealed class LauncherViewModelTests
{
    [Fact]
    public void Load_PopulatesTilesFromStore()
    {
        using var services = AppTestServices.Create();

        var viewModel = new LauncherViewModel(services.Profiles, services.Executor);

        Assert.Equal(6, viewModel.Profiles.Count);
        Assert.Contains(viewModel.Profiles, t => t.Name == "Work");
    }

    [Fact]
    public async Task RunAsync_RunsProfile_RaisesCloseAndRecordsHistory()
    {
        using var services = AppTestServices.Create();
        services.Profiles.Save(new Profile
        {
            Id = "t",
            Name = "T",
            Actions = [new ProfileAction { Type = ActionType.Delay, Delay = TimeSpan.Zero }],
        });

        var viewModel = new LauncherViewModel(services.Profiles, services.Executor);
        var closed = false;
        viewModel.CloseRequested += () => closed = true;

        await viewModel.RunAsync(viewModel.Profiles.First(t => t.Id == "t"));

        Assert.True(closed);
        Assert.Contains(new HistoryStore(services.HistoryFile).GetRecent(), r => r.ProfileId == "t");
    }
}
