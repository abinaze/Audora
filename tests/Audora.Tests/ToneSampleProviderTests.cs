using Audora.Core.Diagnostics;

namespace Audora.Tests;

public class ToneSampleProviderTests
{
    private const int Rate = 48000;

    private static float[] ReadAll(ToneSampleProvider provider)
    {
        var all = new List<float>();
        var buffer = new float[4800];
        int read;
        while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
        {
            all.AddRange(buffer.Take(read));
        }

        return all.ToArray();
    }

    private static float[] Tone(int channels, ToneChannel which, double seconds = 1.0, float level = ToneSampleProvider.DefaultLevel) =>
        ReadAll(new ToneSampleProvider(Rate, channels, new ToneRequest(which, ToneShape.Steady, 440, seconds), level));

    private static IEnumerable<float> Channel(float[] samples, int channels, int channel) =>
        samples.Where((_, i) => i % channels == channel);

    [Fact]
    public void LeftToneKeepsTheRightChannelSilent()
    {
        var samples = Tone(2, ToneChannel.Left);

        Assert.All(Channel(samples, 2, 1), x => Assert.Equal(0f, x));
        Assert.Contains(Channel(samples, 2, 0), x => Math.Abs(x) > 0.01f);
    }

    [Fact]
    public void RightToneKeepsTheLeftChannelSilent()
    {
        var samples = Tone(2, ToneChannel.Right);

        Assert.All(Channel(samples, 2, 0), x => Assert.Equal(0f, x));
        Assert.Contains(Channel(samples, 2, 1), x => Math.Abs(x) > 0.01f);
    }

    [Fact]
    public void BothToneUsesBothChannelsEqually()
    {
        var samples = Tone(2, ToneChannel.Both);

        Assert.Equal(Channel(samples, 2, 0).ToArray(), Channel(samples, 2, 1).ToArray());
        Assert.Contains(Channel(samples, 2, 0), x => Math.Abs(x) > 0.01f);
    }

    [Fact]
    public void LengthMatchesTheDuration()
    {
        var samples = Tone(2, ToneChannel.Both, 1.5);

        Assert.Equal(Rate * 3, samples.Length);
    }

    [Fact]
    public void NeverLouderThanTheDefaultLevel()
    {
        var peak = Tone(2, ToneChannel.Both).Max(x => Math.Abs(x));

        Assert.True(peak <= ToneSampleProvider.DefaultLevel + 0.000001f);
    }

    [Fact]
    public void RequestedLevelIsCutDownToTheHardLimit()
    {
        var peak = Tone(2, ToneChannel.Both, 1.0, 5f).Max(x => Math.Abs(x));

        Assert.True(peak <= ToneSampleProvider.MaxLevel + 0.000001f);
        Assert.True(peak > 0.2f);
    }

    [Fact]
    public void EnvelopeFadesInAndOut()
    {
        Assert.Equal(0.0, ToneSampleProvider.Envelope(0, 3), 6);
        Assert.True(ToneSampleProvider.Envelope(0.1, 3) < ToneSampleProvider.Envelope(1, 3));
        Assert.Equal(1.0, ToneSampleProvider.Envelope(1.5, 3), 6);
        Assert.Equal(0.0, ToneSampleProvider.Envelope(3, 3), 6);
    }

    [Fact]
    public void SweepRunsFromLowToHigh()
    {
        Assert.Equal(100.0, ToneSampleProvider.SweepFrequency(0, 5), 6);
        Assert.Equal(8000.0, ToneSampleProvider.SweepFrequency(5, 5), 6);
        Assert.InRange(ToneSampleProvider.SweepFrequency(2.5, 5), 890.0, 899.0);
    }

    [Fact]
    public void MonoDeviceStillHearsTheTone()
    {
        var samples = Tone(1, ToneChannel.Left);

        Assert.Contains(samples, x => Math.Abs(x) > 0.01f);
    }

    [Fact]
    public void SurroundDeviceKeepsTheOtherChannelsSilent()
    {
        var samples = Tone(6, ToneChannel.Both);

        for (var channel = 2; channel < 6; channel++)
        {
            Assert.All(Channel(samples, 6, channel), x => Assert.Equal(0f, x));
        }

        Assert.Contains(Channel(samples, 6, 0), x => Math.Abs(x) > 0.01f);
    }

    [Fact]
    public void ReadsWholeFramesOnly()
    {
        var provider = new ToneSampleProvider(Rate, 2, new ToneRequest(ToneChannel.Both, ToneShape.Steady, 440, 1));
        var buffer = new float[5];

        Assert.Equal(4, provider.Read(buffer, 0, 5));
    }
}
