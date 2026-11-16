using Audora.Core.Cleanup;

namespace Audora.Tests;

public class VoiceCleanerTests
{
    private sealed class FakeDenoiser : IFrameDenoiser
    {
        private readonly float _gain;
        private readonly int _failOnFrame;

        public FakeDenoiser(float gain = 1f, int failOnFrame = 0)
        {
            _gain = gain;
            _failOnFrame = failOnFrame;
        }

        public int FrameSize => 4;

        public int Frames { get; private set; }

        public bool Disposed { get; private set; }

        public float Process(float[] input, float[] output)
        {
            Frames++;
            if (Frames == _failOnFrame)
            {
                throw new InvalidOperationException("The denoiser failed.");
            }

            for (var i = 0; i < FrameSize; i++)
            {
                output[i] = input[i] * _gain;
            }

            return 0f;
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private static float[] Ramp(int count) => Enumerable.Range(1, count).Select(i => (float)i).ToArray();

    private static void AssertClose(float[] expected, float[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal((double)expected[i], (double)actual[i], 4);
        }
    }

    private static float[] RunInChunks(int[] sizes)
    {
        using var cleaner = new VoiceCleaner(1, () => new FakeDenoiser(0.5f), 8) { Strength = 0.7f };
        cleaner.Enabled = true;
        var samples = Ramp(60);
        var start = 0;
        foreach (var size in sizes)
        {
            cleaner.Process(samples, start, size);
            start += size;
        }

        return samples;
    }

    [Fact]
    public void WhenItIsOffTheSoundIsUntouchedAndNothingIsStarted()
    {
        var made = 0;
        using var cleaner = new VoiceCleaner(1, () =>
        {
            made++;
            return new FakeDenoiser();
        });
        var samples = Ramp(10);

        cleaner.Process(samples, 0, samples.Length);

        Assert.Equal(Ramp(10), samples);
        Assert.Equal(0, made);
    }

    [Fact]
    public void WhenItIsSwitchedOnTheSoundFadesToTheCleanedSoundDelayedByOneFrame()
    {
        using var cleaner = new VoiceCleaner(1, () => new FakeDenoiser(), 8);
        cleaner.Enabled = true;
        var samples = Enumerable.Repeat(1f, 20).ToArray();

        cleaner.Process(samples, 0, samples.Length);

        var expected = new[] { 0.875f, 0.75f, 0.625f, 0.5f }.Concat(Enumerable.Repeat(1f, 16)).ToArray();
        AssertClose(expected, samples);
    }

    [Fact]
    public void StrengthZeroPlaysTheOriginalSoundDelayedToStayInStep()
    {
        using var cleaner = new VoiceCleaner(1, () => new FakeDenoiser(0.1f), 1) { Strength = 0f };
        cleaner.Enabled = true;
        var samples = Ramp(20);

        cleaner.Process(samples, 0, samples.Length);

        AssertClose(new float[4].Concat(Ramp(16)).ToArray(), samples);
    }

    [Fact]
    public void StrengthMixesTheCleanedAndTheOriginalSound()
    {
        using var cleaner = new VoiceCleaner(1, () => new FakeDenoiser(0.5f), 1) { Strength = 0.5f };
        cleaner.Enabled = true;
        var samples = Ramp(20);

        cleaner.Process(samples, 0, samples.Length);

        var expected = Enumerable.Range(0, 20).Select(i => i < 4 ? 0f : 0.75f * (i - 3)).ToArray();
        AssertClose(expected, samples);
    }

    [Fact]
    public void WhenItIsSwitchedOffItFadesBackAndThenTheSoundIsUntouchedAgain()
    {
        using var cleaner = new VoiceCleaner(1, () => new FakeDenoiser(), 8);
        cleaner.Enabled = true;
        cleaner.Process(Enumerable.Repeat(1f, 16).ToArray(), 0, 16);

        cleaner.Enabled = false;
        cleaner.Process(Enumerable.Repeat(1f, 8).ToArray(), 0, 8);
        var samples = Ramp(8);
        cleaner.Process(samples, 0, samples.Length);

        Assert.Equal(Ramp(8), samples);
    }

    [Fact]
    public void StereoChannelsAreCleanedSeparately()
    {
        var made = 0;
        using var cleaner = new VoiceCleaner(2, () =>
        {
            made++;
            return new FakeDenoiser(0.5f);
        }, 1);
        cleaner.Enabled = true;
        var samples = new float[16];
        for (var frame = 0; frame < 8; frame++)
        {
            samples[frame * 2] = 1f;
            samples[frame * 2 + 1] = 10f;
        }

        cleaner.Process(samples, 0, samples.Length);

        var expected = new float[16];
        for (var frame = 4; frame < 8; frame++)
        {
            expected[frame * 2] = 0.5f;
            expected[frame * 2 + 1] = 5f;
        }

        AssertClose(expected, samples);
        Assert.Equal(2, made);
    }

    [Fact]
    public void TheChunkSizeDoesNotChangeTheResult()
    {
        var whole = RunInChunks(new[] { 60 });
        var pieces = RunInChunks(new[] { 1, 3, 5, 7, 2, 11, 6, 25 });

        Assert.Equal(whole, pieces);
    }

    [Fact]
    public void OnlyTheSamplesFromTheOffsetAreTouched()
    {
        using var plain = new VoiceCleaner(1, () => new FakeDenoiser(0.5f), 4);
        plain.Enabled = true;
        var reference = Ramp(20);
        plain.Process(reference, 0, 20);

        using var shifted = new VoiceCleaner(1, () => new FakeDenoiser(0.5f), 4);
        shifted.Enabled = true;
        var samples = new float[] { 9f, 9f }.Concat(Ramp(20)).ToArray();
        shifted.Process(samples, 2, 20);

        Assert.Equal(9f, samples[0]);
        Assert.Equal(9f, samples[1]);
        Assert.Equal(reference, samples.Skip(2).ToArray());
    }

    [Fact]
    public void ADenoiserThatThrowsLeavesTheSoundUntouched()
    {
        using var cleaner = new VoiceCleaner(1, () => new FakeDenoiser(failOnFrame: 2), 1);
        cleaner.Enabled = true;
        var samples = Ramp(12);

        cleaner.Process(samples, 0, samples.Length);

        Assert.Equal(Ramp(12), samples);
        Assert.True(cleaner.Failed);
        Assert.NotNull(cleaner.Error);

        var more = Ramp(4);
        cleaner.Process(more, 0, more.Length);
        Assert.Equal(Ramp(4), more);
    }

    [Fact]
    public void AFactoryThatThrowsSwitchesCleaningOffInsteadOfCrashing()
    {
        using var cleaner = new VoiceCleaner(1, () => throw new InvalidOperationException("No library."));

        cleaner.Enabled = true;
        var samples = Ramp(8);
        cleaner.Process(samples, 0, samples.Length);

        Assert.True(cleaner.Failed);
        Assert.Equal("No library.", cleaner.Error);
        Assert.Equal(Ramp(8), samples);
    }

    [Fact]
    public void AfterItIsDisposedTheDenoiserIsReleasedAndTheSoundIsUntouched()
    {
        var denoiser = new FakeDenoiser();
        var cleaner = new VoiceCleaner(1, () => denoiser, 1);
        cleaner.Enabled = true;

        cleaner.Dispose();
        var samples = Ramp(8);
        cleaner.Process(samples, 0, samples.Length);

        Assert.True(denoiser.Disposed);
        Assert.Equal(Ramp(8), samples);
    }

    [Fact]
    public void OnlyMonoAndStereoAreSupported()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new VoiceCleaner(0, () => new FakeDenoiser()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new VoiceCleaner(3, () => new FakeDenoiser()));
    }

    [Fact]
    public void StrengthStaysBetweenZeroAndOne()
    {
        using var cleaner = new VoiceCleaner(1, () => new FakeDenoiser());

        cleaner.Strength = 5f;
        Assert.Equal(1f, cleaner.Strength);

        cleaner.Strength = -2f;
        Assert.Equal(0f, cleaner.Strength);

        cleaner.Strength = float.NaN;
        Assert.Equal(0f, cleaner.Strength);
    }
}
