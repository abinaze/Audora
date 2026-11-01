namespace Audora.Core.Devices;

/// <summary>A snapshot of one Windows audio output device.</summary>
public sealed record AudioDeviceInfo(
    string Id,
    string Name,
    ConnectionType ConnectionType,
    DeviceStatus Status,
    bool IsDefault);
