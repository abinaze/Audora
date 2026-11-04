namespace Audora.Core.Devices;

/// <summary>Decides which devices are worth showing: connected now, one entry per physical device.</summary>
public static class DeviceCatalog
{
    public static IReadOnlyList<AudioDeviceInfo> Visible(IEnumerable<AudioDeviceInfo> devices)
    {
        var active = devices
            .Where(d => d.Status == DeviceStatus.Active && !string.IsNullOrWhiteSpace(d.Name))
            .ToList();

        // A Bluetooth device shows up twice: stereo ("Headphones (X)") and phone-call mode
        // ("Headset (X Hands-Free)"). Keep the call entry only if it is the only one.
        var stereo = active
            .Where(d => !IsHandsFree(d.Name))
            .Select(d => BaseName(d.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return active
            .Where(d => !IsHandsFree(d.Name) || !stereo.Contains(BaseName(d.Name)))
            .ToList();
    }

    public static bool IsHandsFree(string name) =>
        name.Contains("Hands-Free", StringComparison.OrdinalIgnoreCase);

    /// <summary>"Headset (MT Hands-Free)" and "Headphones (MT)" both give "MT".</summary>
    public static string BaseName(string name)
    {
        var open = name.IndexOf('(');
        var close = name.LastIndexOf(')');
        var inner = open >= 0 && close > open ? name.Substring(open + 1, close - open - 1) : name;

        var cut = inner.IndexOf("Hands-Free", StringComparison.OrdinalIgnoreCase);
        if (cut >= 0)
        {
            inner = inner[..cut];
        }

        return inner.Trim();
    }
}
