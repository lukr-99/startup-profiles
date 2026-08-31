namespace StartupProfiles.Core.Execution;

/// <summary>Time-delay seam so the runner's waits are deterministic in tests (rule: isolate the clock).</summary>
public interface IDelayer
{
    Task DelayAsync(TimeSpan delay, CancellationToken ct = default);
}
