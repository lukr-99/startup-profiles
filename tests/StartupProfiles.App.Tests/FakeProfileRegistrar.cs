using StartupProfiles.App.Integration;
using StartupProfiles.Core.Integration;

namespace StartupProfiles.App.Tests;

/// <summary>Records what the registration view model applied, without touching any store or HTTP.</summary>
internal sealed class FakeProfileRegistrar : IProfileRegistrar
{
    private readonly RegistrationException? _throws;

    public FakeProfileRegistrar(RegistrationException? throws = null) => _throws = throws;

    public RegistrationRequest? AppliedRequest { get; private set; }
    public RegistrationTargets? AppliedTargets { get; private set; }

    public RegistrationOutcome Apply(RegistrationRequest request, RegistrationTargets targets)
    {
        if (_throws is not null) throw _throws;
        AppliedRequest = request;
        AppliedTargets = targets;
        return new RegistrationOutcome([.. targets.ProfileIds], [], [], "example-app", true, targets.IncludeBase);
    }
}
