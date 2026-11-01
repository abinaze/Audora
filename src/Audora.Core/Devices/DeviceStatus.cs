namespace Audora.Core.Devices;

/// <summary>Availability of an audio endpoint as reported by Windows.</summary>
public enum DeviceStatus
{
    Active,
    Disabled,
    NotPresent,
    Unplugged
}
