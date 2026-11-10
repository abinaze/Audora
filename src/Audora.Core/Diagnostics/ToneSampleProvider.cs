using NAudio.Wave;

namespace Audora.Core.Diagnostics;

public enum ToneChannel
{
    Left,
    Right,
    Both
}

public enum ToneShape
{
    Steady,
    Sweep
}

/// <summary>What to play. Frequency is ignored for a sweep.</summary>
public sealed record ToneRequest(ToneChannel Channel, ToneShape Shape, double Frequency, double Seconds);

/// <summary>
/// Makes a quiet test tone on the front left and right channels only. It fades in and out, never goes above
/// <see cref="MaxLevel"/>, and ends by itself.
/// </summary>
public sealed class ToneSampleProvider : ISampleProvider
{
    /// <summary>About -20 dB: quiet.</summary>
    public const float DefaultLevel = 0.1f;

    /// <summary>The hard limit, about -12 dB. A larger request is cut down to this.</summary>
    public const float MaxLevel = 0.25f;

    public const double SweepStartHz = 100;
    public const double SweepEndHz = 8000;

    private const double FadeInSeconds = 0.5;
    private const double FadeOutSeconds = 0.3;

    private readonly int _sampleRate;
    private readonly int _channels;
    private readonly ToneRequest _request;
    private readonly float _level;
    private readonly long _totalFrames;
    private readonly double _totalSeconds;
    private long _frame;

    public ToneSampleProvider(int sampleRate, int channels, ToneRequest request, float level = DefaultLevel)
    {
        _sampleRate = sampleRate;
        _channels = Math.Max(1, channels);
        _request = request;
        _level = Math.Clamp(level, 0f, MaxLevel);
        _totalFrames = (long)(sampleRate * Math.Max(0.1, request.Seconds));
        _totalSeconds = _totalFrames / (double)sampleRate;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, _channels);
    }

    public WaveFormat WaveFormat { get; }

    /// <summary>Loudness from 0 to 1 at time t, with a soft start and a soft end.</summary>
    public static double Envelope(double t, double total)
    {
        var fadeIn = Math.Min(FadeInSeconds, total / 4);
        var fadeOut = Math.Min(FadeOutSeconds, total / 4);

        var start = t < fadeIn ? 0.5 - 0.5 * Math.Cos(Math.PI * t / fadeIn) : 1.0;
        var left = total - t;
        var end = left < fadeOut ? 0.5 - 0.5 * Math.Cos(Math.PI * Math.Max(left, 0) / fadeOut) : 1.0;
        return start * end;
    }

    /// <summary>The pitch of the sweep at time t. It rises smoothly from the start to the end pitch.</summary>
    public static double SweepFrequency(double t, double total) =>
        SweepStartHz * Math.Pow(SweepEndHz / SweepStartHz, Math.Clamp(t / total, 0, 1));

    /// <summary>Writes only whole frames, so the channels never get out of step. Returns 0 when the tone is over.</summary>
    public int Read(float[] buffer, int offset, int count)
    {
        var written = 0;
        while (written + _channels <= count && _frame < _totalFrames)
        {
            var t = _frame / (double)_sampleRate;
            var value = (float)(Math.Sin(Phase(t)) * Envelope(t, _totalSeconds) * _level);

            for (var channel = 0; channel < _channels; channel++)
            {
                buffer[offset + written] = Plays(channel) ? value : 0f;
                written++;
            }

            _frame++;
        }

        return written;
    }

    private bool Plays(int channel)
    {
        if (_channels == 1)
        {
            return true;
        }

        return _request.Channel switch
        {
            ToneChannel.Left => channel == 0,
            ToneChannel.Right => channel == 1,
            _ => channel <= 1
        };
    }

    private double Phase(double t)
    {
        if (_request.Shape == ToneShape.Steady)
        {
            return 2 * Math.PI * _request.Frequency * t;
        }

        var ratio = SweepEndHz / SweepStartHz;
        return 2 * Math.PI * SweepStartHz * _totalSeconds / Math.Log(ratio) * (Math.Pow(ratio, t / _totalSeconds) - 1);
    }
}
