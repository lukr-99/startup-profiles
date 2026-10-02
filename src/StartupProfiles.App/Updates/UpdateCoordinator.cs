using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using DotNetLib.Core.Mvvm;
using DotNetLib.Core.Updating;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.App.Updates;

/// <summary>
/// Self-update, following CodePrint's update seam: DotNetLib's <see cref="IReleaseSource"/> finds the latest
/// release, <see cref="ReleaseVersion"/> decides whether it is newer, the installer asset is downloaded to a
/// private folder and checked against the <c>.sha256</c> file published next to it, and only then is the
/// installer started. The app exits only once the installer is running (<see cref="ExitRequested"/>). WPF-free,
/// so the Settings tab and the tray only show its state and ask for consent.
/// </summary>
public sealed class UpdateCoordinator : ObservableObject
{
    /// <summary>The release page, the manual download path when the updater cannot help.</summary>
    public const string ReleasesPage = "https://github.com/lukr-99/startup-profiles/releases";

    /// <summary>Arguments for a silent upgrade; the installer relaunches the app when it is done.</summary>
    public const string InstallerArguments = "/SILENT /SUPPRESSMSGBOXES /NORESTART";

    /// <summary>The automatic check runs at most once in this interval.</summary>
    public static readonly TimeSpan AutoCheckInterval = TimeSpan.FromHours(20);

    private readonly IReleaseSource _source;
    private readonly HttpClient _http;
    private readonly IConfigStore _config;
    private readonly Func<string, string, bool> _startInstaller;
    private readonly Func<DateTimeOffset> _now;
    private readonly string _downloadDirectory;
    private ReleaseInfo? _available;
    private bool _isBusy;
    private string? _status;

    /// <param name="source">Where releases come from (GitHub Releases in the app).</param>
    /// <param name="http">Downloads the installer and its checksum.</param>
    /// <param name="currentVersion">The running version, like "0.1.0".</param>
    /// <param name="config">Holds the on/off setting and the time of the last check.</param>
    /// <param name="startInstaller">Starts the installer at a path with arguments; false if it did not start.</param>
    /// <param name="downloadDirectory">A private folder for downloads.</param>
    /// <param name="now">The clock; the system clock when omitted.</param>
    public UpdateCoordinator(IReleaseSource source, HttpClient http, string currentVersion, IConfigStore config,
        Func<string, string, bool> startInstaller, string downloadDirectory, Func<DateTimeOffset>? now = null)
    {
        _source = source;
        _http = http;
        CurrentVersion = currentVersion;
        _config = config;
        _startInstaller = startInstaller;
        _downloadDirectory = downloadDirectory;
        _now = now ?? (() => DateTimeOffset.Now);
    }

    public string CurrentVersion { get; }

    /// <summary>A newer release that can be installed, or null.</summary>
    public ReleaseInfo? Available
    {
        get => _available;
        private set { if (SetProperty(ref _available, value)) OnPropertyChanged(nameof(AvailableVersion)); }
    }

    public string? AvailableVersion => Available?.Version.TrimStart('v', 'V');

    /// <summary>True while a check or an install runs.</summary>
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }

    /// <summary>What happened last, in plain words.</summary>
    public string? Status { get => _status; private set => SetProperty(ref _status, value); }

    /// <summary>Whether the app checks once a day on its own (on unless turned off).</summary>
    public bool AutoCheck
    {
        get => !string.Equals(_config.GetValue(AppSettingKeys.CheckForUpdates), "false", StringComparison.OrdinalIgnoreCase);
        set
        {
            if (value == AutoCheck) return;
            _config.SetValue(AppSettingKeys.CheckForUpdates, value ? null : "false");
            OnPropertyChanged();
        }
    }

    /// <summary>When the last check ran, or null if never.</summary>
    public DateTimeOffset? LastChecked =>
        DateTimeOffset.TryParse(_config.GetValue(AppSettingKeys.LastUpdateCheck), CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var at) ? at : null;

    /// <summary>Raised once the installer is running, so the app can exit and let it replace the files.</summary>
    public event Action? ExitRequested;

    /// <summary>True when the automatic check is on and the last check is older than <see cref="AutoCheckInterval"/>.</summary>
    public bool IsAutoCheckDue => AutoCheck && (LastChecked is not { } last || _now() - last >= AutoCheckInterval);

    /// <summary>Asks for the latest release. Never throws; the outcome is in <see cref="Available"/> and <see cref="Status"/>.</summary>
    public async Task CheckAsync(CancellationToken ct = default)
    {
        if (IsBusy) return;
        IsBusy = true;
        Status = "Checking for updates...";
        try
        {
            var latest = await _source.GetLatestAsync(ct).ConfigureAwait(true);
            _config.SetValue(AppSettingKeys.LastUpdateCheck, _now().ToString("O", CultureInfo.InvariantCulture));
            OnPropertyChanged(nameof(LastChecked));

            if (latest is null)
            {
                Status = "Could not reach the release page. Check your connection, or download it yourself.";
                return;
            }

            Available = ReleaseVersion.IsNewer(latest.Version, CurrentVersion) ? latest : null;
            Status = Available is null
                ? $"You have the latest version ({CurrentVersion})."
                : $"Version {AvailableVersion} is available.";
        }
        catch (OperationCanceledException)
        {
            Status = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Downloads the available installer, checks its SHA-256, starts it, and raises <see cref="ExitRequested"/>.
    /// Nothing is started if the download fails or the checksum is missing or wrong.
    /// </summary>
    public async Task InstallAsync(CancellationToken ct = default)
    {
        if (IsBusy || Available is not { } release) return;
        IsBusy = true;
        Status = $"Downloading version {AvailableVersion}...";
        string? path = null;
        try
        {
            Directory.CreateDirectory(_downloadDirectory);
            var name = Path.GetFileName(new Uri(release.DownloadUrl).LocalPath);
            path = Path.Combine(_downloadDirectory, string.IsNullOrEmpty(name) ? $"StartupProfiles-Setup-{AvailableVersion}.exe" : name);
            await new UpdateService(_source, CurrentVersion, _http).DownloadAsync(release, path, ct).ConfigureAwait(true);

            var expected = await ExpectedHashAsync(release, ct).ConfigureAwait(true);
            string actual;
            await using (var file = File.OpenRead(path))
                actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(file, ct).ConfigureAwait(true));

            if (expected is null || !string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(path);
                Status = expected is null
                    ? "The release has no checksum, so it was not installed. Download it yourself from the release page."
                    : "The download did not match its checksum, so it was not installed. Try again later.";
                return;
            }

            Status = "Starting the installer...";
            if (!_startInstaller(path, InstallerArguments))
            {
                Status = "The installer did not start. Download it yourself from the release page.";
                return;
            }

            ExitRequested?.Invoke();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            if (path is not null && File.Exists(path)) File.Delete(path);
            Status = $"The update could not be downloaded ({ex.Message}). Try again later.";
        }
        catch (OperationCanceledException)
        {
            if (path is not null && File.Exists(path)) File.Delete(path);
            Status = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // The release publishes "<installer>.sha256" next to the installer, in sha256sum format: "<hash>  <name>".
    private async Task<string?> ExpectedHashAsync(ReleaseInfo release, CancellationToken ct)
    {
        try
        {
            var text = await _http.GetStringAsync(release.DownloadUrl + ".sha256", ct).ConfigureAwait(true);
            var hash = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return hash is { Length: 64 } && hash.All(Uri.IsHexDigit) ? hash : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}
