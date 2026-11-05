using Audora.Core.Audio;
using NAudio.Wave;

namespace Audora.Tests;

public class GainRampTests
{
    private sealed class Constant : ISampleProvider
    {
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);

        public int Read(float[] buffer, int offset, int count)
        {
            for (var i = 0; i < count; i++)
            {
                buffer[offset + i] = 0.5f;
            }
            return count;
        }
    }

    [Fact]
    public void VolumeChange_GlidesAcrossTheBlockInsteadOfJumping()
    {
        var provider = new GainBalanceSampleProvider(new Constant());
        provider.Read(new float[8], 0, 8);

        provider.Volume = 0f;
        var second = new float[8];
        provider.Read(second, 0, 8);

        // left channel is every second sample: 0.375, 0.25, 0.125, 0
        Assert.True(second[0] > second[2] && second[2] > second[4] && second[4] > second[6]);
        Assert.InRange(second[0], 0.3, 0.45);
        Assert.Equal(0.0, second[6], 3);
    }
}
