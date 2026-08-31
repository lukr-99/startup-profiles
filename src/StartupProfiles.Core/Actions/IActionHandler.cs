using StartupProfiles.Core.Models;

namespace StartupProfiles.Core.Actions;

/// <summary>
/// Executes one <see cref="ActionType"/>. Handlers are registered by <see cref="Type"/> in the
/// <see cref="ActionHandlerRegistry"/>, so adding an action type means adding a handler - the runner
/// and the wire model never change (Relay's provider model is the reference shape).
/// </summary>
public interface IActionHandler
{
    ActionType Type { get; }
    Task<ActionResult> ExecuteAsync(ProfileAction action, CancellationToken ct = default);
}
