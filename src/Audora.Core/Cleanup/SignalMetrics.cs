namespace Audora.Core.Cleanup;

/// <summary>Plain measurements for comparing a cleaned sound with the original. Used by the lab commands.</summary>
public static class SignalMetrics
{
    /// <summary>The similarity score is kept between minus and plus this many decibels.</summary>
    public const double SiSdrCap = 60.0;

    /// <summary>The average loudness (root mean square) of the samples.</summary>
    public static double Rms(ReadOnlySpan<float> samples)
    {
        if (samples.Length == 0)
        {
            return 0.0;
        }

        double sum = 0;
        foreach (var sample in samples)
        {
            sum += (double)sample * sample;
        }

        return Math.Sqrt(sum / samples.Length);
    }

    /// <summary>Decibels for a ratio of loudness (not power). Zero and bad numbers read about -180.</summary>
    public static double Db(double ratio) => 20.0 * Math.Log10(Math.Max(ratio, 1e-9));

    /// <summary>
    /// How close the estimate is to the reference, in decibels (scale-invariant SI-SDR). Higher is closer. Identical
    /// sound scores the top value, and a sound unrelated to the reference scores very low.
    /// </summary>
    public static double SiSdr(ReadOnlySpan<float> reference, ReadOnlySpan<float> estimate)
    {
        var n = Math.Min(reference.Length, estimate.Length);
        double dot = 0;
        double referenceEnergy = 0;
        for (var i = 0; i < n; i++)
        {
            dot += (double)reference[i] * estimate[i];
            referenceEnergy += (double)reference[i] * reference[i];
        }

        if (referenceEnergy <= 0)
        {
            return -SiSdrCap;
        }

        var alpha = dot / referenceEnergy;
        var targetEnergy = alpha * alpha * referenceEnergy;
        double noiseEnergy = 0;
        for (var i = 0; i < n; i++)
        {
            var error = estimate[i] - (alpha * reference[i]);
            noiseEnergy += error * error;
        }

        if (targetEnergy <= 0)
        {
            return -SiSdrCap;
        }

        var value = 10.0 * Math.Log10(targetEnergy / Math.Max(noiseEnergy, 1e-12));
        return Math.Clamp(value, -SiSdrCap, SiSdrCap);
    }

    /// <summary>
    /// How many samples the estimate lags behind the reference (0 to <paramref name="maxDelay"/>), found by sliding one
    /// against the other over a piece from the middle of the sound.
    /// </summary>
    public static int BestDelay(ReadOnlySpan<float> reference, ReadOnlySpan<float> estimate, int maxDelay)
    {
        if (maxDelay < 0)
        {
            return 0;
        }

        var usable = Math.Min(reference.Length, estimate.Length) - maxDelay;
        if (usable <= 0)
        {
            return 0;
        }

        var length = Math.Min(usable, 96000);
        var start = (usable - length) / 2;
        var best = 0;
        var bestScore = double.NegativeInfinity;
        for (var delay = 0; delay <= maxDelay; delay++)
        {
            double score = 0;
            for (var i = 0; i < length; i++)
            {
                score += (double)reference[start + i] * estimate[start + i + delay];
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = delay;
            }
        }

        return best;
    }

    /// <summary>
    /// Mixes noise into a voice so the noise is the given number of decibels below the voice. The noise is repeated or cut
    /// to the voice length. <paramref name="scaledNoise"/> returns the noise as it was mixed in.
    /// </summary>
    public static float[] MixAtSnr(float[] voice, float[] noise, double snrDb, out float[] scaledNoise)
    {
        ArgumentNullException.ThrowIfNull(voice);
        ArgumentNullException.ThrowIfNull(noise);

        scaledNoise = new float[voice.Length];
        var mixture = new float[voice.Length];
        if (noise.Length == 0)
        {
            Array.Copy(voice, mixture, voice.Length);
            return mixture;
        }

        for (var i = 0; i < voice.Length; i++)
        {
            scaledNoise[i] = noise[i % noise.Length];
        }

        var noiseRms = Rms(scaledNoise);
        var gain = noiseRms > 0 ? Rms(voice) / Math.Pow(10.0, snrDb / 20.0) / noiseRms : 0.0;
        for (var i = 0; i < voice.Length; i++)
        {
            scaledNoise[i] = (float)(scaledNoise[i] * gain);
            mixture[i] = voice[i] + scaledNoise[i];
        }

        return mixture;
    }
}
