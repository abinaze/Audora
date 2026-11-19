using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Audora.Core.Cleanup;

/// <summary>How loud the microphone sound was (0 to 1) before and after cleaning, since the last time it was asked.</summary>
public readonly record struct MicrophoneLevels(float InputPeak, float InputRms, float OutputPeak, float OutputRms);

/// <summary>
/// Cleans the voice in the sound of one microphone. Feed it the bytes a Windows capture gives you (32-bit float,
/// mono or stereo) with <see cref="Push"/>, then collect the cleaned mono sound with <see cref="ReadAvailable"/>.
/// The output has the sample rate of the microphone. Until it is switched on it changes nothing. If the cleaning
/// fails, the sound passes through untouched. It never saves or sends any sound.
/// </summary>
public sealed class MicrophoneCleaner : IDisposable
{
    private const int MaxRounds = 100;

    private readonly object _gate = new();
    private readonly BufferedWaveProvider? _buffer;
    private readonly LevelTap? _inputTap;
    private readonly VoiceCleanupSampleProvider? _cleaner;
    private readonly string? _reason;
    private readonly float[] _scratch = new float[4800];
    private float _outputPeak;
    private double _outputSquares;
    private long _outputSamples;
    private bool _disposed;

    public MicrophoneCleaner(int sampleRate, int channels, Func<IFrameDenoiser> denoiserFactory)
    {
        ArgumentNullException.ThrowIfNull(denoiserFactory);
        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "The sample rate must be above zero.");
        }

        if (channels is < 1 or > 2)
        {
            _reason = "This microphone gives more than two channels. Noise removal works with mono and stereo sound only.";
            return;
        }

        var format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        _buffer = new BufferedWaveProvider(format)
        {
            ReadFully = false,
            DiscardOnBufferOverflow = true,
            BufferDuration = TimeSpan.FromSeconds(2),
        };

        ISampleProvider mono = _buffer.ToSampleProvider();
        if (channels == 2)
        {
            mono = new StereoToMonoSampleProvider(mono);
        }

        _inputTap = new LevelTap(mono);
        _cleaner = new VoiceCleanupSampleProvider(_inputTap, denoiserFactory, resampleIfNeeded: true);
    }

    /// <summary>True when this microphone's sound can be handled.</summary>
    public bool IsSupported => _cleaner is not null;

    /// <summary>True when noise removal could not start or stopped working. The sound then passes through untouched.</summary>
    public bool Failed => _cleaner?.Failed ?? false;

    /// <summary>Why noise removal is not available, or null when it is.</summary>
    public string? UnavailableReason => _reason ?? _cleaner?.UnavailableReason;

    /// <summary>Switches the cleaning on or off. Switching on starts the denoiser, so do it off the audio thread.</summary>
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

    /// <summary>0 keeps only the original sound, 1 plays only the cleaned sound.</summary>
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

    /// <summary>Adds sound from the microphone: bytes of 32-bit float samples, whole frames only.</summary>
    public void Push(byte[] data, int bytes)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (_buffer is null || bytes <= 0)
        {
            return;
        }

        lock (_gate)
        {
            if (!_disposed)
            {
                _buffer.AddSamples(data, 0, Math.Min(bytes, data.Length));
            }
        }
    }

    /// <summary>
    /// Cleans everything pushed so far. The cleaned mono samples are handed to <paramref name="sink"/> in pieces
    /// (the array and how many samples of it are valid). Returns how many samples were produced.
    /// </summary>
    public int ReadAvailable(Action<float[], int>? sink = null)
    {
        if (_cleaner is null)
        {
            return 0;
        }

        var total = 0;
        lock (_gate)
        {
            if (_disposed)
            {
                return 0;
            }

            for (var round = 0; round < MaxRounds; round++)
            {
                var read = _cleaner.Read(_scratch, 0, _scratch.Length);
                if (read <= 0)
                {
                    break;
                }

                Measure(read);
                total += read;
                sink?.Invoke(_scratch, read);
                if (read < _scratch.Length)
                {
                    break; // the pushed sound is used up
                }
            }
        }

        return total;
    }

    /// <summary>The loudness since the last call. Asking starts a new period.</summary>
    public MicrophoneLevels TakeLevels()
    {
        lock (_gate)
        {
            var input = _inputTap?.Take() ?? (0f, 0.0, 0L);
            var levels = new MicrophoneLevels(
                input.Peak,
                Rms(input.Squares, input.Samples),
                _outputPeak,
                Rms(_outputSquares, _outputSamples));
            _outputPeak = 0f;
            _outputSquares = 0.0;
            _outputSamples = 0;
            return levels;
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
            _cleaner?.Dispose();
        }
    }

    private static float Rms(double squares, long samples) => samples > 0 ? (float)Math.Sqrt(squares / samples) : 0f;

    private void Measure(int count)
    {
        for (var i = 0; i < count; i++)
        {
            var value = _scratch[i];
            var magnitude = Math.Abs(value);
            if (magnitude > _outputPeak)
            {
                _outputPeak = magnitude;
            }

            _outputSquares += (double)value * value;
        }

        _outputSamples += count;
    }

    private sealed class LevelTap : ISampleProvider
    {
        private readonly ISampleProvider _inner;
        private float _peak;
        private double _squares;
        private long _samples;

        public LevelTap(ISampleProvider inner)
        {
            _inner = inner;
        }

        public WaveFormat WaveFormat => _inner.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            var read = _inner.Read(buffer, offset, count);
            for (var i = 0; i < read; i++)
            {
                var value = buffer[offset + i];
                var magnitude = Math.Abs(value);
                if (magnitude > _peak)
                {
                    _peak = magnitude;
                }

                _squares += (double)value * value;
            }

            _samples += read;
            return read;
        }

        public (float Peak, double Squares, long Samples) Take()
        {
            var result = (_peak, _squares, _samples);
            _peak = 0f;
            _squares = 0.0;
            _samples = 0;
            return result;
        }
    }
}
