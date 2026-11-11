using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using Audora.App.Infrastructure;
using Audora.Core.Devices;
using Audora.Core.Diagnostics;

namespace Audora.App.ViewModels;

/// <summary>One output device that can be tested.</summary>
public sealed class SoundDeviceChoice : ObservableObject
{
    private bool _isActive;

    public SoundDeviceChoice(AudioDeviceInfo device)
    {
        Id = device.Id;
        Name = device.Name;
        Label = device.IsDefault ? $"{device.Name} (default)" : device.Name;
        IsDefault = device.IsDefault;
    }

    public string Id { get; }

    public string Name { get; }

    public string Label { get; }

    public bool IsDefault { get; }

    public bool IsActive
    {
        get => _isActive;
        set => Set(ref _isActive, value);
    }
}

/// <summary>
/// Sound Check: plays a short, quiet tone and asks what you heard. It never changes the Windows volume.
/// The rule for when a tone may play lives in <see cref="SoundCheckGuard"/>. Stop ends the sound at once.
/// </summary>
public sealed partial class SoundCheckViewModel : ObservableObject, IDisposable
{
    private const string Intro = "Pick a device, then press a test and listen.";

    private readonly DeviceEnumerator _devices;
    private readonly Dispatcher _ui;
    private readonly Func<bool> _isEngineOn;
    private readonly Func<string> _copiesTo;
    private readonly Action _turnEngineOff;
    private readonly SoundCheckPlayer _player;
    private SoundDeviceChoice? _selected;
    private SoundCheckTest? _current;
    private SoundCheckTest? _asking;
    private string? _refusedKey;
    private string _playedOn = string.Empty;
    private string _volumeText = string.Empty;
    private bool _volumeWarning;
    private bool _playing;
    private bool _disposed;
    private string _message = Intro;

    public SoundCheckViewModel(DeviceEnumerator devices, Dispatcher ui, Func<bool> isEngineOn, Func<string> copiesTo, Action turnEngineOff)
    {
        _devices = devices;
        _ui = ui;
        _isEngineOn = isEngineOn;
        _copiesTo = copiesTo;
        _turnEngineOff = turnEngineOff;

        // Created off the UI thread, like the other Windows audio objects.
        _player = Task.Run(() => new SoundCheckPlayer()).GetAwaiter().GetResult();
        _player.Finished += OnPlayerFinished;

        PlayCommand = new RelayCommand(p => _ = PlayAsync(p as string ?? string.Empty, false), _ => !_playing && _selected is not null);
        PlayAnywayCommand = new RelayCommand(
            () =>
            {
                var key = _refusedKey;
                if (key is not null)
                {
                    _ = PlayAsync(key, true);
                }
            },
            () => !_playing && _refusedKey is not null);
        StopCommand = new RelayCommand(Stop, () => _playing);
        ChooseDeviceCommand = new RelayCommand(p =>
        {
            if (p is SoundDeviceChoice choice)
            {
                Select(choice);
            }
        });
        YesCommand = new RelayCommand(() => Answer(true));
        NoCommand = new RelayCommand(() => Answer(false));
        TurnEngineOffCommand = new RelayCommand(() =>
        {
            _turnEngineOff();
            Raise(nameof(ShowEngineNote));
        });

        _ = LoadDevicesAsync();
    }

    public ObservableCollection<SoundDeviceChoice> Devices { get; } = new();

    public ObservableCollection<string> Results { get; } = new();

    public RelayCommand PlayCommand { get; }

    public RelayCommand PlayAnywayCommand { get; }

    public RelayCommand StopCommand { get; }

    public RelayCommand ChooseDeviceCommand { get; }

    public RelayCommand YesCommand { get; }

    public RelayCommand NoCommand { get; }

    public RelayCommand TurnEngineOffCommand { get; }

    public string Message
    {
        get => _message;
        private set => Set(ref _message, value);
    }

