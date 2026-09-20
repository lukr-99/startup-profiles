using StartupProfiles.App.Mvvm;
using StartupProfiles.App.Ui;

namespace StartupProfiles.App.Integration;

/// <summary>
/// One tickable destination in the registration window: a profile, or the base pinned above them
/// (<see cref="IsBase"/>). Carries what the row shows - icon, name, and how much already starts there -
/// plus whether the user ticked it.
/// </summary>
public sealed class RegistrationChoice : ObservableObject
{
    private bool _isSelected;

    private RegistrationChoice(string id, string name, string glyph, string detail, bool isBase)
    {
        Id = id;
        Name = name;
        Glyph = glyph;
        Detail = detail;
        IsBase = isBase;
    }

    /// <summary>A profile row; <paramref name="actionCount"/> is what that profile already starts.</summary>
    public static RegistrationChoice ForProfile(string id, string name, string? icon, int actionCount) =>
        new(id, name, ProfileGlyph.For(icon, name), Describe(actionCount), isBase: false);

    /// <summary>The base row, shown first because it feeds every profile that includes it.</summary>
    public static RegistrationChoice ForBase(int actionCount) =>
        new("base", "Base", ProfileGlyph.For(null, "Base"), $"Starts with every profile - {Describe(actionCount)}", isBase: true);

    public string Id { get; }
    public string Name { get; }

    /// <summary>The profile's emoji icon, or the initial of its name (see <see cref="ProfileGlyph"/>).</summary>
    public string Glyph { get; }

    /// <summary>The muted second line: what the destination already starts.</summary>
    public string Detail { get; }

    /// <summary>True for the base, which is not a profile and is set apart in the list.</summary>
    public bool IsBase { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private static string Describe(int actionCount) => actionCount switch
    {
        0 => "nothing yet",
        1 => "1 item",
        _ => $"{actionCount} items",
    };
}
