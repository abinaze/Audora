using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace Audora.Core.Devices;

/// <summary>
/// Lists Windows audio output devices and raises <see cref="DevicesChanged"/>
/// when devices are added, removed, connected, disconnected or the default changes.
/// Events can arrive on any thread; UI code must marshal to its own thread.
/// </summary>
public sealed partial class DeviceEnumerator : IDisposable
{
    private static readonly Guid FormFactorFormat = new("1da5d803-d492-4edd-8c23-e0c0ffee7f0e");
    private const int FormFactorPid = 0;
    private const int DebounceMilliseconds = 300;

    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly NotificationClient _client;
    private readonly System.Threading.Timer _debounce;
    private readonly object _sync = new();
    private readonly object _raiseLock = new();
    private bool _disposed;

    public event EventHandler? DevicesChanged;

    public DeviceEnumerator()
    {
        _debounce = new System.Threading.Timer(_ =>
        {
            if (!_disposed)
            {
                lock (_raiseLock) { DevicesChanged?.Invoke(this, EventArgs.Empty); }
            }
        });
        _client = new NotificationClient(() =>
        {
            if (!_disposed)
            {
                _debounce.Change(DebounceMilliseconds, Timeout.Infinite);
            }
        });
        _enumerator.RegisterEndpointNotificationCallback(_client);
    }

    private IReadOnlyList<AudioDeviceInfo> ReadRenderDevices(DeviceState states)
    {
        var defaultId = GetDefaultRenderId();
        var result = new List<AudioDeviceInfo>();

        foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, states))
        {
            using (device)
            {
                var name = SafeName(device);
                var type = DeviceClassifier.Classify(ReadFormFactor(device), name, ReadStringProperties(device));
                result.Add(new AudioDeviceInfo(
                    device.ID,
                    name,
                    type,
                    MapState(device.State),
                    string.Equals(device.ID, defaultId, StringComparison.OrdinalIgnoreCase)));
            }
        }

        return result;
    }

    /// <summary>Raw property dump of every output device, for troubleshooting classification.</summary>
    public IReadOnlyList<string> DumpProperties()
    {
        var lines = new List<string>();

        foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.All))
        {
            using (device)
            {
                lines.Add($"== {SafeName(device)}  [{device.State}]");
                lines.Add($"   id: {device.ID}");

                try
                {
                    var props = device.Properties;
                    for (var i = 0; i < props.Count; i++)
                    {
                        try
                        {
                            var p = props[i];
                            lines.Add($"   {p.Key.formatId:B},{p.Key.propertyId} = {FormatValue(p.Value)}");
                        }
                        catch (Exception ex)
                        {
                            lines.Add($"   property #{i}: unreadable ({ex.GetType().Name})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    lines.Add($"   properties unavailable ({ex.GetType().Name})");
                }

                lines.Add(string.Empty);
            }
        }

        return lines;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try { _enumerator.UnregisterEndpointNotificationCallback(_client); } catch { }
        try { _debounce.Dispose(); } catch { }
        try { _enumerator.Dispose(); } catch { }
    }

    private string? GetDefaultRenderId()
    {
        try
        {
            using var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            return device.ID;
        }
        catch
        {
            return null; // no active output device
        }
    }

    private static string SafeName(MMDevice device)
    {
        try { return device.FriendlyName; }
        catch { return device.ID; }
    }

    private static int? ReadFormFactor(MMDevice device)
    {
        try
        {
            var key = new PropertyKey(FormFactorFormat, FormFactorPid);
            var props = device.Properties;
            if (props.Contains(key))
            {
                return Convert.ToInt32(props[key].Value);
            }
        }
        catch
        {
            // fall through: unknown form factor
        }

        return null;
    }

    private static List<string> ReadStringProperties(MMDevice device)
    {
        var strings = new List<string>();

        try
        {
            var props = device.Properties;
            for (var i = 0; i < props.Count; i++)
            {
                try
                {
                    var value = props[i].Value;
                    if (value is string s && s.Length is > 0 and < 500)
                    {
                        strings.Add(s);
                    }
                    else if (value is string[] many)
                    {
                        strings.AddRange(many);
                    }
                }
                catch
                {
                    // skip unreadable property
                }
            }
        }
        catch
        {
            // no property store
        }

        return strings;
    }

    private static string FormatValue(object? value) => value switch
    {
        null => "(null)",
        string[] many => string.Join("; ", many),
        _ => value.ToString() ?? "(null)"
    };

    private static DeviceStatus MapState(DeviceState state) => state switch
    {
        DeviceState.Active => DeviceStatus.Active,
        DeviceState.Disabled => DeviceStatus.Disabled,
        DeviceState.NotPresent => DeviceStatus.NotPresent,
        DeviceState.Unplugged => DeviceStatus.Unplugged,
        _ => DeviceStatus.NotPresent
    };

    private sealed class NotificationClient : IMMNotificationClient
    {
        private readonly Action _onChange;

        public NotificationClient(Action onChange) => _onChange = onChange;

        public void OnDeviceStateChanged(string deviceId, DeviceState newState) => _onChange();
        public void OnDeviceAdded(string pwstrDeviceId) => _onChange();
        public void OnDeviceRemoved(string deviceId) => _onChange();
        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId) => _onChange();
        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { } // too noisy; ignored
    }
}