    public string VolumeText
    {
        get => _volumeText;
        private set => Set(ref _volumeText, value);
    }

    public bool VolumeWarning
    {
        get => _volumeWarning;
        private set => Set(ref _volumeWarning, value);
    }

    public bool CanPlayAnyway => _refusedKey is not null;

    public bool HasQuestion => _asking is not null;

    public string Question => _asking?.Question ?? string.Empty;

    public bool HasResults => Results.Count > 0;

    public bool ShowEngineNote => _isEngineOn() && _selected is { IsDefault: true } && _copiesTo().Length > 0;

    public string EngineNote =>
        $"Audora is on, so this tone is also copied to: {_copiesTo()}. Press Turn Audora off for testing to hear only the device you picked.";

    /// <summary>Called when the page is shown, so the engine note, device list and volume are current.</summary>
    public void RefreshState()
    {
        Raise(nameof(ShowEngineNote));
        _ = LoadDevicesAsync();
        _ = RefreshVolumeAsync();
    }

    /// <summary>Ends any tone at once. Safe to call when nothing is playing.</summary>
    public void Stop()
    {
        if (!_playing)
        {
            return;
        }

        SetPlaying(false);
        _asking = null;
        RaiseQuestion();
        Message = "Stopped.";
        Log.Info("Sound Check: stopped by you.");
        _ = Task.Run(() => _player.Stop());
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _player.Finished -= OnPlayerFinished;
        Task.Run(() => _player.Dispose()).Wait(TimeSpan.FromSeconds(2));
    }

    private async Task LoadDevicesAsync()
    {
        try
        {
            var found = await Task.Run(() => _devices.GetActiveRenderDevices().ToList());
            var keep = _selected?.Id;
            var defaultId = found.FirstOrDefault(d => d.IsDefault)?.Id;

            Devices.Clear();
            foreach (var device in found)
            {
                Devices.Add(new SoundDeviceChoice(device));
            }

            var pick = Devices.FirstOrDefault(d => d.Id == keep)
                       ?? Devices.FirstOrDefault(d => d.Id == defaultId)
                       ?? Devices.FirstOrDefault();

            if (pick is null)
            {
                _selected = null;
                VolumeText = string.Empty;
                VolumeWarning = false;
                CommandManager.InvalidateRequerySuggested();
            }
            else
            {
                Select(pick);
            }
        }
        catch (Exception ex)
        {
            Log.Error("Sound Check could not list the devices", ex);
            Message = "Windows would not list the sound outputs. Details are in the log.";
        }
    }

    private void Select(SoundDeviceChoice choice)
    {
        foreach (var device in Devices)
        {
            device.IsActive = ReferenceEquals(device, choice);
        }

        _selected = choice;
        Raise(nameof(ShowEngineNote));
        Raise(nameof(EngineNote));
        SetRefused(null);
        CommandManager.InvalidateRequerySuggested();
        _ = RefreshVolumeAsync();
    }

    private async Task RefreshVolumeAsync()
    {
        var device = _selected;
        if (device is null)
        {
            return;
        }

        var state = await Task.Run(() => _player.ReadWindowsVolume(device.Id));
        if (ReferenceEquals(device, _selected))
        {
            ShowVolume(state);
        }
    }

    private void ShowVolume(DeviceVolumeState? state)
    {
        if (state is null)
        {
            VolumeText = "The Windows volume of this device could not be read.";
            VolumeWarning = false;
        }
        else if (state.Muted)
        {
            VolumeText = "This device is muted in Windows.";
            VolumeWarning = true;
        }
        else
        {
            var percent = state.Volume * 100;
            VolumeWarning = state.Volume > SoundCheckGuard.LoudVolume;
            VolumeText = VolumeWarning
                ? $"Windows volume of this device: {percent:0}%. Above 50% a tone could be loud. Lower it first, or press Play anyway after a refusal."
                : $"Windows volume of this device: {percent:0}%.";
        }
    }

