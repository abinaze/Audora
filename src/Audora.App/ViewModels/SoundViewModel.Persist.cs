using System.Collections.ObjectModel;
using Audora.App.Infrastructure;
using Audora.Core.Audio;
using Audora.Core.Settings;

namespace Audora.App.ViewModels;

public sealed class RoomItem : ObservableObject
{
    private bool _isActive;

    public RoomItem(string name) => Name = name;

    public string Name { get; }

    public bool IsActive
    {
        get => _isActive;
        set => Set(ref _isActive, value);
    }
}

public sealed partial class SoundViewModel
{
    private string _newPresetName = string.Empty;
    private RelayCommand? _savePresetCommand;
    private RelayCommand? _deletePresetCommand;
    private RelayCommand? _chooseRoomCommand;
    private double _virtualizer;
    private double _reverbAmount;
    private string _reverbRoom = "Room";

    public string NewPresetName
    {
        get => _newPresetName;
        set => Set(ref _newPresetName, value ?? string.Empty);
    }

    public RelayCommand SavePresetCommand => _savePresetCommand ??= new RelayCommand(SavePreset);

    public RelayCommand DeletePresetCommand => _deletePresetCommand ??= new RelayCommand(DeletePreset);

    public bool CanDeletePreset => Presets.Any(p => p.IsActive && p.Custom is not null);

    // ---------- effects ----------

    public ObservableCollection<RoomItem> Rooms { get; } = new()
    {
        new RoomItem("Room") { IsActive = true },
        new RoomItem("Hall"),
        new RoomItem("Stage"),
    };

    public RelayCommand ChooseRoomCommand => _chooseRoomCommand ??= new RelayCommand(parameter =>
    {
        if (parameter is not RoomItem room)
        {
            return;
        }

        _reverbRoom = room.Name;
        RaiseRooms();

        // choosing a room with the reverb off turns it on at a gentle level
        if (_reverbAmount < 1)
        {
            Set(ref _reverbAmount, 25d, nameof(ReverbAmount));
            Raise(nameof(ReverbText));
        }

        OnManualChange();
    });

    /// <summary>3D virtualizer, 0 (off) to 100.</summary>
    public double Virtualizer
    {
        get => _virtualizer;
        set
        {
            if (Set(ref _virtualizer, Math.Round(Math.Clamp(value, 0, 100))))
            {
                Raise(nameof(VirtualizerText));
                OnManualChange();
            }
        }
    }

    public string VirtualizerText => _virtualizer <= 0 ? "Off" : $"{_virtualizer:0}%";

    /// <summary>Reverb amount, 0 (off) to 100.</summary>
    public double ReverbAmount
    {
        get => _reverbAmount;
        set
        {
            if (Set(ref _reverbAmount, Math.Round(Math.Clamp(value, 0, 100))))
            {
                Raise(nameof(ReverbText));
                OnManualChange();
            }
        }
    }

    public string ReverbText => _reverbAmount <= 0 ? "Off" : $"{_reverbAmount:0}%";

