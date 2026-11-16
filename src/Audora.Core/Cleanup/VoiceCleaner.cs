namespace Audora.Core.Cleanup;

/// <summary>
/// Cleans the voice in 48 kHz sound of one or two channels, in place. It fades in and out when it is switched,
/// so switching never clicks. While it is off it does nothing and adds no delay. While it is on, the sound is
/// delayed by one denoiser frame (10 ms for RNNoise). <see cref="Strength"/> mixes the cleaned and the original
/// sound, both delayed the same, so the mix does not colour the sound. If anything goes wrong it switches itself
/// off and the sound passes through untouched.
/// </summary>
public sealed class VoiceCleaner : IDisposable
{
    public const int SampleRate = 48000;

    /// <summary>The fade when switching: 30 ms at 48 kHz.</summary>
    public const int DefaultFadeSamples = 1440;

    private readonly object _gate = new();
    private readonly int _channels;
    private readonly Func<IFrameDenoiser> _factory;
    private readonly float _fadeStep;
    private IFrameDenoiser[]? _denoisers;
    private FrameStreamProcessor[] _wet = Array.Empty<FrameStreamProcessor>();
    private FixedDelayLine[] _dry = Array.Empty<FixedDelayLine>();
    private float[][] _wetScratch = Array.Empty<float[]>();
    private float[][] _dryScratch = Array.Empty<float[]>();
    private volatile bool _enabled;
    private volatile float _strength = 1f;
    private float _fade;
    private bool _needsReset;
    private bool _failed;
    private string? _error;
    private bool _disposed;

    public VoiceCleaner(int channels, Func<IFrameDenoiser> denoiserFactory, int fadeSamples = DefaultFadeSamples)
    {
        if (channels is < 1 or > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(channels), "Only mono and stereo sound are supported.");
        }

        ArgumentNullException.ThrowIfNull(denoiserFactory);
        if (fadeSamples < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(fadeSamples), "The fade must be at least one sample.");
        }

        _channels = channels;
        _factory = denoiserFactory;
        _fadeStep = 1f / fadeSamples;
    }

    /// <summary>Switches the cleaning on or off. Switching on starts the denoisers, so do it off the audio thread.</summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (value)
            {
                lock (_gate)
                {
                    Prepare();
                }
            }

            _enabled = value;
        }
    }

    /// <summary>0 keeps only the original sound, 1 plays only the cleaned sound.</summary>
    public float Strength
    {
        get => _strength;
        set
        {
            if (float.IsFinite(value))
            {
                _strength = Math.Clamp(value, 0f, 1f);
            }
        }
    }

    /// <summary>True when the denoiser could not start or stopped working. The sound then passes through untouched.</summary>
    public bool Failed
    {
        get
        {
            lock (_gate)
            {
                return _failed;
            }
        }
    }

    /// <summary>Why it failed, or null.</summary>
    public string? Error
    {
        get
        {
            lock (_gate)
            {
                return _error;
            }
        }
    }

    /// <summary>Cleans interleaved samples in place. Only whole frames of sound are touched.</summary>
    public void Process(float[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        lock (_gate)
        {
            if (_failed || _disposed)
            {
                return;
            }

            var enabled = _enabled;
            if (!enabled && _fade <= 0f)
            {
                return;
            }

            if (_denoisers is null)
            {
                return;
            }

            var frames = count / _channels;
            if (frames == 0)
            {
                return;
            }

            try
            {
                Run(buffer, offset, frames, enabled);
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _enabled = false;
            ReleaseDenoisers();
        }
    }

    private void Prepare()
    {
        if (_failed || _disposed || _denoisers is not null)
        {
            return;
        }

        var created = new List<IFrameDenoiser>();
        try
        {
            for (var channel = 0; channel < _channels; channel++)
            {
                created.Add(_factory());
            }

            _denoisers = created.ToArray();
            BuildState();
        }
        catch (Exception ex)
        {
            foreach (var denoiser in created)
            {
                SafeDispose(denoiser);
            }

            _denoisers = null;
            Fail(ex);
        }
    }

    private void BuildState()
    {
        var denoisers = _denoisers!;
        _wet = denoisers.Select(d => new FrameStreamProcessor(d)).ToArray();
        _dry = denoisers.Select(d => new FixedDelayLine(d.FrameSize)).ToArray();
        _needsReset = false;
    }

    private void Run(float[] buffer, int offset, int frames, bool enabled)
    {
        if (_needsReset)
        {
            BuildState();
        }

        EnsureScratch(frames);

        // The sound is only changed in the mixing loop at the end, so a failure above leaves it untouched.
        for (var channel = 0; channel < _channels; channel++)
        {
            var wet = _wetScratch[channel];
            var dry = _dryScratch[channel];
            var delay = _dry[channel];
            for (var frame = 0; frame < frames; frame++)
            {
                var sample = buffer[offset + frame * _channels + channel];
                wet[frame] = sample;
                dry[frame] = delay.Push(sample);
            }

            _wet[channel].Process(wet.AsSpan(0, frames));
        }

        var strength = _strength;
        for (var frame = 0; frame < frames; frame++)
        {
            _fade = enabled ? Math.Min(1f, _fade + _fadeStep) : Math.Max(0f, _fade - _fadeStep);
            var fade = _fade;
            for (var channel = 0; channel < _channels; channel++)
            {
                var index = offset + frame * _channels + channel;
                var aligned = _dryScratch[channel][frame] * (1f - strength) + _wetScratch[channel][frame] * strength;
                buffer[index] = buffer[index] * (1f - fade) + aligned * fade;
            }
        }

        if (!enabled && _fade <= 0f)
        {
            _needsReset = true;
        }
    }

    private void EnsureScratch(int frames)
    {
        if (_wetScratch.Length == _channels && _wetScratch[0].Length >= frames)
        {
            return;
        }

        var size = Math.Max(frames, 4096);
        _wetScratch = new float[_channels][];
        _dryScratch = new float[_channels][];
        for (var channel = 0; channel < _channels; channel++)
        {
            _wetScratch[channel] = new float[size];
            _dryScratch[channel] = new float[size];
        }
    }

    private void Fail(Exception ex)
    {
        _failed = true;
        _error = ex.Message;
        ReleaseDenoisers();
    }

    private void ReleaseDenoisers()
    {
        if (_denoisers is not null)
        {
            foreach (var denoiser in _denoisers)
            {
                SafeDispose(denoiser);
            }
        }

        _denoisers = null;
        _wet = Array.Empty<FrameStreamProcessor>();
        _dry = Array.Empty<FixedDelayLine>();
    }

    private static void SafeDispose(IDisposable disposable)
    {
        try
        {
            disposable.Dispose();
        }
        catch
        {
        }
    }
}
