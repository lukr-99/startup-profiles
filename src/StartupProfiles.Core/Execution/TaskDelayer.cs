namespace StartupProfiles.Core.Execution;

/// <summary>Real <see cref="IDelayer"/> backed by <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.</summary>
public sealed class TaskDelayer : IDelayer
{
    public Task DelayAsync(TimeSpan delay, CancellationToken ct = default)
        => delay <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(delay, ct);
}
