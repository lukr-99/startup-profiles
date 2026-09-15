using System.Collections.ObjectModel;
using StartupProfiles.App.Mvvm;
using StartupProfiles.Core.Execution;
using StartupProfiles.Core.Models;

namespace StartupProfiles.App.Config;

/// <summary>
/// Editable view model for a whole <see cref="Profile"/>, or for the <see cref="StartupBase"/> when
/// <see cref="IsBase"/> (which only has actions: no name, icon, startup behaviour, or base toggle).
/// </summary>
public sealed class ProfileEditor : ObservableObject
{
    private string _name = "";
    private string _icon = "";
    private bool _includeBase = true;
    private StartupBehaviour _startupBehaviour = StartupBehaviour.Default;

    public required string Id { get; init; }

    public bool IsBase { get; init; }
    public bool IsProfile => !IsBase;

    public string Name { get => _name; set => SetProperty(ref _name, value); }

    public string Icon
    {
        get => _icon;
        set { if (SetProperty(ref _icon, value)) OnPropertyChanged(nameof(IconDisplay)); }
    }

    /// <summary>The emoji to show on the picker button, or a prompt when none is set.</summary>
    public string IconDisplay => string.IsNullOrWhiteSpace(Icon) ? "Choose…" : Icon;

    public bool IncludeBase { get => _includeBase; set => SetProperty(ref _includeBase, value); }

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
            IncludeBase = profile.IncludeBase,
            StartupBehaviour = profile.StartupBehaviour,
        };
        foreach (var action in profile.Actions) editor.Actions.Add(ActionEditor.FromAction(action));
        return editor;
    }

    public static ProfileEditor FromBase(StartupBase value)
    {
        var editor = new ProfileEditor { Id = ProfileExecutor.BaseRunId, IsBase = true, Name = "Base" };
        foreach (var action in value.Actions) editor.Actions.Add(ActionEditor.FromAction(action));
        return editor;
    }

    public Profile ToProfile() => new()
    {
        Id = Id,
        Name = Name,
        Icon = string.IsNullOrEmpty(Icon) ? null : Icon,
        IncludeBase = IncludeBase,
        StartupBehaviour = StartupBehaviour,
        Actions = Actions.Select(a => a.ToAction()).ToList(),
    };

    public StartupBase ToBase() => new() { Actions = Actions.Select(a => a.ToAction()).ToList() };
}
