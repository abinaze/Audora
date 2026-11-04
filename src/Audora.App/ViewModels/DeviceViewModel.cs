using Audora.App.Infrastructure;
using Audora.Core.Audio;
using Audora.Core.Devices;

namespace Audora.App.ViewModels;

/// <summary>
/// One audio device on the dashboard. Mirrored devices are controlled through Audora's own
/// gain and delay. The Windows default device plays directly, so its volume and mute
/// control the real Windows volume of that device instead.
/// </summary>
public sealed partial class DeviceViewModel : ObservableObject
{
    private readonly MirrorController _controller;
    private readonly Func<double> _master;
    private bool _applying;

    private string _name;
    private ConnectionType _type;
    private DeviceStatus _status;
    private bool _isDefault;
    private bool _isSelected;
    private double _volume = 100;
    private bool _isMuted;
    private double _balance;
    private double _delay;
    private string _stateText = string.Empty;
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
        Update(info, null);
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

    public bool IsAvailable => _status == DeviceStatus.Active;

    public bool IsDefault => _isDefault;

    public string StateText
    {
        get => _stateText;
        private set => Set(ref _stateText, value);
    }

    public bool CanSelect => IsAvailable && !_isDefault;

    public bool CanMakeDefault => IsAvailable && !_isDefault;

    public bool CanAdjustMirror => !_isDefault;

    public bool ShowDefaultNote => _isDefault;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => Set(ref _isExpanded, value);
    }

    public RelayCommand MakeDefaultCommand { get; }

    public RelayCommand ToggleDetailsCommand { get; }

    /// <summary>Play on this device (mirror to it). The Windows default device always plays.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (Set(ref _isSelected, value) && !_applying)
            {
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
    }

    /// <summary>Volume slider, 0 to 100.</summary>
    public double Volume
    {
        get => _volume;
        set
        {
            if (Set(ref _volume, Math.Clamp(value, 0, 100)) && !_applying)
            {
                ApplyAudio();
            }
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (Set(ref _isMuted, value) && !_applying)
            {
                ApplyMute();
            }
        }
    }

    /// <summary>Left/right balance, -100 (left) to 100 (right). Mirrored devices only.</summary>
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

    /// <summary>Extra delay in milliseconds, 0 to 2000. Mirrored devices only.</summary>
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

    /// <summary>Sends the slider value (times the master volume) to the right place.</summary>
    public void ApplyAudio()
    {
        var id = Id;
        var level = (float)(_volume / 100.0 * _master());
        var native = _isDefault;

        SerialWorker.Run(() =>
        {
            if (native)
            {
                EndpointVolume.SetVolume(id, level);
            }
            else
            {
                _controller.SetVolume(id, level);
            }
        });
    }

    private void ApplyMute()
    {
        var id = Id;
        var muted = _isMuted;
        var native = _isDefault;

        SerialWorker.Run(() =>
        {
            if (native)
            {
                EndpointVolume.SetMuted(id, muted);
            }
            else
            {
                _controller.SetMuted(id, muted);
            }
        });
    }

    public void MarkGone() =>
        Update(new AudioDeviceInfo(Id, Name, _type, DeviceStatus.NotPresent, false), null);

    /// <summary>Refreshes this card from Windows. mirrorState is null when the device is not selected.</summary>
    public void Update(AudioDeviceInfo info, string? mirrorState)
    {
        _applying = true;
        try
        {
            var wasDefault = _isDefault;
            var nowDefault = info.IsDefault && info.Status == DeviceStatus.Active;

            Name = info.Name;
            Set(ref _type, info.ConnectionType, nameof(TypeText));
            Set(ref _status, info.Status, nameof(IsAvailable));
            Set(ref _isDefault, nowDefault, nameof(IsDefault));

            if (nowDefault && !wasDefault)
            {
                // The sliders now mean the real Windows volume: show it, never overwrite it.
                var volume = EndpointVolume.GetVolume(Id);
                var muted = EndpointVolume.GetMuted(Id);
                Set(ref _volume, volume is null ? _volume : Math.Round(volume.Value * 100), nameof(Volume));
                Set(ref _isMuted, muted ?? _isMuted, nameof(IsMuted));
            }

            Set(ref _isSelected, nowDefault || mirrorState is not null, nameof(IsSelected));

            StateText = !IsAvailable ? (mirrorState ?? "Not connected")
                : nowDefault ? "Playing directly (Windows default)"
                : (mirrorState == "Mirroring" ? "Playing" : mirrorState) ?? "Ready";

            Raise(nameof(CanSelect));
            Raise(nameof(CanMakeDefault));
            Raise(nameof(CanAdjustMirror));
            Raise(nameof(ShowDefaultNote));

            if (!nowDefault && wasDefault)
            {
                ApplyAudio();
                ApplyMute();
            }
        }
        finally
        {
            _applying = false;
        }
    }
}
