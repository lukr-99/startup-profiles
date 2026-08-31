using StartupProfiles.Core.Actions;

namespace StartupProfiles.Core.Windows;

/// <summary>Starts a Windows service by name. Implemented by a platform adapter in StartupProfiles.Windows.</summary>
public interface IServiceController
{
    /// <summary>Starts the named service (a no-op success if it is already running).</summary>
    ActionResult StartService(string serviceName);
}
