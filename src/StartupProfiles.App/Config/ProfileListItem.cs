using StartupProfiles.App.Mvvm;

namespace StartupProfiles.App.Config;

/// <summary>A profile row in the config sidebar; name and count update in place after edits.</summary>
public sealed class ProfileListItem : ObservableObject
{
    private string _name;
    private int _actionCount;

    public ProfileListItem(string id, string name, int actionCount)
    {
        Id = id;
        _name = name;
        _actionCount = actionCount;
    }

    public string Id { get; }
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public int ActionCount { get => _actionCount; set => SetProperty(ref _actionCount, value); }
}
