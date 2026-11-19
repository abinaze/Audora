using Audora.Core.Cleanup;

namespace Audora.Tests;

public class MicrophoneCleanerTests
{
    private sealed class QuietDenoiser : IFrameDenoiser
    {
        public int FrameSize => 4;

        public float Process(float[] input, float[] output)
        {
            for (var i = 0; i < FrameSize; i++)
            {
                output[i] = input[i];
            }

            return 0f;
        }

        public void Dispose()
        {
        }
    }

    private static byte[] Bytes(params float[] samples)
    {
        var bytes = new byte[samples.Length * sizeof(float)];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] Collect(MicrophoneCleaner cleaner)
    {
        var all = new List<float>();
        cleaner.ReadAvailable((buffer, count) =>
        {
            for (var i = 0; i < count; i++)
            {
                all.Add(buffer[i]);
            }
        });
        return all.ToArray();
    }

    private static void AssertClose(float[] expected, float[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal((double)expected[i], (double)actual[i], 5);
        }
    }

    [Fact]
    public void MonoSoundPassesThroughWhileOff()
    {
        using var cleaner = new MicrophoneCleaner(48000, 1, () => new QuietDenoiser());
        var sound = new[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f };

        var bytes = Bytes(sound);
        cleaner.Push(bytes, bytes.Length);

        AssertClose(sound, Collect(cleaner));
    }

    [Fact]
    public void StereoSoundIsMixedToMono()
    {
        using var cleaner = new MicrophoneCleaner(48000, 2, () => new QuietDenoiser());

        var bytes = Bytes(0.2f, 0.4f, 0.6f, 0.8f);
        cleaner.Push(bytes, bytes.Length);

        AssertClose(new[] { 0.3f, 0.7f }, Collect(cleaner));
    }

    [Fact]
    public void MoreThanTwoChannelsAreNotSupported()
    {
        using var cleaner = new MicrophoneCleaner(48000, 4, () => new QuietDenoiser());

        Assert.False(cleaner.IsSupported);
        Assert.Contains("mono and stereo", cleaner.UnavailableReason);

        var bytes = Bytes(0.1f, 0.2f, 0.3f, 0.4f);
        cleaner.Push(bytes, bytes.Length);
        Assert.Equal(0, cleaner.ReadAvailable());
    }

    [Fact]
    public void LevelsShowPeakAndAverageThenStartOver()
    {
        using var cleaner = new MicrophoneCleaner(48000, 1, () => new QuietDenoiser());

        var bytes = Bytes(0.5f, -0.5f, 0.5f, -0.5f);
        cleaner.Push(bytes, bytes.Length);
        cleaner.ReadAvailable();
        var levels = cleaner.TakeLevels();

        Assert.Equal(0.5, (double)levels.InputPeak, 5);
        Assert.Equal(0.5, (double)levels.InputRms, 5);
        Assert.Equal(0.5, (double)levels.OutputPeak, 5);
        Assert.Equal(0.5, (double)levels.OutputRms, 5);

        var again = cleaner.TakeLevels();
        Assert.Equal(0.0, (double)again.InputPeak, 5);
        Assert.Equal(0.0, (double)again.OutputRms, 5);
    }

    [Fact]
    public void NothingPushedMeansNothingRead()
    {
        using var cleaner = new MicrophoneCleaner(48000, 1, () => new QuietDenoiser());

        Assert.Equal(0, cleaner.ReadAvailable());
    }

    [Fact]
    public void ADenoiserThatCannotStartLeavesTheSoundUntouched()
    {
        using var cleaner = new MicrophoneCleaner(48000, 1, () => throw new InvalidOperationException("It cannot start."));

        cleaner.Enabled = true;
        Assert.True(cleaner.Failed);

        var sound = new[] { 0.1f, 0.2f, 0.3f };
        var bytes = Bytes(sound);
        cleaner.Push(bytes, bytes.Length);

        AssertClose(sound, Collect(cleaner));
    }
}
