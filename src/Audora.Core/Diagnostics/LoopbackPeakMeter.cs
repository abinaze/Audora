using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Audora.Core.Diagnostics;

/// <summary>
/// Listens to what one output device plays (WASAPI loopback) and remembers only the highest sample since the
/// last call. It saves no audio and changes nothing. It is used to find out where Windows takes that sound from:
/// before or after the Windows volume.
/// </summary>
public sealed class LoopbackPeakMeter : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private WasapiLoopbackCapture? _capture;
    private MMDevice? _device;
    private volatile float _peak;

    /// <summary>Starts listening to the sound that plays on the given device.</summary>
    public void Start(string deviceId)
    {
        if (_capture is not null)
        {
            return;
        }

        var device = _enumerator.GetDevice(deviceId);
        WasapiLoopbackCapture? capture = null;
        try
        {
            capture = new WasapiLoopbackCapture(device);
            if (capture.WaveFormat.BitsPerSample != 32)
            {
                throw new NotSupportedException($"Unexpected capture format ({capture.WaveFormat}); expected 32-bit float.");
            }

            capture.DataAvailable += OnData;
            capture.StartRecording();
        }
        catch
        {
            capture?.Dispose();
            device.Dispose();
            throw;
        }

        _device = device;
        _capture = capture;
    }

    /// <summary>The highest sample magnitude (0 to 1) heard since the last call. Reading it starts a new period.</summary>
    public float TakePeak()
    {
        var value = _peak;
        _peak = 0f;
        return value;
    }

    /// <summary>The highest sample magnitude in the first bytes of a buffer of 32-bit float samples.</summary>
    public static float PeakOf(byte[] buffer, int bytes)
    {
        var count = Math.Clamp(bytes, 0, buffer.Length);
        var samples = MemoryMarshal.Cast<byte, float>(buffer.AsSpan(0, count - count % sizeof(float)));
        var peak = 0f;
        foreach (var sample in samples)
        {
            var magnitude = Math.Abs(sample);
            if (magnitude > peak)
            {
                peak = magnitude;
            }
        }

        return peak;
    }

    public void Dispose()
    {
        var capture = _capture;
        _capture = null;
        if (capture is not null)
        {
            capture.DataAvailable -= OnData;
            try { capture.StopRecording(); } catch { }
            try { capture.Dispose(); } catch { }
        }

        try { _device?.Dispose(); } catch { }
        _device = null;
        _enumerator.Dispose();
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        var peak = PeakOf(e.Buffer, e.BytesRecorded);
        if (peak > _peak)
        {
            _peak = peak;
        }
    }
}
