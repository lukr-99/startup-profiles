using StartupProfiles.Core.Models;
using StartupProfiles.Core.Windows;

namespace StartupProfiles.Core.Actions;

/// <summary>Starts the Windows service named by the action's target.</summary>
public sealed class StartServiceHandler : IActionHandler
{
    private readonly IServiceController _services;

    public StartServiceHandler(IServiceController services) => _services = services;

    public ActionType Type => ActionType.StartService;

    public Task<ActionResult> ExecuteAsync(ProfileAction action, CancellationToken ct = default)
        => Task.FromResult(_services.StartService(action.Target));
}
