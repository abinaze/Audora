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

/// <summary>The Sound tab: presets, 10-band equalizer, preamp, bass, treble and stereo width.</summary>
public sealed class SoundViewModel : ObservableObject
{
    private readonly MirrorController _controller;
    private bool _loading;
    private double _preamp;
    private double _bass;
    private double _treble;
    private double _width = 100;
    private string _summary = "Original: no processing";

    public SoundViewModel(MirrorController controller)
    {
        _controller = controller;

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
        ResetCommand = new RelayCommand(() => Choose(Presets[0]));
    }

    public ObservableCollection<BandViewModel> Bands { get; } = new();

    public ObservableCollection<PresetItem> Presets { get; } = new();

    public RelayCommand ChoosePresetCommand { get; }

    public RelayCommand ResetCommand { get; }

    public string Summary
    {
        get => _summary;
        private set => Set(ref _summary, value);
    }

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

    private void Choose(PresetItem item)
    {
        _loading = true;
        try
        {
            for (var i = 0; i < Bands.Count; i++)
            {
                Bands[i].SetSilently(item.Preset.Bands[i]);
            }

            Set(ref _preamp, item.Name == SoundPresets.OriginalName ? 0 : SoundPresets.PreampFor(item.Preset.Bands), nameof(Preamp));
            Set(ref _bass, 0d, nameof(Bass));
            Set(ref _treble, 0d, nameof(Treble));
            Set(ref _width, 100d, nameof(Width));
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
            preset.IsActive = ReferenceEquals(preset, item);
        }

        var original = item.Name == SoundPresets.OriginalName;
        Summary = original ? "Original: no processing" : item.Name;
        Push(!original);
    }

    private void OnManualChange()
    {
        if (_loading)
        {
            return;
        }

        foreach (var preset in Presets)
        {
            preset.IsActive = false;
        }

        Summary = "Custom";
        Push(true);
    }

    private void Push(bool enabled)
    {
        var bands = Bands.Select(b => (float)b.Gain).ToArray();
        var settings = enabled
            ? new EqSettings(true, (float)_preamp, bands, (float)_bass, (float)_treble, (float)(_width / 100.0))
            : EqSettings.Original;

        SerialWorker.Run(() => _controller.SetSound(settings));
    }
}
