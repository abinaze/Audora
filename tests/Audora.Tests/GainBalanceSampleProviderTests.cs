using Audora.Core.Audio;
using NAudio.Wave;

namespace Audora.Tests;

public class GainBalanceSampleProviderTests
{
    private sealed class ConstantSource : ISampleProvider
    {
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);

        public int Read(float[] buffer, int offset, int count)
        {
            for (var i = 0; i < count; i++)
            {
                buffer[offset + i] = 1f;
            }
            return count;
        }
    }

    private static float[] ReadFrame(GainBalanceSampleProvider provider)
    {
        var frame = new float[2];
        provider.Read(frame, 0, 2);
        return frame;
    }

    [Fact]
    public void FullVolume_PassesSamplesThrough()
    {
        var frame = ReadFrame(new GainBalanceSampleProvider(new ConstantSource()));
        Assert.Equal(1.0, frame[0], 3);
        Assert.Equal(1.0, frame[1], 3);
    }

    [Fact]
    public void HalfVolume_UsesSquaredCurve()
    {
        var provider = new GainBalanceSampleProvider(new ConstantSource()) { Volume = 0.5f };
        var frame = ReadFrame(provider);
        Assert.Equal(0.25, frame[0], 3);
        Assert.Equal(0.25, frame[1], 3);
    }

    [Fact]
    public void Muted_ProducesSilence()
    {
        var provider = new GainBalanceSampleProvider(new ConstantSource()) { Muted = true };
        var frame = ReadFrame(provider);
        Assert.Equal(0.0, frame[0], 3);
        Assert.Equal(0.0, frame[1], 3);
    }

    [Fact]
    public void FullRightBalance_SilencesLeftChannel()
    {
        var provider = new GainBalanceSampleProvider(new ConstantSource()) { Balance = 1f };
        var frame = ReadFrame(provider);
        Assert.Equal(0.0, frame[0], 3);
        Assert.Equal(1.0, frame[1], 3);
    }

    [Fact]
    public void LeftBalance_AttenuatesRightChannel()
    {
        var provider = new GainBalanceSampleProvider(new ConstantSource()) { Balance = -0.5f };
        var frame = ReadFrame(provider);
        Assert.Equal(1.0, frame[0], 3);
        Assert.Equal(0.5, frame[1], 3);
    }
}
