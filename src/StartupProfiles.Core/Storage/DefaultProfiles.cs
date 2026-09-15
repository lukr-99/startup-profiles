using StartupProfiles.Core.Models;

namespace StartupProfiles.Core.Storage;

/// <summary>The starter set of contexts a fresh install shows before the user edits anything.</summary>
public static class DefaultProfiles
{
    /// <summary>The "start all startup apps" profile the install-time startup takeover fills.</summary>
    public const string EverythingId = "everything";

    public static IReadOnlyList<Profile> Create() =>
    [
        Named("work", "Work", "💼"),
        Named("dev", "Dev", "💻"),
        Named("school", "School", "🎓"),
        Named("games", "Games", "🎮"),
        Named("chill", "Chill", "🎧"),
        Everything(),
    ];

    public static Profile Everything() => Named(EverythingId, "Everything", "✨");

    private static Profile Named(string id, string name, string icon) => new()
    {
        Id = id,
        Name = name,
        Icon = icon,
    };
}
