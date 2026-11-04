namespace Audora.Core.Audio;

/// <summary>Filter settings for one equalizer stage (Robert Bristow-Johnson's audio EQ cookbook).</summary>
public readonly record struct BiquadCoefficients(float B0, float B1, float B2, float A1, float A2)
{
    public static BiquadCoefficients Identity { get; } = new(1f, 0f, 0f, 0f, 0f);

    /// <summary>A bell-shaped boost or cut around one frequency.</summary>
    public static BiquadCoefficients Peaking(float sampleRate, float frequency, float gainDb, float q)
    {
        if (!Usable(sampleRate, frequency) || Math.Abs(gainDb) < 0.05f)
        {
            return Identity;
        }

        var a = Math.Pow(10.0, gainDb / 40.0);
        var w0 = 2.0 * Math.PI * frequency / sampleRate;
        var cos = Math.Cos(w0);
        var alpha = Math.Sin(w0) / (2.0 * q);

        return Normalize(
            1 + alpha * a, -2 * cos, 1 - alpha * a,
            1 + alpha / a, -2 * cos, 1 - alpha / a);
    }

    /// <summary>Boosts or cuts everything below the frequency.</summary>
    public static BiquadCoefficients LowShelf(float sampleRate, float frequency, float gainDb)
    {
        if (!Usable(sampleRate, frequency) || Math.Abs(gainDb) < 0.05f)
        {
            return Identity;
        }

        var a = Math.Pow(10.0, gainDb / 40.0);
        var w0 = 2.0 * Math.PI * frequency / sampleRate;
        var cos = Math.Cos(w0);
        var alpha = Math.Sin(w0) / 2.0 * Math.Sqrt(2.0);
        var k = 2.0 * Math.Sqrt(a) * alpha;

        return Normalize(
            a * ((a + 1) - (a - 1) * cos + k),
            2 * a * ((a - 1) - (a + 1) * cos),
            a * ((a + 1) - (a - 1) * cos - k),
            (a + 1) + (a - 1) * cos + k,
            -2 * ((a - 1) + (a + 1) * cos),
            (a + 1) + (a - 1) * cos - k);
    }

    /// <summary>Boosts or cuts everything above the frequency.</summary>
    public static BiquadCoefficients HighShelf(float sampleRate, float frequency, float gainDb)
    {
        if (!Usable(sampleRate, frequency) || Math.Abs(gainDb) < 0.05f)
        {
            return Identity;
        }

        var a = Math.Pow(10.0, gainDb / 40.0);
        var w0 = 2.0 * Math.PI * frequency / sampleRate;
        var cos = Math.Cos(w0);
        var alpha = Math.Sin(w0) / 2.0 * Math.Sqrt(2.0);
        var k = 2.0 * Math.Sqrt(a) * alpha;

        return Normalize(
            a * ((a + 1) + (a - 1) * cos + k),
            -2 * a * ((a - 1) + (a + 1) * cos),
            a * ((a + 1) + (a - 1) * cos - k),
            (a + 1) - (a - 1) * cos + k,
            2 * ((a - 1) - (a + 1) * cos),
            (a + 1) - (a - 1) * cos - k);
    }

    private static bool Usable(float sampleRate, float frequency) =>
        frequency > 10f && frequency < sampleRate * 0.45f;

    private static BiquadCoefficients Normalize(double b0, double b1, double b2, double a0, double a1, double a2) =>
        new((float)(b0 / a0), (float)(b1 / a0), (float)(b2 / a0), (float)(a1 / a0), (float)(a2 / a0));
}
