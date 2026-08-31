using StartupProfiles.Core.Models;

namespace StartupProfiles.Core.Execution;

/// <summary>History record for one executed action within a <see cref="ProfileRun"/>.</summary>
public sealed record ActionExecution
{
    public required int Index { get; init; }
    public required ActionType Type { get; init; }
    public required string Target { get; init; }
    public required bool Success { get; init; }
    public string? Output { get; init; }
    public string? Error { get; init; }
    public required int Attempts { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required TimeSpan Duration { get; init; }
}
