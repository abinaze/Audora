namespace Audora.Core.Audio;

public sealed partial class OutputChannel
{
    /// <summary>The sample rate of the sound this device receives (the sound of the Windows default output).</summary>
    public int SourceSampleRate { get; }

    /// <summary>True when noise removal can run on the sound this device receives.</summary>
    public bool NoiseRemovalSupported => _noise.IsSupported;

    /// <summary>
    /// True when the sound is converted to 48 kHz and back for noise removal. That is decided when the output is
    /// built, so turning noise removal on or off for such a device means building the output again.
    /// </summary>
    public bool NoiseRemovalResampled => _noise.IsResampled;

    /// <summary>True when noise removal could not start or stopped working. The sound then plays untouched.</summary>
    public bool NoiseRemovalFailed => _noise.Failed;

    /// <summary>Why noise removal is not available on this device right now, or null.</summary>
    public string? NoiseRemovalMessage => _noise.UnavailableReason;

    /// <summary>Turns noise removal on or off. Strength 0 keeps the original sound, 1 plays only the cleaned sound.</summary>
    public void ApplyNoiseRemoval(bool enabled, float strength)
    {
        _noise.Strength = strength;
        _noise.Enabled = enabled;
    }
}
