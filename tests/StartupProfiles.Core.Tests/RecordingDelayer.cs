using StartupProfiles.Core.Execution;

namespace StartupProfiles.Core.Tests;

/// <summary>Records requested delays without waiting, keeping runner tests fast and deterministic.</summary>
internal sealed class RecordingDelayer : IDelayer
{
    public List<TimeSpan> Delays { get; } = [];

    public Task DelayAsync(TimeSpan delay, CancellationToken ct = default)
    {
        Delays.Add(delay);
        return Task.CompletedTask;
    }
}
