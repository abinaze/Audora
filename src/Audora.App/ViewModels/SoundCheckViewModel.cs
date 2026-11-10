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
    }

    public string Id { get; }

    public string Name { get; }

    public string Label { get; }

    public bool IsActive
    {
        get => _isActive;
        set => Set(ref _isActive, value);
    }
}

/// <summary>
/// Sound Check: plays a short, quiet tone and asks what you heard. It never changes the Windows volume,
/// refuses when the volume is high or the device is muted, and Stop ends the sound at once.
/// </summary>
public sealed class SoundCheckViewModel : ObservableObject, IDisposable
{
    private const float LoudVolume = 0.5f;
    private const string Intro = "Pick a device, then press a test and listen.";

    private readonly DeviceEnumerator _devices;
    private readonly Dispatcher _ui;
    private readonly Func<bool> _isEngineOn;
    private readonly SoundCheckPlayer _player;
    private SoundDeviceChoice? _selected;
    private SoundCheckTest? _current;
    private SoundCheckTest? _asking;
    private string _playedOn = string.Empty;
    private bool _playing;
    private bool _disposed;
    private string _message = Intro;

    public SoundCheckViewModel(DeviceEnumerator devices, Dispatcher ui, Func<bool> isEngineOn)
    {
        _devices = devices;
        _ui = ui;
        _isEngineOn = isEngineOn;

        // Created off the UI thread, like the other Windows audio objects.
        _player = Task.Run(() => new SoundCheckPlayer()).GetAwaiter().GetResult();
        _player.Finished += OnPlayerFinished;

        PlayCommand = new RelayCommand(p => _ = PlayAsync(p as string ?? string.Empty), _ => !_playing && _selected is not null);
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

        _ = LoadDevicesAsync();
    }

    public ObservableCollection<SoundDeviceChoice> Devices { get; } = new();

    public ObservableCollection<string> Results { get; } = new();

    public RelayCommand PlayCommand { get; }

    public RelayCommand StopCommand { get; }

    public RelayCommand ChooseDeviceCommand { get; }

    public RelayCommand YesCommand { get; }

    public RelayCommand NoCommand { get; }

    public string Message
    {
        get => _message;
        private set => Set(ref _message, value);
    }

    public bool HasQuestion => _asking is not null;

    public string Question => _asking?.Question ?? string.Empty;

    public bool HasResults => Results.Count > 0;

    public bool ShowEngineNote => _isEngineOn();

    public string EngineNote =>
        "Audora is on, so a tone on your default device is also copied to the other devices you switched on. Turn Audora off to hear only the device you picked.";

    /// <summary>Called when the page is shown, so the engine note and device list are current.</summary>
    public void RefreshState()
    {
        Raise(nameof(ShowEngineNote));
        _ = LoadDevicesAsync();
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
        CommandManager.InvalidateRequerySuggested();
    }

    private async Task PlayAsync(string key)
    {
        var test = SoundCheckTests.Find(key);
        var device = _selected;
        if (test is null || device is null || _playing)
        {
            return;
        }

        _asking = null;
        RaiseQuestion();

        var state = await Task.Run(() => _player.ReadWindowsVolume(device.Id));
        if (state is not null && state.Muted)
        {
            Message = $"{device.Name} is muted in Windows, so you would hear nothing. Unmute it first.";
            return;
        }

        if (state is not null && state.Volume > LoudVolume)
        {
            Message = $"The Windows volume of this device is {state.Volume * 100:0}%. A test tone could be loud. Lower it to 50% or less first (for the default device, use the Master volume slider). Audora never changes your volume.";
            return;
        }

        SetPlaying(true);
        _current = test;
        _playedOn = device.Name;
        Message = $"{test.Title} on {device.Name}. {test.Hint}";

        try
        {
            await Task.Run(() => _player.Play(device.Id, test.Request));
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
        Message = "Finished.";
        _ = Task.Run(() => _player.Stop());
    }

    private void Answer(bool heard)
    {
        if (_asking is null)
        {
            return;
        }

        var verdict = heard ? "you heard it correctly" : "it was not as expected";
        Results.Insert(0, $"{DateTime.Now:HH:mm}  {_asking.Title} on {_playedOn}: {verdict}");
        _asking = null;
        RaiseQuestion();
        Raise(nameof(HasResults));
        Message = heard
            ? "Noted. Try another test."
            : "Noted. A test that is not as expected is worth a closer look. Repairs will come later and will always ask you first.";
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
