using NAudio.Wave;

namespace Audora.Core.Audio;

/// <summary>
/// Applies per-device volume, mute and left/right balance, with a soft limiter so boosting
/// above 100% never produces harsh digital clipping. Changes glide over one block of audio,
/// so dragging a slider never causes clicks.
/// Volume runs from 0 to 2 (0% to 200%). Up to 100% it follows a squared curve that feels
/// natural to the ear; above 100% it adds up to +12 dB.
/// </summary>
public sealed class GainBalanceSampleProvider : ISampleProvider
{
    private const float LimiterStart = 0.95f;
    private const float LimiterRange = 0.05f;

    private readonly ISampleProvider _source;
    private float _lastLeft = float.NaN;   // NaN until the first block
    private float _lastRight;

    public GainBalanceSampleProvider(ISampleProvider source)
    {
        _source = source;
        Volume = 1f;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>Slider position, 0 (silent) to 2 (200%).</summary>
    public volatile float Volume;

    public volatile bool Muted;

    /// <summary>-1 = left only, 0 = centered, +1 = right only.</summary>
    public volatile float Balance;

    public static float GainFor(float volume)
    {
        var v = Math.Clamp(volume, 0f, 2f);
        return v <= 1f ? v * v : MathF.Pow(10f, (v - 1f) * 12f / 20f);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var read = _source.Read(buffer, offset, count);
        var channels = WaveFormat.Channels;

        var gain = Muted ? 0f : GainFor(Volume);
        var balance = Math.Clamp(Balance, -1f, 1f);
        var left = channels == 2 ? gain * (balance > 0f ? 1f - balance : 1f) : gain;
        var right = channels == 2 ? gain * (balance < 0f ? 1f + balance : 1f) : gain;

        if (float.IsNaN(_lastLeft))
        {
            _lastLeft = left;
            _lastRight = right;
        }

        var frames = read / channels;
        for (var f = 0; f < frames; f++)
        {
            var t = (f + 1f) / frames;
            var l = _lastLeft + (left - _lastLeft) * t;
            var r = _lastRight + (right - _lastRight) * t;
            var index = offset + f * channels;

            if (channels == 2)
            {
                buffer[index] = Limit(buffer[index] * l);
                buffer[index + 1] = Limit(buffer[index + 1] * r);
            }
            else
            {
                for (var ch = 0; ch < channels; ch++)
                {
                    buffer[index + ch] = Limit(buffer[index + ch] * l);
                }
            }
        }

        for (var i = offset + frames * channels; i < offset + read; i++)
        {
            buffer[i] = Limit(buffer[i] * left);
        }

        _lastLeft = left;
        _lastRight = right;
        return read;
    }

    // Leaves normal levels alone and smoothly squeezes anything near or above full scale below 1.0.
    private static float Limit(float x)
    {
        var a = Math.Abs(x);
        if (a <= LimiterStart)
        {
            return x;
        }

        var limited = LimiterStart + LimiterRange * MathF.Tanh((a - LimiterStart) / LimiterRange);
        return x < 0f ? -limited : limited;
    }
}
