using Audora.Core.Cleanup;

namespace Audora.Core.Settings;

/// <summary>What Audora remembers about one microphone, found again by its Windows endpoint id.</summary>
public sealed class SavedMicrophone
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DateTime LastSeenUtc { get; set; }

    /// <summary>Noise removal was on for this microphone. Off unless the user switches it on.</summary>
    public bool NoiseRemoval { get; set; }

    /// <summary>How much of the sound is the cleaned one, 0 to 100.</summary>
    public double NoiseStrength { get; set; } = 100;

    /// <summary>
    /// Which engine cleans this microphone: "Standard" (RNNoise) or "Studio" (DeepFilterNet3). Studio gives way to
    /// Standard when its files are missing. Older settings files have no value and load as Studio.
    /// </summary>
    public string NoiseEngine { get; set; } = NoiseEngines.Default;
}
