using System.Collections.ObjectModel;
using System.Windows.Input;
using StartupProfiles.App.Mvvm;
using StartupProfiles.Core.Integration;

namespace StartupProfiles.App.Integration;

/// <summary>
/// Drives the trusted registration window: shows the requesting app's details and the profiles the user
/// can add it to. The user always chooses - a <see cref="RegistrationRequest.SuggestedProfile"/> only
/// pre-ticks that one profile, and "Everything" is never pre-selected (docs/INTEGRATION.md).
/// </summary>
public sealed class RegistrationViewModel : ObservableObject
{
    private readonly RegistrationRequest _request;
    private readonly IProfileRegistrar _registrar;
    private readonly RelayCommand _addCommand;
    private bool _busy;
    private string? _status;

    public RegistrationViewModel(RegistrationRequest request, IReadOnlyList<ProfileChoice> choices, IProfileRegistrar registrar)
    {
        _request = request;
        _registrar = registrar;
        Choices = new ObservableCollection<ProfileChoice>(choices);

        _addCommand = new RelayCommand(_ => _ = AddAsync(), _ => !_busy && Choices.Any(c => c.IsSelected));
        DontAddCommand = new RelayCommand(_ => CloseRequested?.Invoke());

        foreach (var choice in Choices)
        {
            choice.PropertyChanged += (_, _) => _addCommand.NotifyCanExecuteChanged();

            // A suggested profile only pre-ticks a hint - and never "Everything", which the contract says
            // must never be auto-selected (docs/INTEGRATION.md), even if an app suggests it.
            if (string.Equals(choice.Id, request.SuggestedProfile, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(choice.Id, EverythingProfileId, StringComparison.OrdinalIgnoreCase))
                choice.IsSelected = true;
        }
    }

    private const string EverythingProfileId = "everything";

    public string AppName => _request.Name;
    public string Target => _request.Target;
    public string? Arguments => _request.Arguments;
    public string Publisher => string.IsNullOrWhiteSpace(_request.Publisher) ? "Unknown publisher" : _request.Publisher;

    public ObservableCollection<ProfileChoice> Choices { get; }

    public ICommand AddCommand => _addCommand;
    public ICommand DontAddCommand { get; }

    public string? Status { get => _status; private set => SetProperty(ref _status, value); }

    /// <summary>True once the app has been added; the window uses it to know the outcome on close.</summary>
    public bool Added { get; private set; }

    public event Action? CloseRequested;

    private async Task AddAsync()
    {
        var ids = Choices.Where(c => c.IsSelected).Select(c => c.Id).ToArray();
        if (ids.Length == 0) return;

        _busy = true;
        _addCommand.NotifyCanExecuteChanged();
        Status = "Adding...";

        try
        {
            // Off the UI thread: the running-instance registrar makes blocking HTTP calls.
            var outcome = await Task.Run(() => _registrar.Apply(_request, ids)).ConfigureAwait(true);
            Added = outcome.ChangedAnything || outcome.AlreadyPresentIn.Count > 0;
            CloseRequested?.Invoke();
        }
        catch (RegistrationException ex)
        {
            Status = ex.Message;
            _busy = false;
            _addCommand.NotifyCanExecuteChanged();
        }
    }
}
