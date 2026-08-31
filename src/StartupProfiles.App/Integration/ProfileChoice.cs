using StartupProfiles.App.Mvvm;

namespace StartupProfiles.App.Integration;

/// <summary>One selectable profile in the registration window: its id/name and whether the user ticked it.</summary>
public sealed class ProfileChoice : ObservableObject
{
    private bool _isSelected;

    public ProfileChoice(string id, string name)
    {
        Id = id;
        Name = name;
    }

    public string Id { get; }
    public string Name { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
