using NAudio.Wave;

namespace Audora.Core.Audio;

/// <summary>
/// Applies per-device volume, mute and left/right balance to a float sample stream.
/// Volume is a 0..1 slider position mapped through a squared curve so the
/// slider feels natural to the ear.
/// </summary>
public sealed class GainBalanceSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;

    public GainBalanceSampleProvider(ISampleProvider source)
    {
        _source = source;
        Volume = 1f;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>Slider position, 0 (silent) to 1 (full).</summary>
    public volatile float Volume;

    public volatile bool Muted;

    /// <summary>-1 = left only, 0 = centered, +1 = right only.</summary>
    public volatile float Balance;

    public int Read(float[] buffer, int offset, int count)
    {
        var read = _source.Read(buffer, offset, count);

        var slider = Math.Clamp(Volume, 0f, 1f);
        var gain = Muted ? 0f : slider * slider;
        var balance = Math.Clamp(Balance, -1f, 1f);

        if (WaveFormat.Channels == 2)
        {
            var left = gain * (balance > 0f ? 1f - balance : 1f);
            var right = gain * (balance < 0f ? 1f + balance : 1f);

            for (var i = 0; i + 1 < read; i += 2)
            {
                buffer[offset + i] *= left;
                buffer[offset + i + 1] *= right;
            }
        }
        else
        {
            for (var i = 0; i < read; i++)
            {
                buffer[offset + i] *= gain;
            }
        }

        return read;
    }
}
