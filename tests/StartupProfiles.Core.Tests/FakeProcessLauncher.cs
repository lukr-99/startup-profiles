using StartupProfiles.Core.Actions;

namespace StartupProfiles.Core.Tests;

/// <summary>Deterministic <see cref="IProcessLauncher"/> for tests: records calls, returns queued results.</summary>
internal sealed class FakeProcessLauncher : IProcessLauncher
{
    private readonly Queue<ActionResult> _startResults = new();

    public List<ProcessLaunchSpec> Started { get; } = [];
    public List<string> Killed { get; } = [];

    public void EnqueueStartResult(ActionResult result) => _startResults.Enqueue(result);

    public ActionResult Start(ProcessLaunchSpec spec)
    {
        Started.Add(spec);
        return _startResults.Count > 0 ? _startResults.Dequeue() : ActionResult.Ok($"started {spec.FileName}");
    }

    public ActionResult KillByName(string processName)
    {
        Killed.Add(processName);
        return ActionResult.Ok($"killed {processName}");
    }
}
