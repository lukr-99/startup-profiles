using StartupProfiles.App.Mvvm;
using StartupProfiles.Core.Models;

namespace StartupProfiles.App.Config;

/// <summary>Editable view model for one <see cref="ProfileAction"/> (delay is edited in whole seconds).</summary>
public sealed class ActionEditor : ObservableObject
{
    private ActionType _type = ActionType.LaunchApp;
    private string _target = "";
    private string _arguments = "";
    private int _delaySeconds;
    private bool _runAsAdmin;
    private FailureBehaviour _failureBehaviour = FailureBehaviour.Continue;
    private int _retryCount = 1;

    public ActionType Type { get => _type; set => SetProperty(ref _type, value); }
    public string Target { get => _target; set => SetProperty(ref _target, value); }
    public string Arguments { get => _arguments; set => SetProperty(ref _arguments, value); }
    public int DelaySeconds { get => _delaySeconds; set => SetProperty(ref _delaySeconds, value); }
    public bool RunAsAdmin { get => _runAsAdmin; set => SetProperty(ref _runAsAdmin, value); }
    public FailureBehaviour FailureBehaviour { get => _failureBehaviour; set => SetProperty(ref _failureBehaviour, value); }
    public int RetryCount { get => _retryCount; set => SetProperty(ref _retryCount, value); }

    public static IReadOnlyList<ActionType> ActionTypes { get; } = Enum.GetValues<ActionType>();
    public static IReadOnlyList<FailureBehaviour> FailureBehaviours { get; } = Enum.GetValues<FailureBehaviour>();

    public static ActionEditor FromAction(ProfileAction action) => new()
    {
        Type = action.Type,
        Target = action.Target,
        Arguments = action.Arguments ?? "",
        DelaySeconds = (int)action.Delay.TotalSeconds,
        RunAsAdmin = action.RunAsAdmin,
        FailureBehaviour = action.FailureBehaviour,
        RetryCount = action.RetryCount,
    };

    public ProfileAction ToAction() => new()
    {
        Type = Type,
        Target = Target,
        Arguments = string.IsNullOrEmpty(Arguments) ? null : Arguments,
        Delay = TimeSpan.FromSeconds(DelaySeconds),
        RunAsAdmin = RunAsAdmin,
        FailureBehaviour = FailureBehaviour,
        RetryCount = RetryCount,
    };
}
