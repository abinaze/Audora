using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using Audora.App.Infrastructure;
using Audora.Core.Audio;
using Audora.Core.Cleanup;
using Audora.Core.Devices;
using Audora.Core.Settings;

namespace Audora.App.ViewModels;

/// <summary>One microphone you can pick.</summary>
public sealed class MicrophoneChoice
{
    public MicrophoneChoice(string id, string name, bool isDefault)
    {
        Id = id;
        Name = name;
        IsDefault = isDefault;
        Label = isDefault ? name + " (default)" : name;
    }

    public string Id { get; }

    public string Name { get; }

    public bool IsDefault { get; }

    public string Label { get; }
}

/// <summary>
/// The Microphone page. Nothing listens until you press Test, and a test stops by itself after 15 seconds. The sound
/// is only measured: it is never saved, played or sent. The choices are remembered per microphone.
/// </summary>
public sealed class MicrophoneViewModel : ObservableObject, IDisposable
{
    private const int TestSeconds = 15;
    private const int TickMilliseconds = 200;

    private readonly DeviceEnumerator _devices;
    private readonly Dispatcher _ui;
    private readonly List<SavedMicrophone> _saved;
    private readonly DispatcherTimer _timer;
    private MicrophoneSession? _session;
    private MicrophoneChoice? _selected;
    private bool _noiseRemoval;
    private double _noiseStrength = 100;
    private string _engine = NoiseEngines.Default;
    private string _engineText = string.Empty;
    private bool _applying;
    private bool _testing;
    private int _elapsedMs;
    private float _peak;
    private double _inputSquares;
    private double _outputSquares;
    private int _levelTicks;
    private double _inputBar;
    private double _outputBar;
    private string _inputText = string.Empty;
    private string _outputText = string.Empty;
    private string _status = "Pick a microphone and press Test.";

