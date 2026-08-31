using System.Collections.ObjectModel;
using StartupProfiles.App.Mvvm;
using StartupProfiles.Core.Models;

namespace StartupProfiles.App.Config;

/// <summary>Editable view model for a whole <see cref="Profile"/>.</summary>
public sealed class ProfileEditor : ObservableObject
{
    private string _name = "";
    private string _icon = "";
    private StartupBehaviour _startupBehaviour = StartupBehaviour.Default;

    public required string Id { get; init; }
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string Icon { get => _icon; set => SetProperty(ref _icon, value); }
    public StartupBehaviour StartupBehaviour { get => _startupBehaviour; set => SetProperty(ref _startupBehaviour, value); }
    public ObservableCollection<ActionEditor> Actions { get; } = [];

    public static IReadOnlyList<StartupBehaviour> StartupBehaviours { get; } = Enum.GetValues<StartupBehaviour>();

    public static ProfileEditor FromProfile(Profile profile)
    {
        var editor = new ProfileEditor
        {
            Id = profile.Id,
            Name = profile.Name,
            Icon = profile.Icon ?? "",
            StartupBehaviour = profile.StartupBehaviour,
        };
        foreach (var action in profile.Actions) editor.Actions.Add(ActionEditor.FromAction(action));
        return editor;
    }

    public Profile ToProfile() => new()
    {
        Id = Id,
        Name = Name,
        Icon = string.IsNullOrEmpty(Icon) ? null : Icon,
        StartupBehaviour = StartupBehaviour,
        Actions = Actions.Select(a => a.ToAction()).ToList(),
    };
}
