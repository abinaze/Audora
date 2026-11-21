using Audora.Core.Cleanup;

namespace Audora.Tests;

public class SignalMetricsTests
{
    private static float[] Tone(int count, double frequency, double amplitude)
    {
        var samples = new float[count];
        for (var i = 0; i < count; i++)
        {
            samples[i] = (float)(amplitude * Math.Sin(2 * Math.PI * frequency * i / 48000.0));
        }

        return samples;
    }

    private static float[] Noise(int count, int seed)
    {
        var random = new Random(seed);
        var samples = new float[count];
        for (var i = 0; i < count; i++)
        {
            samples[i] = (float)(random.NextDouble() * 2 - 1);
        }

        return samples;
    }

    [Fact]
    public void ASquareWaveHasAnAverageLoudnessEqualToItsHeight()
    {
        Assert.Equal(0.5, SignalMetrics.Rms(new[] { 0.5f, -0.5f, 0.5f, -0.5f }), 5);
        Assert.Equal(0.0, SignalMetrics.Rms(Array.Empty<float>()), 5);
    }

    [Fact]
    public void IdenticalSoundGetsTheTopScore()
    {
        var tone = Tone(4800, 1000, 0.5);

        Assert.Equal(SignalMetrics.SiSdrCap, SignalMetrics.SiSdr(tone, tone), 5);
    }

    [Fact]
    public void MakingTheSoundQuieterDoesNotChangeTheScore()
    {
        var tone = Tone(4800, 1000, 0.5);
        var quieter = Tone(4800, 1000, 0.15);

        Assert.True(SignalMetrics.SiSdr(tone, quieter) > 59.0);
    }

    [Fact]
    public void AnAddedToneOneTenthAsLoudInPowerScoresTenDecibels()
    {
        var reference = Tone(4800, 1000, 1.0);
        var other = Tone(4800, 1700, 0.31622776);
        var estimate = new float[4800];
        for (var i = 0; i < estimate.Length; i++)
        {
            estimate[i] = reference[i] + other[i];
        }

        Assert.Equal(10.0, SignalMetrics.SiSdr(reference, estimate), 1);
    }

    [Fact]
    public void ItFindsHowManySamplesTheSoundWasDelayed()
    {
        var reference = Noise(5000, 7);
        var delayed = new float[5000];
        for (var i = 37; i < delayed.Length; i++)
        {
            delayed[i] = reference[i - 37];
        }

        Assert.Equal(37, SignalMetrics.BestDelay(reference, delayed, 100));
    }

    [Fact]
    public void NoiseIsMixedInAtTheAskedLevelBelowTheVoice()
    {
        var voice = new float[1000];
        Array.Fill(voice, 0.1f);

        var mixture = SignalMetrics.MixAtSnr(voice, Noise(300, 3), 10.0, out var scaled);

        Assert.Equal(1000, mixture.Length);
        Assert.Equal(0.0316228, SignalMetrics.Rms(scaled), 4);
        Assert.Equal((double)(voice[5] + scaled[5]), (double)mixture[5], 5);
    }

    [Fact]
    public void SilentNoiseLeavesTheVoiceAlone()
    {
        var voice = Tone(480, 440, 0.2);

        var mixture = SignalMetrics.MixAtSnr(voice, new float[100], 5.0, out var scaled);

        Assert.Equal(0.0, SignalMetrics.Rms(scaled), 5);
        for (var i = 0; i < voice.Length; i++)
        {
            Assert.Equal((double)voice[i], (double)mixture[i], 5);
        }
    }
}
