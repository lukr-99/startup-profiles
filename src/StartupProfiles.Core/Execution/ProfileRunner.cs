using StartupProfiles.Core.Actions;
using StartupProfiles.Core.Models;

namespace StartupProfiles.Core.Execution;

/// <summary>
/// Walks a profile's actions in order: applies each action's lead <see cref="ProfileAction.Delay"/>,
/// dispatches it to its handler, applies <see cref="FailureBehaviour"/> (continue / stop / retry), and
/// records a per-action history entry. Pure orchestration - all I/O is behind injected seams.
/// </summary>
public sealed class ProfileRunner
{
    private readonly ActionHandlerRegistry _handlers;
    private readonly IDelayer _delayer;
    private readonly TimeProvider _time;

    public ProfileRunner(ActionHandlerRegistry handlers, IDelayer delayer, TimeProvider? time = null)
    {
        _handlers = handlers;
        _delayer = delayer;
        _time = time ?? TimeProvider.System;
    }

    public async Task<ProfileRun> RunAsync(Profile profile, CancellationToken ct = default)
    {
        var startedAt = _time.GetUtcNow();
        var startTimestamp = _time.GetTimestamp();
        var executed = new List<ActionExecution>(profile.Actions.Count);

        for (var i = 0; i < profile.Actions.Count; i++)
        {
            var action = profile.Actions[i];
            await _delayer.DelayAsync(action.Delay, ct).ConfigureAwait(false);

            var execution = await ExecuteAsync(i, action, ct).ConfigureAwait(false);
            executed.Add(execution);

            if (!execution.Success && action.FailureBehaviour == FailureBehaviour.Stop) break;
        }

        return new ProfileRun
        {
            ProfileId = profile.Id,
            ProfileName = profile.Name,
            StartedAt = startedAt,
            Duration = _time.GetElapsedTime(startTimestamp),
            Actions = executed,
        };
    }

    private async Task<ActionExecution> ExecuteAsync(int index, ProfileAction action, CancellationToken ct)
    {
        var startedAt = _time.GetUtcNow();
        var startTimestamp = _time.GetTimestamp();

        ActionResult result;
        var attempts = 0;

        if (!_handlers.TryGet(action.Type, out var handler))
        {
            result = ActionResult.Fail($"No handler registered for action type '{action.Type}'.");
            attempts = 1;
        }
        else
        {
            var maxAttempts = action.FailureBehaviour == FailureBehaviour.Retry
                ? Math.Max(1, action.RetryCount + 1)
                : 1;
            do
            {
                attempts++;
                result = await handler.ExecuteAsync(action, ct).ConfigureAwait(false);
            }
            while (!result.Success && attempts < maxAttempts);
        }

        return new ActionExecution
        {
            Index = index,
            Type = action.Type,
            Target = action.Target,
            Success = result.Success,
            Output = result.Output,
            Error = result.Error,
            Attempts = attempts,
            StartedAt = startedAt,
            Duration = _time.GetElapsedTime(startTimestamp),
        };
    }
}
