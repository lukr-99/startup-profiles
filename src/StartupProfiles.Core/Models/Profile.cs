namespace StartupProfiles.Core.Models;

/// <summary>
/// A named startup context (Work, Dev, Games, ...). A profile is declarative data - an ordered list of
/// <see cref="ProfileAction"/> the runner executes - not hardcoded logic. Persisted as JSON.
/// </summary>
public sealed record Profile
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Icon { get; init; }
    public IReadOnlyList<ProfileAction> Actions { get; init; } = [];
    public StartupBehaviour StartupBehaviour { get; init; } = StartupBehaviour.Default;
    public IReadOnlyList<ProfileCondition> Conditions { get; init; } = [];
}
