namespace StartupProfiles.Core.Models;

/// <summary>
/// A stored, not-yet-evaluated gate on a profile. Placeholder for the conditions feature; the value's
/// meaning depends on <see cref="Type"/> once evaluation is implemented.
/// </summary>
public sealed class ProfileCondition
{
    public required ConditionType Type { get; init; }
    public string? Value { get; init; }
}
