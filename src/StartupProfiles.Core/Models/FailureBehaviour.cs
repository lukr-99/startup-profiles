namespace StartupProfiles.Core.Models;

/// <summary>What the runner does when an action fails.</summary>
public enum FailureBehaviour
{
    /// <summary>Record the failure and run the next action.</summary>
    Continue,

    /// <summary>Record the failure and stop the profile.</summary>
    Stop,

    /// <summary>Retry the action up to <see cref="ProfileAction.RetryCount"/> more times before giving up.</summary>
    Retry,
}
