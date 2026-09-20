namespace StartupProfiles.Core.Integration;

/// <summary>
/// Applies an approved <see cref="RegistrationRequest"/> to the destinations the user chose, by keeping the
/// requesting app in the global library and adding a launch action linked to it. The choice of destinations
/// is an input - this type never decides where an app belongs, and never creates or removes profiles.
/// </summary>
public interface IProfileRegistrar
{
    /// <summary>
    /// Keeps the app in the library, then adds a launch action for it to each destination in
    /// <paramref name="targets"/> that does not already start it.
    /// </summary>
    RegistrationOutcome Apply(RegistrationRequest request, RegistrationTargets targets);
}
