using Audora.App.Infrastructure;
using Audora.Core.Audio;
using Audora.Core.Devices;

namespace Audora.App.ViewModels;

/// <summary>
/// One audio device on the dashboard. The Windows default device plays directly and its volume is
/// the master volume. Every other device is played by Audora, with its own volume (up to 200%),
/// mute, balance and delay.
/// </summary>
public sealed partial class DeviceViewModel : ObservableObject
{
    private readonly MirrorController _controller;
    private readonly Func<double> _master;
    private bool _applying;
    private bool _pendingValue;
    private DateTime _pendingUntil = DateTime.MinValue;

    private string _name;
    private ConnectionType _type;
    private bool _isDefault;
    private bool _isSelected;
    private double _volume = 100;
    private bool _isMuted;
    private double _balance;
    private double _delay;
    private string _stateText = string.Empty;
    private string? _detail;
    private bool _isExpanded;

    public DeviceViewModel(AudioDeviceInfo info, MirrorController controller, Func<double> master)
    {
        Id = info.Id;
        _name = info.Name;
        _controller = controller;
        _master = master;

        MakeDefaultCommand = new RelayCommand(() =>
        {
            var id = Id;
            SerialWorker.Run(() => DefaultDeviceSwitcher.SetDefaultOutput(id));
        }, () => CanMakeDefault);

        ToggleDetailsCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
        Update(info, null, null);
    }

    public string Id { get; }

    public string Name
    {
        get => _name;
        private set => Set(ref _name, value);
    }

    public string TypeText => _type switch
    {
        ConnectionType.Bluetooth => "Bluetooth",
        ConnectionType.Usb => "USB",
        ConnectionType.Hdmi => "HDMI / DisplayPort",
        ConnectionType.Analog => "Wired",
        ConnectionType.Other => "Other",
        _ => "Audio output"
    };

    public string IconGlyph => _type switch
    {
        ConnectionType.Bluetooth => "\uE702",
        ConnectionType.Usb => "\uE88E",
        ConnectionType.Hdmi => "\uE7F4",
        _ => _name.Contains("Headphone", StringComparison.OrdinalIgnoreCase)
             || _name.Contains("Headset", StringComparison.OrdinalIgnoreCase) ? "\uE7F6" : "\uE767"
    };

    public bool IsAvailable => true;

    public bool IsDefault => _isDefault;

    public bool CanSelect => !_isDefault;

    public bool CanMakeDefault => !_isDefault;

    public bool ShowMirrorControls => !_isDefault;

    public bool ShowDefaultNote => _isDefault;

    public string SwitchLabel => _isDefault ? "Always on" : _isSelected ? "Playing here" : "Off";

    public string StateText
    {
        get => _stateText;
        private set => Set(ref _stateText, value);
    }

    public string? DetailText
    {
        get => _detail;
        private set
        {
            if (Set(ref _detail, value))
            {
                Raise(nameof(HasDetail));
            }
        }
    }

    public bool HasDetail => !string.IsNullOrWhiteSpace(_detail);

    public bool IsExpanded
    {
        get => _isExpanded;
        set => Set(ref _isExpanded, value);
    }

    public RelayCommand MakeDefaultCommand { get; }

    public RelayCommand ToggleDetailsCommand { get; }

    /// <summary>Play on this device. The Windows default device always plays.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!Set(ref _isSelected, value))
            {
                return;
            }

            Raise(nameof(SwitchLabel));
            if (_applying)
            {
                return;
            }

            // Hold the switch where the user put it until the engine confirms (or a few seconds pass).
            _pendingValue = value;
            _pendingUntil = DateTime.UtcNow.AddSeconds(6);

