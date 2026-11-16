using Audora.Core.Cleanup;
using NAudio.Wave;

namespace Audora.Tests;

public class VoiceCleanupResamplingTests
{
    private sealed class ConstantSource : ISampleProvider
    {
        private readonly float _value;

        public ConstantSource(int sampleRate, int channels, float value = 1f)
        {
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
            _value = value;
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            for (var i = 0; i < count; i++)
            {
                buffer[offset + i] = _value;
            }

            return count;
        }
    }

    private sealed class HalvingDenoiser : IFrameDenoiser
    {
        public int FrameSize => 4;

        public float Process(float[] input, float[] output)
        {
            for (var i = 0; i < FrameSize; i++)
            {
                output[i] = input[i] * 0.5f;
            }

            return 0f;
        }

        public void Dispose()
        {
        }
    }

    private static double AverageOfTheEnd(float[] buffer, int read, int count)
    {
        double sum = 0;
        for (var i = read - count; i < read; i++)
        {
            sum += buffer[i];
        }

        return sum / count;
    }

    [Fact]
    public void SoundAt44100HzIsSupportedWhenConvertingIsAllowed()
    {
        using var provider = new VoiceCleanupSampleProvider(new ConstantSource(44100, 2), () => new HalvingDenoiser(), true);

        Assert.True(provider.IsSupported);
        Assert.True(provider.IsResampled);
        Assert.Null(provider.UnavailableReason);
        Assert.Equal(44100, provider.WaveFormat.SampleRate);
    }

    [Fact]
    public void SoundAt48kHzIsNeverConverted()
    {
        using var provider = new VoiceCleanupSampleProvider(new ConstantSource(48000, 2), () => new HalvingDenoiser(), true);

        Assert.True(provider.IsSupported);
        Assert.False(provider.IsResampled);
    }

    [Fact]
    public void WithoutPermissionToConvertSoundAt44100HzIsNotSupported()
    {
        using var provider = new VoiceCleanupSampleProvider(new ConstantSource(44100, 2), () => new HalvingDenoiser());

        Assert.False(provider.IsSupported);
        Assert.False(provider.IsResampled);
    }

    [Fact]
    public void WhenItIsOffAConvertedChainStillPlaysTheSoundAtTheSameLevel()
    {
        using var provider = new VoiceCleanupSampleProvider(new ConstantSource(44100, 2), () => new HalvingDenoiser(), true);
        var buffer = new float[20000];

        var read = provider.Read(buffer, 0, buffer.Length);

        Assert.True(read > 10000);
        Assert.InRange(AverageOfTheEnd(buffer, read, 200), 0.95, 1.05);
    }

    [Fact]
    public void WhenItIsOnTheCleanedSoundComesOutAtTheOriginalRate()
    {
        using var provider = new VoiceCleanupSampleProvider(new ConstantSource(44100, 2), () => new HalvingDenoiser(), true);
        provider.Enabled = true;
        var buffer = new float[20000];

        var read = provider.Read(buffer, 0, buffer.Length);

        Assert.True(read > 10000);
        Assert.InRange(AverageOfTheEnd(buffer, read, 200), 0.45, 0.55);
    }
}
