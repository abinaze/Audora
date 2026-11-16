using NAudio.Wave;

namespace Audora.Core.Cleanup;

/// <summary>
/// Sits in the output chain of one device and cleans the voice in the sound that passes through. Until it is
/// switched on it does nothing and adds no delay. It needs 48 kHz sound in one or two channels. For any other
/// sound <see cref="IsSupported"/> is false and the sound always passes through untouched.
/// </summary>
public sealed class VoiceCleanupSampleProvider : ISampleProvider, IDisposable
{
    private readonly ISampleProvider _source;
    private readonly VoiceCleaner? _cleaner;
    private readonly string? _unsupportedReason;

    public VoiceCleanupSampleProvider(ISampleProvider source)
        : this(source, () => new RnNoiseDenoiser())
    {
    }

    public VoiceCleanupSampleProvider(ISampleProvider source, Func<IFrameDenoiser> denoiserFactory)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(denoiserFactory);
        _source = source;

        var format = source.WaveFormat;
        if (format.SampleRate != VoiceCleaner.SampleRate)
        {
            _unsupportedReason = $"Clean voice needs 48 kHz sound. This sound is {format.SampleRate} Hz.";
        }
        else if (format.Channels is < 1 or > 2)
        {
            _unsupportedReason = "Clean voice works with mono and stereo sound only.";
        }
        else
        {
            _cleaner = new VoiceCleaner(format.Channels, denoiserFactory);
        }
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>True when this sound can be cleaned (48 kHz, mono or stereo).</summary>
    public bool IsSupported => _cleaner is not null;

    /// <summary>True when cleaning could not start or stopped working. The sound then passes through untouched.</summary>
    public bool Failed => _cleaner?.Failed ?? false;

    /// <summary>Why cleaning is not available, or null when it is.</summary>
    public string? UnavailableReason => _unsupportedReason ?? _cleaner?.Error;

    public bool Enabled
    {
        get => _cleaner?.Enabled ?? false;
        set
        {
            if (_cleaner is not null)
            {
                _cleaner.Enabled = value;
            }
        }
    }

    public float Strength
    {
        get => _cleaner?.Strength ?? 0f;
        set
        {
            if (_cleaner is not null)
            {
                _cleaner.Strength = value;
            }
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var read = _source.Read(buffer, offset, count);
        _cleaner?.Process(buffer, offset, read);
        return read;
    }

    public void Dispose()
    {
        _cleaner?.Dispose();
    }
}
