using StartupProfiles.Core.Models;

namespace StartupProfiles.Core.Backup;

/// <summary>
/// A full backup of the user's setup: every profile, the base, the library, and the app settings. Run history and
/// the startup takeover record are machine state and are left out. Written as JSON by <see cref="BackupService"/>.
/// </summary>
public sealed record BackupDocument
{
    /// <summary>The value of <see cref="Format"/> in every backup this app writes.</summary>
    public const string FormatName = "startup-profiles-backup";

    /// <summary>The backup version this app writes. It reads this one and older ones.</summary>
    public const int CurrentVersion = 1;

    public string Format { get; init; } = FormatName;
    public int Version { get; init; } = CurrentVersion;
    public DateTimeOffset ExportedAt { get; init; }
    public string AppVersion { get; init; } = "";

    public IReadOnlyList<Profile> Profiles { get; init; } = [];
    public StartupBase Base { get; init; } = new();
    public IReadOnlyList<LibraryItem> Library { get; init; } = [];
    public IReadOnlyDictionary<string, string> Settings { get; init; } = new Dictionary<string, string>();
}
