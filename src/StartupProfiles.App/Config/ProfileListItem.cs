using DotNetLib.Core.Mvvm;
using StartupProfiles.App.Ui;

namespace StartupProfiles.App.Config;

/// <summary>
/// A row in the config sidebar - a profile, or the base pinned above them (<see cref="IsBase"/>); name, icon,
/// and count update in place after edits.
/// </summary>
public sealed class ProfileListItem : ObservableObject
{
    private string _name;
    private string? _icon;
    private int _actionCount;
    private bool _isDropTarget;

    public ProfileListItem(string id, string name, int actionCount, bool isBase = false, string? icon = null)
    {
        Id = id;
        _name = name;
        _icon = icon;
        _actionCount = actionCount;
        IsBase = isBase;
    }

    public string Id { get; }
    public bool IsBase { get; }

    public string Name
    {
        get => _name;
        set { if (SetProperty(ref _name, value)) OnPropertyChanged(nameof(Glyph)); }
    }

    public string? Icon
    {
        get => _icon;
        set { if (SetProperty(ref _icon, value)) OnPropertyChanged(nameof(Glyph)); }
    }

    /// <summary>The profile's emoji or initial; the base shows a stack glyph instead (see the sidebar template).</summary>
    public string Glyph => ProfileGlyph.For(Icon, Name);

    public int ActionCount
    {
        get => _actionCount;
        set { if (SetProperty(ref _actionCount, value)) OnPropertyChanged(nameof(CountText)); }
    }

    /// <summary>"Nothing yet", "1 step", "6 steps".</summary>
    public string CountText => ActionCount switch
    {
        0 => "Nothing yet",
        1 => "1 step",
        var n => $"{n} steps",
    };

    /// <summary>True while something is dragged over the row (the window highlights it as a drop target).</summary>
    public bool IsDropTarget { get => _isDropTarget; set => SetProperty(ref _isDropTarget, value); }
}
