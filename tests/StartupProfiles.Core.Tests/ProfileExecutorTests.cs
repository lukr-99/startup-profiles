using StartupProfiles.Core.Actions;
using StartupProfiles.Core.Execution;
using StartupProfiles.Core.Models;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.Core.Tests;

public sealed class ProfileExecutorTests : IDisposable
{
    private readonly string _historyFile = Path.Combine(Path.GetTempPath(), $"sp-exh-{Guid.NewGuid():N}.json");
    private readonly string _baseFile = Path.Combine(Path.GetTempPath(), $"sp-exb-{Guid.NewGuid():N}.json");
    private readonly string _libraryFile = Path.Combine(Path.GetTempPath(), $"sp-exl-{Guid.NewGuid():N}.json");
    private readonly FakeProcessLauncher _launcher = new();
    private readonly BaseStore _base;
    private readonly LibraryStore _library;
    private readonly ProfileExecutor _executor;

    public ProfileExecutorTests()
    {
        _base = new BaseStore(_baseFile);
        _library = new LibraryStore(_libraryFile);
        var runner = new ProfileRunner(ActionHandlerRegistry.CreateDefault(_launcher), new RecordingDelayer());
        _executor = new ProfileExecutor(runner, new HistoryStore(_historyFile), _base, _library);
    }

    private static ProfileAction Launch(string target) => new() { Type = ActionType.LaunchApp, Target = target };

    private static Profile Games(bool includeBase = true) => new()
    {
        Id = "games",
        Name = "Games",
        IncludeBase = includeBase,
        Actions = [Launch("steam.exe")],
    };

    [Fact]
    public async Task RunAndRecord_RunsBaseActionsBeforeTheProfile()
    {
        _base.Save(new StartupBase { Actions = [Launch("noise.exe"), Launch("onedrive.exe")] });

        var run = await _executor.RunAndRecordAsync(Games());

        Assert.Equal(["noise.exe", "onedrive.exe", "steam.exe"], _launcher.Started.Select(s => s.FileName));
        Assert.Equal("games", run.ProfileId);
        Assert.Equal(3, run.Actions.Count);
    }

    [Fact]
    public async Task RunAndRecord_SkipsTheBase_WhenTheProfileExcludesIt()
    {
        _base.Save(new StartupBase { Actions = [Launch("noise.exe")] });

        await _executor.RunAndRecordAsync(Games(includeBase: false));

        Assert.Equal(["steam.exe"], _launcher.Started.Select(s => s.FileName));
    }

    [Fact]
    public async Task RunAndRecord_WithEmptyBase_RunsJustTheProfile()
    {
        await _executor.RunAndRecordAsync(Games());

        Assert.Equal(["steam.exe"], _launcher.Started.Select(s => s.FileName));
    }

    [Fact]
    public async Task RunBaseAndRecord_RunsOnlyTheBase_UnderTheBaseId()
    {
        _base.Save(new StartupBase { Actions = [Launch("noise.exe")] });

        var run = await _executor.RunBaseAndRecordAsync();

        Assert.Equal(["noise.exe"], _launcher.Started.Select(s => s.FileName));
        Assert.Equal(ProfileExecutor.BaseRunId, run.ProfileId);
        Assert.Contains(new HistoryStore(_historyFile).GetRecent(), r => r.ProfileId == ProfileExecutor.BaseRunId);
    }

    [Fact]
    public async Task RunAndRecord_LinkedActions_StartTheLibraryItemsCurrentValues_WithTheirOwnRunOptions()
    {
        _library.Save(new LibraryItem { Id = "steam", Name = "Steam", Target = @"D:\Steam\steam.exe", Arguments = "-silent" });
        var profile = new Profile
        {
            Id = "games",
            Name = "Games",
            Actions =
            [
                // Saved before the item moved to D:, with its own delay.
                new ProfileAction { Type = ActionType.LaunchApp, Target = @"C:\Steam\steam.exe", LibraryItemId = "steam", Delay = TimeSpan.FromSeconds(4) },
            ],
        };

        var run = await _executor.RunAndRecordAsync(profile);

        var started = Assert.Single(_launcher.Started);
        Assert.Equal(@"D:\Steam\steam.exe", started.FileName);
        Assert.Equal("-silent", started.Arguments);
        Assert.True(run.Succeeded);
    }

    [Fact]
    public async Task RunAndRecord_LinkToADeletedItem_RunsTheLastSavedCopy()
    {
        var profile = new Profile
        {
            Id = "games",
            Name = "Games",
            Actions = [new ProfileAction { Type = ActionType.LaunchApp, Target = @"C:\Steam\steam.exe", LibraryItemId = "gone" }],
        };

        await _executor.RunAndRecordAsync(profile);

        Assert.Equal(@"C:\Steam\steam.exe", Assert.Single(_launcher.Started).FileName);
    }

    public void Dispose()
    {
        File.Delete(_historyFile);
        File.Delete(_baseFile);
        File.Delete(_libraryFile);
    }
}
