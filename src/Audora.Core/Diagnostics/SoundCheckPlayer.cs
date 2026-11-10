using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Audora.Core.Diagnostics;

/// <summary>The Windows volume and mute state of a device. Read only.</summary>
public sealed record DeviceVolumeState(float Volume, bool Muted);

/// <summary>
/// Plays a test tone on one device in shared mode. It never changes the Windows volume, never uses
/// exclusive mode, and Stop ends the sound at once.
/// </summary>
public sealed class SoundCheckPlayer : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly object _gate = new();
    private WasapiOut? _out;
    private MMDevice? _device;

    /// <summary>Raised when the tone has played to the end or the device stopped. Call Stop or Dispose afterwards.</summary>
    public event EventHandler? Finished;

    /// <summary>Reads the Windows volume and mute state of a device. Returns null if Windows will not say.</summary>
    public DeviceVolumeState? ReadWindowsVolume(string deviceId)
    {
        try
        {
            using var device = _enumerator.GetDevice(deviceId);
            var endpoint = device.AudioEndpointVolume;
            return new DeviceVolumeState(endpoint.MasterVolumeLevelScalar, endpoint.Mute);
        }
        catch
        {
            return null;
        }
    }

    public void Play(string deviceId, ToneRequest request)
    {
        Stop();

        var device = _enumerator.GetDevice(deviceId);
        WasapiOut? output = null;
        try
        {
            var mix = device.AudioClient.MixFormat;
            var provider = new ToneSampleProvider(mix.SampleRate, mix.Channels, request);

            output = new WasapiOut(device, AudioClientShareMode.Shared, true, 100);
            output.PlaybackStopped += OnStopped;
            output.Init(provider.ToWaveProvider());
            output.Play();

            lock (_gate)
            {
                _device = device;
                _out = output;
            }
        }
        catch
        {
            if (output is not null)
            {
                output.PlaybackStopped -= OnStopped;
                output.Dispose();
            }

            device.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The peak level (0 to 1) of each channel at the device right now, as Windows measures it.
    /// Read only. Returns null when nothing is playing or the meter cannot be read.
    /// </summary>
    public float[]? ReadChannelPeaks()
    {
        MMDevice? device;
        lock (_gate)
        {
            device = _device;
        }

        if (device is null)
        {
            return null;
        }

        try
        {
            var channels = device.AudioMeterInformation.PeakValues;
            var result = new float[channels.Count];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = channels[i];
            }

            return result;
        }
        catch
        {
            return null; // the device may have just been released
        }
    }

    /// <summary>Ends the sound at once and lets go of the device.</summary>
    public void Stop()
    {
        WasapiOut? output;
        MMDevice? device;
        lock (_gate)
        {
            output = _out;
            device = _device;
            _out = null;
            _device = null;
        }

        if (output is not null)
        {
            output.PlaybackStopped -= OnStopped;
            try
            {
                output.Stop();
            }
            catch
            {
                // already stopped or the device is gone
            }

            output.Dispose();
        }

        device?.Dispose();
    }

    public void Dispose()
    {
        Stop();
        _enumerator.Dispose();
    }

    private void OnStopped(object? sender, StoppedEventArgs e) => Finished?.Invoke(this, EventArgs.Empty);
}
