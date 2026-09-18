using StartupProfiles.App.Mvvm;

namespace StartupProfiles.App.Config;

/// <summary>
/// A row in the config sidebar - a profile, or the base pinned above them (<see cref="IsBase"/>); name and
/// count update in place after edits.
/// </summary>
public sealed class ProfileListItem : ObservableObject
{
    private string _name;
    private int _actionCount;
    private bool _isDropTarget;

    public ProfileListItem(string id, string name, int actionCount, bool isBase = false)
    {
        Id = id;
        _name = name;
        _actionCount = actionCount;
        IsBase = isBase;
    }

    public string Id { get; }
    public bool IsBase { get; }
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public int ActionCount { get => _actionCount; set => SetProperty(ref _actionCount, value); }

    /// <summary>True while something is dragged over the row (the window highlights it as a drop target).</summary>
    public bool IsDropTarget { get => _isDropTarget; set => SetProperty(ref _isDropTarget, value); }
}
