namespace StartupProfiles.Core.Models;

/// <summary>
/// The gates a profile may declare. Placeholder for the conditions feature: conditions are stored but
/// not evaluated yet, so the runner ignores them in this milestone.
/// </summary>
public enum ConditionType
{
    Network,
    Battery,
    Docked,
    TimeOfDay,
}
