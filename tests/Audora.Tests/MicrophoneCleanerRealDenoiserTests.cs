using Audora.Core.Cleanup;

namespace Audora.Tests;

/// <summary>Runs steady noise through the microphone cleaner with the real RNNoise, so no room or microphone is involved.</summary>
public class MicrophoneCleanerRealDenoiserTests
{
    private const int Rate = 48000;

    private static float[] Noise(int count, float amplitude, int seed)
    {
        var random = new Random(seed);
        var samples = new float[count];
        for (var i = 0; i < count; i++)
        {
            samples[i] = (float)((random.NextDouble() * 2 - 1) * amplitude);
        }

        return samples;
    }

    private static double Energy(float[] samples, int from, int count)
    {
        double sum = 0;
        for (var i = from; i < from + count; i++)
        {
            sum += samples[i] * samples[i];
        }

        return sum;
    }

    private static float[] Run(MicrophoneCleaner cleaner, float[] input)
    {
        var output = new List<float>();
        const int chunk = 1000;
        for (var start = 0; start < input.Length; start += chunk)
        {
            var count = Math.Min(chunk, input.Length - start);
            var bytes = new byte[count * sizeof(float)];
            Buffer.BlockCopy(input, start * sizeof(float), bytes, 0, bytes.Length);
            cleaner.Push(bytes, bytes.Length);
            cleaner.ReadAvailable((buffer, produced) =>
            {
                for (var i = 0; i < produced; i++)
                {
                    output.Add(buffer[i]);
                }
            });
        }

        return output.ToArray();
    }

    private static double ReductionDb(float[] input, float[] output)
    {
        var from = Rate;
        var count = Rate;
        return 10 * Math.Log10(Energy(input, from, count) / Math.Max(Energy(output, from, count), 1e-12));
    }

    [Fact]
    public void SteadyNoiseThroughTheMicrophoneCleanerGetsMuchQuieter()
    {
        using var cleaner = new MicrophoneCleaner(Rate, 1, () => new RnNoiseDenoiser());
        cleaner.Enabled = true;
        Assert.False(cleaner.Failed);

        var input = Noise(Rate * 3, 0.05f, 42);
        var output = Run(cleaner, input);

        var reduction = ReductionDb(input, output);
        Assert.True(reduction > 10, $"The noise was only {reduction:0.0} dB quieter.");
    }

    [Fact]
    public void AtStrengthZeroTheNoiseIsLeftAlone()
    {
        using var cleaner = new MicrophoneCleaner(Rate, 1, () => new RnNoiseDenoiser());
        cleaner.Strength = 0f;
        cleaner.Enabled = true;

        var input = Noise(Rate * 3, 0.05f, 42);
        var output = Run(cleaner, input);

        var reduction = ReductionDb(input, output);
        Assert.InRange(reduction, -1.0, 1.0);
    }
}
