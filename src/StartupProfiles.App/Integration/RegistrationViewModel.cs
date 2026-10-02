using System.Collections.ObjectModel;
using System.Windows.Input;
using DotNetLib.Core.Mvvm;
using StartupProfiles.App.Ui;
using StartupProfiles.Core.Integration;
using StartupProfiles.Core.Models;

namespace StartupProfiles.App.Integration;

/// <summary>
/// Drives the trusted registration window: shows the requesting app's details and the destinations the user
/// can add it to - the base first, then every profile. The user always chooses: a
/// <see cref="RegistrationRequest.SuggestedProfile"/> only pre-ticks that one profile, and neither
/// "Everything" nor the base is ever pre-selected (docs/INTEGRATION.md). "Just recognize" takes the third
/// way out: the app is kept in the library, ready to be added to a profile later.
/// </summary>
public sealed class RegistrationViewModel : ObservableObject
{
    private const string EverythingProfileId = "everything";

    private readonly RegistrationRequest _request;
    private readonly IProfileRegistrar _registrar;
    private readonly RelayCommand _addCommand;
    private readonly RelayCommand _recognizeCommand;
    private bool _busy;
    private string? _status;
    private bool _statusIsError;

    public RegistrationViewModel(RegistrationRequest request, IReadOnlyList<RegistrationChoice> choices, IProfileRegistrar registrar)
    {
        _request = request;
        _registrar = registrar;
        Choices = new ObservableCollection<RegistrationChoice>(choices);

        _addCommand = new RelayCommand(_ => _ = ApplyAsync(Chosen()), _ => !_busy && Choices.Any(c => c.IsSelected));
        _recognizeCommand = new RelayCommand(_ => _ = ApplyAsync(RegistrationTargets.LibraryOnly), _ => !_busy);
        DontAddCommand = new RelayCommand(_ => CloseRequested?.Invoke());

        foreach (var choice in Choices)
        {
            choice.PropertyChanged += (_, _) => _addCommand.NotifyCanExecuteChanged();

            // A suggested profile only pre-ticks a hint - never the base, and never "Everything", which the
            // contract says must not be auto-selected (docs/INTEGRATION.md), even if an app suggests it.
            if (!choice.IsBase &&
                string.Equals(choice.Id, request.SuggestedProfile, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(choice.Id, EverythingProfileId, StringComparison.OrdinalIgnoreCase))
                choice.IsSelected = true;
        }
    }

    public string AppName => _request.Name;
    public string Target => _request.Target;
    public string? Arguments => _request.Arguments;
    public string Publisher => string.IsNullOrWhiteSpace(_request.Publisher) ? "Unknown publisher" : _request.Publisher;

    /// <summary>The shell icon shown in the card: the supplied icon reference if any, else the target itself.</summary>
    public string? IconSource =>
        ActionIconSource.For(ActionType.LaunchApp, _request.Icon ?? _request.Target, _request.Arguments);

    public ObservableCollection<RegistrationChoice> Choices { get; }

    public ICommand AddCommand => _addCommand;

    /// <summary>Keeps the app in the library without adding it to any profile.</summary>
    public ICommand RecognizeCommand => _recognizeCommand;

    public ICommand DontAddCommand { get; }

    public string? Status { get => _status; private set => SetProperty(ref _status, value); }

    /// <summary>True when <see cref="Status"/> reports a failure rather than progress.</summary>
    public bool StatusIsError { get => _statusIsError; private set => SetProperty(ref _statusIsError, value); }

    /// <summary>What was applied, or null while the user has not confirmed anything yet.</summary>
    public RegistrationOutcome? Outcome { get; private set; }

    /// <summary>True once the app has been added or recognized; the window uses it to know the outcome on close.</summary>
    public bool Added => Outcome is not null;

    public event Action? CloseRequested;

    private RegistrationTargets Chosen() => RegistrationTargets.For(
        Choices.Where(c => c is { IsSelected: true, IsBase: false }).Select(c => c.Id),
        Choices.Any(c => c is { IsSelected: true, IsBase: true }));

    private async Task ApplyAsync(RegistrationTargets targets)
    {
        _busy = true;
        NotifyCommands();
        SetStatus(targets.IsLibraryOnly ? "Adding to the library..." : "Adding...", isError: false);

        try
        {
            // Off the UI thread: the running-instance registrar makes blocking HTTP calls.
            Outcome = await Task.Run(() => _registrar.Apply(_request, targets)).ConfigureAwait(true);
            CloseRequested?.Invoke();
        }
        catch (RegistrationException ex)
        {
            SetStatus(ex.Message, isError: true);
            _busy = false;
            NotifyCommands();
        }
    }

    private void SetStatus(string message, bool isError)
    {
        Status = message;
        StatusIsError = isError;
    }

    private void NotifyCommands()
    {
        _addCommand.NotifyCanExecuteChanged();
        _recognizeCommand.NotifyCanExecuteChanged();
    }
}
