using StartupProfiles.Core.Models;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.Core.Tests.Integration;

/// <summary>In-memory <see cref="IBaseStore"/> so registrar tests stay deterministic and file-free.</summary>
internal sealed class FakeBaseStore : IBaseStore
{
    private StartupBase _value;

    public FakeBaseStore(params ProfileAction[] seed) => _value = new StartupBase { Actions = [.. seed] };

    public StartupBase Load() => _value;

    public void Save(StartupBase value) => _value = value;
}
