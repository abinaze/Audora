using Audora.Core.Diagnostics;
using NAudio.CoreAudioApi;

namespace Audora.Core.Devices;

public sealed partial class DeviceEnumerator
{
    private static readonly Guid EngineFormatGroup = new("f19f064d-082c-4e27-bc73-6882a1bb8e4c");
    private static readonly Guid EndpointGroup = new("1da5d803-d492-4edd-8c23-e0c0ffee7f0e");
    private static readonly Guid HardwareGroup = new("b3f8fa53-0004-438e-9003-51a46e139bfc");
    private static readonly Guid DriverRecordGroup = new("83da6326-97a6-4088-9453-a1923f573b29");

    /// <summary>
    /// Raw property values of every connected output and input. Read only: it opens the
    /// property store of each device and nothing else. Used by diagnostics.
    /// </summary>
    public IReadOnlyList<EndpointRaw> GetActiveEndpointDetails()
    {
        var result = new List<EndpointRaw>();
        ReadDetails(DataFlow.Render, GetDefaultRenderId(), result);
        ReadDetails(DataFlow.Capture, GetDefaultCaptureId(), result);
        return result;
    }

    private void ReadDetails(DataFlow flow, string? defaultId, List<EndpointRaw> into)
    {
        foreach (var device in _enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
        {
            using (device)
            {
                into.Add(new EndpointRaw(
                    NameFor(device),
                    flow == DataFlow.Render,
                    string.Equals(device.ID, defaultId, StringComparison.OrdinalIgnoreCase),
                    ReadProperty(device, EngineFormatGroup, 0) as byte[],
                    ToInt(ReadProperty(device, EndpointGroup, 5)),
                    ReadProperty(device, HardwareGroup, 2) as string,
                    ReadProperty(device, DriverRecordGroup, 3) as string));
            }
        }
    }

    private static object? ReadProperty(MMDevice device, Guid group, int id)
    {
        try
        {
            var key = new PropertyKey(group, id);
            var props = device.Properties;
            return props.Contains(key) ? props[key].Value : null;
        }
        catch
        {
            return null; // some properties cannot be read; that is fine
        }
    }

    private static int? ToInt(object? value)
    {
        try
        {
            return value is null ? null : Convert.ToInt32(value);
        }
        catch
        {
            return null;
        }
    }
}
