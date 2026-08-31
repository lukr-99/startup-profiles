namespace StartupProfiles.Core.Integration;

/// <summary>
/// Applies an approved <see cref="RegistrationRequest"/> to the profiles the user chose, by adding a
/// launch action for the requesting app. The choice of profiles is an input - this type never decides
/// where an app belongs, and never creates or removes profiles.
/// </summary>
public interface IProfileRegistrar
{
    /// <summary>
    /// Adds a launch action for <paramref name="request"/> to each existing profile in
    /// <paramref name="profileIds"/> that does not already launch that target.
    /// </summary>
    RegistrationOutcome Apply(RegistrationRequest request, IReadOnlyCollection<string> profileIds);
}
