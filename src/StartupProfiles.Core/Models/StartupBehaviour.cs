namespace StartupProfiles.Core.Models;

/// <summary>How the launcher selects a profile at login.</summary>
public enum StartupBehaviour
{
    Default,
    RememberLast,
    AutoSelectAfterTimeout,
}
