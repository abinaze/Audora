using Audora.Core.Audio;
using NAudio.Wave;

namespace Audora.Tests;

public class GainBalanceSampleProviderTests
{
    private sealed class ConstantSource : ISampleProvider
    {
        private readonly float _value;

        public ConstantSource(float value) => _value = value;

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);

        public int Read(float[] buffer, int offset, int count)
        {
            for (var i = 0; i < count; i++)
            {
                buffer[offset + i] = _value;
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
        var frame = ReadFrame(new GainBalanceSampleProvider(new ConstantSource(0.5f)));
        Assert.Equal(0.5, frame[0], 3);
        Assert.Equal(0.5, frame[1], 3);
    }

    [Fact]
    public void HalfVolume_UsesSquaredCurve()
    {
        var provider = new GainBalanceSampleProvider(new ConstantSource(0.5f)) { Volume = 0.5f };
        var frame = ReadFrame(provider);
        Assert.Equal(0.125, frame[0], 3);
        Assert.Equal(0.125, frame[1], 3);
    }

    [Fact]
    public void Muted_ProducesSilence()
    {
        var provider = new GainBalanceSampleProvider(new ConstantSource(0.5f)) { Muted = true };
        var frame = ReadFrame(provider);
        Assert.Equal(0.0, frame[0], 3);
        Assert.Equal(0.0, frame[1], 3);
    }

    [Fact]
    public void FullRightBalance_SilencesLeftChannel()
    {
        var provider = new GainBalanceSampleProvider(new ConstantSource(0.5f)) { Balance = 1f };
        var frame = ReadFrame(provider);
        Assert.Equal(0.0, frame[0], 3);
        Assert.Equal(0.5, frame[1], 3);
    }

    [Fact]
    public void LeftBalance_AttenuatesRightChannel()
    {
        var provider = new GainBalanceSampleProvider(new ConstantSource(0.5f)) { Balance = -0.5f };
        var frame = ReadFrame(provider);
        Assert.Equal(0.5, frame[0], 3);
        Assert.Equal(0.25, frame[1], 3);
    }

    [Fact]
    public void GainFor_BoostAbove100Percent_AddsDecibels()
    {
        Assert.Equal(1.0, GainBalanceSampleProvider.GainFor(1f), 3);
        Assert.Equal(1.995, GainBalanceSampleProvider.GainFor(1.5f), 2);   // +6 dB
        Assert.Equal(3.981, GainBalanceSampleProvider.GainFor(2f), 2);     // +12 dB
    }

    [Fact]
    public void Boost_NeverExceedsFullScale()
    {
        var provider = new GainBalanceSampleProvider(new ConstantSource(0.9f)) { Volume = 2f };
        var frame = ReadFrame(provider);
        Assert.InRange(frame[0], 0.95, 1.0);
    }

    [Fact]
    public void HugeInput_IsStillLimitedBelowFullScale()
    {
        var provider = new GainBalanceSampleProvider(new ConstantSource(5f));
        var frame = ReadFrame(provider);
        Assert.InRange(frame[0], 0.95, 1.0);
    }
}
