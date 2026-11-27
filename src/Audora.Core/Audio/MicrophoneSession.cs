using System.Runtime.InteropServices;
using Audora.Core.Cleanup;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Audora.Core.Audio;

/// <summary>
/// Listens to one microphone and cleans its voice. It never saves, plays or sends the sound. It only measures
/// loudness. Nothing starts until <see cref="Start"/>, and noise removal stays off until it is switched on. If the
/// microphone cannot be opened, <see cref="Start"/> says why instead of throwing.
/// </summary>
public sealed class MicrophoneSession : IDisposable
{
    private readonly object _gate = new();
    private MMDeviceEnumerator? _enumerator;
    private MMDevice? _device;
    private WasapiCapture? _capture;
    private volatile MicrophoneCleaner? _cleaner;
    private bool _noiseRemoval;
    private float _strength = 1f;
    private readonly string _appFolder;
    private string _engine = NoiseEngines.Default;
    private NoiseEngineSelector? _selector;
    private bool _disposed;

    public MicrophoneSession(string deviceId, string? appFolder = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        DeviceId = deviceId;
        _appFolder = string.IsNullOrWhiteSpace(appFolder) ? AppContext.BaseDirectory : appFolder;
    }

    /// <summary>Raised on a background thread when the microphone stops by itself, for example when it is unplugged.</summary>
    public event Action<string?>? Stopped;

    public string DeviceId { get; }

    /// <summary>The sample rate of the microphone, known after a successful <see cref="Start"/>.</summary>
    public int SampleRate { get; private set; }

    /// <summary>The channels of the microphone, known after a successful <see cref="Start"/>.</summary>
    public int Channels { get; private set; }

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _capture is not null;
            }
        }
    }

    /// <summary>Switches noise removal on or off. It can be set before <see cref="Start"/>.</summary>
    public bool NoiseRemoval
    {
        get
        {
            lock (_gate)
            {
                return _noiseRemoval;
            }
        }

        set
        {
            MicrophoneCleaner? cleaner;
            lock (_gate)
            {
                _noiseRemoval = value;
                cleaner = _cleaner;
            }

            if (cleaner is not null)
            {
                cleaner.Enabled = value;
            }
        }
    }

    /// <summary>How much of the sound is the cleaned one, 0 to 1.</summary>
    public float Strength
    {
        get
        {
            lock (_gate)
            {
                return _strength;
            }
        }

        set
        {
            var fixedValue = float.IsFinite(value) ? Math.Clamp(value, 0f, 1f) : 1f;
            MicrophoneCleaner? cleaner;
            lock (_gate)
            {
                _strength = fixedValue;
                cleaner = _cleaner;
            }

            if (cleaner is not null)
            {
                cleaner.Strength = fixedValue;
            }
        }
    }

    /// <summary>
    /// Which engine cleans the voice, "Standard" or "Studio". Studio falls back to Standard when its files are missing
    /// or it fails to start. It is read when the microphone starts, so set it before <see cref="Start"/>.
    /// </summary>
    public string Engine
    {
        get
        {
            lock (_gate)
            {
                return _engine;
            }
        }

        set
        {
            lock (_gate)
            {
                _engine = NoiseEngines.Normalize(value);
            }
        }
    }

    /// <summary>The engine in use, or the one that will be tried first.</summary>
    public string ActiveEngine
    {
        get
        {
            lock (_gate)
            {
                return _selector?.ActiveEngine ?? _engine;
            }
        }
    }

    /// <summary>A plain sentence when Studio was wanted but Standard is used instead. Null otherwise, and before the microphone starts.</summary>
    public string? EngineNote
    {
        get
        {
            lock (_gate)
            {
                return _selector?.FallbackReason;
            }
        }
    }

    /// <summary>True when noise removal could not start or stopped working. The sound is then left untouched.</summary>
    public bool NoiseRemovalFailed
    {
        get
        {
            lock (_gate)
            {
                return _cleaner?.Failed ?? false;
            }
        }
    }

    public string? NoiseRemovalReason
    {
        get
        {
            lock (_gate)
            {
                return _cleaner?.UnavailableReason;
            }
        }
    }

    /// <summary>Opens the microphone. Returns null when it works, or a plain sentence saying why not.</summary>
    public string? Start()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return "This microphone session is closed.";
            }

            if (_capture is not null)
            {
                return null;
            }

            try
            {
                _enumerator = new MMDeviceEnumerator();
                _device = _enumerator.GetDevice(DeviceId);
                var capture = new WasapiCapture(_device);
                _capture = capture;

                var format = capture.WaveFormat;
                if (format.BitsPerSample != 32)
                {
                    Teardown();
                    return $"This microphone's sound format ({format.BitsPerSample}-bit) is not supported.";
                }

                var selector = NoiseEngineSelector.ForMicrophone(_engine, _appFolder);
                _selector = selector;
                var cleaner = new MicrophoneCleaner(format.SampleRate, format.Channels, selector.Create);
                _cleaner = cleaner;
                if (!cleaner.IsSupported)
                {
                    var reason = cleaner.UnavailableReason ?? "This microphone's sound is not supported.";
                    Teardown();
                    return reason;
                }

                SampleRate = format.SampleRate;
                Channels = format.Channels;
                cleaner.Strength = _strength;
                cleaner.Enabled = _noiseRemoval;
                capture.DataAvailable += OnData;
                capture.RecordingStopped += OnStopped;
                capture.StartRecording();
                return null;
            }
            catch (Exception ex)
            {
                Teardown();
                return DescribeStartError(ex);
            }
        }
    }

    /// <summary>Stops listening. Safe to call at any time.</summary>
    public void Stop()
    {
        lock (_gate)
        {
            Teardown();
        }
    }

    /// <summary>The loudness since the last call. All zero when the microphone is not running.</summary>
    public MicrophoneLevels TakeLevels()
    {
        lock (_gate)
        {
            return _cleaner?.TakeLevels() ?? default;
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
            Teardown();
        }
    }

    /// <summary>A plain sentence for a problem with opening or running a microphone.</summary>
    public static string DescribeStartError(Exception ex)
    {
        if (ex is UnauthorizedAccessException || (ex is COMException com && com.HResult == unchecked((int)0x80070005)))
        {
            return "Windows blocked access to the microphone. Check Settings > Privacy & security > Microphone.";
        }

        return string.IsNullOrWhiteSpace(ex.Message) ? "The microphone could not be opened." : ex.Message;
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        var cleaner = _cleaner;
        if (cleaner is null)
        {
            return;
        }

        cleaner.Push(e.Buffer, e.BytesRecorded);
        cleaner.ReadAvailable();
    }

    private void OnStopped(object? sender, StoppedEventArgs e)
    {
        Stopped?.Invoke(e.Exception is null ? null : DescribeStartError(e.Exception));
    }

    // Called with the lock held. The handlers are removed first, so no audio thread comes back in while stopping.
    private void Teardown()
    {
        var capture = _capture;
        var cleaner = _cleaner;
        var device = _device;
        var enumerator = _enumerator;
        _capture = null;
        _cleaner = null;
        _selector = null;
        _device = null;
        _enumerator = null;

        if (capture is not null)
        {
            capture.DataAvailable -= OnData;
            capture.RecordingStopped -= OnStopped;
            try { capture.StopRecording(); } catch { }
            try { capture.Dispose(); } catch { }
        }

        try { cleaner?.Dispose(); } catch { }
        try { device?.Dispose(); } catch { }
        try { enumerator?.Dispose(); } catch { }
    }
}
