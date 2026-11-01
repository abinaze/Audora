namespace Audora.Core.Audio;

/// <summary>Per-device controls remembered across disconnects, reconnects and default changes.</summary>
public sealed class DeviceSettings
{
    public float Volume { get; set; } = 1f;

    public bool Muted { get; set; }

    public float Balance { get; set; }

    public int DelayMilliseconds { get; set; }
}
