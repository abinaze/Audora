using Audora.Core.Devices;

namespace Audora.Core.Diagnostics;

/// <summary>
/// Quick scan: reads the audio services, sound outputs and microphones, then applies the rules.
/// It reuses the one device-detection engine and changes nothing in Windows.
/// </summary>
public sealed class AudioScanner
{
    private readonly DeviceEnumerator _devices;

    public AudioScanner(DeviceEnumerator devices) => _devices = devices;

    public ScanReport Run()
    {
        var input = new ScanInput(
            ServiceProbe.ReadAudioServices(),
            _devices.GetRenderDevices(),
            _devices.GetCaptureDevices());

        return new ScanReport(DateTime.Now, input, ScanEvaluator.Evaluate(input));
    }
}
