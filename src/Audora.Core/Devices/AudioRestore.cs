using Audora.Core.Settings;

namespace Audora.Core.Devices;

/// <summary>
/// Remembers how Windows audio looks (default output, its volume and mute) and puts it back on request.
/// It only changes things when Restore is called, which the user confirms first.
/// </summary>
public static class AudioRestore
{
    public static SavedRestorePoint? Capture(DeviceEnumerator devices)
    {
        var current = devices.GetActiveRenderDevices().FirstOrDefault(d => d.IsDefault);
        if (current is null)
        {
            return null;
        }

        var volume = EndpointVolume.GetVolume(current.Id);
        var muted = EndpointVolume.GetMuted(current.Id);

        return new SavedRestorePoint
        {
            DefaultDeviceId = current.Id,
            DefaultDeviceName = current.Name,
            Volume = volume is null ? -1 : Math.Round(volume.Value * 100),
            Muted = muted ?? false,
            TakenUtc = DateTime.UtcNow,
        };
    }

    /// <summary>Finds the saved device among the connected ones, by id first, then by name (USB ports change the id).</summary>
    public static AudioDeviceInfo? Resolve(SavedRestorePoint point, IEnumerable<AudioDeviceInfo> active)
    {
        var list = active.ToList();
        return list.FirstOrDefault(d => string.Equals(d.Id, point.DefaultDeviceId, StringComparison.OrdinalIgnoreCase))
            ?? list.FirstOrDefault(d => DeviceMatcher.Normalize(d.Name) == DeviceMatcher.Normalize(point.DefaultDeviceName));
    }

    /// <summary>Puts the saved default output, volume and mute back. Returns a plain-English result.</summary>
    public static string Restore(DeviceEnumerator devices, SavedRestorePoint point)
    {
        var target = Resolve(point, devices.GetActiveRenderDevices());
        if (target is null)
        {
            return $"The device that was your default ({point.DefaultDeviceName}) is not connected right now, so nothing was changed.";
        }

        var steps = new List<string>();
        try
        {
            if (!target.IsDefault)
            {
                DefaultDeviceSwitcher.SetDefaultOutput(target.Id);
                steps.Add($"default output set back to {target.Name}");
            }
        }
        catch (Exception)
        {
            return "Windows would not change the default output. Choose it in Windows Sound settings instead.";
        }

        if (point.Volume >= 0)
        {
            EndpointVolume.SetVolume(target.Id, (float)(point.Volume / 100.0));
            steps.Add($"volume set to {point.Volume:0}%");
        }

        EndpointVolume.SetMuted(target.Id, point.Muted);
        steps.Add(point.Muted ? "muted" : "not muted");

        return "Restored: " + string.Join(", ", steps) + ".";
    }
}
