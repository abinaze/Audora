using System.Collections.Concurrent;
using NAudio.CoreAudioApi;

namespace Audora.Core.Devices;

public sealed partial class DeviceEnumerator
{
    // Reading a device's friendly name from Windows is slow, so it is remembered and only
    // re-read when Windows reports that this device changed.
    private readonly ConcurrentDictionary<string, string> _nameCache = new(StringComparer.OrdinalIgnoreCase);

    private string NameFor(MMDevice device)
    {
        var id = device.ID;
        if (_nameCache.TryGetValue(id, out var cached))
        {
            return cached;
        }

        var name = SafeName(device);
        if (!string.Equals(name, id, StringComparison.Ordinal))
        {
            _nameCache[id] = name;
        }

        return name;
    }

    private void ForgetName(string deviceId) => _nameCache.TryRemove(deviceId, out _);
}
