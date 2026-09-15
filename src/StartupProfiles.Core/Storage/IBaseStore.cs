using StartupProfiles.Core.Models;

namespace StartupProfiles.Core.Storage;

/// <summary>Persistence for the <see cref="StartupBase"/> (base.json).</summary>
public interface IBaseStore
{
    /// <summary>The current base; empty when none has been saved.</summary>
    StartupBase Load();

    void Save(StartupBase value);
}
