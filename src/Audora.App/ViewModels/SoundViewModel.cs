using System.Collections.ObjectModel;
using Audora.App.Infrastructure;
using Audora.Core.Audio;
using Audora.Core.Settings;

namespace Audora.App.ViewModels;

public sealed class BandViewModel : ObservableObject
{
    private readonly Action _changed;
    private double _gain;

    public BandViewModel(string label, Action changed)
    {
        Label = label;
        _changed = changed;
    }

    public string Label { get; }

    public double Gain
    {
        get => _gain;
        set
        {
            if (Set(ref _gain, Math.Round(Math.Clamp(value, -12, 12) * 2) / 2))
            {
                Raise(nameof(GainText));
                _changed();
            }
        }
    }

    public string GainText => _gain.ToString("+0.#;-0.#;0");

    public void SetSilently(double gain)
    {
        _gain = gain;
        Raise(nameof(Gain));
        Raise(nameof(GainText));
    }
}

public sealed class PresetItem : ObservableObject
{
    private bool _isActive;

    public PresetItem(SoundPreset preset) => Preset = preset;

    public SoundPreset Preset { get; }

    /// <summary>Set for presets the user saved; null for the built-in profiles.</summary>
    public SavedSound? Custom { get; init; }

    public string Name => Preset.Name;

    public bool IsActive
    {
        get => _isActive;
        set => Set(ref _isActive, value);
    }
}

/// <summary>Either "All devices" (Id is null) or one device that can have its own sound.</summary>
public sealed class SoundTarget : ObservableObject
{
    private string _name;
    private bool _isActive;
    private bool _hasOverride;

    public SoundTarget(string? id, string name)
    {
        Id = id;
        _name = name;
    }

    public string? Id { get; }

    public string Name
    {
        get => _name;
        set
        {
            if (Set(ref _name, value))
            {
                Raise(nameof(Label));
            }
        }
    }

    /// <summary>A star marks a device that has its own sound.</summary>
    public string Label => _hasOverride ? Name + "  *" : Name;

    public bool IsActive
    {
        get => _isActive;
        set => Set(ref _isActive, value);
    }

    public bool HasOverride
    {
        get => _hasOverride;
        set
        {
            if (Set(ref _hasOverride, value))
            {
                Raise(nameof(Label));
            }
        }
    }
}

/// <summary>
/// The Sound tab: profiles (Music, Movie, Communication, Gaming and Smart), effects, and an advanced
/// equalizer, for all devices together or for one device on its own.
/// </summary>
public sealed class SoundViewModel : ObservableObject
{
    private sealed record EditorState(
        string Preset, double[] Bands, double Preamp, double Bass, double Treble,
        double Width, double Virtualizer, double VoiceClarity, double Stabilizer);

    private const string AllKey = "*";

    private readonly MirrorController _controller;
    private readonly Dictionary<string, EditorState> _saved = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _overrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly SoundTarget _all = new(null, "All devices");
    private SoundTarget _current;
    private bool _loading;
    private bool _smart;
    private string? _smartProfile;
    private bool _showAdvanced;
    private string _preset = SoundPresets.OriginalName;
    private double _preamp;
    private double _bass;
    private double _treble;
    private double _width = 100;
    private double _virtualizer;
    private double _voice;
    private double _stabilizer;
    private string _summary = "Original: no processing";
    private string _description = SoundPresets.All[0].Description;
    private string _newPresetName = string.Empty;

    public SoundViewModel(MirrorController controller)
    {
        _controller = controller;
        _current = _all;
        _all.IsActive = true;
        Targets.Add(_all);

        foreach (var label in new[] { "31", "62", "125", "250", "500", "1k", "2k", "4k", "8k", "16k" })
        {
            Bands.Add(new BandViewModel(label, OnManualChange));
        }

        foreach (var preset in SoundPresets.All)
        {
            Presets.Add(new PresetItem(preset));
        }

        Presets[0].IsActive = true;

        ChoosePresetCommand = new RelayCommand(parameter =>
        {
            if (parameter is PresetItem item)
            {
                Choose(item);
            }
        });
        ChooseTargetCommand = new RelayCommand(parameter =>
        {
            if (parameter is SoundTarget target)
            {
                SelectTarget(target);
            }
        });
        ResetCommand = new RelayCommand(() => Choose(Presets[0]));
        FollowAllCommand = new RelayCommand(FollowAll);
        SavePresetCommand = new RelayCommand(SavePreset);
        DeletePresetCommand = new RelayCommand(DeletePreset);
    }

