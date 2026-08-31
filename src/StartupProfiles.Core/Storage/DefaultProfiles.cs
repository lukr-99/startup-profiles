using StartupProfiles.Core.Models;

namespace StartupProfiles.Core.Storage;

/// <summary>The starter set of contexts a fresh install shows before the user edits anything.</summary>
public static class DefaultProfiles
{
    public static IReadOnlyList<Profile> Create() =>
    [
        Named("work", "Work"),
        Named("dev", "Dev"),
        Named("school", "School"),
        Named("games", "Games"),
        Named("chill", "Chill"),
        Named("everything", "Everything"),
    ];

    private static Profile Named(string id, string name) => new()
    {
        Id = id,
        Name = name,
        Icon = id,
    };
}
