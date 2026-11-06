namespace Audora.Core.Audio;

/// <summary>
/// A sound profile: equalizer bands (31, 62, 125, 250, 500, 1k, 2k, 4k, 8k, 16k Hz, in dB) plus effects.
/// Voice clarity, stabilizer and virtualizer run from 0 to 1. Width is 1 for normal stereo.
/// </summary>
public sealed record SoundPreset(
    string Name,
    float[] Bands,
    string Description = "",
    float Bass = 0f,
    float Treble = 0f,
    float VoiceClarity = 0f,
    float Stabilizer = 0f,
    float Virtualizer = 0f,
    float Width = 1f);

/// <summary>
/// Profiles for the main ways people listen: music, movies, calls and games, in the style of Nahimic,
/// plus Smart, which picks one of them from the app you are using. They are starting points: adjust by ear.
/// </summary>
public static class SoundPresets
{
    public const string OriginalName = "Original";
    public const string MusicName = "Music";
    public const string MovieName = "Movie";
    public const string CommunicationName = "Communication";
    public const string GamingName = "Gaming";
    public const string SmartName = "Smart";

    public static IReadOnlyList<SoundPreset> All { get; } = new[]
    {
        new SoundPreset(OriginalName, new float[10],
            "No changes. Your audio plays exactly as it is."),

        new SoundPreset(MusicName, new float[] { 2, 2, 1, 0, 0, 0, 0, 1, 2, 2 },
            "Fuller bass, clearer highs and a slightly wider sound.",
            Bass: 1f, Treble: 1f, Width: 1.1f),

        new SoundPreset(MovieName, new float[] { 4, 3, 1, 0, 0, 1, 2, 2, 1, 1 },
            "Deeper bass, clearer dialogue and a wide sound. Evens out loud scenes and quiet speech.",
            Bass: 2f, VoiceClarity: 0.35f, Stabilizer: 0.4f, Virtualizer: 0.5f, Width: 1.25f),

        new SoundPreset(CommunicationName, new float[] { -6, -5, -3, -1, 0, 1, 2, 2, 0, -2 },
            "Clear, centred voices for calls and meetings. Cuts low rumble and evens out the volume.",
            VoiceClarity: 0.8f, Stabilizer: 0.6f, Width: 0f),

        new SoundPreset(GamingName, new float[] { 2, 2, 0, -1, 0, 1, 3, 4, 3, 2 },
            "Sharper footsteps and directions, with a wide, positional sound.",
            Treble: 1f, VoiceClarity: 0.2f, Stabilizer: 0.3f, Virtualizer: 0.7f, Width: 1.3f),

        new SoundPreset(SmartName, new float[10],
            "Picks Music, Movie, Communication or Gaming for you, based on the app you are using."),
    };

    /// <summary>Lowers the overall level a little when something is boosted, so profiles stay clean.</summary>
    public static float PreampFor(SoundPreset preset)
    {
        var peak = Math.Max(preset.Bands.Max(), Math.Max(preset.Bass, preset.Treble));
        return -MathF.Round(Math.Max(0f, peak) * 0.6f);
    }

    public static EqSettings ToSettings(SoundPreset preset) =>
        preset.Name is OriginalName or SmartName
            ? EqSettings.Original
            : new EqSettings(
                true,
                PreampFor(preset),
                preset.Bands,
                preset.Bass,
                preset.Treble,
                preset.Width,
                preset.Virtualizer,
                0f,
                ReverbKind.Room,
                preset.VoiceClarity,
                preset.Stabilizer);
}
