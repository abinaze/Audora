using System.Runtime.InteropServices;

namespace Audora.Core.Devices;

public sealed partial class DeviceEnumerator
{
    private const int SnapshotAttempts = 3;
    private const int SnapshotRetryDelayMilliseconds = 150;

    /// <summary>
    /// Returns every known output device. While Windows is in the middle of a change
    /// the device list can briefly come back empty or fail, so this retries a few times.
    /// </summary>
    public IReadOnlyList<AudioDeviceInfo> GetRenderDevices()
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                IReadOnlyList<AudioDeviceInfo> devices;
                lock (_sync)
                {
                    devices = ReadRenderDevices();
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