    public MicrophoneViewModel(DeviceEnumerator devices, Dispatcher ui, List<SavedMicrophone> saved)
    {
        _devices = devices;
        _ui = ui;
        _saved = saved;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TickMilliseconds) };
        _timer.Tick += (_, _) => OnTick();

        ChooseCommand = new RelayCommand(p =>
        {
            if (p is MicrophoneChoice choice)
            {
                Select(choice);
            }
        });
        TestCommand = new RelayCommand(ToggleTest, () => _selected is not null);
        ChooseEngineCommand = new RelayCommand(
            p =>
            {
                if (p is string engine)
                {
                    NoiseEngine = engine;
                }
            },
            _ => CanChangeEngine);
    }

    public ObservableCollection<MicrophoneChoice> Microphones { get; } = new();

    public RelayCommand ChooseCommand { get; }

    public RelayCommand TestCommand { get; }

    public RelayCommand ChooseEngineCommand { get; }

    public bool HasMicrophones => Microphones.Count > 0;

    public bool HasNoMicrophones => Microphones.Count == 0;

    public bool HasSelection => _selected is not null;

    public string SelectedText => _selected is null ? "No microphone selected." : "Selected: " + _selected.Name;

    public bool IsTesting => _testing;

    public string TestButtonText => _testing ? "Stop test" : "Test microphone";

    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    public double InputBar
    {
        get => _inputBar;
        private set => Set(ref _inputBar, value);
    }

    public double OutputBar
    {
        get => _outputBar;
        private set => Set(ref _outputBar, value);
    }

    public string InputText
    {
        get => _inputText;
        private set => Set(ref _inputText, value);
    }

    public string OutputText
    {
        get => _outputText;
        private set => Set(ref _outputText, value);
    }

    /// <summary>Removes steady background noise from this microphone's voice during the test. Off until switched on.</summary>
    public bool NoiseRemoval
    {
        get => _noiseRemoval;
        set
        {
            if (Set(ref _noiseRemoval, value) && !_applying)
            {
                Remember();
                if (_session is not null)
                {
                    _session.NoiseRemoval = value;
                    UpdateEngineText();
                }
            }
        }
    }

    /// <summary>0 keeps the original sound, 100 plays only the cleaned sound.</summary>
    public double NoiseStrength
    {
        get => _noiseStrength;
        set
        {
            if (Set(ref _noiseStrength, Math.Clamp(Math.Round(value), 0, 100)) && !_applying)
            {
                Remember();
                if (_session is not null)
                {
                    _session.Strength = (float)(_noiseStrength / 100.0);
                }
            }
        }
    }

    /// <summary>"Studio" or "Standard". It is used from the next test and saved with the microphone.</summary>
    public string NoiseEngine
    {
        get => _engine;
        set
        {
            var engine = NoiseEngines.Normalize(value);
            if (_testing || !Set(ref _engine, engine))
            {
                return;
            }

            if (!_applying)
            {
                Remember();
            }

            UpdateEngineText();
        }
    }

    /// <summary>The engine can be changed while no test is running.</summary>
    public bool CanChangeEngine => _selected is not null && !_testing;

    /// <summary>A plain sentence about the engine in use, or why Standard is used instead.</summary>
    public string EngineText
    {
        get => _engineText;
        private set => Set(ref _engineText, value);
    }

    /// <summary>Lists the microphones that are connected now. Called when the page is shown.</summary>
    public void Refresh()
    {
        if (_testing)
        {
            return;
        }

        List<AudioDeviceInfo> active;
        try
        {
            active = _devices.GetCaptureDevices().Where(d => d.Status == DeviceStatus.Active).ToList();
        }
        catch (Exception ex)
        {
            Log.Error("Could not list microphones", ex);
            active = new List<AudioDeviceInfo>();
        }

        Microphones.Clear();
        foreach (var info in active)
        {
            Microphones.Add(new MicrophoneChoice(info.Id, info.Name, info.IsDefault));
        }

        var keep = _selected is null ? null : Microphones.FirstOrDefault(m => m.Id == _selected.Id);
        Select(keep ?? Microphones.FirstOrDefault(m => m.IsDefault) ?? Microphones.FirstOrDefault());
        Raise(nameof(HasMicrophones));
        Raise(nameof(HasNoMicrophones));
    }

    /// <summary>Stops a running test. Safe to call at any time.</summary>
    public void StopTest() => EndTest(true);

    public void Dispose() => EndTest(false);

    private void Select(MicrophoneChoice? choice)
    {
        if (_testing)
        {
            return;
        }

        _selected = choice;
        _applying = true;
        try
        {
            var record = choice is null
                ? null
                : _saved.FirstOrDefault(s => string.Equals(s.Id, choice.Id, StringComparison.OrdinalIgnoreCase));
            Set(ref _noiseRemoval, record?.NoiseRemoval ?? false, nameof(NoiseRemoval));
            Set(ref _noiseStrength, record?.NoiseStrength ?? 100, nameof(NoiseStrength));
            Set(ref _engine, NoiseEngines.Normalize(record?.NoiseEngine), nameof(NoiseEngine));
        }
        finally
        {
            _applying = false;
        }

        Raise(nameof(SelectedText));
        Raise(nameof(CanChangeEngine));
        UpdateEngineText();
        Raise(nameof(HasSelection));
        CommandManager.InvalidateRequerySuggested();
    }

    private void Remember()
    {
        if (_selected is null)
        {
            return;
        }

        var record = _saved.FirstOrDefault(s => string.Equals(s.Id, _selected.Id, StringComparison.OrdinalIgnoreCase));
        if (record is null)
        {
            record = new SavedMicrophone { Id = _selected.Id };
            _saved.Add(record);
        }

        record.Name = _selected.Name;
        record.LastSeenUtc = DateTime.UtcNow;
        record.NoiseRemoval = _noiseRemoval;
        record.NoiseStrength = _noiseStrength;
        record.NoiseEngine = _engine;
    }

    private void ToggleTest()
    {
        if (_testing)
        {
            EndTest(true);
        }
        else
        {
            StartTest();
        }
    }

    private void StartTest()
    {
        if (_selected is null)
        {
            return;
        }

        var session = new MicrophoneSession(_selected.Id)
        {
            NoiseRemoval = _noiseRemoval,
            Strength = (float)(_noiseStrength / 100.0),
            Engine = _engine,
        };

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var error = session.Start();
        watch.Stop();
        if (error is not null)
        {
            session.Dispose();
            Status = error;
            return;
        }

        session.Stopped += OnSessionStopped;
        _session = session;
        _elapsedMs = 0;
        _peak = 0f;
        _inputSquares = 0;
        _outputSquares = 0;
        _levelTicks = 0;
        _testing = true;
        Status = NoiseRemovalNote(session) ?? $"Listening for {TestSeconds} seconds. Speak, or make the noise you want to remove.";
        _timer.Start();
        Log.Info($"Microphone test started on {_selected.Name} with the {session.ActiveEngine} engine, ready in {watch.ElapsedMilliseconds} ms.");
        if (session.EngineNote is not null)
        {
            Log.Info(session.EngineNote);
        }

        UpdateEngineText();
        RaiseTestState();
    }

    private static string? NoiseRemovalNote(MicrophoneSession session) =>
        session.NoiseRemovalFailed ? "Noise removal could not start: " + session.NoiseRemovalReason : null;

    private void OnTick()
    {
        var session = _session;
        if (session is null)
        {
            return;
        }

        var levels = session.TakeLevels();
        InputBar = MicrophoneMeter.Bar(levels.InputRms);
        OutputBar = MicrophoneMeter.Bar(levels.OutputRms);
        InputText = MicrophoneMeter.Db(levels.InputRms).ToString("0") + " dB";
        OutputText = MicrophoneMeter.Db(levels.OutputRms).ToString("0") + " dB";

        if (levels.InputPeak > _peak)
        {
            _peak = levels.InputPeak;
        }

        _inputSquares += (double)levels.InputRms * levels.InputRms;
        _outputSquares += (double)levels.OutputRms * levels.OutputRms;
        _levelTicks++;
        _elapsedMs += TickMilliseconds;

        if (_elapsedMs >= TestSeconds * 1000)
        {
            EndTest(true);
        }
    }

    private void OnSessionStopped(string? message)
    {
        var session = _session;
        _ui.BeginInvoke(new Action(() =>
        {
            if (session is null || !ReferenceEquals(_session, session))
            {
                return;
            }

            EndTest(false);
            Status = message ?? "The microphone stopped.";
        }));
    }

    private void EndTest(bool summarize)
    {
        if (!_testing)
        {
            return;
        }

        _timer.Stop();
        var session = _session;
        _session = null;
        _testing = false;
        if (session is not null)
        {
            session.Stopped -= OnSessionStopped;
            session.Dispose();
        }

        InputBar = 0;
        OutputBar = 0;
        InputText = string.Empty;
        OutputText = string.Empty;

        if (summarize && _levelTicks > 0)
        {
            var inputRms = (float)Math.Sqrt(_inputSquares / _levelTicks);
            var outputRms = (float)Math.Sqrt(_outputSquares / _levelTicks);
            var level = MicrophoneCheck.Describe(MicrophoneCheck.Judge(_peak));
            Status = _noiseRemoval
                ? level + " " + MicrophoneMeter.ChangeText(inputRms, outputRms)
                : level + " Switch noise removal on to see how much it removes.";
        }
        else
        {
            Status = "Test stopped.";
        }

        Log.Info("Microphone test finished.");
        RaiseTestState();
    }

    private void UpdateEngineText()
    {
        var note = _session?.EngineNote ?? NoiseEngineSelector.ForMicrophone(_engine, AppContext.BaseDirectory).FallbackReason;
        EngineText = NoiseEngines.Describe(_engine, note);
    }

    private void RaiseTestState()
    {
        Raise(nameof(IsTesting));
        Raise(nameof(TestButtonText));
        Raise(nameof(CanChangeEngine));
        CommandManager.InvalidateRequerySuggested();
    }
}
