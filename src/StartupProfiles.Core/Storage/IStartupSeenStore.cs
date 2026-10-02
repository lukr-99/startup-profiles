namespace StartupProfiles.Core.Storage;

/// <summary>
/// Persistence for the Windows startup entries Startup Profiles has already looked at (startup-seen.json), so a
/// new one can be told apart from one the user already decided about.
/// </summary>
public interface IStartupSeenStore
{
    /// <summary>The seen entry keys, or null when nothing was ever recorded (the first run).</summary>
    IReadOnlySet<string>? Load();

    void Save(IEnumerable<string> keys);
}
