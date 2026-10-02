using System.Collections.ObjectModel;
using System.Windows.Input;
using StartupProfiles.App.Mvvm;
using StartupProfiles.Core.Models;
using StartupProfiles.Core.Startup;

namespace StartupProfiles.App.Discovery;

/// <summary>
/// Drives the "New startup apps" window: every app that set itself to start with Windows since last time, each
/// with a choice of where it should start instead. Apply places them all (see <see cref="StartupDiscovery.Place"/>);
/// "Ask me later" closes without deciding, so they are offered again next time.
/// </summary>
public sealed class NewStartupAppsViewModel : ObservableObject
{
    private readonly StartupDiscovery _discovery;
    private string? _status;
    private bool _isDone;

    public NewStartupAppsViewModel(IReadOnlyList<StartupEntry> found, IReadOnlyList<Profile> profiles, StartupDiscovery discovery)
    {
        _discovery = discovery;

        IReadOnlyList<PlacementOption> options =
        [
            .. profiles.Select(p => new PlacementOption($"Start with {p.Name}", StartupPlacement.Profile(p.Id))),
            new("Start with Base (every profile)", StartupPlacement.Base),
            new("Keep in Created for later", StartupPlacement.LibraryOnly),
            new("Leave it to Windows", StartupPlacement.LeaveInWindows),
        ];
        var suggested = options.FirstOrDefault(o => o.Placement == discovery.SuggestedPlacement) ?? options[^1];

        foreach (var entry in found.Where(e => e.Launch is not null)) Apps.Add(new NewStartupAppRow(entry, options, suggested));

        ApplyCommand = new RelayCommand(_ => Apply());
        LaterCommand = new RelayCommand(_ => CloseRequested?.Invoke());
    }

    public ObservableCollection<NewStartupAppRow> Apps { get; } = [];

    public string Heading => Apps.Count == 1
        ? $"{Apps[0].App.Name} now starts with Windows"
        : $"{Apps.Count} apps now start with Windows";

    public ICommand ApplyCommand { get; }

    /// <summary>Closes without deciding; the apps are offered again next time.</summary>
    public ICommand LaterCommand { get; }

    public string? Status { get => _status; private set => SetProperty(ref _status, value); }

    /// <summary>True once applied but a message is left to read; the window then offers only Close.</summary>
    public bool IsDone { get => _isDone; private set => SetProperty(ref _isDone, value); }

    public event Action? CloseRequested;

    private void Apply()
    {
        var refused = Apps.Where(row => !_discovery.Place(row.Entry, row.Selected.Placement)).Select(row => row.App.Name).ToList();
        if (refused.Count == 0)
        {
            CloseRequested?.Invoke();
            return;
        }

        // Rare (policy or permissions): the step was added, but Windows will also still start it.
        Status = $"Added, but Windows would not switch off {string.Join(", ", refused)}. Turn it off in Task Manager > Startup apps.";
        IsDone = true;
    }
}
