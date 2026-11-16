using Audora.Core.Cleanup;

namespace Audora.Core.Audio;

public sealed partial class MirrorController
{
    /// <summary>Turns noise removal on or off for one device and sets how strong it is (0 to 1).</summary>
    public void SetNoiseRemoval(string deviceId, bool enabled, float strength)
    {
        var rebuild = false;
        string? note = null;
        lock (_gate)
        {
            var settings = GetOrCreate(deviceId);
            var changed = settings.NoiseRemoval != enabled;
            settings.NoiseRemoval = enabled;
            if (float.IsFinite(strength))
            {
                settings.NoiseStrength = Math.Clamp(strength, 0f, 1f);
            }

            var engine = _engine;
            var channel = engine?.Outputs.FirstOrDefault(o =>
                string.Equals(o.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
            if (engine is null || channel is null)
            {
                return;
            }

            if (channel.NoiseRemovalResampled != NeedsResampling(engine, settings))
            {
                // Sound that is not 48 kHz is converted when the output is built, so build it again.
                engine.RemoveOutput(deviceId);
                _addedAt.Remove(deviceId);
                rebuild = true;
                note = $"Noise removal is {StateWord(enabled)} for {channel.Name}. The output is rebuilt.";
            }
            else
            {
                channel.ApplyNoiseRemoval(settings.NoiseRemoval, settings.NoiseStrength);
                if (changed)
                {
                    var converted = channel.NoiseRemovalResampled
                        ? $" (the sound is converted from {channel.SourceSampleRate} Hz)"
                        : string.Empty;
                    note = channel.NoiseRemovalFailed
                        ? $"Noise removal could not start for {channel.Name}: {channel.NoiseRemovalMessage}"
                        : $"Noise removal is {StateWord(enabled)} for {channel.Name}{converted}.";
                }
            }
        }

        if (note is not null)
        {
            Raise(note);
        }

        if (rebuild)
        {
            Reconcile();
        }
    }

    /// <summary>Why noise removal cannot work on the sound Audora is copying right now, or null when it can.</summary>
    public string? NoiseRemovalUnavailableReason
    {
        get
        {
            lock (_gate)
            {
                return _engine?.CaptureFormat is { Channels: > 2 }
                    ? "Noise removal works with mono and stereo sound only."
                    : null;
            }
        }
    }

    private static string StateWord(bool enabled) => enabled ? "on" : "off";

    private static bool NeedsResampling(MirrorEngine engine, DeviceSettings settings) =>
        settings.NoiseRemoval
        && engine.CaptureFormat is { } format
        && format.SampleRate != VoiceCleaner.SampleRate
        && format.Channels is >= 1 and <= 2;
}
