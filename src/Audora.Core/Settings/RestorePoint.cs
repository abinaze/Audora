namespace Audora.Core.Settings;

/// <summary>What Windows audio looked like when Audora was turned on: the default output, its volume and mute.</summary>
public sealed class SavedRestorePoint
{
    public string DefaultDeviceId { get; set; } = string.Empty;

    public string DefaultDeviceName { get; set; } = string.Empty;

    /// <summary>0 to 100, or -1 when it could not be read.</summary>
    public double Volume { get; set; } = -1;

    public bool Muted { get; set; }

    public DateTime TakenUtc { get; set; }
}
