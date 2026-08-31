namespace StartupProfiles.Core.Actions;

/// <summary>A request to start a process, built by a handler and carried out by an <see cref="IProcessLauncher"/>.</summary>
public sealed record ProcessLaunchSpec
{
    public required string FileName { get; init; }
    public string? Arguments { get; init; }
    public bool UseShellExecute { get; init; }
    public bool CreateNoWindow { get; init; }

    /// <summary>Shell verb (e.g. "runas" for elevation). Requires <see cref="UseShellExecute"/>.</summary>
    public string? Verb { get; init; }
}
