using System.Collections.ObjectModel;
using System.Windows.Threading;
using Audora.App.Infrastructure;
using Audora.Core.Devices;
using Audora.Core.Settings;

namespace Audora.App.ViewModels;

public sealed partial class MainViewModel
{
    private readonly SettingsStore _store = new(SettingsStore.DefaultPath);
    private AppSettings _settings = new();
    private string _lastSavedJson = string.Empty;
    private DispatcherTimer? _saveTimer;
    private string? _activeGroupId;
    private string _newGroupName = string.Empty;
    private string _groupMessage = string.Empty;
    private string _bluetoothHint = string.Empty;

    public ObservableCollection<GroupViewModel> Groups { get; } = new();

    public ObservableCollection<KnownDeviceViewModel> KnownDevices { get; } = new();

    public RelayCommand CreateGroupCommand { get; private set; } = null!;

    public bool IsGroupsTab
    {
        get => _tab == 3;
        set => SelectTab(3, value);
    }

    public bool HasNoGroups => Groups.Count == 0;

    public bool HasKnownDevices => KnownDevices.Count > 0;

    public bool HasBluetoothHint => _bluetoothHint.Length > 0;

    public string BluetoothHint
    {
        get => _bluetoothHint;
        private set
        {
            if (Set(ref _bluetoothHint, value))
            {
                Raise(nameof(HasBluetoothHint));
            }
        }
    }

    public string NewGroupName
    {
        get => _newGroupName;
        set => Set(ref _newGroupName, value ?? string.Empty);
    }

    public string GroupMessage
    {
        get => _groupMessage;
        private set => Set(ref _groupMessage, value);
    }

    // ---------- start up, saving ----------

    private void InitializeExtras()
    {
        _settings = _store.Load();
        _isDark = _settings.DarkTheme;
        _closeToTray = _settings.CloseToTray;
        _minimizeToTray = _settings.MinimizeToTray;
        _trayHintShown = _settings.TrayHintShown;
        _startEngineOn = _settings.StartEngineOn;
        ThemeService.Apply(_isDark);

        Sound.Import(_settings);

        CreateGroupCommand = new RelayCommand(CreateGroup);
        foreach (var saved in _settings.Groups)
        {
            AddGroup(saved);
        }

        _activeGroupId = Groups.Any(g => g.Id == _settings.ActiveGroupId) ? _settings.ActiveGroupId : null;
        foreach (var group in Groups)
        {
            group.IsActive = group.Id == _activeGroupId;
        }

        _lastSavedJson = _store.Serialize(BuildSettings());
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _saveTimer.Tick += (_, _) => SaveIfChanged();
        _saveTimer.Start();
        StartSmartWatcher();
    }

    private AppSettings BuildSettings()
    {
        foreach (var vm in Devices)
        {
            var record = _settings.Devices.FirstOrDefault(d => string.Equals(d.Id, vm.Id, StringComparison.OrdinalIgnoreCase));
            if (record is null)
            {
                continue;
            }

            vm.FillSaved(record);
            record.OwnSound = Sound.OwnSoundOf(vm.Id);
        }

        var result = new AppSettings
        {
            DarkTheme = _isDark,
            Devices = _settings.Devices.ToList(),
            Microphones = _settings.Microphones.ToList(),
            Groups = Groups.Select(g => g.ToSaved()).ToList(),
            ActiveGroupId = _activeGroupId,
            CloseToTray = _closeToTray,
            MinimizeToTray = _minimizeToTray,
            TrayHintShown = _trayHintShown,
            StartEngineOn = _startEngineOn,
            RestorePoint = _settings.RestorePoint,
        };

        Sound.Export(result);
        return result;
    }

    private void SaveIfChanged()
    {
        try
        {
            var json = _store.Serialize(BuildSettings());
            if (json == _lastSavedJson)
            {
                return;
            }

            _lastSavedJson = json;
            Task.Run(() =>
            {
                try { _store.Save(json); } catch (Exception ex) { Log.Error("Could not save settings", ex); }
            });
        }
        catch (Exception ex)
        {
            Log.Error("Could not prepare settings", ex);
        }
    }

    private void SaveNow()
    {
        _saveTimer?.Stop();
        try
        {
            _store.Save(_store.Serialize(BuildSettings()));
        }
        catch (Exception ex)
        {
            Log.Error("Could not save settings", ex);
        }
    }

    // ---------- devices appearing ----------

