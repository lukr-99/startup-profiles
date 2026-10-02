namespace StartupProfiles.Core.Backup;

/// <summary>
/// What restoring a checked backup would change, shown to the user before anything is written. A full backup
/// replaces everything; an old profiles-only export (<see cref="IsProfilesOnly"/>) adds and replaces profiles only.
/// </summary>
public sealed record BackupPlan(
    BackupDocument Document,
    bool IsProfilesOnly,
    IReadOnlyList<string> AddedProfiles,
    IReadOnlyList<string> ReplacedProfiles,
    IReadOnlyList<string> RemovedProfiles)
{
    /// <summary>A plain description of the restore, for the confirmation prompt.</summary>
    public string Summary
    {
        get
        {
            var lines = new List<string>();
            if (Document.ExportedAt != default) lines.Add($"Backup from {Document.ExportedAt.LocalDateTime:g}.");
            lines.Add($"Profiles: {AddedProfiles.Count} new, {ReplacedProfiles.Count} replaced" +
                      (RemovedProfiles.Count > 0 ? $", {RemovedProfiles.Count} removed ({string.Join(", ", RemovedProfiles)})." : "."));
            if (!IsProfilesOnly)
            {
                lines.Add($"Base: {Document.Base.Actions.Count} step(s). Created: {Document.Library.Count} item(s). Settings: {Document.Settings.Count}.");
                lines.Add("This replaces your current profiles, Base, Created items, and settings.");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }
}