    private async Task PlayAsync(string key, bool allowLoud)
    {
        var test = SoundCheckTests.Find(key);
        var device = _selected;
        if (test is null || device is null || _playing)
        {
            return;
        }

        _asking = null;
        RaiseQuestion();
        SetRefused(null);

        var state = await Task.Run(() => _player.ReadWindowsVolume(device.Id));
        ShowVolume(state);
        var percent = state is null ? 0 : state.Volume * 100;

        switch (SoundCheckGuard.Decide(state, allowLoud))
        {
            case GuardDecision.Muted:
                Message = $"{device.Name} is muted in Windows, so you would hear nothing. Unmute it first.";
                Log.Info($"Sound Check refused: {device.Name} is muted in Windows.");
                return;

            case GuardDecision.TooLoud:
                Message = $"The Windows volume of this device is {percent:0}%, which is above 80%. Sound Check will not play. Lower it first (for the default device, use the Master volume slider). Audora never changes your volume.";
                Log.Info($"Sound Check refused: the Windows volume of {device.Name} is {percent:0}%, above the 80% limit.");
                return;

            case GuardDecision.Loud:
                Message = $"The Windows volume of this device is {percent:0}%. A test tone could be loud. Lower it to 50% or less first (for the default device, use the Master volume slider), or press Play anyway. Audora never changes your volume.";
                Log.Info($"Sound Check refused: the Windows volume of {device.Name} is {percent:0}%.");
                SetRefused(key);
                return;
        }

        if (allowLoud && percent > SoundCheckGuard.LoudVolume * 100)
        {
            Log.Info($"Sound Check: playing at your request although the Windows volume is {percent:0}%.");
        }

        SetPlaying(true);
        _current = test;
        _playedOn = device.Name;
        _playedVolumePercent = percent;
        Message = $"{test.Title} on {device.Name}. {test.Hint}";
        Log.Info($"Sound Check: {test.Title} on {device.Name}.");

        try
        {
            await Task.Run(() => _player.Play(device.Id, test.Request));
            StartMeasuring(test.Request.Channel);
        }
        catch (Exception ex)
        {
            Log.Error("Sound Check could not play", ex);
            SetPlaying(false);
            Message = "Windows would not let Audora play the test on this device. Nothing was changed. Details are in the log.";
        }
    }

    private void OnPlayerFinished(object? sender, EventArgs e) =>
        _ui.BeginInvoke(new Action(HandleFinished));

    private void HandleFinished()
    {
        if (_disposed || !_playing)
        {
            return;
        }

        SetPlaying(false);
        _asking = _current;
        RaiseQuestion();
        Message = "Finished." + FinishMeasuring();
        Log.Info("Sound Check: the tone finished.");
        _ = Task.Run(() => _player.Stop());
    }

    private void Answer(bool heard)
    {
        if (_asking is null)
        {
            return;
        }

        var verdict = heard ? "you heard it correctly" : "it was not as expected";
        Results.Insert(0, $"{DateTime.Now:HH:mm}  {_asking.Title} on {_playedOn}: {verdict}.{_measuredShort}");
        _asking = null;
        RaiseQuestion();
        Raise(nameof(HasResults));
        Message = heard
            ? "Noted. Try another test."
            : "Noted. A test that is not as expected is worth a closer look. Repairs will come later and will always ask you first.";
    }

    private void SetRefused(string? key)
    {
        _refusedKey = key;
        Raise(nameof(CanPlayAnyway));
        CommandManager.InvalidateRequerySuggested();
    }

    private void SetPlaying(bool value)
    {
        _playing = value;
        Raise(nameof(ShowEngineNote));
        CommandManager.InvalidateRequerySuggested();
    }

    private void RaiseQuestion()
    {
        Raise(nameof(HasQuestion));
        Raise(nameof(Question));
    }
}
