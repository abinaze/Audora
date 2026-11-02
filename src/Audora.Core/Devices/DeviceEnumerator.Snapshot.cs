using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace Audora.Core.Devices;

public sealed partial class DeviceEnumerator
{
    private const int SnapshotAttempts = 3;
    private const int SnapshotRetryDelayMilliseconds = 150;

    /// <summary>Every device Windows knows, including disconnected and remembered ones (slower).</summary>
    public IReadOnlyList<AudioDeviceInfo> GetRenderDevices() => Snapshot(DeviceState.All);

    /// <summary>Only devices that are connected and working right now (fast; used by the audio engine).</summary>
    public IReadOnlyList<AudioDeviceInfo> GetActiveRenderDevices() => Snapshot(DeviceState.Active);

    // While Windows is in the middle of a change the list can briefly come back empty or fail,
    // so this retries a few times.
    private IReadOnlyList<AudioDeviceInfo> Snapshot(DeviceState states)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                IReadOnlyList<AudioDeviceInfo> devices;
                lock (_sync)
                {
                    devices = ReadRenderDevices(states);
                }

                if (devices.Count > 0 || attempt == SnapshotAttempts)
                {
                    return devices;
                }
            }
            catch (COMException) when (attempt < SnapshotAttempts)
            {
                // Windows is mid-change; wait and try again.
            }

            Thread.Sleep(SnapshotRetryDelayMilliseconds);
        }
    }
}
