namespace StartupProfiles.Core.Models;

/// <summary>One ordered step in a <see cref="Profile"/>: a typed action with its target and options.</summary>
public sealed class ProfileAction
{
    public required ActionType Type { get; init; }

    /// <summary>Exe path, URL, file, folder, service name, or script/command - interpreted per <see cref="Type"/>.</summary>
    public string Target { get; init; } = "";

    /// <summary>Optional launch arguments.</summary>
    public string? Arguments { get; init; }

    /// <summary>Delay applied before this action runs.</summary>
    public TimeSpan Delay { get; init; }

    /// <summary>Launch elevated. Honored by the process-launch handlers on Windows.</summary>
    public bool RunAsAdmin { get; init; }

    public FailureBehaviour FailureBehaviour { get; init; } = FailureBehaviour.Continue;

    /// <summary>Extra attempts when <see cref="FailureBehaviour"/> is <see cref="FailureBehaviour.Retry"/>.</summary>
    public int RetryCount { get; init; } = 1;
}