            var id = Id;
            var selected = value;
            SerialWorker.Run(() =>
            {
                if (selected)
                {
                    _controller.Select(id);
                }
                else
                {
                    _controller.Deselect(id);
                }
            });
        }
    }

    /// <summary>Volume, 0 to 200 (above 100 is a boost).</summary>
    public double Volume
    {
        get => _volume;
        set
        {
            if (Set(ref _volume, Math.Clamp(value, 0, 200)))
            {
                Raise(nameof(VolumeText));
                if (!_applying)
                {
                    ApplyAudio();
                }
            }
        }
    }

    public string VolumeText => _volume > 100 ? $"{_volume:0}% boost" : $"{_volume:0}%";

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (Set(ref _isMuted, value) && !_applying && !_isDefault)
            {
                PushMute();
            }
        }
    }

    /// <summary>Left/right balance, -100 (left) to 100 (right).</summary>
    public double Balance
    {
        get => _balance;
        set
        {
            if (Set(ref _balance, Math.Clamp(value, -100, 100)) && !_applying)
            {
                var id = Id;
                var balance = (float)(_balance / 100.0);
                SerialWorker.Run(() => _controller.SetBalance(id, balance));
            }
        }
    }

    /// <summary>Extra delay in milliseconds, 0 to 2000.</summary>
    public double Delay
    {
        get => _delay;
        set
        {
            if (Set(ref _delay, Math.Clamp(Math.Round(value), 0, 2000)) && !_applying)
            {
                var id = Id;
                var delay = (int)_delay;
                SerialWorker.Run(() => _controller.SetDelay(id, delay));
            }
        }
    }

    /// <summary>Sends the slider value (times the master volume when that option is on) to the audio engine.</summary>
    public void ApplyAudio()
    {
        if (_isDefault)
        {
            return; // the default device follows the Windows volume, which is the master slider
        }

        var id = Id;
        var level = (float)Math.Clamp(_volume / 100.0 * _master() * GroupFactor, 0.0, 2.0);
        SerialWorker.Run(() => _controller.SetVolume(id, level));
    }

    private void PushMute()
    {
        var id = Id;
        var muted = _isMuted;
        SerialWorker.Run(() => _controller.SetMuted(id, muted));
    }

    /// <summary>Refreshes this card from Windows and the audio engine. mirrorState is null when not selected.</summary>
    public void Update(AudioDeviceInfo info, string? mirrorState, string? detail)
    {
        _applying = true;
        try
        {
            var wasDefault = _isDefault;
            var nowDefault = info.IsDefault && info.Status == DeviceStatus.Active;

            Name = info.Name;
            Set(ref _type, info.ConnectionType, nameof(TypeText));
            Raise(nameof(IconGlyph));
            Raise(nameof(DefaultNote));
            Set(ref _isDefault, nowDefault, nameof(IsDefault));

            var confirmed = nowDefault || mirrorState is not null;
            var waiting = DateTime.UtcNow < _pendingUntil && confirmed != _pendingValue;
            if (!waiting)
            {
                _pendingUntil = DateTime.MinValue;
                Set(ref _isSelected, confirmed, nameof(IsSelected));
            }

            StateText = nowDefault ? "Windows default"
                : waiting ? (_pendingValue ? "Starting..." : "Stopping...")
                : MapState(mirrorState);
            DetailText = nowDefault ? null : FriendlyError(detail);

            Raise(nameof(SwitchLabel));
            Raise(nameof(CanSelect));
            Raise(nameof(CanMakeDefault));
            Raise(nameof(ShowMirrorControls));
            Raise(nameof(ShowDefaultNote));
            Raise(nameof(CanUseNoiseRemoval));
            Raise(nameof(NoiseHint));

            if (wasDefault && !nowDefault)
            {
                ApplyAudio();
                PushMute();
            }
        }
        finally
        {
            _applying = false;
        }
    }

    private static string MapState(string? state) => state switch
    {
        null => "Ready",
        "Mirroring" => "Playing together",
        "Starting" => "Starting...",
        "Recovering after an error" => "Reconnecting...",
        "Paused after repeated errors" => "Stopped, see below",
        "Waiting for device" => "Waiting for device",
        _ => state
    };

    private static string? FriendlyError(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (raw.Contains("0x88890004", StringComparison.OrdinalIgnoreCase))
        {
            return "Windows reset this device. Audora retries automatically.";
        }

        if (raw.Contains("0x8889000A", StringComparison.OrdinalIgnoreCase))
        {
            return "Another app is using this device exclusively. Close it and try again.";
        }

        if (raw.Contains("0x88890008", StringComparison.OrdinalIgnoreCase))
        {
            return "This device does not support the audio format Audora sends.";
        }

        return raw.Length > 140 ? raw[..140] + "..." : raw;
    }
}
