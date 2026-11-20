using Audora.App.Infrastructure;

namespace Audora.App.ViewModels;

public sealed partial class DeviceViewModel
{
    private bool _noiseRemoval;
    private double _noiseStrength = 100;
    private bool _noiseOnlyInCalls = true;
    private bool _callActive;

    /// <summary>Removes steady background noise from the voices in the sound this device plays.</summary>
    public bool NoiseRemoval
    {
        get => _noiseRemoval;
        set
        {
            if (Set(ref _noiseRemoval, value))
            {
                Raise(nameof(NoiseStateText));
                if (!_applying && !_isDefault)
                {
                    PushNoise();
                }
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

    /// <summary>Clean only while a call app is in front, so music is never touched. On by default.</summary>
    public bool NoiseOnlyInCalls
    {
        get => _noiseOnlyInCalls;
        set
        {
            if (Set(ref _noiseOnlyInCalls, value))
            {
                Raise(nameof(NoiseStateText));
                if (!_applying && !_isDefault)
                {
                    PushNoise();
                }
            }
        }
    }

    /// <summary>True when Audora needs to watch which app is in front for this device.</summary>
    public bool NeedsCallDetection => _noiseRemoval && _noiseOnlyInCalls && !_isDefault;

    /// <summary>Tells the device whether a call app is in front now.</summary>
    public void SetCallActive(bool active)
    {
        if (_callActive == active)
        {
            return;
        }

        _callActive = active;
        Raise(nameof(NoiseStateText));
        if (NeedsCallDetection)
        {
            PushNoise();
        }
    }

    /// <summary>What noise removal is doing on this device right now, in plain words.</summary>
    public string NoiseStateText =>
        !_noiseRemoval ? string.Empty
        : !_noiseOnlyInCalls ? "Cleaning all the time, music included."
        : _callActive ? "Cleaning now: a call app is in front."
        : "Waiting. It starts when a call app (Teams, Zoom, Meet, Discord and similar) is in front.";

    /// <summary>Why noise removal cannot work on the sound Audora copies right now, or null.</summary>
    public string? NoiseUnavailableReason => _controller.NoiseRemovalUnavailableReason;

    public bool CanUseNoiseRemoval => !_isDefault && NoiseUnavailableReason is null;

    public string NoiseHint => NoiseUnavailableReason
        ?? "For calls and talk. By default it works only while a call app is in front, so music is never touched. It adds about 10 ms.";

    /// <summary>Shows and applies what was saved on disk.</summary>
    public void RestoreNoise(bool enabled, double strength, bool onlyInCalls = true)
    {
        _applying = true;
        try
        {
            Set(ref _noiseRemoval, enabled, nameof(NoiseRemoval));
            Set(ref _noiseStrength, Math.Clamp(strength, 0, 100), nameof(NoiseStrength));
            Set(ref _noiseOnlyInCalls, onlyInCalls, nameof(NoiseOnlyInCalls));
        }
        finally
        {
            _applying = false;
        }

        Raise(nameof(NoiseStateText));
        PushNoise();
    }

    private void PushNoise()
    {
        var id = Id;
        var enabled = _noiseRemoval && (!_noiseOnlyInCalls || _callActive);
        var strength = (float)(_noiseStrength / 100.0);
        SerialWorker.Run(() => _controller.SetNoiseRemoval(id, enabled, strength));
    }
}
