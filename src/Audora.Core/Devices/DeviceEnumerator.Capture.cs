using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace Audora.Core.Devices;

public sealed partial class DeviceEnumerator
{
    /// <summary>
    /// Every microphone and other input Windows knows, including disconnected ones.
    /// Read only. Used by diagnostics; shares this class's name cache and enumerator.
    /// </summary>
    public IReadOnlyList<AudioDeviceInfo> GetCaptureDevices()
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return ReadCaptureDevices();
            }
            catch (COMException) when (attempt < SnapshotAttempts)
            {
                Thread.Sleep(SnapshotRetryDelayMilliseconds);
            }
        }
    }

    private IReadOnlyList<AudioDeviceInfo> ReadCaptureDevices()
    {
        var defaultId = GetDefaultCaptureId();
        var result = new List<AudioDeviceInfo>();

        foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.All))
        {
            using (device)
            {
                result.Add(new AudioDeviceInfo(
                    device.ID,
                    NameFor(device),
                    ConnectionType.Unknown,
                    MapState(device.State),
                    string.Equals(device.ID, defaultId, StringComparison.OrdinalIgnoreCase)));
            }
        }

        return result;
    }

    private string? GetDefaultCaptureId()
    {
        try
        {
            using var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
            return device.ID;
        }
        catch
        {
            return null; // no active input device
        }
    }
}
