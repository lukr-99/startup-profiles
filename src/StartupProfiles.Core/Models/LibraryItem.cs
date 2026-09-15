namespace StartupProfiles.Core.Models;

/// <summary>
/// Something startable kept once in the global library - an app, URL, folder, file, or script. Profile and base
/// actions link to it by <see cref="Id"/> (<see cref="ProfileAction.LibraryItemId"/>), so changing it here changes
/// every profile that uses it. Persisted in library.json.
/// </summary>
public sealed record LibraryItem
{
    /// <summary>Assigned by the library when the item is added (empty until then).</summary>
    public string Id { get; init; } = "";
    public required string Name { get; init; }
    public ActionType Type { get; init; } = ActionType.LaunchApp;
    public string Target { get; init; } = "";
    public string? Arguments { get; init; }
    public bool RunAsAdmin { get; init; }

    /// <summary>
    /// <paramref name="action"/> linked to this item: what to start comes from the item, how to run it (delay,
    /// failure behaviour, retries) stays with the action.
    /// </summary>
    public ProfileAction ApplyTo(ProfileAction action) => action with
    {
        LibraryItemId = Id,
        Type = Type,
        Target = Target,
        Arguments = Arguments,
        RunAsAdmin = RunAsAdmin,
    };

    /// <summary>True if <paramref name="action"/> starts the same thing (type, target, arguments), linked or not.</summary>
    public bool Starts(ProfileAction action) =>
        Type == action.Type &&
        string.Equals(Target, action.Target, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Arguments ?? "", action.Arguments ?? "", StringComparison.OrdinalIgnoreCase);
}
