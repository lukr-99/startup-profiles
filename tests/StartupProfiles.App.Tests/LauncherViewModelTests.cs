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

    private static LauncherViewModel Open(AppTestServices services, bool atLogin = false, int hour = 9) =>
        new(services.Profiles, services.Executor, new HistoryStore(services.HistoryFile), () => new DateTime(2026, 10, 2, hour, 0, 0), atLogin);

    private static async Task RecordRunAsync(AppTestServices services, string profileId) =>
        await services.Executor.RunAndRecordAsync(services.Profiles.Find(profileId)!);

    [Theory]
    [InlineData(8, "Good morning")]
    [InlineData(14, "Good afternoon")]
    [InlineData(20, "Good evening")]
    [InlineData(2, "Hello")]
    public void Greeting_FollowsTheTimeOfDay(int hour, string greeting)
    {
        using var services = AppTestServices.Create();
        Assert.Equal(greeting, Open(services, hour: hour).Greeting);
    }

    [Fact]
    public void Tiles_NumberTheFirstNine_AndCountSteps()
    {
        using var services = AppTestServices.Create();
        var viewModel = Open(services);

        Assert.Equal(1, viewModel.Profiles[0].Number);
        Assert.Equal(viewModel.Profiles.Count, viewModel.Profiles[^1].Number);
        Assert.All(viewModel.Profiles, t => Assert.False(string.IsNullOrEmpty(t.CountText)));
    }

    [Fact]
    public async Task TheLastProfileRun_IsMarked_AndPreSelected()
    {
        using var services = AppTestServices.Create();
        await RecordRunAsync(services, "games");
        await services.Executor.RunBaseAndRecordAsync(); // a base run is not a profile choice

        var viewModel = Open(services);

        Assert.Equal("games", viewModel.Selected!.Id);
        Assert.True(viewModel.Profiles.Single(t => t.IsLast).Id == "games");
        Assert.False(viewModel.IsCountingDown);
    }

    [Fact]
    public void WithNoHistory_TheFirstProfileIsSelected()
    {
        using var services = AppTestServices.Create();
        var viewModel = Open(services);

        Assert.Same(viewModel.Profiles[0], viewModel.Selected);
        Assert.DoesNotContain(viewModel.Profiles, t => t.IsLast);
    }

    [Fact]
    public void AtLogin_AnAlwaysStartProfile_CountsDown_AndStartsAtZero()
    {
        using var services = AppTestServices.Create();
        services.Profiles.Save(services.Profiles.Find("work")! with { StartupBehaviour = StartupBehaviour.AutoSelectAfterTimeout });
        var viewModel = Open(services, atLogin: true);
        var closed = false;
        viewModel.CloseRequested += () => closed = true;

        Assert.Equal("work", viewModel.AutoStart!.Id);
        Assert.Equal(LauncherViewModel.CountdownSeconds, viewModel.SecondsLeft);
        Assert.Contains("Work", viewModel.CountdownText, StringComparison.Ordinal);

        for (var i = 0; i < LauncherViewModel.CountdownSeconds; i++) viewModel.Tick();

        Assert.False(viewModel.IsCountingDown);
        Assert.True(closed);
        Assert.Contains(new HistoryStore(services.HistoryFile).GetRecent(), r => r.ProfileId == "work");
    }

    [Fact]
    public void Cancelling_StopsTheCountdown_AndNothingStarts()
    {
        using var services = AppTestServices.Create();
        services.Profiles.Save(services.Profiles.Find("work")! with { StartupBehaviour = StartupBehaviour.AutoSelectAfterTimeout });
        var viewModel = Open(services, atLogin: true);

        viewModel.CancelCountdown();
        for (var i = 0; i < LauncherViewModel.CountdownSeconds; i++) viewModel.Tick();

        Assert.False(viewModel.IsCountingDown);
        Assert.Empty(new HistoryStore(services.HistoryFile).GetRecent());
    }

    [Fact]
    public void ReopenedLater_NothingCountsDown()
    {
        using var services = AppTestServices.Create();
        services.Profiles.Save(services.Profiles.Find("work")! with { StartupBehaviour = StartupBehaviour.AutoSelectAfterTimeout });

        Assert.False(Open(services, atLogin: false).IsCountingDown);
    }

    [Fact]
    public async Task StartIfUsedLast_CountsDown_OnlyWhenItRanLast()
    {
        using var services = AppTestServices.Create();
        services.Profiles.Save(services.Profiles.Find("games")! with { StartupBehaviour = StartupBehaviour.RememberLast });

        Assert.False(Open(services, atLogin: true).IsCountingDown);

        await RecordRunAsync(services, "games");
        var viewModel = Open(services, atLogin: true);

        Assert.Equal("games", viewModel.AutoStart!.Id);
        Assert.Same(viewModel.AutoStart, viewModel.Selected);
    }
}
