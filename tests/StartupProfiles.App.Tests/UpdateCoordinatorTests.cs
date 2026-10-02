using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using DotNetLib.Core.Updating;
using StartupProfiles.App.Updates;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.App.Tests;

public sealed class UpdateCoordinatorTests : IDisposable
{
    private const string InstallerUrl = "https://github.com/lukr-99/startup-profiles/releases/download/v0.2.0/StartupProfiles-Setup-0.2.0.exe";
    private static readonly byte[] Installer = Encoding.UTF8.GetBytes("pretend installer");
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 9, 0, 0, TimeSpan.FromHours(2));

    private readonly string _configFile = Path.Combine(Path.GetTempPath(), $"sp-upd-{Guid.NewGuid():N}.json");
    private readonly string _downloads = Path.Combine(Path.GetTempPath(), $"sp-upd-{Guid.NewGuid():N}");
    private readonly ConfigStore _config;
    private readonly FakeHandler _web = new();
    private readonly List<(string Path, string Arguments)> _started = [];
    private ReleaseInfo? _latest = new("v0.2.0", "v0.2.0", InstallerUrl, "Notes");
    private bool _installerStarts = true;
    private DateTimeOffset _now = Now;

    public UpdateCoordinatorTests()
    {
        _config = new ConfigStore(_configFile);
        _web.Responses[InstallerUrl] = Installer;
        _web.Responses[InstallerUrl + ".sha256"] = Encoding.UTF8.GetBytes(
            $"{Convert.ToHexStringLower(SHA256.HashData(Installer))}  StartupProfiles-Setup-0.2.0.exe\n");
    }

    public void Dispose()
    {
        File.Delete(_configFile);
        if (Directory.Exists(_downloads)) Directory.Delete(_downloads, recursive: true);
    }

    private UpdateCoordinator Coordinator(string current = "0.1.0") => new(
        new FakeSource(() => _latest), new HttpClient(_web), current, _config,
        (path, args) => { _started.Add((path, args)); return _installerStarts; }, _downloads, () => _now);

    [Fact]
    public async Task ANewerRelease_IsOffered()
    {
        var updates = Coordinator();

        await updates.CheckAsync();

        Assert.Equal("0.2.0", updates.AvailableVersion);
        Assert.Equal("Version 0.2.0 is available.", updates.Status);
        Assert.False(updates.IsBusy);
    }

    [Fact]
    public async Task TheSameOrAnOlderRelease_IsNotOffered()
    {
        var updates = Coordinator(current: "0.2.0");

        await updates.CheckAsync();

        Assert.Null(updates.Available);
        Assert.Contains("latest version", updates.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoAnswer_SaysTheReleasePageCouldNotBeReached()
    {
        _latest = null;
        var updates = Coordinator();

        await updates.CheckAsync();

        Assert.Null(updates.Available);
        Assert.Contains("Could not reach", updates.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheAutomaticCheck_IsDueOncePerInterval_AndCanBeTurnedOff()
    {
        var updates = Coordinator();
        Assert.True(updates.IsAutoCheckDue);

        await updates.CheckAsync();
        Assert.False(updates.IsAutoCheckDue);
        Assert.Equal(Now, updates.LastChecked);

        _now = Now + UpdateCoordinator.AutoCheckInterval;
        Assert.True(updates.IsAutoCheckDue);

        updates.AutoCheck = false;
        Assert.False(updates.IsAutoCheckDue);
        Assert.Equal("false", _config.GetValue(AppSettingKeys.CheckForUpdates));
    }

    [Fact]
    public async Task Install_VerifiesTheChecksum_StartsTheInstallerSilently_AndThenAsksToExit()
    {
        var updates = Coordinator();
        var exit = false;
        updates.ExitRequested += () => exit = true;
        await updates.CheckAsync();

        await updates.InstallAsync();

        var (path, args) = Assert.Single(_started);
        Assert.StartsWith(_downloads, path, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Installer, await File.ReadAllBytesAsync(path));
        Assert.Equal(UpdateCoordinator.InstallerArguments, args);
        Assert.True(exit);
    }

    [Fact]
    public async Task AWrongChecksum_StartsNothing_AndRemovesTheDownload()
    {
        _web.Responses[InstallerUrl + ".sha256"] = Encoding.UTF8.GetBytes(new string('0', 64) + "  x.exe");
        var updates = Coordinator();
        var exit = false;
        updates.ExitRequested += () => exit = true;
        await updates.CheckAsync();

        await updates.InstallAsync();

        Assert.Empty(_started);
        Assert.False(exit);
        Assert.Contains("did not match", updates.Status, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(_downloads));
    }

    [Fact]
    public async Task AMissingChecksum_StartsNothing()
    {
        _web.Responses.Remove(InstallerUrl + ".sha256");
        var updates = Coordinator();
        await updates.CheckAsync();

        await updates.InstallAsync();

        Assert.Empty(_started);
        Assert.Contains("no checksum", updates.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenTheInstallerDoesNotStart_TheAppStays()
    {
        _installerStarts = false;
        var updates = Coordinator();
        var exit = false;
        updates.ExitRequested += () => exit = true;
        await updates.CheckAsync();

        await updates.InstallAsync();

        Assert.False(exit);
        Assert.Contains("did not start", updates.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFailedDownload_IsReported_NotThrown()
    {
        _web.Responses.Remove(InstallerUrl);
        var updates = Coordinator();
        await updates.CheckAsync();

        await updates.InstallAsync();

        Assert.Empty(_started);
        Assert.Contains("could not be downloaded", updates.Status, StringComparison.Ordinal);
        Assert.False(updates.IsBusy);
    }

    private sealed class FakeSource(Func<ReleaseInfo?> latest) : IReleaseSource
    {
        public Task<ReleaseInfo?> GetLatestAsync(CancellationToken cancellationToken = default) => Task.FromResult(latest());
    }

    /// <summary>Serves canned bodies by URL; anything else is a 404.</summary>
    private sealed class FakeHandler : HttpMessageHandler
    {
        public Dictionary<string, byte[]> Responses { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Responses.TryGetValue(request.RequestUri!.ToString(), out var body)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