    private SavedDevice? FindRecord(string id, string name) =>
        _settings.Devices.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? _settings.Devices.FirstOrDefault(d => !_byId.ContainsKey(d.Id)
            && DeviceMatcher.Normalize(d.Name) == DeviceMatcher.Normalize(name));

    private bool InActiveGroup(AudioDeviceInfo info)
    {
        var active = Groups.FirstOrDefault(g => g.Id == _activeGroupId);
        return active is not null && active.Members.Any(m =>
            string.Equals(m.DeviceId, info.Id, StringComparison.OrdinalIgnoreCase)
            || DeviceMatcher.Normalize(m.Name) == DeviceMatcher.Normalize(info.Name));
    }

    // Called once for each device the first time it appears in this run.
    private void OnDeviceAdded(DeviceViewModel vm, AudioDeviceInfo info)
    {
        if (_memory.ContainsKey(info.Id))
        {
            return; // came back during this run; the audio engine still holds its settings
        }

        vm.SetCallActive(_callActive);
        var saved = FindRecord(info.Id, info.Name);
        if (saved is not null)
        {
            vm.RestoreFromSaved(saved);
            if (saved.OwnSound is not null)
            {
                Sound.ImportDevice(info.Id, saved.OwnSound);
            }
        }

        if ((saved?.Selected == true || InActiveGroup(info)) && vm.CanSelect)
        {
            vm.IsSelected = true;
        }
    }

    // Called after every refresh of the device list.
    private void AfterApply(IReadOnlyList<AudioDeviceInfo> visible)
    {
        var now = DateTime.UtcNow;

        // Remember every device we see. A device that returns under a new Windows id (for example a
        // USB device in another port) updates its old record instead of adding a second one.
        foreach (var info in visible)
        {
            var record = FindRecord(info.Id, info.Name);
            if (record is null)
            {
                record = new SavedDevice();
                _settings.Devices.Add(record);
            }

            record.Id = info.Id;
            record.Name = info.Name;
            record.Type = info.ConnectionType.ToString();
            record.LastSeenUtc = now;
        }

        if (_settings.Devices.Count > 40)
        {
            var keep = new HashSet<string>(Groups.SelectMany(g => g.Members).Select(m => m.DeviceId), StringComparer.OrdinalIgnoreCase);
            var excess = _settings.Devices
                .Where(d => !_byId.ContainsKey(d.Id) && !keep.Contains(d.Id))
                .OrderBy(d => d.LastSeenUtc)
                .Take(_settings.Devices.Count - 40)
                .ToList();
            foreach (var old in excess)
            {
                _settings.Devices.Remove(old);
            }
        }

        // Saved devices that are not connected now.
        var offline = _settings.Devices
            .Where(d => !_byId.ContainsKey(d.Id))
            .OrderByDescending(d => d.LastSeenUtc)
            .ToList();
        if (!KnownDevices.Select(k => k.Id).SequenceEqual(offline.Select(d => d.Id)))
        {
            KnownDevices.Clear();
            foreach (var record in offline)
            {
                KnownDevices.Add(new KnownDeviceViewModel(record, ForgetDevice, ConnectKnown));
            }

            Raise(nameof(HasKnownDevices));
        }

        LinkMembers();
        RefreshGroupFactors();

        RefreshBluetoothSoon();
        var bluetooth = Devices.Count(d => d.IsBluetooth && d.IsSelected);
        BluetoothHint = bluetooth >= 2
            ? "Several Bluetooth devices are active. Most PCs handle two or three; if the sound stutters, turn one off."
            : string.Empty;
    }

    private void ForgetDevice(KnownDeviceViewModel known)
    {
        _settings.Devices.RemoveAll(d => string.Equals(d.Id, known.Id, StringComparison.OrdinalIgnoreCase));
        KnownDevices.Remove(known);
        Raise(nameof(HasKnownDevices));
    }

    // ---------- groups ----------

    private void AddGroup(SavedGroup saved)
    {
        var group = new GroupViewModel(saved, OnGroupChanged);
        group.PlayCommand = new RelayCommand(() => ActivateGroup(group));
        group.StopCommand = new RelayCommand(() => StopGroup(group));
        group.DeleteCommand = new RelayCommand(() => DeleteGroup(group));
        group.UseCurrentCommand = new RelayCommand(() => UseCurrentDevices(group));

        foreach (var member in group.Members)
        {
            WireMember(group, member);
        }

        Groups.Add(group);
        Raise(nameof(HasNoGroups));
    }