    public ObservableCollection<BandViewModel> Bands { get; } = new();

    public ObservableCollection<PresetItem> Presets { get; } = new();

    public ObservableCollection<SoundTarget> Targets { get; } = new();

    public RelayCommand ChoosePresetCommand { get; }

    public RelayCommand ChooseTargetCommand { get; }

    public RelayCommand ResetCommand { get; }

    public RelayCommand FollowAllCommand { get; }

    public RelayCommand SavePresetCommand { get; }

    public RelayCommand DeletePresetCommand { get; }

    public bool IsSmartOn => _smart;

    public string Summary
    {
        get => _summary;
        private set
        {
            if (Set(ref _summary, value))
            {
                Raise(nameof(CanDeletePreset));
            }
        }
    }

    public string ActiveDescription
    {
        get => _description;
        private set => Set(ref _description, value);
    }

    public string TargetTitle => _current.Name;

    public bool ShowFollowAll => _current.Id is not null && _current.HasOverride;

    public bool CanDeletePreset => Presets.Any(p => p.IsActive && p.Custom is not null);

    public bool ShowAdvanced
    {
        get => _showAdvanced;
        set => Set(ref _showAdvanced, value);
    }

    public string NewPresetName
    {
        get => _newPresetName;
        set => Set(ref _newPresetName, value ?? string.Empty);
    }

    public string ScopeNote => _current.Id is null
        ? "These settings apply to every device Audora plays on, except devices marked with a star, which have their own sound."
        : _current.HasOverride
            ? $"{_current.Name} has its own sound. Changes here do not affect other devices."
            : $"{_current.Name} follows All devices. Change anything below to give it its own sound.";

    // ---------- the controls ----------

    private static double Half(double value, double min, double max) => Math.Round(Math.Clamp(value, min, max) * 2) / 2;

    public double Preamp
    {
        get => _preamp;
        set
        {
            if (Set(ref _preamp, Half(value, -12, 12)))
            {
                Raise(nameof(PreampText));
                OnManualChange();
            }
        }
    }

    public string PreampText => $"{_preamp.ToString("+0.#;-0.#;0")} dB";

    public double Bass
    {
        get => _bass;
        set
        {
            if (Set(ref _bass, Half(value, -10, 10)))
            {
                Raise(nameof(BassText));
                OnManualChange();
            }
        }
    }

    public string BassText => $"{_bass.ToString("+0.#;-0.#;0")} dB";

    public double Treble
    {
        get => _treble;
        set
        {
            if (Set(ref _treble, Half(value, -10, 10)))
            {
                Raise(nameof(TrebleText));
                OnManualChange();
            }
        }
    }

    public string TrebleText => $"{_treble.ToString("+0.#;-0.#;0")} dB";

    /// <summary>Stereo width, 0 (mono) to 200 (extra wide). 100 is normal.</summary>
    public double Width
    {
        get => _width;
        set
        {
            if (Set(ref _width, Math.Round(Math.Clamp(value, 0, 200))))
            {
                Raise(nameof(WidthText));
                OnManualChange();
            }
        }
    }

    public string WidthText => _width <= 0 ? "Mono" : $"{_width:0}%";

    /// <summary>Surround (3D), 0 (off) to 100.</summary>
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

    /// <summary>Voice clarity, 0 (off) to 100.</summary>
    public double VoiceClarity
    {
        get => _voice;
        set
        {
            if (Set(ref _voice, Math.Round(Math.Clamp(value, 0, 100))))
            {
                Raise(nameof(VoiceText));
                OnManualChange();
            }
        }
    }

    public string VoiceText => _voice <= 0 ? "Off" : $"{_voice:0}%";

