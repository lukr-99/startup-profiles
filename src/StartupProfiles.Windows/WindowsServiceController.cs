using System.ServiceProcess;
using StartupProfiles.Core.Actions;
using CoreServiceController = StartupProfiles.Core.Windows.IServiceController;

namespace StartupProfiles.Windows;

/// <summary>Starts a Windows service via <see cref="ServiceController"/>, waiting up to a fixed timeout.</summary>
public sealed class WindowsServiceController : CoreServiceController
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(30);

    public ActionResult StartService(string serviceName)
    {
        try
        {
            using var service = new ServiceController(serviceName);
            if (service.Status is ServiceControllerStatus.Running)
                return ActionResult.Ok($"Service '{serviceName}' already running.");

            if (service.Status is not (ServiceControllerStatus.StartPending or ServiceControllerStatus.ContinuePending))
                service.Start();

            service.WaitForStatus(ServiceControllerStatus.Running, StartTimeout);
            return ActionResult.Ok($"Started service '{serviceName}'.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ServiceProcess.TimeoutException)
        {
            return ActionResult.Fail($"Failed to start service '{serviceName}': {ex.Message}");
        }
    }
}
