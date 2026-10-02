using StartupProfiles.App.Config;
using DotNetLib.Core.Mvvm;
using StartupProfiles.Core.Startup;

namespace StartupProfiles.App.Discovery;

/// <summary>A newly found startup app in the discovery window, with the user's choice of where it should start.</summary>
public sealed class NewStartupAppRow : ObservableObject
{
    private PlacementOption _selected;

    public NewStartupAppRow(StartupEntry entry, IReadOnlyList<PlacementOption> options, PlacementOption selected)
    {
        Entry = entry;
        App = StartupAppItem.From(entry, entry.Launch!);
        Options = options;
        _selected = selected;
    }

    public StartupEntry Entry { get; }

    /// <summary>Name, "Registry · starts with Windows", and the launch action (for the icon).</summary>
    public StartupAppItem App { get; }

    public IReadOnlyList<PlacementOption> Options { get; }

    public PlacementOption Selected { get => _selected; set => SetProperty(ref _selected, value); }
}
