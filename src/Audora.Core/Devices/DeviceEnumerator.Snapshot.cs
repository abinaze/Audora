using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace Audora.Core.Devices;

public sealed partial class DeviceEnumerator
{
    private const int SnapshotAttempts = 3;
    private const int SnapshotRetryDelayMilliseconds = 150;

    // How a device is connected never changes, so it is worked out once and remembered.
    private readonly ConcurrentDictionary<string, ConnectionType> _types = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every device Windows knows, including disconnected and remembered ones (slow).</summary>
    public IReadOnlyList<AudioDeviceInfo> GetRenderDevices() => Snapshot(DeviceState.All, true);

    /// <summary>Connected devices only, without connection types. Fast; used by the audio engine.</summary>
    public IReadOnlyList<AudioDeviceInfo> GetActiveRenderDevices() => Snapshot(DeviceState.Active, false);

    /// <summary>Connected devices with connection types. The first call per device is slower; later calls are fast.</summary>
    public IReadOnlyList<AudioDeviceInfo> GetActiveRenderDevicesWithTypes() => Snapshot(DeviceState.Active, true);

    private ConnectionType TypeFor(MMDevice device, string name, bool classify)
    {
        var id = device.ID;
        if (_types.TryGetValue(id, out var known))
        {
            return known;
        }

        if (!classify)
        {
            return ConnectionType.Unknown;
        }

        var type = DeviceClassifier.Classify(ReadFormFactor(device), name, ReadStringProperties(device));
        _types[id] = type;
        return type;
    }

    // While Windows is in the middle of a change the list can briefly come back empty or fail,
    // so this retries a few times.
    private IReadOnlyList<AudioDeviceInfo> Snapshot(DeviceState states, bool classify)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var devices = ReadRenderDevices(states, classify);
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
