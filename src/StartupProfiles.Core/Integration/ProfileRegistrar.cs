using StartupProfiles.Core.Library;
using StartupProfiles.Core.Models;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.Core.Integration;

/// <summary>
/// Store-backed <see cref="IProfileRegistrar"/>. Every registration first keeps the requesting app in the
/// global library (reusing the item that already starts the same thing), then appends an action linked to
/// that item to the base and to each chosen profile - skipping any destination that already starts the
/// target, so registering twice is idempotent. Unknown profile ids are reported, not created.
/// </summary>
public sealed class ProfileRegistrar : IProfileRegistrar
{
    private readonly IProfileStore _profiles;
    private readonly IBaseStore _base;
    private readonly LibraryService _library;

    public ProfileRegistrar(IProfileStore profiles, IBaseStore baseStore, LibraryService library)
    {
        _profiles = profiles;
        _base = baseStore;
        _library = library;
    }

    public RegistrationOutcome Apply(RegistrationRequest request, RegistrationTargets targets)
    {
        var launch = LaunchActionFor(request);

        // The library entry comes first, so every action added below links to one item - and so a
        // recognize-only registration still leaves something to drag into a profile later.
        var known = _library.GetAll().Any(i => i.Starts(launch));
        var item = _library.FindOrAdd(request.Name, launch);
        var linked = item.ApplyTo(launch);

        var baseValue = _base.Load();
        var baseStarts = Launches(baseValue.Actions, request.Target);
        var addedToBase = targets.IncludeBase && !baseStarts;
        if (addedToBase)
        {
            _base.Save(baseValue with { Actions = [.. baseValue.Actions, linked] });
            baseStarts = true;
        }

        var addedTo = new List<string>();
        var alreadyPresent = new List<string>();
        var unknown = new List<string>();

        foreach (var id in targets.ProfileIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (_profiles.Find(id) is not { } profile)
            {
                unknown.Add(id);
                continue;
            }

            // A profile that includes the base already starts what the base starts; adding it again there
            // would launch the app twice.
            if (Launches(profile.Actions, request.Target) || (profile.IncludeBase && baseStarts))
            {
                alreadyPresent.Add(profile.Id);
                continue;
            }

            _profiles.Save(profile with { Actions = [.. profile.Actions, linked] });
            addedTo.Add(profile.Id);
        }

        return new RegistrationOutcome(
            addedTo, alreadyPresent, unknown, item.Id, !known, addedToBase,
            AlreadyInBase: targets.IncludeBase && !addedToBase);
    }

    private static bool Launches(IEnumerable<ProfileAction> actions, string target) =>
        actions.Any(a => a.Type == ActionType.LaunchApp &&
            string.Equals(a.Target, target, StringComparison.OrdinalIgnoreCase));

    private static ProfileAction LaunchActionFor(RegistrationRequest request) => new()
    {
        Type = ActionType.LaunchApp,
        Target = request.Target,
        Arguments = request.Arguments,
    };
}
