using Audora.Core.Audio;
using NAudio.Wave;

namespace Audora.Tests;

public class EffectsTests
{
    private const int Rate = 48000;

    private sealed class Impulse : ISampleProvider
    {
        private bool _sent;

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(Rate, 2);

        public int Read(float[] buffer, int offset, int count)
        {
            Array.Clear(buffer, offset, count);
            if (!_sent && count >= 2)
            {
                buffer[offset] = 1f;
                buffer[offset + 1] = 1f;
                _sent = true;
            }

            return count;
        }
    }

    /// <summary>A sine wave in one ear only.</summary>
    private sealed class OneEar : ISampleProvider
    {
        private long _n;

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(Rate, 2);

        public int Read(float[] buffer, int offset, int count)
        {
            for (var i = 0; i + 1 < count; i += 2)
            {
                buffer[offset + i] = (float)(0.3 * Math.Sin(2 * Math.PI * 1000 * _n++ / Rate));
                buffer[offset + i + 1] = 0f;
            }

            return count;
        }
    }

    private sealed class Sine : ISampleProvider
    {
        private long _n;

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(Rate, 2);

        public int Read(float[] buffer, int offset, int count)
        {
            for (var i = 0; i + 1 < count; i += 2)
            {
                var v = (float)(0.3 * Math.Sin(2 * Math.PI * 1000 * _n++ / Rate));
                buffer[offset + i] = v;
                buffer[offset + i + 1] = v;
            }

            return count;
        }
    }

    private static EqSettings Fx(float virtualizer = 0f, float reverb = 0f, ReverbKind room = ReverbKind.Room) =>
        new(true, 0f, new float[10], 0f, 0f, 1f, virtualizer, reverb, room);

    private static float[] Render(ISampleProvider source, EqSettings settings, int frames)
    {
        var eq = new EqualizerSampleProvider(source);
        eq.Apply(settings);
        var buffer = new float[frames * 2];
        eq.Read(buffer, 0, buffer.Length);
        return buffer;
    }

    private static double Energy(float[] stereo, int fromFrame, int toFrame)
    {
        double sum = 0;
        for (var f = fromFrame; f < toFrame; f++)
        {
            sum += stereo[f * 2] * (double)stereo[f * 2];
        }

        return sum;
    }

    private static double Rms(float[] stereo, int channel, int fromFrame)
    {
        double sum = 0;
        var frames = stereo.Length / 2;
        for (var f = fromFrame; f < frames; f++)
        {
            sum += stereo[f * 2 + channel] * (double)stereo[f * 2 + channel];
        }

        return Math.Sqrt(sum / (frames - fromFrame));
    }

    [Fact]
    public void NoEffects_PassTheSignalThroughUnchanged()
    {
        var output = Render(new Impulse(), Fx(), 100);
        Assert.Equal(1f, output[0], 4);
        Assert.Equal(0f, output[10], 4);
    }

    [Fact]
    public void Reverb_KeepsMostOfTheOriginalAndAddsATailLater()
    {
        var output = Render(new Impulse(), Fx(reverb: 0.5f), Rate);

        Assert.InRange(output[0], 0.86f, 0.89f);                  // the original, a little quieter
        Assert.True(Energy(output, 2400, Rate) > 1e-6);           // the echo, after 50 ms
    }

    [Fact]
    public void Reverb_TailFadesAwayInARoom()
    {
        var output = Render(new Impulse(), Fx(reverb: 1f, room: ReverbKind.Room), Rate * 3);

        var early = Energy(output, 1, Rate / 2);
        var late = Energy(output, Rate * 5 / 2, Rate * 3);

        Assert.True(early > 0);
        Assert.True(late < early * 0.001);
        Assert.All(output, sample => Assert.True(float.IsFinite(sample)));
    }

    [Theory]
    [InlineData(ReverbKind.Room)]
    [InlineData(ReverbKind.Hall)]
    [InlineData(ReverbKind.Stage)]
    public void Reverb_StaysStableOnContinuousSound(ReverbKind room)
    {
        var output = Render(new Sine(), Fx(reverb: 1f, room: room), Rate * 5);

        Assert.All(output, sample => Assert.True(float.IsFinite(sample) && Math.Abs(sample) < 4f));
    }

    [Fact]
    public void Virtualizer_SpreadsASoundFromOneEarToTheOther()
    {
        var off = Render(new OneEar(), Fx(), Rate);
        var on = Render(new OneEar(), Fx(virtualizer: 1f), Rate);

        Assert.True(Rms(off, 1, 4800) < 1e-6);                       // nothing in the right ear
        Assert.True(Rms(on, 1, 4800) > Rms(on, 0, 4800) * 0.1);      // now it is there
    }
}
