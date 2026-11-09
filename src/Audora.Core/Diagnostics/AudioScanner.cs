using Audora.Core.Devices;

namespace Audora.Core.Diagnostics;

/// <summary>
/// Quick scan: reads the audio services, sound outputs, microphones, driver status and sound format,
/// then applies the rules. It reuses the one device-detection engine and changes nothing in Windows.
/// </summary>
public sealed class AudioScanner
{
    private readonly DeviceEnumerator _devices;

    public AudioScanner(DeviceEnumerator devices) => _devices = devices;

    public ScanReport Run()
    {
        var details = EndpointDetailsReader.Build(_devices.GetActiveEndpointDetails(), PnpStatus.ProblemCode);

        var input = new ScanInput(
            ServiceProbe.ReadAudioServices(),
            _devices.GetRenderDevices(),
            _devices.GetCaptureDevices(),
            details);

        return new ScanReport(DateTime.Now, input, ScanEvaluator.Evaluate(input));
    }
}
