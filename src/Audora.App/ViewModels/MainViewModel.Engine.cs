using System.Windows;
using Audora.App.Infrastructure;
using Audora.Core.Devices;
using Audora.Core.Settings;

namespace Audora.App.ViewModels;

public sealed partial class MainViewModel
{
    private bool _engineOn;
    private bool _startEngineOn;
    private bool _engineBusy;
    private string _engineMessage = string.Empty;

    public RelayCommand TurnOnEngineCommand { get; private set; } = null!;

    public RelayCommand RestoreWindowsAudioCommand { get; private set; } = null!;

    /// <summary>
    /// The Audora engine. While it is off Audora does nothing to any audio device: it does not listen, copy,
    /// watch devices or change anything, and Windows works as usual.
    /// </summary>
    public bool EngineOn
    {
        get => _engineOn;
        set
        {
            if (value)
            {
                _ = TurnOnEngineAsync();
            }
            else
            {
                TurnOffEngine();
            }
        }
    }

    public bool EngineOff => !_engineOn;

    public bool ShowOffPanel => !_engineOn && !IsSettingsTab && !IsHealthTab;

    public string EngineText => _engineOn ? "Audora is on" : "Audora is off";

    public string EngineHint => _engineOn
        ? "Playing on the devices you switched on. Turn it off to give Windows full control again."
        : "Audora is not touching your audio. Windows works exactly as usual.";

    /// <summary>Turn the engine on by itself when Audora starts. Off by default.</summary>
    public bool StartEngineOn
    {
        get => _startEngineOn;
        set => Set(ref _startEngineOn, value);
    }

    public string EngineMessage
    {
        get => _engineMessage;
        private set => Set(ref _engineMessage, value);
    }

    // Called once from the constructor, instead of starting the audio engine straight away.
    private void ApplyStartupEngineState()
    {
        TurnOnEngineCommand = new RelayCommand(() => _ = TurnOnEngineAsync());
        RestoreWindowsAudioCommand = new RelayCommand(RestoreWindowsAudio);

        RoutingText = "Audora is off. Windows audio works normally.";
        Status = "Audora is off. Turn it on to play on several devices.";

        if (_startEngineOn)
        {
            Log.Info("Turning Audora on at start, because the start-on setting is switched on.");
            _ = TurnOnEngineAsync();
        }
    }

    private void RaiseEngine()
    {
        Raise(nameof(EngineOff));
        Raise(nameof(EngineText));
        Raise(nameof(EngineHint));
        Raise(nameof(ShowOffPanel));
    }

    private async Task TurnOnEngineAsync()
    {
        if (_engineOn || _engineBusy || _disposed)
        {
            return;
        }

        _engineBusy = true;
        try
        {
            // Remember how Windows audio looks right now, so it can be put back. A virtual cable that
            // Audora's Full control mode made the default is not a state worth coming back to.
            var point = await Task.Run(() => AudioRestore.Capture(_devices));
            if (_disposed)
            {
                return;
            }

            if (point is not null && !point.DefaultDeviceName.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase))
            {
                _settings.RestorePoint = point;
            }

            _engineOn = true;
            Log.Info("Audora is on.");
            RaiseEngine();
            Status = "Audora is on.";
            RoutingText = "Looking for audio devices...";

            SerialWorker.Run(() => _controller.StartEngine());
            _ = RefreshAsync();
            _ = RefreshBluetoothAsync();
        }
        catch (Exception ex)
        {
            Log.Error("Could not turn Audora on", ex);
            Status = "Audora could not turn on. See the log for details.";
            RaiseEngine();
        }
        finally
        {
            _engineBusy = false;
            Raise(nameof(EngineOn));
        }
    }

    private void TurnOffEngine()
    {
        if (!_engineOn)
        {
            return;
        }

        _engineOn = false;
        Log.Info("Audora is off.");
        _watchedDefault = null;
        RaiseEngine();

        SerialWorker.Run(() =>
        {
            _controller.StopEngine();
            _watcher.Attach(null);
        });

        Status = "Audora is off.";
        RoutingText = "Audora is off. Windows audio works normally.";
        Raise(nameof(EngineOn));
    }

    private void RestoreWindowsAudio()
    {
        var point = _settings.RestorePoint;
        if (point is null)
        {
            EngineMessage = "There is nothing to restore yet. Audora saves how Windows audio looks each time you turn it on.";
            return;
        }

        var volume = point.Volume >= 0 ? $"{point.Volume:0}%" : "unchanged";
        var answer = MessageBox.Show(
            "This turns Audora off and puts Windows audio back the way it was when you last turned Audora on:\n\n"
            + $"  Default output: {point.DefaultDeviceName}\n"
            + $"  Volume: {volume}\n"
            + $"  {(point.Muted ? "Muted" : "Not muted")}\n\n"
            + "Continue?",
            "Restore Windows audio",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        TurnOffEngine();
        _ = RunRestoreAsync(point);
    }

    private async Task RunRestoreAsync(SavedRestorePoint point)
    {
        EngineMessage = "Restoring...";
        string result;
        try
        {
            await Task.Delay(700); // let Audora release the devices first
            result = await Task.Run(() => AudioRestore.Restore(_devices, point));
        }
        catch (Exception ex)
        {
            Log.Error("Restore failed", ex);
            result = "Windows would not accept the change. Open Windows Sound settings and choose your device there.";
        }

        EngineMessage = result;
    }
}
