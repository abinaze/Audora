using Audora.Core.Cleanup;
using NAudio.Wave;

namespace Audora.Tests;

public class VoiceCleanupSampleProviderTests
{
    private sealed class ConstantSource : ISampleProvider
    {
        private readonly float _value;
        private readonly int _limit;

        public ConstantSource(int sampleRate, int channels, float value = 1f, int limit = int.MaxValue)
        {
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
            _value = value;
            _limit = limit;
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            var read = Math.Min(count, _limit);
            for (var i = 0; i < read; i++)
            {
                buffer[offset + i] = _value;
            }

            return read;
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

    [Fact]
    public void StereoSoundAt48kHzIsSupported()
    {
        using var provider = new VoiceCleanupSampleProvider(new ConstantSource(48000, 2), () => new HalvingDenoiser());

        Assert.True(provider.IsSupported);
        Assert.Null(provider.UnavailableReason);
    }

    [Fact]
    public void SoundAt44100HzIsNotSupportedAndTheReasonSaysWhy()
    {
        using var provider = new VoiceCleanupSampleProvider(new ConstantSource(44100, 2), () => new HalvingDenoiser());

        Assert.False(provider.IsSupported);
        Assert.Contains("48 kHz", provider.UnavailableReason);
        Assert.Contains("44100", provider.UnavailableReason);
    }

    [Fact]
    public void SurroundSoundIsNotSupported()
    {
        using var provider = new VoiceCleanupSampleProvider(new ConstantSource(48000, 6), () => new HalvingDenoiser());

        Assert.False(provider.IsSupported);
        Assert.Contains("mono and stereo", provider.UnavailableReason);
    }

    [Fact]
    public void WhenItIsOffTheSoundPassesStraightThrough()
    {
        using var provider = new VoiceCleanupSampleProvider(new ConstantSource(48000, 2, 0.25f), () => new HalvingDenoiser());
        var buffer = new float[100];

        var read = provider.Read(buffer, 0, buffer.Length);

        Assert.Equal(100, read);
        Assert.All(buffer, sample => Assert.Equal(0.25f, sample));
    }

    [Fact]
    public void WhenItIsOnTheCleanedSoundComesOutAfterTheFade()
    {
        using var provider = new VoiceCleanupSampleProvider(new ConstantSource(48000, 2), () => new HalvingDenoiser())
        {
            Enabled = true,
        };
        var buffer = new float[4000];

        provider.Read(buffer, 0, buffer.Length);

        Assert.Equal(0.5, buffer[3999], 3);
        Assert.Equal(0.5, buffer[3998], 3);
    }

    [Fact]
    public void ADenoiserThatCannotStartLeavesTheSoundAloneAndSaysWhy()
    {
        using var provider = new VoiceCleanupSampleProvider(
            new ConstantSource(48000, 2, 0.25f),
            () => throw new InvalidOperationException("No library."));

        provider.Enabled = true;
        var buffer = new float[100];
        provider.Read(buffer, 0, buffer.Length);

        Assert.True(provider.Failed);
        Assert.Equal("No library.", provider.UnavailableReason);
        Assert.All(buffer, sample => Assert.Equal(0.25f, sample));
    }

    [Fact]
    public void ItReturnsExactlyWhatTheSourceGave()
    {
        using var provider = new VoiceCleanupSampleProvider(new ConstantSource(48000, 2, 1f, 10), () => new HalvingDenoiser());
        provider.Enabled = true;
        var buffer = new float[100];

        var read = provider.Read(buffer, 0, buffer.Length);

        Assert.Equal(10, read);
    }
}
