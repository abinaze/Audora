using Audora.Core.Cleanup;

namespace Audora.Tests;

public class RnNoiseDenoiserTests
{
    private static double Energy(float[] samples, int from, int count)
    {
        double sum = 0;
        for (var i = from; i < from + count; i++)
        {
            sum += samples[i] * samples[i];
        }

        return sum;
    }

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

    [Fact]
    public void TheLibraryLoadsOnThisComputer()
    {
        Assert.True(RnNoiseDenoiser.IsAvailable());
    }

    [Fact]
    public void SteadyNoiseGetsMuchQuieter()
    {
        using var denoiser = new RnNoiseDenoiser();
        var input = Noise(RnNoiseDenoiser.Frame * 300, 0.05f, 42);
        var output = new float[input.Length];
        var frameIn = new float[RnNoiseDenoiser.Frame];
        var frameOut = new float[RnNoiseDenoiser.Frame];

        for (var start = 0; start < input.Length; start += RnNoiseDenoiser.Frame)
        {
            Array.Copy(input, start, frameIn, 0, RnNoiseDenoiser.Frame);
            denoiser.Process(frameIn, frameOut);
            Array.Copy(frameOut, 0, output, start, RnNoiseDenoiser.Frame);
        }

        var from = RnNoiseDenoiser.Frame * 100;
        var count = RnNoiseDenoiser.Frame * 200;
        var reduction = 10 * Math.Log10(Energy(input, from, count) / Math.Max(Energy(output, from, count), 1e-12));

        Assert.True(reduction > 10, $"The noise was only {reduction:0.0} dB quieter.");
    }

    [Fact]
    public void SilenceStaysSilent()
    {
        using var denoiser = new RnNoiseDenoiser();
        var silence = new float[RnNoiseDenoiser.Frame];
        var output = new float[RnNoiseDenoiser.Frame];

        for (var i = 0; i < 10; i++)
        {
            denoiser.Process(silence, output);
        }

        Assert.All(output, sample => Assert.InRange(sample, -0.001f, 0.001f));
    }

    [Fact]
    public void TheOutputStaysBetweenMinusOneAndOne()
    {
        using var denoiser = new RnNoiseDenoiser();
        var loud = Noise(RnNoiseDenoiser.Frame, 1f, 7);
        var output = new float[RnNoiseDenoiser.Frame];

        for (var i = 0; i < 20; i++)
        {
            denoiser.Process(loud, output);
            Assert.All(output, sample => Assert.InRange(sample, -1f, 1f));
        }
    }

    [Fact]
    public void AFrameOfTheWrongSizeIsRefused()
    {
        using var denoiser = new RnNoiseDenoiser();

        Assert.Throws<ArgumentException>(() => denoiser.Process(new float[100], new float[RnNoiseDenoiser.Frame]));
    }

    [Fact]
    public void ItCannotBeUsedAfterItIsDisposed()
    {
        var denoiser = new RnNoiseDenoiser();
        denoiser.Dispose();

        Assert.Throws<ObjectDisposedException>(() => denoiser.Process(new float[RnNoiseDenoiser.Frame], new float[RnNoiseDenoiser.Frame]));
    }

    [Fact]
    public void TheStreamGivesTheSameResultWhateverTheChunkSize()
    {
        var input = Noise(48000, 0.05f, 3);

        var whole = (float[])input.Clone();
        using (var one = new RnNoiseDenoiser())
        {
            new FrameStreamProcessor(one).Process(whole);
        }

        var pieces = (float[])input.Clone();
        using (var two = new RnNoiseDenoiser())
        {
            var processor = new FrameStreamProcessor(two);
            var start = 0;
            foreach (var size in new[] { 1000, 333, 480, 7, 2048, 11 })
            {
                processor.Process(pieces.AsSpan(start, size));
                start += size;
            }

            processor.Process(pieces.AsSpan(start));
        }

        Assert.Equal(whole, pieces);
    }
}
