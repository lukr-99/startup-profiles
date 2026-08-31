using StartupProfiles.Core.Models;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.Core.Integration;

/// <summary>
/// <see cref="IProfileStore"/>-backed <see cref="IProfileRegistrar"/>. Appends a <see cref="ActionType.LaunchApp"/>
/// action for the requesting app to each chosen profile, skipping any profile that already launches the
/// same target (so registering twice is idempotent). Unknown profile ids are reported, not created.
/// </summary>
public sealed class ProfileRegistrar : IProfileRegistrar
{
    private readonly IProfileStore _profiles;

    public ProfileRegistrar(IProfileStore profiles) => _profiles = profiles;

    public RegistrationOutcome Apply(RegistrationRequest request, IReadOnlyCollection<string> profileIds)
    {
        var addedTo = new List<string>();
        var alreadyPresent = new List<string>();
        var unknown = new List<string>();

        foreach (var id in profileIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (_profiles.Find(id) is not { } profile)
            {
                unknown.Add(id);
                continue;
            }

            if (LaunchesTarget(profile, request.Target))
            {
                alreadyPresent.Add(profile.Id);
                continue;
            }

            _profiles.Save(profile with { Actions = [.. profile.Actions, LaunchActionFor(request)] });
            addedTo.Add(profile.Id);
        }

        return new RegistrationOutcome(addedTo, alreadyPresent, unknown);
    }

    private static bool LaunchesTarget(Profile profile, string target) =>
        profile.Actions.Any(a => a.Type == ActionType.LaunchApp &&
            string.Equals(a.Target, target, StringComparison.OrdinalIgnoreCase));

    private static ProfileAction LaunchActionFor(RegistrationRequest request) => new()
    {
        Type = ActionType.LaunchApp,
        Target = request.Target,
        Arguments = request.Arguments,
    };
}
