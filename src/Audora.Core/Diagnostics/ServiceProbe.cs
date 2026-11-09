using System.ComponentModel;
using System.ServiceProcess;
using StartMode = System.ServiceProcess.ServiceStartMode;

namespace Audora.Core.Diagnostics;

/// <summary>Reads the state of the Windows audio services. It never starts, stops or changes anything.</summary>
public static class ServiceProbe
{
    private const int ServiceDoesNotExist = 1060;

    private static readonly (string Name, string Display)[] AudioServices =
    {
        ("Audiosrv", "Windows Audio"),
        ("AudioEndpointBuilder", "Windows Audio Endpoint Builder")
    };

    public static IReadOnlyList<ServiceSnapshot> ReadAudioServices() =>
        AudioServices.Select(s => Read(s.Name, s.Display)).ToList();

    private static ServiceSnapshot Read(string name, string display)
    {
        try
        {
            using var controller = new ServiceController(name);
            return new ServiceSnapshot(name, display, true, MapState(controller.Status), MapStartup(controller.StartType));
        }
        catch (InvalidOperationException ex) when (ex.InnerException is Win32Exception win && win.NativeErrorCode == ServiceDoesNotExist)
        {
            return new ServiceSnapshot(name, display, false, ServiceRunState.Unknown, ServiceStartup.Unknown);
        }
        catch (Exception)
        {
            // The service exists but Windows would not tell us its state.
            return new ServiceSnapshot(name, display, true, ServiceRunState.Unknown, ServiceStartup.Unknown);
        }
    }

    private static ServiceRunState MapState(ServiceControllerStatus status) => status switch
    {
        ServiceControllerStatus.Running => ServiceRunState.Running,
        ServiceControllerStatus.Stopped => ServiceRunState.Stopped,
        ServiceControllerStatus.StartPending => ServiceRunState.StartPending,
        ServiceControllerStatus.StopPending => ServiceRunState.StopPending,
        ServiceControllerStatus.Paused => ServiceRunState.Paused,
        ServiceControllerStatus.PausePending => ServiceRunState.Paused,
        ServiceControllerStatus.ContinuePending => ServiceRunState.Paused,
        _ => ServiceRunState.Unknown
    };

    private static ServiceStartup MapStartup(StartMode mode) => mode switch
    {
        StartMode.Automatic => ServiceStartup.Automatic,
        StartMode.Boot => ServiceStartup.Automatic,
        StartMode.System => ServiceStartup.Automatic,
        StartMode.Manual => ServiceStartup.Manual,
        StartMode.Disabled => ServiceStartup.Disabled,
        _ => ServiceStartup.Unknown
    };
}
