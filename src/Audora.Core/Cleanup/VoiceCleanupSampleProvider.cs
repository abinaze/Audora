using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Audora.Core.Cleanup;

/// <summary>
/// Sits in the output chain of one device and removes noise from the voices in the sound that passes through.
/// Until it is switched on it does nothing and adds no delay. The noise remover works on 48 kHz sound, so for
/// sound at another rate the provider can convert to 48 kHz and back. That conversion is part of the chain for as
/// long as the provider lives, so ask for it only when noise removal is wanted (<c>resampleIfNeeded</c>).
/// Without it, sound that is not 48 kHz is not supported and always passes through untouched.
/// </summary>
public sealed class VoiceCleanupSampleProvider : ISampleProvider, IDisposable
{
    private readonly ISampleProvider _source;
    private readonly ISampleProvider _output;
    private readonly VoiceCleaner? _cleaner;
    private readonly string? _unsupportedReason;

    public VoiceCleanupSampleProvider(ISampleProvider source, bool resampleIfNeeded = false)
        : this(source, () => new RnNoiseDenoiser(), resampleIfNeeded)
    {
    }

    public VoiceCleanupSampleProvider(ISampleProvider source, Func<IFrameDenoiser> denoiserFactory, bool resampleIfNeeded = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(denoiserFactory);
        _source = source;
        _output = source;

        var format = source.WaveFormat;
        if (format.Channels is < 1 or > 2)
        {
            _unsupportedReason = "Noise removal works with mono and stereo sound only.";
        }
        else if (format.SampleRate == VoiceCleaner.SampleRate)
        {
            _cleaner = new VoiceCleaner(format.Channels, denoiserFactory);
            _output = new CleaningStage(source, _cleaner);
        }
        else if (resampleIfNeeded)
        {
            _cleaner = new VoiceCleaner(format.Channels, denoiserFactory);
            var toCleaner = new WdlResamplingSampleProvider(source, VoiceCleaner.SampleRate);
            _output = new WdlResamplingSampleProvider(new CleaningStage(toCleaner, _cleaner), format.SampleRate);
            IsResampled = true;
        }
        else
        {
            _unsupportedReason = $"Noise removal needs 48 kHz sound. This sound is {format.SampleRate} Hz.";
        }
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>True when this sound can be cleaned.</summary>
    public bool IsSupported => _cleaner is not null;

    /// <summary>True when the sound is converted to 48 kHz and back, which only happens when it was asked for.</summary>
    public bool IsResampled { get; }

    /// <summary>True when noise removal could not start or stopped working. The sound then passes through untouched.</summary>
    public bool Failed => _cleaner?.Failed ?? false;

    /// <summary>Why noise removal is not available, or null when it is.</summary>
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

    public int Read(float[] buffer, int offset, int count) => _output.Read(buffer, offset, count);

    public void Dispose()
    {
        _cleaner?.Dispose();
    }

    private sealed class CleaningStage : ISampleProvider
    {
        private readonly ISampleProvider _inner;
        private readonly VoiceCleaner _cleaner;

        public CleaningStage(ISampleProvider inner, VoiceCleaner cleaner)
        {
            _inner = inner;
            _cleaner = cleaner;
        }

        public WaveFormat WaveFormat => _inner.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            var read = _inner.Read(buffer, offset, count);
            _cleaner.Process(buffer, offset, read);
            return read;
        }
    }
}