    /// <summary>Volume stabilizer, 0 (off) to 100.</summary>
    public double Stabilizer
    {
        get => _stabilizer;
        set
        {
            if (Set(ref _stabilizer, Math.Round(Math.Clamp(value, 0, 100))))
            {
                Raise(nameof(StabilizerText));
                OnManualChange();
            }
        }
    }

    public string StabilizerText => _stabilizer <= 0 ? "Off" : $"{_stabilizer:0}%";

    // ---------- devices ----------

    /// <summary>Keeps the device chips in step with the devices Audora can process (everything except the Windows default).</summary>
    public void SetDevices(IReadOnlyList<(string Id, string Name)> devices)
    {
        var wanted = devices.ToDictionary(d => d.Id, d => d.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var gone in Targets.Where(t => t.Id is { } id && !wanted.ContainsKey(id)).ToList())
        {
            if (ReferenceEquals(gone, _current))
            {
                SelectTarget(_all);
            }

            Targets.Remove(gone);
        }

        foreach (var (id, name) in devices)
        {
            var existing = Targets.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                Targets.Add(new SoundTarget(id, name) { HasOverride = _overrides.Contains(id) });
            }
            else
            {
                existing.Name = name;
            }
        }
    }

    private static string KeyOf(SoundTarget target) => target.Id ?? AllKey;

    private static EditorState OriginalState() => StateOf(SoundPresets.All[0]);

    private static EditorState StateOf(SoundPreset p, string? presetName = null)
    {
        var original = p.Name == SoundPresets.OriginalName;
        return new EditorState(
            presetName ?? p.Name,
            p.Bands.Select(b => (double)b).ToArray(),
            original ? 0 : SoundPresets.PreampFor(p),
            p.Bass,
            p.Treble,
            p.Width * 100,
            p.Virtualizer * 100,
            p.VoiceClarity * 100,
            p.Stabilizer * 100);
    }

    private EditorState Capture() =>
        new(_preset, Bands.Select(b => b.Gain).ToArray(), _preamp, _bass, _treble, _width, _virtualizer, _voice, _stabilizer);

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
                0f,
                ReverbKind.Room,
                (float)(s.VoiceClarity / 100.0),
                (float)(s.Stabilizer / 100.0));

    // ---------- switching and choosing ----------

    private void SelectTarget(SoundTarget target)
    {
        if (ReferenceEquals(target, _current))
        {
            return;
        }

        // Remember what the editor shows. A device that just follows All keeps nothing of its own.
        if (_current.Id is null || _current.HasOverride)
        {
            _saved[KeyOf(_current)] = Capture();
        }

        _current.IsActive = false;
        _current = target;
        _current.IsActive = true;

        if (target.Id is not null && target.HasOverride && _saved.TryGetValue(KeyOf(target), out var own))
        {
            Load(own);
        }
        else
        {
            Load(_saved.TryGetValue(AllKey, out var all) ? all : OriginalState());
        }

        RaiseScope();
    }

    private void FollowAll()
    {
        if (_current.Id is not { } id)
        {
            return;
        }

        _overrides.Remove(id);
        _saved.Remove(id);
        _current.HasOverride = false;
        SerialWorker.Run(() => _controller.SetDeviceSound(id, null));

        Load(_saved.TryGetValue(AllKey, out var all) ? all : OriginalState());
        RaiseScope();
    }

    private void Choose(PresetItem item)
    {
        if (item.Custom is not null)
        {
            _smart = false;
            Load(ToState(item.Custom, item.Name));
            Commit();
            return;
        }

        if (item.Name == SoundPresets.SmartName)
        {
            StartSmart();
            return;
        }

        _smart = false;
        Load(StateOf(item.Preset));
        Commit();
    }

    private void StartSmart()
    {
        // Smart works for all devices together.
        if (_current.Id is not null)
        {
            SelectTarget(_all);
        }

        _smart = true;
        _smartProfile = null;
        ApplySmart(SoundPresets.MusicName);
    }

    /// <summary>Called by the app watcher: switch to the profile that fits what the user is doing. Does nothing unless Smart is on.</summary>
    public void ApplySmart(string profile)
    {
        if (!_smart || _smartProfile == profile)
        {
            return;
        }

        var preset = SoundPresets.All.FirstOrDefault(p => p.Name == profile);
        if (preset is null)
        {
            return;
        }

        _smartProfile = profile;
        var state = StateOf(preset, SoundPresets.SmartName);

        if (_current.Id is null)
        {
            Load(state);
            Commit();
        }
        else
        {
            _saved[AllKey] = state;
            var eq = BuildFrom(state);
            SerialWorker.Run(() => _controller.SetSound(eq));
        }

        Summary = $"Smart: {profile}";
        ActiveDescription = $"Smart picked {profile} for the app you are using. {preset.Description}";
    }

    private void Load(EditorState state)
    {
        _loading = true;
        try
        {
            for (var i = 0; i < Bands.Count; i++)
            {
                Bands[i].SetSilently(state.Bands[i]);
            }

            Set(ref _preamp, state.Preamp, nameof(Preamp));
            Set(ref _bass, state.Bass, nameof(Bass));
            Set(ref _treble, state.Treble, nameof(Treble));
            Set(ref _width, state.Width, nameof(Width));
            Set(ref _virtualizer, state.Virtualizer, nameof(Virtualizer));
            Set(ref _voice, state.VoiceClarity, nameof(VoiceClarity));
            Set(ref _stabilizer, state.Stabilizer, nameof(Stabilizer));
            _preset = state.Preset;

            Raise(nameof(PreampText));
            Raise(nameof(BassText));
            Raise(nameof(TrebleText));
            Raise(nameof(WidthText));
            Raise(nameof(VirtualizerText));
            Raise(nameof(VoiceText));
            Raise(nameof(StabilizerText));
        }
        finally
        {
            _loading = false;
        }

        foreach (var preset in Presets)
        {
            preset.IsActive = preset.Name == _preset;
        }

        var known = Presets.FirstOrDefault(p => p.Name == _preset);
        Summary = _preset == SoundPresets.OriginalName ? "Original: no processing"
            : _preset == SoundPresets.SmartName ? $"Smart: {_smartProfile ?? "choosing"}"
            : _preset;
        ActiveDescription = known is null ? "Your own settings." : known.Preset.Description;
    }

    private void OnManualChange()
    {
        if (_loading)
        {
            return;
        }

        _smart = false;
        _preset = "Custom";
        foreach (var preset in Presets)
        {
            preset.IsActive = false;
        }

        Summary = "Custom";
        ActiveDescription = "Your own settings. Save them as your own preset below if you like them.";
        Commit();
    }

    private void Commit()
    {
        var settings = BuildFrom(Capture());

        if (_current.Id is null)
        {
            _saved[AllKey] = Capture();
            SerialWorker.Run(() => _controller.SetSound(settings));
            return;
        }

        var id = _current.Id!;
        _overrides.Add(id);
        _saved[id] = Capture();
        _current.HasOverride = true;
        SerialWorker.Run(() => _controller.SetDeviceSound(id, settings));
        RaiseScope();
    }

    private void RaiseScope()
    {
        Raise(nameof(TargetTitle));
        Raise(nameof(ShowFollowAll));
        Raise(nameof(ScopeNote));
    }

    // ---------- my presets ----------

    private PresetItem AddCustom(string name, SavedSound sound)
    {
        var item = new PresetItem(new SoundPreset(name, Fit(sound.Bands).Select(b => (float)b).ToArray(), "Your own preset."))
        {
            Custom = sound
        };
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
        _smart = false;
        _preset = name;
        foreach (var preset in Presets)
        {
            preset.IsActive = ReferenceEquals(preset, item);
        }

        Summary = name;
        ActiveDescription = "Your own preset.";
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
        ActiveDescription = "Your own settings.";
    }

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
        VoiceClarity = s.VoiceClarity,
        Stabilizer = s.Stabilizer,
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
            Math.Clamp(s.Virtualizer, 0, 100), Math.Clamp(s.VoiceClarity, 0, 100), Math.Clamp(s.Stabilizer, 0, 100));

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
        _smart = state.Preset == SoundPresets.SmartName;
        _smartProfile = null;
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
