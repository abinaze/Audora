using Audora.Core.Audio;
using NAudio.Wave;

namespace Audora.Tests;

public class DelaySampleProviderTests
{
    /// <summary>Stereo source at 1000 Hz producing 1, 2, 3, ... so delays are easy to see.</summary>
    private sealed class RampSource : ISampleProvider
    {
        private int _next = 1;

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(1000, 2);

        public int Read(float[] buffer, int offset, int count)
        {
            for (var i = 0; i < count; i++)
            {
                buffer[offset + i] = _next++;
            }
            return count;
        }
    }

    [Fact]
    public void ZeroDelay_PassesSamplesThrough()
    {
        var provider = new DelaySampleProvider(new RampSource());
        var buffer = new float[4];
        provider.Read(buffer, 0, 4);
        Assert.Equal(new float[] { 1, 2, 3, 4 }, buffer);
    }

    [Fact]
    public void TwoMillisecondDelay_ShiftsByTwoFrames()
    {
        // 1000 Hz x 2 ms = 2 frames = 4 interleaved samples of silence first.
        var provider = new DelaySampleProvider(new RampSource()) { DelayMilliseconds = 2 };
        var buffer = new float[8];
        provider.Read(buffer, 0, 8);
        Assert.Equal(new float[] { 0, 0, 0, 0, 1, 2, 3, 4 }, buffer);
    }

    [Fact]
    public void Delay_IsClampedToMaximum()
    {
        var provider = new DelaySampleProvider(new RampSource()) { DelayMilliseconds = 99999 };
        Assert.Equal(DelaySampleProvider.MaxDelayMilliseconds, provider.DelayMilliseconds);
    }

    [Fact]
    public void NegativeDelay_IsClampedToZero()
    {
        var provider = new DelaySampleProvider(new RampSource()) { DelayMilliseconds = -50 };
        Assert.Equal(0, provider.DelayMilliseconds);
    }
}
