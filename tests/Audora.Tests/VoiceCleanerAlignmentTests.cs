using Audora.Core.Cleanup;

namespace Audora.Tests;

public class VoiceCleanerAlignmentTests
{
    // Hands each sample back 'AlgorithmicDelaySamples' later, like a model that overlaps its windows.
    private sealed class DelayingDenoiser : IFrameDenoiser
    {
        private const int Inner = 4;
        private float[] _tail = new float[Inner];

        public int FrameSize => 8;

        public int AlgorithmicDelaySamples => Inner;

        public float Process(float[] input, float[] output)
        {
            var combined = new float[Inner + FrameSize];
            Array.Copy(_tail, 0, combined, 0, Inner);
            Array.Copy(input, 0, combined, Inner, FrameSize);
            Array.Copy(combined, 0, output, 0, FrameSize);
            _tail = new float[Inner];
            Array.Copy(input, FrameSize - Inner, _tail, 0, Inner);
            return 0f;
        }

        public void Dispose()
        {
        }
    }

    private sealed class PlainDenoiser : IFrameDenoiser
    {
        public int FrameSize => 8;

        public float Process(float[] input, float[] output)
        {
            Array.Copy(input, output, FrameSize);
            return 0f;
        }

        public void Dispose()
        {
        }
    }

    // Like DeepFilterNet: frames of 480, and the cleaned sound comes out 1440 samples late on top of the wrapper's frame.
    private sealed class LongDelayDenoiser : IFrameDenoiser
    {
        private readonly Queue<float> _queue = new(new float[DeepFilterDenoiser.AlgorithmicDelay]);

        public int FrameSize => DeepFilterDenoiser.Frame;

        public int AlgorithmicDelaySamples => DeepFilterDenoiser.AlgorithmicDelay;

        public float Process(float[] input, float[] output)
        {
            for (var i = 0; i < FrameSize; i++)
            {
                _queue.Enqueue(input[i]);
                output[i] = _queue.Dequeue();
            }

            return 0f;
        }

        public void Dispose()
        {
        }
    }

    private static float[] Noise(int count)
    {
        var random = new Random(11);
        var samples = new float[count];
        for (var i = 0; i < count; i++)
        {
            samples[i] = (float)(random.NextDouble() * 2 - 1);
        }

        return samples;
    }

    [Fact]
    public void ADenoiserThatHoldsBackSoundStillLinesUpWithTheOriginal()
    {
        using var cleaner = new VoiceCleaner(1, () => new DelayingDenoiser(), fadeSamples: 1);
        cleaner.Strength = 0.5f;
        cleaner.Enabled = true;

        var input = Noise(800);
        var buffer = (float[])input.Clone();
        cleaner.Process(buffer, 0, buffer.Length);

        for (var n = 40; n < 700; n++)
        {
            Assert.Equal((double)input[n - 12], (double)buffer[n], 4);
        }
    }

    [Fact]
    public void ADenoiserWithNoDelayOfItsOwnStillLinesUp()
    {
        using var cleaner = new VoiceCleaner(1, () => new PlainDenoiser(), fadeSamples: 1);
        cleaner.Strength = 0.5f;
        cleaner.Enabled = true;

        var input = Noise(800);
        var buffer = (float[])input.Clone();
        cleaner.Process(buffer, 0, buffer.Length);

        for (var n = 40; n < 700; n++)
        {
            Assert.Equal((double)input[n - 8], (double)buffer[n], 4);
        }
    }

    [Fact]
    public void RnNoiseSaysItHoldsBackOneFrame()
    {
        using var denoiser = new RnNoiseDenoiser();

        Assert.Equal(480, denoiser.AlgorithmicDelaySamples);
    }

    [Fact]
    public void ADenoiserThatHoldsBackAsMuchAsDeepFilterNetStillLinesUp()
    {
        using var cleaner = new VoiceCleaner(1, () => new LongDelayDenoiser(), fadeSamples: 1);
        cleaner.Strength = 0.5f;
        cleaner.Enabled = true;

        var input = Noise(8000);
        var buffer = (float[])input.Clone();
        cleaner.Process(buffer, 0, buffer.Length);

        for (var n = 2500; n < 7900; n++)
        {
            Assert.Equal((double)input[n - 1920], (double)buffer[n], 4);
        }
    }
}
