using StartupProfiles.Core.Startup;

namespace StartupProfiles.Core.Storage;

/// <summary>
/// Persistence for the startup entries Startup Profiles switched off (startup-takeover.json), so uninstall
/// can switch exactly those back on.
/// </summary>
public interface IStartupTakeoverStore
{
    IReadOnlyList<StartupEntry> Load();
    void Save(IReadOnlyList<StartupEntry> entries);
    void Clear();
}