    private void WireMember(GroupViewModel group, GroupMemberViewModel member) =>
        member.RemoveCommand = new RelayCommand(() =>
        {
            group.Members.Remove(member);
            RefreshGroupFactors();
        });

    private void CreateGroup()
    {
        var playing = Devices.Where(d => d.IsSelected).ToList();
        if (playing.Count == 0)
        {
            GroupMessage = "Select the group devices on the Devices tab first, then save.";
            return;
        }

        var name = string.IsNullOrWhiteSpace(_newGroupName) ? $"Group {Groups.Count + 1}" : _newGroupName.Trim();
        var saved = new SavedGroup { Name = name };
        foreach (var device in playing)
        {
            saved.Members.Add(new SavedMember { DeviceId = device.Id, Name = device.Name });
        }

        AddGroup(saved);
        NewGroupName = string.Empty;
        LinkMembers();
        GroupMessage = $"Saved \"{name}\" with {playing.Count} device{(playing.Count == 1 ? string.Empty : "s")}.";
    }

    private void ActivateGroup(GroupViewModel group)
    {
        LinkMembers();
        foreach (var other in Groups)
        {
            other.IsActive = ReferenceEquals(other, group);
        }

        _activeGroupId = group.Id;

        // Exactly the group's devices play (the Windows default device always plays).
        foreach (var device in Devices.Where(d => d.CanSelect))
        {
            device.IsSelected = group.Members.Any(m => ReferenceEquals(m.Device, device));
        }

        // Members that are not connected yet start by themselves when they connect.
        RefreshGroupFactors();
        GroupMessage = $"Playing on \"{group.Name}\".";
    }

    private void StopGroup(GroupViewModel group)
    {
        group.IsActive = false;
        if (_activeGroupId == group.Id)
        {
            _activeGroupId = null;
        }

        foreach (var member in group.Members)
        {
            if (member.Device is { CanSelect: true } device)
            {
                device.IsSelected = false;
            }
        }

        RefreshGroupFactors();
        GroupMessage = $"Stopped \"{group.Name}\".";
    }

    private void DeleteGroup(GroupViewModel group)
    {
        if (_activeGroupId == group.Id)
        {
            _activeGroupId = null;
        }

        Groups.Remove(group);
        RefreshGroupFactors();
        Raise(nameof(HasNoGroups));
        GroupMessage = $"Deleted \"{group.Name}\".";
    }

    private void UseCurrentDevices(GroupViewModel group)
    {
        var playing = Devices.Where(d => d.IsSelected).ToList();
        if (playing.Count == 0)
        {
            GroupMessage = "No devices are playing. Select some first.";
            return;
        }

        group.Members.Clear();
        foreach (var device in playing)
        {
            var member = new GroupMemberViewModel(device.Id, device.Name);
            WireMember(group, member);
            group.Members.Add(member);
        }

        LinkMembers();
        RefreshGroupFactors();
        GroupMessage = $"\"{group.Name}\" now has the {playing.Count} playing device{(playing.Count == 1 ? string.Empty : "s")}.";
    }

    private void OnGroupChanged(GroupViewModel group)
    {
        if (group.Id == _activeGroupId)
        {
            RefreshGroupFactors();
        }
    }

    // Connects each group member to the connected device it stands for.
    private void LinkMembers()
    {
        foreach (var group in Groups)
        {
            foreach (var member in group.Members)
            {
                var device = Devices.FirstOrDefault(d => string.Equals(d.Id, member.DeviceId, StringComparison.OrdinalIgnoreCase))
                    ?? Devices.FirstOrDefault(d =>
                        DeviceMatcher.Normalize(d.Name) == DeviceMatcher.Normalize(member.Name)
                        && !group.Members.Any(o => !ReferenceEquals(o, member)
                            && string.Equals(o.DeviceId, d.Id, StringComparison.OrdinalIgnoreCase)));

                member.Device = device;
                if (device is not null)
                {
                    member.DeviceId = device.Id;
                    member.Name = device.Name;
                }

                member.Refresh();
            }
        }
    }

    // The group that is playing sets the volume factor of its devices; everything else stays at 1.
    private void RefreshGroupFactors()
    {
        var active = Groups.FirstOrDefault(g => g.Id == _activeGroupId);
        foreach (var device in Devices)
        {
            var isMember = active is not null && active.Members.Any(m => ReferenceEquals(m.Device, device));
            device.SetGroupFactor(isMember ? active!.Factor : 1.0);
        }
    }
}
