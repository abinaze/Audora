namespace Audora.Core.Cleanup;

/// <summary>The noise removal engines a microphone can use, as they are saved in settings.json.</summary>
public static class NoiseEngines
{
    /// <summary>RNNoise: small and light, and always part of Audora.</summary>
    public const string Standard = "Standard";

    /// <summary>DeepFilterNet3: much cleaner in the lab, uses more of the processor and needs its own files.</summary>
    public const string Studio = "Studio";

    /// <summary>What a microphone uses when nothing is saved. Studio quietly gives way to Standard when its files are missing.</summary>
    public const string Default = Studio;

    /// <summary>Turns a saved value into one of the two names. Anything else becomes the default.</summary>
    public static string Normalize(string? value)
    {
        var text = value?.Trim();
        if (string.Equals(text, Standard, StringComparison.OrdinalIgnoreCase))
        {
            return Standard;
        }

        if (string.Equals(text, Studio, StringComparison.OrdinalIgnoreCase))
        {
            return Studio;
        }

        return Default;
    }
}
