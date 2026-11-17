using Audora.App.Infrastructure;

namespace Audora.App.ViewModels;

public sealed partial class DeviceViewModel
{
    private bool _noiseRemoval;
    private double _noiseStrength = 100;

    /// <summary>Removes steady background noise from the voices in the sound this device plays.</summary>
    public bool NoiseRemoval
    {
        get => _noiseRemoval;
        set
        {
            if (Set(ref _noiseRemoval, value) && !_applying && !_isDefault)
            {
                PushNoise();
            }
        }
    }

    /// <summary>0 keeps the original sound, 100 plays only the cleaned sound.</summary>
    public double NoiseStrength
    {
        get => _noiseStrength;
        set
        {
            if (Set(ref _noiseStrength, Math.Clamp(Math.Round(value), 0, 100)) && !_applying && !_isDefault)
            {
                PushNoise();
            }
        }
    }

    /// <summary>Why noise removal cannot work on the sound Audora copies right now, or null.</summary>
    public string? NoiseUnavailableReason => _controller.NoiseRemovalUnavailableReason;

    public bool CanUseNoiseRemoval => !_isDefault && NoiseUnavailableReason is null;

    public string NoiseHint => NoiseUnavailableReason
        ?? "For calls and talk only. It removes fans, hum and keyboard noise from voices, but it treats music as noise and dulls it, so keep it off for songs. It adds about 10 ms.";

    /// <summary>Shows and applies what was saved on disk.</summary>
    public void RestoreNoise(bool enabled, double strength)
    {
        _applying = true;
        try
        {
            Set(ref _noiseRemoval, enabled, nameof(NoiseRemoval));
            Set(ref _noiseStrength, Math.Clamp(strength, 0, 100), nameof(NoiseStrength));
        }
        finally
        {
            _applying = false;
        }

        PushNoise();
    }

    private void PushNoise()
    {
        var id = Id;
        var enabled = _noiseRemoval;
        var strength = (float)(_noiseStrength / 100.0);
        SerialWorker.Run(() => _controller.SetNoiseRemoval(id, enabled, strength));
    }
}
