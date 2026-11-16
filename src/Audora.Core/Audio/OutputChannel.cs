using NAudio.CoreAudioApi;
using NAudio.Wave;
using Audora.Core.Cleanup;

namespace Audora.Core.Audio;

/// <summary>
/// One output device receiving mirrored audio, with its own buffer, volume,
/// mute and balance. Changing one channel never affects another.
/// </summary>
public sealed partial class OutputChannel : IDisposable
{
    private readonly MMDevice _device;
    private readonly BufferedWaveProvider _buffer;
    private readonly GainBalanceSampleProvider _gain;
    private readonly DelaySampleProvider _delay;
    private readonly EqualizerSampleProvider _eq;
    private readonly VoiceCleanupSampleProvider _noise;
    private readonly WasapiOut _out;
    private bool _disposed;

    internal OutputChannel(MMDevice device, string name, WaveFormat sourceFormat, bool resampleForNoiseRemoval = false)
    {
        _device = device;
        DeviceId = device.ID;
        Name = name;
        SourceSampleRate = sourceFormat.SampleRate;

        _buffer = new BufferedWaveProvider(sourceFormat)
        {
            BufferDuration = TimeSpan.FromMilliseconds(500),
            DiscardOnBufferOverflow = true,
            ReadFully = true
        };

        _noise = new VoiceCleanupSampleProvider(_buffer.ToSampleProvider(), resampleForNoiseRemoval);
        _eq = new EqualizerSampleProvider(_noise);
        _gain = new GainBalanceSampleProvider(_eq);
        _delay = new DelaySampleProvider(_gain);
        _out = new WasapiOut(device, AudioClientShareMode.Shared, true, 100);
        _out.PlaybackStopped += OnPlaybackStopped;
        _out.Init(_delay.ToWaveProvider());
        _out.Play();
    }

    public string DeviceId { get; }

    public string Name { get; }

    /// <summary>Raised when this device's output stops because of an error (for example, it was disconnected).</summary>
    public event EventHandler<Exception>? Faulted;

    /// <summary>Slider position 0..1. Applied on top of the device's own Windows volume.</summary>
    public float Volume
    {
        get => _gain.Volume;
        set => _gain.Volume = Math.Clamp(value, 0f, 2f);
    }

    public bool IsMuted
    {
        get => _gain.Muted;
        set => _gain.Muted = value;
    }

    /// <summary>-1 = left only, 0 = centered, +1 = right only.</summary>
    public float Balance
    {
        get => _gain.Balance;
        set => _gain.Balance = Math.Clamp(value, -1f, 1f);
    }

    internal void Write(byte[] data, int offset, int count)
    {
        if (!_disposed)
        {
            _buffer.AddSamples(data, offset, count);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _out.PlaybackStopped -= OnPlaybackStopped;
        try { _out.Stop(); } catch { }
        try { _out.Dispose(); } catch { }
        try { _noise.Dispose(); } catch { }
        try { _device.Dispose(); } catch { }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null && !_disposed)
        {
            IsFaulted = true;
            Faulted?.Invoke(this, e.Exception);
        }
    }
}
