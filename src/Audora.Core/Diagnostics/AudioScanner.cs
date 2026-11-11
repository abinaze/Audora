using Audora.Core.Devices;

namespace Audora.Core.Diagnostics;

/// <summary>
/// Quick scan: reads the audio services, sound outputs, microphones, driver status, sound format, the Windows
/// Mono audio setting and muted devices, then applies the rules. It reuses the one device-detection engine and
/// changes nothing in Windows.
/// </summary>
public sealed class AudioScanner
{
    private readonly DeviceEnumerator _devices;

    public AudioScanner(DeviceEnumerator devices) => _devices = devices;

    public ScanReport Run()
    {
        var details = EndpointDetailsReader.Build(_devices.GetActiveEndpointDetails(), PnpStatus.ProblemCode);
        var render = _devices.GetRenderDevices();
        var capture = _devices.GetCaptureDevices();

        var input = new ScanInput(
            ServiceProbe.ReadAudioServices(),
            render,
            capture,
            details,
            MonoAudioProbe.Read(),
            ReadMutedIds(render.Concat(capture)));

        return new ScanReport(DateTime.Now, input, ScanEvaluator.Evaluate(input));
    }

    private static IReadOnlyCollection<string> ReadMutedIds(IEnumerable<AudioDeviceInfo> devices) =>
        devices
            .Where(d => d.Status == DeviceStatus.Active && EndpointVolume.GetMuted(d.Id) == true)
            .Select(d => d.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
