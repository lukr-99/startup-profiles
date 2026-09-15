using StartupProfiles.App.Mvvm;
using StartupProfiles.Core.Models;

namespace StartupProfiles.App.Config;

/// <summary>
/// Editable view model for one <see cref="ProfileAction"/> (delay is edited in whole seconds). A row linked to a
/// <see cref="LibraryItem"/> shows the item's name and values; what it starts is edited in the library, while
/// delay, failure behaviour, and retries stay editable per row.
/// </summary>
public sealed class ActionEditor : ObservableObject
{
    private ActionType _type = ActionType.LaunchApp;
    private string _target = "";
    private string _arguments = "";
    private int _delaySeconds;
    private bool _runAsAdmin;
    private FailureBehaviour _failureBehaviour = FailureBehaviour.Continue;
    private int _retryCount = 1;
    private string? _libraryItemId;
    private string? _libraryItemName;

    public ActionType Type { get => _type; set => SetProperty(ref _type, value); }

    public string Target
    {
        get => _target;
        set { if (SetProperty(ref _target, value)) OnPropertyChanged(nameof(TargetDisplay)); }
    }

    public string Arguments { get => _arguments; set => SetProperty(ref _arguments, value); }
    public int DelaySeconds { get => _delaySeconds; set => SetProperty(ref _delaySeconds, value); }
    public bool RunAsAdmin { get => _runAsAdmin; set => SetProperty(ref _runAsAdmin, value); }
    public FailureBehaviour FailureBehaviour { get => _failureBehaviour; set => SetProperty(ref _failureBehaviour, value); }
    public int RetryCount { get => _retryCount; set => SetProperty(ref _retryCount, value); }

    /// <summary>The linked library item's id, or null for a standalone row.</summary>
    public string? LibraryItemId => _libraryItemId;

    public bool IsLinked => _libraryItemId is not null;

    /// <summary>What the Target column shows: the library item's name for a linked row, else the target.</summary>
    public string TargetDisplay => _libraryItemName ?? Target;

    public static IReadOnlyList<ActionType> ActionTypes { get; } = Enum.GetValues<ActionType>();
    public static IReadOnlyList<FailureBehaviour> FailureBehaviours { get; } = Enum.GetValues<FailureBehaviour>();

    /// <summary>
    /// A row for <paramref name="action"/>, showing its library item's current values when it is linked and the
    /// item still exists (a link to a deleted item loads as a standalone row with its last-saved values).
    /// </summary>
    public static ActionEditor FromAction(ProfileAction action, Func<string, LibraryItem?>? findItem = null)
    {
        var editor = new ActionEditor
        {
            Type = action.Type,
            Target = action.Target,
            Arguments = action.Arguments ?? "",
            DelaySeconds = (int)action.Delay.TotalSeconds,
            RunAsAdmin = action.RunAsAdmin,
            FailureBehaviour = action.FailureBehaviour,
            RetryCount = action.RetryCount,
        };

        if (action.LibraryItemId is { } id && findItem?.Invoke(id) is { } item) editor.LinkTo(item);
        return editor;
    }

    /// <summary>Links the row to <paramref name="item"/>, taking its name and what it starts.</summary>
    public void LinkTo(LibraryItem item)
    {
        Type = item.Type;
        Target = item.Target;
        Arguments = item.Arguments ?? "";
        RunAsAdmin = item.RunAsAdmin;
        _libraryItemId = item.Id;
        _libraryItemName = item.Name;
        OnPropertyChanged(nameof(LibraryItemId));
        OnPropertyChanged(nameof(IsLinked));
        OnPropertyChanged(nameof(TargetDisplay));
    }

    /// <summary>Turns a linked row into a standalone one that keeps its current values.</summary>
    public void Unlink()
    {
        _libraryItemId = null;
        _libraryItemName = null;
        OnPropertyChanged(nameof(LibraryItemId));
        OnPropertyChanged(nameof(IsLinked));
        OnPropertyChanged(nameof(TargetDisplay));
    }

    public ProfileAction ToAction() => new()
    {
        Type = Type,
        Target = Target,
        Arguments = string.IsNullOrEmpty(Arguments) ? null : Arguments,
        Delay = TimeSpan.FromSeconds(DelaySeconds),
        RunAsAdmin = RunAsAdmin,
        FailureBehaviour = FailureBehaviour,
        RetryCount = RetryCount,
        LibraryItemId = LibraryItemId,
    };
}