    private void RaiseRooms()
    {
        foreach (var room in Rooms)
        {
            room.IsActive = string.Equals(room.Name, _reverbRoom, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static ReverbKind RoomKind(string? name) =>
        Enum.TryParse<ReverbKind>(name, true, out var kind) ? kind : ReverbKind.Room;

    // ---------- saving and loading ----------

    private static SavedSound ToSaved(EditorState s) => new()
    {
        Preset = s.Preset,
        Bands = (double[])s.Bands.Clone(),
        Preamp = s.Preamp,
        Bass = s.Bass,
        Treble = s.Treble,
        Width = s.Width,
        Virtualizer = s.Virtualizer,
        ReverbAmount = s.Reverb,
        ReverbRoom = s.Room,
    };

    private static double[] Fit(double[]? bands)
    {
        var result = new double[10];
        if (bands is not null)
        {
            Array.Copy(bands, result, Math.Min(bands.Length, 10));
        }

        return result;
    }

    private static EditorState ToState(SavedSound s, string? presetName = null) =>
        new(presetName ?? (string.IsNullOrWhiteSpace(s.Preset) ? SoundPresets.OriginalName : s.Preset),
            Fit(s.Bands), s.Preamp, s.Bass, s.Treble, s.Width,
            Math.Clamp(s.Virtualizer, 0, 100), Math.Clamp(s.ReverbAmount, 0, 100),
            string.IsNullOrWhiteSpace(s.ReverbRoom) ? "Room" : s.ReverbRoom);

    private static EqSettings BuildFrom(EditorState s) =>
        s.Preset == SoundPresets.OriginalName
            ? EqSettings.Original
            : new EqSettings(
                true,
                (float)s.Preamp,
                s.Bands.Select(b => (float)b).ToArray(),
                (float)s.Bass,
                (float)s.Treble,
                (float)(s.Width / 100.0),
                (float)(s.Virtualizer / 100.0),
                (float)(s.Reverb / 100.0),
                RoomKind(s.Room));

    private PresetItem AddCustom(string name, SavedSound sound)
    {
        var bands = Fit(sound.Bands).Select(b => (float)b).ToArray();
        var item = new PresetItem(new SoundPreset(name, bands)) { Custom = sound };
        Presets.Add(item);
        return item;
    }

    private void SavePreset()
    {
        var name = _newPresetName.Trim();
        if (name.Length == 0)
        {
            return;
        }

        if (SoundPresets.All.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            name += " (mine)";
        }

        var saved = ToSaved(Capture());
        saved.Preset = name;

        var existing = Presets.FirstOrDefault(p => p.Custom is not null
            && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            Presets.Remove(existing);
        }

        var item = AddCustom(name, saved);
        NewPresetName = string.Empty;

        // the sound on screen now belongs to this preset
        _preset = name;
        foreach (var preset in Presets)
        {
            preset.IsActive = ReferenceEquals(preset, item);
        }

        Summary = name;
    }

    private void DeletePreset()
    {
        var active = Presets.FirstOrDefault(p => p.IsActive && p.Custom is not null);
        if (active is null)
        {
            return;
        }

        Presets.Remove(active);
        _preset = "Custom";
        Summary = "Custom";
    }

    private void LoadCustom(PresetItem item)
    {
        Load(ToState(item.Custom!, item.Name));
        Commit();
    }

    /// <summary>Copies the shared sound and the user's saved presets into the settings.</summary>
    public void Export(AppSettings settings)
    {
        var shared = _current.Id is null
            ? Capture()
            : _saved.TryGetValue(AllKey, out var all) ? all : OriginalState();

        settings.SharedSound = ToSaved(shared);
        settings.Presets = Presets
            .Where(p => p.Custom is not null)
            .Select(p => new SavedPreset { Name = p.Name, Sound = p.Custom! })
            .ToList();
    }

    /// <summary>The sound a device has of its own, or null when it follows All devices.</summary>
    public SavedSound? OwnSoundOf(string deviceId)
    {
        if (!_overrides.Contains(deviceId))
        {
            return null;
        }

        EditorState? state = string.Equals(_current.Id, deviceId, StringComparison.OrdinalIgnoreCase)
            ? Capture()
            : _saved.TryGetValue(deviceId, out var own) ? own : null;

        return state is null ? null : ToSaved(state);
    }

    /// <summary>Loads the presets and the shared sound that were saved last time.</summary>
    public void Import(AppSettings settings)
    {
        foreach (var preset in settings.Presets)
        {
            if (!string.IsNullOrWhiteSpace(preset.Name)
                && Presets.All(p => !string.Equals(p.Name, preset.Name, StringComparison.OrdinalIgnoreCase)))
            {
                AddCustom(preset.Name, preset.Sound);
            }
        }

        var state = ToState(settings.SharedSound);
        _saved[AllKey] = state;
        Load(state);

        var eq = BuildFrom(state);
        SerialWorker.Run(() => _controller.SetSound(eq));
    }

    /// <summary>Gives a device the own sound it had last time.</summary>
    public void ImportDevice(string deviceId, SavedSound sound)
    {
        var state = ToState(sound);
        _overrides.Add(deviceId);
        _saved[deviceId] = state;

        var eq = BuildFrom(state);
        SerialWorker.Run(() => _controller.SetDeviceSound(deviceId, eq));

        var target = Targets.FirstOrDefault(t => string.Equals(t.Id, deviceId, StringComparison.OrdinalIgnoreCase));
        if (target is not null)
        {
            target.HasOverride = true;
        }
    }
}
