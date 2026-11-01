using NAudio.Wave;

namespace Audora.Core.Audio;

/// <summary>
/// Delays a float sample stream by a adjustable number of milliseconds using a ring buffer.
/// Used to line a fast device up with a slower one (for example wired versus Bluetooth).
/// Changing the delay while audio is playing may cause a brief click.
/// </summary>
public sealed class DelaySampleProvider : ISampleProvider
{
    public const int MaxDelayMilliseconds = 2000;

    private readonly ISampleProvider _source;
    private readonly float[] _ring;
    private int _writePosition;
    private volatile int _delayMilliseconds;

    public DelaySampleProvider(ISampleProvider source)
    {
        _source = source;
        var format = source.WaveFormat;
        var maxFrames = format.SampleRate * MaxDelayMilliseconds / 1000;
        _ring = new float[(maxFrames + 1) * format.Channels];
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>Delay in milliseconds, clamped to 0..2000.</summary>
    public int DelayMilliseconds
    {
        get => _delayMilliseconds;
        set => _delayMilliseconds = Math.Clamp(value, 0, MaxDelayMilliseconds);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var read = _source.Read(buffer, offset, count);
        var channels = WaveFormat.Channels;
        var delay = (int)((long)_delayMilliseconds * WaveFormat.SampleRate / 1000) * channels;
        var size = _ring.Length;

        for (var i = 0; i < read; i++)
        {
            _ring[_writePosition] = buffer[offset + i];

            var readPosition = _writePosition - delay;
            if (readPosition < 0)
            {
                readPosition += size;
            }

            buffer[offset + i] = _ring[readPosition];
            _writePosition = (_writePosition + 1) % size;
        }

        return read;
    }
}
