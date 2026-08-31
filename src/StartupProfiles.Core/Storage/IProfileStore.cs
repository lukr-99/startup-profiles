using StartupProfiles.Core.Models;

namespace StartupProfiles.Core.Storage;

/// <summary>Persistence for the user's <see cref="Profile"/> list (profiles.json).</summary>
public interface IProfileStore
{
    IReadOnlyList<Profile> GetAll();
    Profile? Find(string id);

    /// <summary>Inserts the profile, or replaces the existing one with the same <see cref="Profile.Id"/>.</summary>
    void Save(Profile profile);

    void Remove(string id);
}
