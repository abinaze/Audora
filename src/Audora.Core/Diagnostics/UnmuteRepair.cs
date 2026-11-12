using Audora.Core.Devices;

namespace Audora.Core.Diagnostics;

/// <summary>Reads and sets the Windows mute of a device. A small seam so the repair can be tested.</summary>
public interface IMuteAccess
{
    bool? GetMuted(string deviceId);

    void SetMuted(string deviceId, bool muted);
}

public sealed class EndpointMuteAccess : IMuteAccess
{
    public bool? GetMuted(string deviceId) => EndpointVolume.GetMuted(deviceId);

    public void SetMuted(string deviceId, bool muted) => EndpointVolume.SetMuted(deviceId, muted);
}

public sealed record RepairResult(bool Success, string Message, bool? PreviousMuted);

/// <summary>
/// Turns off the Windows mute of one device, then checks it. It remembers what it was so it can be undone.
/// It changes nothing else and needs no administrator rights.
/// </summary>
public sealed class UnmuteRepair
{
    private readonly IMuteAccess _access;

    public UnmuteRepair(IMuteAccess access) => _access = access;

    public RepairResult Apply(string deviceId, string deviceName)
    {
        var before = _access.GetMuted(deviceId);
        if (before is null)
        {
            return new RepairResult(false, $"The mute state of {deviceName} could not be read. Nothing was changed.", null);
        }

        if (before == false)
        {
            return new RepairResult(true, $"{deviceName} is not muted. Nothing was changed.", false);
        }

        try
        {
            _access.SetMuted(deviceId, false);
        }
        catch (Exception)
        {
            return new RepairResult(false, $"{deviceName} could not be unmuted. Nothing was changed.", true);
        }

        return _access.GetMuted(deviceId) == false
            ? new RepairResult(true, $"{deviceName} is no longer muted. You can undo this.", true)
            : new RepairResult(false, $"Unmute was requested, but Windows still reports {deviceName} as muted.", true);
    }

    /// <summary>Puts the mute back as it was. Returns true when Windows confirms it.</summary>
    public bool Undo(string deviceId, bool previousMuted)
    {
        try
        {
            _access.SetMuted(deviceId, previousMuted);
            return _access.GetMuted(deviceId) == previousMuted;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
