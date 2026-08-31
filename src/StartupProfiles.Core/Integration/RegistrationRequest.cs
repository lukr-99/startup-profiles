namespace StartupProfiles.Core.Integration;

/// <summary>
/// A request from an external application to be added to a startup profile (see docs/INTEGRATION.md).
/// This is the metadata half of the contract only: the requesting app supplies it, but Startup Profiles
/// owns the decision - the user always chooses the target profile(s) in a Startup Profiles-owned window.
/// A <see cref="SuggestedProfile"/> is a hint, never authority.
/// </summary>
public sealed record RegistrationRequest
{
    /// <summary>Stable unique id for the requesting application (e.g. "com.example.myapp").</summary>
    public required string AppId { get; init; }

    /// <summary>Display name shown in the confirmation window.</summary>
    public required string Name { get; init; }

    /// <summary>Executable path / launch target a launch action would run.</summary>
    public required string Target { get; init; }

    /// <summary>Optional default launch arguments.</summary>
    public string? Arguments { get; init; }

    /// <summary>Optional icon reference shown for recognition.</summary>
    public string? Icon { get; init; }

    /// <summary>Optional publisher, shown for trust.</summary>
    public string? Publisher { get; init; }

    /// <summary>Optional profile hint (e.g. "dev"). The user decides; this only pre-highlights a choice.</summary>
    public string? SuggestedProfile { get; init; }

    /// <summary>Capability flag: the app can start minimized. Metadata only for now (not yet an action field).</summary>
    public bool SupportsMinimized { get; init; }
}
