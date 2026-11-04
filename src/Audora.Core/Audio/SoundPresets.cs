namespace Audora.Core.Audio;

public sealed record SoundPreset(string Name, float[] Bands);

/// <summary>
/// Starting points for common listening styles (bands: 31, 62, 125, 250, 500, 1k, 2k, 4k, 8k, 16k Hz, in dB).
/// They are gentle on purpose. Adjust by ear.
/// </summary>
public static class SoundPresets
{
    public const string OriginalName = "Original";

    public static IReadOnlyList<SoundPreset> All { get; } = new[]
    {
        new SoundPreset(OriginalName, new float[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
        new SoundPreset("Bass Boost", new float[] { 5, 6, 4, -1, -1, 0, 0, 0, 0, 0 }),
        new SoundPreset("Treble Boost", new float[] { 0, 0, 0, 0, 0, 0, 1, 3, 2, 2 }),
        new SoundPreset("Vocal", new float[] { -3, -2, -2, -1, 0, 1, 3, 3, 0, -1 }),
        new SoundPreset("V-Shape", new float[] { 3, 3, 1, -1, -2, -2, -2, 1, 3, 3 }),
        new SoundPreset("Rock", new float[] { 4, 3, 1, 2, 1, -1, 1, 3, 4, 4 }),
        new SoundPreset("Metal", new float[] { 4, 3, 0, -2, 0, 1, 3, 3, 2, 1 }),
        new SoundPreset("Pop", new float[] { -1, 0, 2, 3, 3, 2, 0, -1, -1, -2 }),
        new SoundPreset("Jazz", new float[] { 2, 2, 1, 1, -1, -1, 0, 1, 2, 3 }),
        new SoundPreset("Classical", new float[] { 2, 2, 1, 0, 0, 0, 0, 1, 2, 2 }),
        new SoundPreset("Dance", new float[] { 6, 5, 3, 0, 0, -1, -2, 0, 3, 4 }),
        new SoundPreset("Hip-Hop", new float[] { 5, 5, 3, 1, 0, -1, 0, 1, 2, 3 }),
        new SoundPreset("Loudness", new float[] { 4, 3, 0, 0, 0, 0, 0, 0, 2, 3 }),
    };

    /// <summary>Lowers the overall level a little when bands are boosted, so presets stay clean.</summary>
    public static float PreampFor(IEnumerable<float> bands) =>
        -MathF.Round(Math.Max(0f, bands.Max()) * 0.6f);

    public static EqSettings ToSettings(SoundPreset preset) =>
        preset.Name == OriginalName
            ? EqSettings.Original
            : new EqSettings(true, PreampFor(preset.Bands), preset.Bands, 0f, 0f, 1f);
}
