using StartupProfiles.Core.Actions;
using StartupProfiles.Core.Execution;
using StartupProfiles.Core.Models;

namespace StartupProfiles.Core.Tests;

public sealed class ProfileRunnerTests
{
    private readonly FakeProcessLauncher _launcher = new();
    private readonly RecordingDelayer _delayer = new();

    private ProfileRunner CreateRunner() =>
        new(ActionHandlerRegistry.CreateDefault(_launcher), _delayer, TimeProvider.System);

    private static Profile ProfileWith(params ProfileAction[] actions) =>
        new() { Id = "p", Name = "Test", Actions = actions };

    [Fact]
    public async Task RunAsync_RunsActionsInOrder_RecordsHistoryPerAction()
    {
        var profile = ProfileWith(
            new ProfileAction { Type = ActionType.LaunchApp, Target = "one.exe" },
            new ProfileAction { Type = ActionType.LaunchApp, Target = "two.exe" });

        var run = await CreateRunner().RunAsync(profile);

        Assert.Equal(2, run.Actions.Count);
        Assert.Equal(0, run.Actions[0].Index);
        Assert.Equal("one.exe", run.Actions[0].Target);
        Assert.Equal("two.exe", run.Actions[1].Target);
        Assert.True(run.Succeeded);
        Assert.Equal(["one.exe", "two.exe"], _launcher.Started.Select(s => s.FileName));
    }

    [Fact]
    public async Task RunAsync_StopBehaviour_HaltsAfterFailure()
    {
        _launcher.EnqueueStartResult(ActionResult.Fail("boom"));
        var profile = ProfileWith(
            new ProfileAction { Type = ActionType.LaunchApp, Target = "one.exe", FailureBehaviour = FailureBehaviour.Stop },
            new ProfileAction { Type = ActionType.LaunchApp, Target = "two.exe" });

        var run = await CreateRunner().RunAsync(profile);

        Assert.Single(run.Actions);
        Assert.False(run.Actions[0].Success);
        Assert.False(run.Succeeded);
        Assert.Single(_launcher.Started);
    }

    [Fact]
    public async Task RunAsync_ContinueBehaviour_RunsRemainingAfterFailure()
    {
        _launcher.EnqueueStartResult(ActionResult.Fail("boom"));
        var profile = ProfileWith(
            new ProfileAction { Type = ActionType.LaunchApp, Target = "one.exe", FailureBehaviour = FailureBehaviour.Continue },
            new ProfileAction { Type = ActionType.LaunchApp, Target = "two.exe" });

        var run = await CreateRunner().RunAsync(profile);

        Assert.Equal(2, run.Actions.Count);
        Assert.False(run.Actions[0].Success);
        Assert.True(run.Actions[1].Success);
    }

    [Fact]
    public async Task RunAsync_RetryBehaviour_RetriesUntilSuccess()
    {
        _launcher.EnqueueStartResult(ActionResult.Fail("first"));
        var profile = ProfileWith(new ProfileAction
        {
            Type = ActionType.LaunchApp,
            Target = "one.exe",
            FailureBehaviour = FailureBehaviour.Retry,
            RetryCount = 1,
        });

        var run = await CreateRunner().RunAsync(profile);

        Assert.True(run.Actions[0].Success);
        Assert.Equal(2, run.Actions[0].Attempts);
    }

    [Fact]
    public async Task RunAsync_AppliesLeadDelayBeforeEachAction()
    {
        var profile = ProfileWith(
            new ProfileAction { Type = ActionType.LaunchApp, Target = "one.exe", Delay = TimeSpan.FromSeconds(2) },
            new ProfileAction { Type = ActionType.Delay, Delay = TimeSpan.FromSeconds(5) });

        await CreateRunner().RunAsync(profile);

        Assert.Equal([TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5)], _delayer.Delays);
    }

    [Fact]
    public async Task RunAsync_UnknownHandler_RecordsFailure()
    {
        // StartService is deferred and has no handler in the default registry.
        var profile = ProfileWith(new ProfileAction { Type = ActionType.StartService, Target = "Spooler" });

        var run = await CreateRunner().RunAsync(profile);

        Assert.False(run.Actions[0].Success);
        Assert.Contains("No handler", run.Actions[0].Error);
    }
}
