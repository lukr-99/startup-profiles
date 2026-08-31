using StartupProfiles.Core.Models;

namespace StartupProfiles.Core.Actions;

/// <summary>
/// An explicit pause step. The wait itself is the action's <see cref="ProfileAction.Delay"/>, which the
/// runner applies before every action, so this handler only records that the step ran.
/// </summary>
public sealed class DelayHandler : IActionHandler
{
    public ActionType Type => ActionType.Delay;

    public Task<ActionResult> ExecuteAsync(ProfileAction action, CancellationToken ct = default)
        => Task.FromResult(ActionResult.Ok($"Waited {action.Delay}."));
}
