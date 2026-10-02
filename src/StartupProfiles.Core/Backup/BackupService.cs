using System.Text.Json;
using StartupProfiles.Core.Models;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.Core.Backup;

/// <summary>
/// Full-fidelity manual backup and restore. <see cref="Export"/> writes a versioned <see cref="BackupDocument"/>;
/// <see cref="Plan"/> parses and checks a file without changing anything; <see cref="Restore"/> applies a plan and
/// puts the previous state back if any write fails. Old profiles-only exports (a bare JSON array) still restore.
/// </summary>
public sealed class BackupService
{
    private readonly IProfileStore _profiles;
    private readonly IBaseStore _base;
    private readonly ILibraryStore _library;
    private readonly IConfigStore _config;
    private readonly Func<DateTimeOffset> _now;
    private readonly string _appVersion;

    public BackupService(IProfileStore profiles, IBaseStore baseStore, ILibraryStore library, IConfigStore config,
        string appVersion, Func<DateTimeOffset>? now = null)
    {
        _profiles = profiles;
        _base = baseStore;
        _library = library;
        _config = config;
        _appVersion = appVersion;
        _now = now ?? (() => DateTimeOffset.Now);
    }

    /// <summary>Everything the user set up, as backup JSON.</summary>
    public string Export() => JsonSerializer.Serialize(Snapshot() with { ExportedAt = _now(), AppVersion = _appVersion }, JsonFile.Options);

    /// <summary>Reads and checks <paramref name="json"/>, and works out what restoring it would change.</summary>
    /// <exception cref="BackupException">The file is not a backup this app can restore.</exception>
    public BackupPlan Plan(string json)
    {
        var (document, profilesOnly) = Parse(json);
        Validate(document);

        var current = _profiles.GetAll();
        var incoming = document.Profiles.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new BackupPlan(
            document,
            profilesOnly,
            document.Profiles.Where(p => current.All(c => !Same(c.Id, p.Id))).Select(p => p.Name).ToList(),
            document.Profiles.Where(p => current.Any(c => Same(c.Id, p.Id))).Select(p => p.Name).ToList(),
            profilesOnly ? [] : current.Where(c => !incoming.Contains(c.Id)).Select(c => c.Name).ToList());
    }

    /// <summary>Applies <paramref name="plan"/>. If a write fails, the earlier state is written back.</summary>
    /// <exception cref="BackupException">The restore failed; the earlier state was put back.</exception>
    public void Restore(BackupPlan plan)
    {
        var before = Snapshot();
        try
        {
            Apply(plan.Document, profilesOnly: plan.IsProfilesOnly);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Apply(before, profilesOnly: false);
            throw new BackupException("The restore could not be written, so nothing was changed.", ex);
        }
    }

    private BackupDocument Snapshot() => new()
    {
        Profiles = _profiles.GetAll(),
        Base = _base.Load(),
        Library = _library.GetAll(),
        Settings = _config.GetAll(),
    };

    private void Apply(BackupDocument document, bool profilesOnly)
    {
        if (!profilesOnly)
        {
            foreach (var item in _library.GetAll().Where(i => document.Library.All(n => n.Id != i.Id))) _library.Remove(item.Id);
            foreach (var item in document.Library) _library.Save(item);
            _base.Save(document.Base);
            foreach (var key in _config.GetAll().Keys.Where(k => !document.Settings.ContainsKey(k))) _config.SetValue(key, null);
            foreach (var (key, value) in document.Settings) _config.SetValue(key, value);
            foreach (var profile in _profiles.GetAll().Where(p => document.Profiles.All(n => !Same(n.Id, p.Id)))) _profiles.Remove(profile.Id);
        }

        foreach (var profile in document.Profiles) _profiles.Save(profile);
    }

    private static (BackupDocument Document, bool ProfilesOnly) Parse(string json)
    {
        try
        {
            using var parsed = JsonDocument.Parse(json);
            if (parsed.RootElement.ValueKind == JsonValueKind.Array)
            {
                var profiles = parsed.RootElement.Deserialize<List<Profile>>(JsonFile.Options) ?? [];
                return (new BackupDocument { Version = 0, Profiles = profiles }, true);
            }

            var document = parsed.RootElement.Deserialize<BackupDocument>(JsonFile.Options)
                ?? throw new BackupException("The file is empty.");
            if (!string.Equals(document.Format, BackupDocument.FormatName, StringComparison.Ordinal))
                throw new BackupException("This is not a Startup Profiles backup.");
            if (document.Version > BackupDocument.CurrentVersion)
                throw new BackupException($"This backup was made by a newer version of Startup Profiles ({document.AppVersion}). Update the app first.");
            return (document, false);
        }
        catch (JsonException ex)
        {
            throw new BackupException("The file is not valid JSON, or not in the backup format.", ex);
        }
    }

    private static void Validate(BackupDocument document)
    {
        if (document.Profiles.Any(p => string.IsNullOrWhiteSpace(p.Id) || string.IsNullOrWhiteSpace(p.Name)))
            throw new BackupException("A profile in the file has no id or name.");
        if (document.Profiles.GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            throw new BackupException("Two profiles in the file share an id.");
        if (document.Library.Any(i => string.IsNullOrWhiteSpace(i.Id)) || document.Library.GroupBy(i => i.Id).Any(g => g.Count() > 1))
            throw new BackupException("A Created item in the file has a missing or repeated id.");

        var actions = document.Profiles.SelectMany(p => p.Actions).Concat(document.Base.Actions);
        if (actions.Any(a => !Enum.IsDefined(a.Type)))
            throw new BackupException("A step in the file has a kind this version does not know.");
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
