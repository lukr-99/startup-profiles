namespace StartupProfiles.Core.Execution;

/// <summary>History record for one execution of a profile: its per-action results and overall outcome.</summary>
public sealed record ProfileRun
{
    public required string ProfileId { get; init; }
    public required string ProfileName { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required TimeSpan Duration { get; init; }
    public required IReadOnlyList<ActionExecution> Actions { get; init; }

    /// <summary>True when every executed action succeeded. Serialized for API/history consumers.</summary>
    public bool Succeeded => Actions.All(a => a.Success);
}
