using System.Collections.ObjectModel;
using System.Collections.Specialized;
using DotNetLib.Core.Mvvm;
using StartupProfiles.App.Ui;
using StartupProfiles.Core.Execution;
using StartupProfiles.Core.Models;

namespace StartupProfiles.App.Config;

/// <summary>
/// Editable view model for a whole <see cref="Profile"/>, or for the <see cref="StartupBase"/> when
/// <see cref="IsBase"/> (which only has actions: no name, icon, startup behaviour, or base toggle).
/// <see cref="Changed"/> fires on any edit, including to a row, so the window can save as the user works.
/// </summary>
public sealed class ProfileEditor : ObservableObject
{
    private string _name = "";
    private string _icon = "";
    private bool _includeBase = true;
    private StartupBehaviour _startupBehaviour = StartupBehaviour.Default;

    public ProfileEditor()
    {
        Actions.CollectionChanged += OnActionsChanged;
    }

    public required string Id { get; init; }

    public bool IsBase { get; init; }
    public bool IsProfile => !IsBase;

    public string Name
    {
        get => _name;
        set { if (Set(ref _name, value)) OnPropertyChanged(nameof(Glyph)); }
    }

    public string Icon
    {
        get => _icon;
        set
        {
            if (!Set(ref _icon, value)) return;
            OnPropertyChanged(nameof(IconDisplay));
            OnPropertyChanged(nameof(Glyph));
            OnPropertyChanged(nameof(HasIcon));
        }
    }

    /// <summary>The emoji to show on the picker button, or a prompt when none is set.</summary>
    public string IconDisplay => string.IsNullOrWhiteSpace(Icon) ? "Choose…" : Icon;

    /// <summary>The profile's emoji, or its initial when it has none (as on the launcher).</summary>
    public string Glyph => ProfileGlyph.For(Icon, Name);

    public bool HasIcon => !string.IsNullOrWhiteSpace(Icon);

    public bool IncludeBase { get => _includeBase; set => Set(ref _includeBase, value); }

    public StartupBehaviour StartupBehaviour { get => _startupBehaviour; set => Set(ref _startupBehaviour, value); }
    public ObservableCollection<ActionEditor> Actions { get; } = [];

    public static IReadOnlyList<Choice<StartupBehaviour>> StartupBehaviours { get; } =
    [
        new(StartupBehaviour.Default, "Ask me"),
        new(StartupBehaviour.RememberLast, "Start it if I used it last"),
        new(StartupBehaviour.AutoSelectAfterTimeout, "Always start it"),
    ];

    /// <summary>Raised after any stored value of the profile or one of its rows changes, or rows are added, removed, or moved.</summary>
    public event Action? Changed;

    public static ProfileEditor FromProfile(Profile profile, Func<string, LibraryItem?>? findItem = null)
    {
        var editor = new ProfileEditor
        {
            Id = profile.Id,
            Name = profile.Name,
            Icon = profile.Icon ?? "",
            IncludeBase = profile.IncludeBase,
            StartupBehaviour = profile.StartupBehaviour,
        };
        foreach (var action in profile.Actions) editor.Actions.Add(ActionEditor.FromAction(action, findItem));
        return editor;
    }

    public static ProfileEditor FromBase(StartupBase value, Func<string, LibraryItem?>? findItem = null)
    {
        var editor = new ProfileEditor { Id = ProfileExecutor.BaseRunId, IsBase = true, Name = "Base" };
        foreach (var action in value.Actions) editor.Actions.Add(ActionEditor.FromAction(action, findItem));
        return editor;
    }

    public Profile ToProfile() => new()
    {
        Id = Id,
        Name = Name.Trim(),
        Icon = string.IsNullOrEmpty(Icon) ? null : Icon,
        IncludeBase = IncludeBase,
        StartupBehaviour = StartupBehaviour,
        Actions = Actions.Select(a => a.ToAction()).ToList(),
    };

    public StartupBase ToBase() => new() { Actions = Actions.Select(a => a.ToAction()).ToList() };

    private bool Set<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (!SetProperty(ref field, value, name)) return false;
        Changed?.Invoke();
        return true;
    }

    private void OnActionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var row in e.OldItems?.OfType<ActionEditor>() ?? []) row.Changed -= OnRowChanged;
        foreach (var row in e.NewItems?.OfType<ActionEditor>() ?? []) row.Changed += OnRowChanged;
        Changed?.Invoke();
    }

    private void OnRowChanged(ActionEditor row) => Changed?.Invoke();
}
