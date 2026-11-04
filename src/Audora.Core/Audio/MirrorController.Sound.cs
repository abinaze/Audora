namespace Audora.Core.Audio;

public sealed partial class MirrorController
{
    private EqSettings _sound = EqSettings.Original;
    private readonly Dictionary<string, EqSettings> _deviceSound = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The sound for every device that does not have its own, now and when devices return.</summary>
    public void SetSound(EqSettings settings)
    {
        lock (_gate)
        {
            _sound = settings;
            ApplyAllSounds();
        }
    }

    /// <summary>Gives one device its own sound, or pass null to make it follow the shared sound again.</summary>
    public void SetDeviceSound(string deviceId, EqSettings? settings)
    {
        lock (_gate)
        {
            if (settings is null)
            {
                _deviceSound.Remove(deviceId);
            }
            else
            {
                _deviceSound[deviceId] = settings;
            }

            var channel = _engine?.Outputs.FirstOrDefault(o =>
                string.Equals(o.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
            channel?.ApplySound(SoundFor(deviceId));
        }
    }

    private EqSettings SoundFor(string deviceId) =>
        _deviceSound.TryGetValue(deviceId, out var own) ? own : _sound;

    private void ApplyAllSounds()
    {
        if (_engine is null)
        {
            return;
        }

        foreach (var output in _engine.Outputs)
        {
            output.ApplySound(SoundFor(output.DeviceId));
        }
    }
}
