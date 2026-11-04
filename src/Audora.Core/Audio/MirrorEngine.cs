using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Audora.Core.Audio;

/// <summary>
/// Captures everything playing on the Windows default output (WASAPI loopback)
/// and copies it to any number of additional output devices.
/// The default device keeps playing natively, so it is never a mirror target.
/// </summary>
public sealed partial class MirrorEngine : IDisposable
{
    private readonly MMDeviceEnumerator _devices = new();
    private readonly object _gate = new();
    private OutputChannel[] _outputs = Array.Empty<OutputChannel>();
    private WasapiLoopbackCapture? _capture;
    private MMDevice? _source;
    private WaveFormat? _format;
    private bool _disposed;

    public string? SourceDeviceId => _source?.ID;

    public string? SourceName => _source?.FriendlyName;

    /// <summary>Format of the captured audio (null until started).</summary>
    public WaveFormat? CaptureFormat => _format;

    public IReadOnlyList<OutputChannel> Outputs => Volatile.Read(ref _outputs);

    /// <summary>Raised if capturing stops unexpectedly (for example the default device changed or vanished).</summary>
    public event EventHandler<Exception?>? CaptureStopped;

    public void Start(string? sourceDeviceId = null)
    {
        ThrowIfDisposed();
        if (_capture is not null)
        {
            return;
        }

        _source = sourceDeviceId is null
            ? _devices.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
            : _devices.GetDevice(sourceDeviceId);
        var capture = new WasapiLoopbackCapture(_source);

        if (capture.WaveFormat.BitsPerSample != 32)
        {
            var description = capture.WaveFormat.ToString();
            capture.Dispose();
            throw new NotSupportedException($"Unexpected capture format ({description}); expected 32-bit float.");
        }

        _format = WaveFormat.CreateIeeeFloatWaveFormat(capture.WaveFormat.SampleRate, capture.WaveFormat.Channels);
        capture.DataAvailable += OnDataAvailable;
        capture.RecordingStopped += OnRecordingStopped;
        capture.StartRecording();
        _capture = capture;
    }

    public OutputChannel AddOutput(string deviceId, string? displayName = null)
    {
        ThrowIfDisposed();
        if (_capture is null || _format is null || _source is null)
        {
            throw new InvalidOperationException("Call Start() before adding outputs.");
        }

        if (string.Equals(deviceId, _source.ID, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "That device is the Windows default output, which already plays natively. Pick a different device.");
        }

        lock (_gate)
        {
            if (_outputs.Any(o => string.Equals(o.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("That device is already being mirrored to.");
            }

            var device = _devices.GetDevice(deviceId);
            try
            {
                var channel = new OutputChannel(device, displayName ?? device.FriendlyName, _format);
                _outputs = _outputs.Append(channel).ToArray();
                return channel;
            }
            catch
            {
                device.Dispose();
                throw;
            }
        }
    }

    public void RemoveOutput(string deviceId)
    {
        OutputChannel? removed;
        lock (_gate)
        {
            removed = _outputs.FirstOrDefault(o => string.Equals(o.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
            if (removed is null)
            {
                return;
            }

            _outputs = _outputs.Where(o => o != removed).ToArray();
        }

        removed.Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_capture is not null)
        {
            _capture.DataAvailable -= OnDataAvailable;
            _capture.RecordingStopped -= OnRecordingStopped;
            try { _capture.StopRecording(); } catch { }
            try { _capture.Dispose(); } catch { }
        }

        OutputChannel[] outputs;
        lock (_gate)
        {
            outputs = _outputs;
            _outputs = Array.Empty<OutputChannel>();
        }

        foreach (var output in outputs)
        {
            try { output.Dispose(); } catch { }
        }

        try { _source?.Dispose(); } catch { }
        try { _devices.Dispose(); } catch { }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        RecordStats(e.Buffer, e.BytesRecorded);
        foreach (var output in Volatile.Read(ref _outputs))
        {
            output.Write(e.Buffer, 0, e.BytesRecorded);
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (!_disposed)
        {
            CaptureStopped?.Invoke(this, e.Exception);
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
