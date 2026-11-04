using System.Collections.ObjectModel;
using Audora.App.Infrastructure;
using Audora.Core.Audio;

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
/// The Sound tab: presets, 10-band equalizer, preamp, bass, treble and stereo width,
/// for all devices together or for one device on its own.
/// </summary>
public sealed class SoundViewModel : ObservableObject
{
    private sealed record EditorState(string Preset, double[] Bands, double Preamp, double Bass, double Treble, double Width);

    private const string AllKey = "*";

    private readonly MirrorController _controller;
    private readonly Dictionary<string, EditorState> _saved = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _overrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly SoundTarget _all = new(null, "All devices");
    private SoundTarget _current;
    private bool _loading;
    private string _preset = SoundPresets.OriginalName;
    private double _preamp;
    private double _bass;
    private double _treble;
    private double _width = 100;
    private string _summary = "Original: no processing";

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
    }

    public ObservableCollection<BandViewModel> Bands { get; } = new();

    public ObservableCollection<PresetItem> Presets { get; } = new();

    public ObservableCollection<SoundTarget> Targets { get; } = new();

    public RelayCommand ChoosePresetCommand { get; }

    public RelayCommand ChooseTargetCommand { get; }

    public RelayCommand ResetCommand { get; }

    public RelayCommand FollowAllCommand { get; }

    public string Summary
    {
        get => _summary;
        private set => Set(ref _summary, value);
    }

    public string TargetTitle => _current.Name;

    public bool ShowFollowAll => _current.Id is not null && _current.HasOverride;

    public string ScopeNote => _current.Id is null
        ? "These settings apply to every device Audora plays on, except devices marked with a star, which have their own sound."
        : _current.HasOverride
            ? $"{_current.Name} has its own sound. Changes here do not affect other devices."
            : $"{_current.Name} follows All devices. Change anything below to give it its own sound.";

    public double Preamp
    {
        get => _preamp;
        set
        {
            if (Set(ref _preamp, Math.Round(Math.Clamp(value, -12, 12) * 2) / 2))
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
            if (Set(ref _bass, Math.Round(Math.Clamp(value, -10, 10) * 2) / 2))
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
            if (Set(ref _treble, Math.Round(Math.Clamp(value, -10, 10) * 2) / 2))
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

    public string WidthText => $"{_width:0}%";

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

    private static EditorState OriginalState() =>
        new(SoundPresets.OriginalName, new double[10], 0, 0, 0, 100);

    private EditorState Capture() =>
        new(_preset, Bands.Select(b => b.Gain).ToArray(), _preamp, _bass, _treble, _width);

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
        var original = item.Name == SoundPresets.OriginalName;
        Load(new EditorState(
            item.Name,
            item.Preset.Bands.Select(b => (double)b).ToArray(),
            original ? 0 : SoundPresets.PreampFor(item.Preset.Bands),
            0,
            0,
            100));
        Commit();
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
            _preset = state.Preset;
            Raise(nameof(PreampText));
            Raise(nameof(BassText));
            Raise(nameof(TrebleText));
            Raise(nameof(WidthText));
        }
        finally
        {
            _loading = false;
        }

        foreach (var preset in Presets)
        {
            preset.IsActive = preset.Name == _preset;
        }

        Summary = _preset == SoundPresets.OriginalName ? "Original: no processing" : _preset;
    }

    private void OnManualChange()
    {
        if (_loading)
        {
            return;
        }

        _preset = "Custom";
        foreach (var preset in Presets)
        {
            preset.IsActive = false;
        }

        Summary = "Custom";
        Commit();
    }

    private EqSettings Build() =>
        _preset == SoundPresets.OriginalName
            ? EqSettings.Original
            : new EqSettings(
                true,
                (float)_preamp,
                Bands.Select(b => (float)b.Gain).ToArray(),
                (float)_bass,
                (float)_treble,
                (float)(_width / 100.0));

    private void Commit()
    {
        var settings = Build();

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
}
