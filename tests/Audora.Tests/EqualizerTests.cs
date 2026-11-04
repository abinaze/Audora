using System.Numerics;
using Audora.Core.Audio;
using NAudio.Wave;

namespace Audora.Tests;

public class EqualizerTests
{
    private const int Rate = 48000;

    private sealed class SineSource : ISampleProvider
    {
        private readonly double _frequency;
        private long _n;

        public SineSource(double frequency)
        {
            _frequency = frequency;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(Rate, 2);
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            for (var i = 0; i + 1 < count; i += 2)
            {
                var v = (float)(0.1 * Math.Sin(2 * Math.PI * _frequency * _n++ / Rate));
                buffer[offset + i] = v;
                buffer[offset + i + 1] = v;
            }
            return count;
        }
    }

    private static double Rms(ISampleProvider provider)
    {
        var buffer = new float[Rate * 2];
        provider.Read(buffer, 0, buffer.Length);   // let the filters settle
        provider.Read(buffer, 0, buffer.Length);

        double sum = 0;
        var frames = 0;
        for (var i = 0; i + 1 < buffer.Length; i += 2)
        {
            sum += buffer[i] * (double)buffer[i];
            frames++;
        }
        return Math.Sqrt(sum / frames);
    }

    private static double Ratio(double frequency, EqSettings settings)
    {
        var eq = new EqualizerSampleProvider(new SineSource(frequency));
        eq.Apply(settings);
        return Rms(eq) / Rms(new SineSource(frequency));
    }

    private static EqSettings BandBoost(int band, float db)
    {
        var bands = new float[10];
        bands[band] = db;
        return new EqSettings(true, 0f, bands, 0f, 0f, 1f);
    }

    private static double Magnitude(BiquadCoefficients c, double frequency)
    {
        var w = 2 * Math.PI * frequency / Rate;
        var z1 = Complex.FromPolarCoordinates(1, -w);
        var z2 = Complex.FromPolarCoordinates(1, -2 * w);
        var num = c.B0 + c.B1 * z1 + c.B2 * z2;
        var den = 1 + c.A1 * z1 + c.A2 * z2;
        return (num / den).Magnitude;
    }

    [Fact]
    public void Original_LeavesSamplesUntouched()
    {
        var eq = new EqualizerSampleProvider(new SineSource(1000));
        var reference = new SineSource(1000);
        var a = new float[2000];
        var b = new float[2000];

        eq.Read(a, 0, a.Length);
        reference.Read(b, 0, b.Length);

        Assert.Equal(b, a);
    }

    [Fact]
    public void FlatBands_DoNotChangeTheLevel() =>
        Assert.Equal(1.0, Ratio(1000, new EqSettings(true, 0f, new float[10], 0f, 0f, 1f)), 2);

    [Fact]
    public void BoostAt1kHz_RaisesThatFrequencyAboutSixDecibels() =>
        Assert.InRange(Ratio(1000, BandBoost(5, 6f)), 1.9, 2.1);

    [Fact]
    public void CutAt1kHz_LowersThatFrequencyAboutSixDecibels() =>
        Assert.InRange(Ratio(1000, BandBoost(5, -6f)), 0.47, 0.53);

    [Fact]
    public void BoostAt1kHz_BarelyAffectsAFarAwayFrequency() =>
        Assert.InRange(Ratio(100, BandBoost(5, 6f)), 0.95, 1.05);

    [Fact]
    public void Preamp_ScalesTheLevel()
    {
        var settings = new EqSettings(true, -6f, new float[10], 0f, 0f, 1f);
        Assert.InRange(Ratio(1000, settings), 0.47, 0.53);
    }

    [Fact]
    public void PeakingFilter_HasTheRequestedGainAtItsCentre()
    {
        var c = BiquadCoefficients.Peaking(Rate, 1000f, 6f, 1.41f);
        Assert.InRange(Magnitude(c, 1000), 1.97, 2.02);
        Assert.InRange(Magnitude(c, 50), 0.98, 1.02);
    }

    [Fact]
    public void LowShelf_LiftsLowFrequenciesOnly()
    {
        var c = BiquadCoefficients.LowShelf(Rate, 110f, 6f);
        Assert.InRange(Magnitude(c, 20), 1.9, 2.1);
        Assert.InRange(Magnitude(c, 10000), 0.98, 1.02);
    }

    [Fact]
    public void HighShelf_LiftsHighFrequenciesOnly()
    {
        var c = BiquadCoefficients.HighShelf(Rate, 8000f, 6f);
        Assert.InRange(Magnitude(c, 16000), 1.8, 2.1);
        Assert.InRange(Magnitude(c, 100), 0.98, 1.02);
    }

    [Fact]
    public void OriginalPresetIsFirstAndFlat()
    {
        var first = SoundPresets.All[0];
        Assert.Equal(SoundPresets.OriginalName, first.Name);
        Assert.All(first.Bands, gain => Assert.Equal(0f, gain));
        Assert.False(SoundPresets.ToSettings(first).Enabled);
    }

    [Fact]
    public void EveryPresetHasTenBandsAndAUniqueName()
    {
        Assert.All(SoundPresets.All, p => Assert.Equal(10, p.Bands.Length));
        Assert.Equal(SoundPresets.All.Count, SoundPresets.All.Select(p => p.Name).Distinct().Count());
    }

    [Fact]
    public void BoostingPresetsLowerThePreampToStayClean()
    {
        var bass = SoundPresets.All.First(p => p.Name == "Bass Boost");
        Assert.True(SoundPresets.ToSettings(bass).PreampDb < 0f);
    }
}
