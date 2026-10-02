using System.Text.Json;
using StartupProfiles.Core.Backup;
using StartupProfiles.Core.Models;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.Core.Tests.Backup;

public sealed class BackupServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 9, 30, 0, TimeSpan.FromHours(2));

    private readonly List<string> _files = [];
    private readonly ProfileStore _profiles;
    private readonly BaseStore _base;
    private readonly LibraryStore _library;
    private readonly ConfigStore _config;

    public BackupServiceTests()
    {
        _profiles = new ProfileStore(Temp());
        _base = new BaseStore(Temp());
        _library = new LibraryStore(Temp());
        _config = new ConfigStore(Temp());
    }

    public void Dispose()
    {
        foreach (var file in _files) File.Delete(file);
    }

    private string Temp()
    {
        var file = Path.Combine(Path.GetTempPath(), $"sp-backup-{Guid.NewGuid():N}.json");
        _files.Add(file);
        return file;
    }

    private BackupService Service(IProfileStore? profiles = null) =>
        new(profiles ?? _profiles, _base, _library, _config, "1.2.3", () => Now);

    private void SeedSetup()
    {
        _library.Save(new LibraryItem { Id = "code", Name = "Code", Target = @"C:\Code.exe" });
        _base.Save(new StartupBase { Actions = [new ProfileAction { Type = ActionType.OpenUrl, Target = "https://mail.example" }] });
        _profiles.Save(new Profile
        {
            Id = "work",
            Name = "Work",
            Icon = "💼",
            StartupBehaviour = StartupBehaviour.RememberLast,
            Actions = [new ProfileAction { Type = ActionType.LaunchApp, Target = @"C:\Code.exe", LibraryItemId = "code", Delay = TimeSpan.FromSeconds(3) }],
        });
        _config.SetValue("theme", "Dark");
    }

    [Fact]
    public void Export_WritesTheFormatVersionTimeAndAppVersion()
    {
        SeedSetup();

        using var json = JsonDocument.Parse(Service().Export());
        var root = json.RootElement;

        Assert.Equal(BackupDocument.FormatName, root.GetProperty("Format").GetString());
        Assert.Equal(BackupDocument.CurrentVersion, root.GetProperty("Version").GetInt32());
        Assert.Equal("1.2.3", root.GetProperty("AppVersion").GetString());
        Assert.Equal(Now, root.GetProperty("ExportedAt").GetDateTimeOffset());
    }

    [Fact]
    public void ExportDeleteRestore_BringsEverythingBack()
    {
        SeedSetup();
        var backup = Service().Export();
        var before = (_profiles.GetAll(), _base.Load(), _library.GetAll(), _config.GetAll());

        foreach (var profile in _profiles.GetAll()) _profiles.Remove(profile.Id);
        _base.Save(new StartupBase());
        _library.Remove("code");
        _config.SetValue("theme", null);
        _profiles.Save(new Profile { Id = "stray", Name = "Stray" });

        var plan = Service().Plan(backup);
        Assert.Equal(["Stray"], plan.RemovedProfiles);
        Service().Restore(plan);

        Assert.Equal(before.Item1.Select(p => p.Id).Order(), _profiles.GetAll().Select(p => p.Id).Order());
        Assert.Equal(before.Item1.Single(p => p.Id == "work"), _profiles.Find("work"), ProfileComparer.Instance);
        Assert.Equal(before.Item2.Actions, _base.Load().Actions);
        Assert.Equal(before.Item3, _library.GetAll());
        Assert.Equal("Dark", _config.GetValue("theme"));
    }

    [Fact]
    public void AnOldProfilesOnlyExport_AddsAndReplacesProfiles_AndKeepsTheRest()
    {
        SeedSetup();
        var old = JsonSerializer.Serialize(new[]
        {
            new Profile { Id = "work", Name = "Work (old)" },
            new Profile { Id = "music", Name = "Music" },
        });

        var plan = Service().Plan(old);
        Service().Restore(plan);

        Assert.True(plan.IsProfilesOnly);
        Assert.Equal(["Music"], plan.AddedProfiles);
        Assert.Equal(["Work (old)"], plan.ReplacedProfiles);
        Assert.Empty(plan.RemovedProfiles);
        Assert.Equal("Work (old)", _profiles.Find("work")!.Name);
        Assert.NotNull(_profiles.Find("music"));
        Assert.Single(_base.Load().Actions);
        Assert.Single(_library.GetAll());
    }

    [Theory]
    [InlineData("not json", "not valid JSON")]
    [InlineData("""{ "Format": "something-else", "Version": 1 }""", "not a Startup Profiles backup")]
    [InlineData("""{ "Format": "startup-profiles-backup", "Version": 99, "AppVersion": "9.0.0" }""", "newer version")]
    [InlineData("""{ "Format": "startup-profiles-backup", "Version": 1, "Profiles": [ { "Id": "a", "Name": "A" }, { "Id": "A", "Name": "B" } ] }""", "share an id")]
    [InlineData("""{ "Format": "startup-profiles-backup", "Version": 1, "Profiles": [ { "Id": "", "Name": "A" } ] }""", "no id or name")]
    [InlineData("""{ "Format": "startup-profiles-backup", "Version": 1, "Library": [ { "Id": "x", "Name": "A" }, { "Id": "x", "Name": "B" } ] }""", "repeated id")]
    public void Plan_RefusesFilesItCannotRestore_WithoutChangingAnything(string json, string reason)
    {
        SeedSetup();

        var error = Assert.Throws<BackupException>(() => Service().Plan(json));

        Assert.Contains(reason, error.Message, StringComparison.Ordinal);
        Assert.Equal("Work", _profiles.Find("work")!.Name);
    }

    [Fact]
    public void AFailedRestore_PutsTheEarlierStateBack()
    {
        SeedSetup();
        var backup = Service().Export();
        _profiles.Save(new Profile { Id = "extra", Name = "Extra" });
        var failing = new FailingProfileStore(_profiles) { FailOnSaveId = "work" };

        var service = Service(failing);
        var plan = service.Plan(backup);
        failing.Armed = true;

        Assert.Throws<BackupException>(() => service.Restore(plan));
        failing.Armed = false;

        Assert.NotNull(_profiles.Find("extra"));
        Assert.NotNull(_profiles.Find("work"));
    }

    /// <summary>Compares profiles by value, including their action lists.</summary>
    private sealed class ProfileComparer : IEqualityComparer<Profile?>
    {
        public static readonly ProfileComparer Instance = new();

        public bool Equals(Profile? x, Profile? y) =>
            x is not null && y is not null && x with { Actions = [], Conditions = [] } == y with { Actions = [], Conditions = [] } &&
            x.Actions.SequenceEqual(y.Actions);

        public int GetHashCode(Profile? obj) => obj?.Id.GetHashCode(StringComparison.Ordinal) ?? 0;
    }

    /// <summary>Wraps a store and throws on one profile's save once armed, to exercise the rollback.</summary>
    private sealed class FailingProfileStore(IProfileStore inner) : IProfileStore
    {
        public bool Armed { get; set; }
        public string? FailOnSaveId { get; init; }

        public IReadOnlyList<Profile> GetAll() => inner.GetAll();
        public Profile? Find(string id) => inner.Find(id);
        public void Remove(string id) => inner.Remove(id);

        public void Save(Profile profile)
        {
            if (Armed && profile.Id == FailOnSaveId)
            {
                Armed = false; // fail once; the rollback's own writes go through
                throw new IOException("Disk full.");
            }

            inner.Save(profile);
        }
    }
}
