using NAudio.CoreAudioApi;

namespace Audora.Core.Devices;

public readonly record struct VolumeState(float Volume, bool Muted);

/// <summary>
/// Follows the Windows volume and mute of the default output (the Windows volume bar and keyboard
/// volume keys) and can change them. Call <see cref="Attach"/> whenever the default device changes.
/// <see cref="Changed"/> can be raised on any thread.
/// </summary>
public sealed class DefaultVolumeWatcher : IDisposable
{
    private readonly object _gate = new();
    private readonly MMDeviceEnumerator _enumerator = new();
    private MMDevice? _device;
    private AudioEndpointVolume? _endpoint;
    private bool _disposed;
    private bool _mutedByUs;

    public event EventHandler<VolumeState>? Changed;

    public string? DeviceId { get; private set; }

    public void Attach(string? deviceId)
    {
        VolumeState? current = null;

        lock (_gate)
        {
            if (_disposed || string.Equals(deviceId, DeviceId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ReleaseOurMute();
            Detach();
            if (deviceId is null)
            {
                return;
            }

            try
            {
                _device = _enumerator.GetDevice(deviceId);
                _endpoint = _device.AudioEndpointVolume;
                _endpoint.OnVolumeNotification += OnNotification;
                DeviceId = deviceId;
                current = new VolumeState(_endpoint.MasterVolumeLevelScalar, _endpoint.Mute);
            }
            catch
            {
                Detach();
            }
        }

        if (current is not null)
        {
            Raise(current.Value);
        }
    }

    public void SetVolume(float volume)
    {
        lock (_gate)
        {
            try
            {
                if (_endpoint is not null)
                {
                    _endpoint.MasterVolumeLevelScalar = Math.Clamp(volume, 0f, 1f);
                }
            }
            catch
            {
                // device went away
            }
        }
    }

    public void SetMuted(bool muted)
    {
        lock (_gate)
        {
            try
            {
                if (_endpoint is not null)
                {
                    _endpoint.Mute = muted;
                    _mutedByUs = muted;
                }
            }
            catch
            {
                // device went away
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
            Detach();
            try { _enumerator.Dispose(); } catch { }
        }
    }

    /// <summary>
    /// Audora's Mute button mutes the default device. If the default changes, give the old device its sound back
    /// so it is not left muted. Mutes made by Windows or by you elsewhere are left alone.
    /// </summary>
    private void ReleaseOurMute()
    {
        if (_mutedByUs && _endpoint is not null)
        {
            try
            {
                _endpoint.Mute = false;
            }
            catch
            {
                // the device may already be gone
            }
        }

        _mutedByUs = false;
    }

    private void Detach()
    {
        if (_endpoint is not null)
        {
            try { _endpoint.OnVolumeNotification -= OnNotification; } catch { }
            try { _endpoint.Dispose(); } catch { }
        }

        try { _device?.Dispose(); } catch { }

        _endpoint = null;
        _device = null;
        DeviceId = null;
    }

    private void OnNotification(AudioVolumeNotificationData data) =>
        Raise(new VolumeState(data.MasterVolume, data.Muted));

    private void Raise(VolumeState state)
    {
        try
        {
            Changed?.Invoke(this, state);
        }
        catch
        {
            // a misbehaving listener must not break volume handling
        }
    }
}
