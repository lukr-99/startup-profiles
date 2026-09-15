using StartupProfiles.Core.Actions;
using StartupProfiles.Core.Execution;
using StartupProfiles.Core.Models;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.Core.Tests;

public sealed class ProfileExecutorTests : IDisposable
{
    private readonly string _historyFile = Path.Combine(Path.GetTempPath(), $"sp-exh-{Guid.NewGuid():N}.json");
    private readonly string _baseFile = Path.Combine(Path.GetTempPath(), $"sp-exb-{Guid.NewGuid():N}.json");
    private readonly FakeProcessLauncher _launcher = new();
    private readonly BaseStore _base;
    private readonly ProfileExecutor _executor;

    public ProfileExecutorTests()
    {
        _base = new BaseStore(_baseFile);
        var runner = new ProfileRunner(ActionHandlerRegistry.CreateDefault(_launcher), new RecordingDelayer());
        _executor = new ProfileExecutor(runner, new HistoryStore(_historyFile), _base);
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

    public void Dispose()
    {
        File.Delete(_historyFile);
        File.Delete(_baseFile);
    }
}
