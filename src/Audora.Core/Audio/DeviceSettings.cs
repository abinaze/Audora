namespace Audora.Core.Audio;

/// <summary>Per-device controls remembered across disconnects, reconnects and default changes.</summary>
public sealed class DeviceSettings
{
    public float Volume { get; set; } = 1f;

    public bool Muted { get; set; }

    public float Balance { get; set; }

    public int DelayMilliseconds { get; set; }

    /// <summary>Remove steady background noise (fans, hum, keyboards) from the voices in the sound this device plays.</summary>
    public bool NoiseRemoval { get; set; }

    /// <summary>0 keeps the original sound, 1 plays only the cleaned sound.</summary>
    public float NoiseStrength { get; set; } = 1f;
}
