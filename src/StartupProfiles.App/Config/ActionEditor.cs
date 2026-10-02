using DotNetLib.Core.Mvvm;
using StartupProfiles.Core.Models;

namespace StartupProfiles.App.Config;

/// <summary>
/// Editable view model for one <see cref="ProfileAction"/> (delay is edited in whole seconds). A row linked to a
/// <see cref="LibraryItem"/> shows the item's name and values; what it starts is edited in the library, while
/// delay, failure behaviour, and retries stay editable per row. The display properties (<see cref="DisplayName"/>,
/// <see cref="Summary"/>, <see cref="Badges"/>) put the row in plain words for the action list.
/// </summary>
public sealed class ActionEditor : ObservableObject
{
    private static readonly string[] DisplayProperties =
        [nameof(DisplayName), nameof(Summary), nameof(Badges), nameof(TargetLabel), nameof(CanBrowse), nameof(HasArguments), nameof(Glyph)];

    private ActionType _type = ActionType.LaunchApp;
    private string _target = "";
    private string _arguments = "";
    private int _delaySeconds;
    private bool _runAsAdmin;
    private FailureBehaviour _failureBehaviour = FailureBehaviour.Continue;
    private int _retryCount = 1;
    private string? _libraryItemId;
    private string? _libraryItemName;

    public ActionType Type { get => _type; set => Set(ref _type, value); }
    public string Target { get => _target; set => Set(ref _target, value); }
    public string Arguments { get => _arguments; set => Set(ref _arguments, value); }
    public int DelaySeconds { get => _delaySeconds; set => Set(ref _delaySeconds, Math.Max(0, value)); }
    public bool RunAsAdmin { get => _runAsAdmin; set => Set(ref _runAsAdmin, value); }
    public FailureBehaviour FailureBehaviour { get => _failureBehaviour; set => Set(ref _failureBehaviour, value); }
    public int RetryCount { get => _retryCount; set => Set(ref _retryCount, Math.Max(1, value)); }

    /// <summary>The linked library item's id, or null for a standalone row.</summary>
    public string? LibraryItemId => _libraryItemId;

    public bool IsLinked => _libraryItemId is not null;

    /// <summary>True when what the row starts can be edited here (a linked row is edited in Created).</summary>
    public bool IsEditable => !IsLinked;

    /// <summary>What the row is called: the library item's name, else a name read from the target.</summary>
    public string DisplayName => _libraryItemName ??
        (string.IsNullOrWhiteSpace(Target) ? $"New {ActionLabels.For(Type).ToLowerInvariant()}" : Startables.NameFor(ToAction()));

    /// <summary>The second line: the kind of step, then what it points at (a Store app says so instead of its shell command).</summary>
    public string Summary =>
        string.IsNullOrWhiteSpace(Target) ? ActionLabels.For(Type)
        : Arguments.StartsWith(@"shell:AppsFolder\", StringComparison.OrdinalIgnoreCase) ? $"{ActionLabels.For(Type)} · Microsoft Store app"
        : $"{ActionLabels.For(Type)} · {Target}{(string.IsNullOrWhiteSpace(Arguments) ? "" : " " + Arguments)}";

    /// <summary>Short tags for options that differ from the defaults ("waits 5 s", "as admin").</summary>
    public IReadOnlyList<string> Badges => ActionLabels.Badges(DelaySeconds, FailureBehaviour, RetryCount, RunAsAdmin);

    public string TargetLabel => ActionLabels.TargetLabel(Type);
    public bool CanBrowse => IsEditable && ActionLabels.CanBrowse(Type);
    public bool HasArguments => ActionLabels.HasArguments(Type);

    /// <summary>The fallback icon glyph shown when the shell has no icon for the target.</summary>
    public string Glyph => ActionLabels.Glyph(Type);

    public bool IsRetry => FailureBehaviour == FailureBehaviour.Retry;

    /// <summary>
    /// Raised after any stored value changes (not the display-only properties), so the editor can save.
    /// </summary>
    public event Action<ActionEditor>? Changed;

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
        var changed = _libraryItemId != item.Id || _libraryItemName != item.Name;
        _libraryItemId = item.Id;
        _libraryItemName = item.Name;
        LinkChanged(changed);
    }

    /// <summary>Turns a linked row into a standalone one that keeps its current values.</summary>
    public void Unlink()
    {
        var changed = _libraryItemId is not null;
        _libraryItemId = null;
        _libraryItemName = null;
        LinkChanged(changed);
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

    private void Set<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (!SetProperty(ref field, value, name)) return;
        if (name == nameof(FailureBehaviour)) OnPropertyChanged(nameof(IsRetry));
        RaiseDisplay();
        Changed?.Invoke(this);
    }

    private void LinkChanged(bool changed)
    {
        OnPropertyChanged(nameof(LibraryItemId));
        OnPropertyChanged(nameof(IsLinked));
        OnPropertyChanged(nameof(IsEditable));
        RaiseDisplay();
        if (changed) Changed?.Invoke(this);
    }

    private void RaiseDisplay()
    {
        foreach (var property in DisplayProperties) OnPropertyChanged(property);
    }
}
