using System.Collections.ObjectModel;
using System.Diagnostics;
using Audora.App.Infrastructure;
using Audora.Core.Settings;

namespace Audora.App.ViewModels;

/// <summary>One device inside a group. Device is null while it is not connected.</summary>
public sealed class GroupMemberViewModel : ObservableObject
{
    private DeviceViewModel? _device;
    private string _deviceId;
    private string _name;

    public GroupMemberViewModel(string deviceId, string name)
    {
        _deviceId = deviceId;
        _name = name;
    }

    public string DeviceId
    {
        get => _deviceId;
        set => Set(ref _deviceId, value);
    }

    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    public DeviceViewModel? Device
    {
        get => _device;
        set
        {
            if (Set(ref _device, value))
            {
                Raise(nameof(IsConnected));
                Raise(nameof(StatusText));
            }
        }
    }

    public bool IsConnected => _device is not null;

    public string StatusText => _device is null ? "Not connected" : _device.IsDefault ? "Windows default" : "Connected";

    public RelayCommand? RemoveCommand { get; set; }

    public void Refresh() => Raise(nameof(StatusText));
}

/// <summary>A saved set of devices that play together and have their own volume and mute.</summary>
public sealed class GroupViewModel : ObservableObject
{
    private readonly Action<GroupViewModel> _changed;
    private string _name;
    private double _volume;
    private bool _muted;
    private bool _isActive;

    public GroupViewModel(SavedGroup saved, Action<GroupViewModel> changed)
    {
        _changed = changed;
        Id = saved.Id;
        _name = saved.Name;
        _volume = Math.Clamp(saved.Volume, 0, 200);
        _muted = saved.Muted;

        foreach (var member in saved.Members)
        {
            Members.Add(new GroupMemberViewModel(member.DeviceId, member.Name));
        }

        Members.CollectionChanged += (_, _) => Raise(nameof(Summary));
    }

    public string Id { get; }

    public ObservableCollection<GroupMemberViewModel> Members { get; } = new();

    public RelayCommand? PlayCommand { get; set; }

    public RelayCommand? StopCommand { get; set; }

    public RelayCommand? DeleteCommand { get; set; }

    public RelayCommand? UseCurrentCommand { get; set; }

    public string Name
    {
        get => _name;
        set
        {
            var trimmed = (value ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                Raise(); // an empty name is not allowed: show the old one again
                return;
            }

            Set(ref _name, trimmed);
        }
    }

    /// <summary>Group volume, 0 to 200. Applies to the devices Audora plays on; the Windows default device follows the master volume.</summary>
    public double Volume
    {
        get => _volume;
        set
        {
            if (Set(ref _volume, Math.Clamp(Math.Round(value), 0, 200)))
            {
                Raise(nameof(VolumeText));
                _changed(this);
            }
        }
    }

    public string VolumeText => $"{_volume:0}%";

    public bool IsMuted
    {
        get => _muted;
        set
        {
            if (Set(ref _muted, value))
            {
                _changed(this);
            }
        }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (Set(ref _isActive, value))
            {
                Raise(nameof(IsInactive));
                Raise(nameof(ActiveText));
            }
        }
    }

    public bool IsInactive => !_isActive;

    public string ActiveText => _isActive ? "  -  playing now" : string.Empty;

    public string Summary => $"{Members.Count} device{(Members.Count == 1 ? string.Empty : "s")}";

    public double Factor => _muted ? 0.0 : _volume / 100.0;

    public SavedGroup ToSaved() => new()
    {
        Id = Id,
        Name = _name,
        Volume = _volume,
        Muted = _muted,
        Members = Members.Select(m => new SavedMember { DeviceId = m.DeviceId, Name = m.Name }).ToList(),
    };
}

/// <summary>A device Audora has seen before that is not connected right now.</summary>
public sealed class KnownDeviceViewModel
{
    public KnownDeviceViewModel(SavedDevice saved, Action<KnownDeviceViewModel> forget, Action<KnownDeviceViewModel>? connect = null)
    {
        Id = saved.Id;
        Name = saved.Name;
        IsBluetooth = saved.Type == "Bluetooth";
        TypeText = saved.Type switch
        {
            "Bluetooth" => "Bluetooth",
            "Usb" => "USB",
            "Hdmi" => "HDMI / DisplayPort",
            "Analog" => "Wired",
            _ => "Audio output"
        };

        var seen = saved.LastSeenUtc.ToLocalTime();
        var days = (DateTime.Now.Date - seen.Date).Days;
        LastSeenText = days <= 0 ? "last seen today" : days == 1 ? "last seen yesterday" : $"last seen {seen:d MMM yyyy}";

        ForgetCommand = new RelayCommand(() => forget(this));
        ConnectCommand = new RelayCommand(() => connect?.Invoke(this));
        OpenBluetoothCommand = new RelayCommand(() =>
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Error("Could not open Bluetooth settings", ex);
            }
        });
    }

    public string Id { get; }

    public string Name { get; }

    public bool IsBluetooth { get; }

    public string TypeText { get; }

    public string LastSeenText { get; }

    public RelayCommand ForgetCommand { get; }

    public RelayCommand ConnectCommand { get; }

    public RelayCommand OpenBluetoothCommand { get; }
}
