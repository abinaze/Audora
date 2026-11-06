namespace Audora.Core.Audio;

/// <summary>The size and character of the reverb.</summary>
public enum ReverbKind
{
    Room,
    Hall,
    Stage
}

/// <summary>
/// Sound shaping applied to a device Audora plays on.
/// When Enabled is false (the "Original" setting) the audio is not touched at all.
/// </summary>
public sealed record EqSettings(
    bool Enabled,
    float PreampDb,
    IReadOnlyList<float> BandsDb,
    float BassDb,
    float TrebleDb,
    float StereoWidth,
    float Virtualizer = 0f,
    float ReverbAmount = 0f,
    ReverbKind Room = ReverbKind.Room,
    float VoiceClarity = 0f,
    float Stabilizer = 0f)
{
    /// <summary>Centre frequency of each of the 10 bands, in Hz.</summary>
    public static IReadOnlyList<float> BandFrequencies { get; } =
        new float[] { 31f, 62f, 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f, 16000f };

    public static EqSettings Original { get; } = new(false, 0f, new float[10], 0f, 0f, 1f);
}
