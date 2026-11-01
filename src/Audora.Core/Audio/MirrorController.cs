using System.Collections.Concurrent;
using Audora.Core.Devices;

namespace Audora.Core.Audio;

public sealed record MirrorTargetStatus(string DeviceId, string Name, string State);

/// <summary>
/// Keeps the user's selected devices playing: starts and stops outputs as devices come and go,
/// follows changes of the Windows default output, and restarts capture if it dies.
/// Per-device settings are remembered and re-applied whenever a device returns.
/// <see cref="Message"/> may be raised on any thread; UI code must marshal to its own thread.
/// </summary>
public sealed class MirrorController : IDisposable
{
    private const int MaxConsecutiveFailures = 5;

    private readonly DeviceEnumerator _devices;
    private readonly object _gate = new();
    private readonly HashSet<string> _selected = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DeviceSettings> _settings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _names = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _suppressed = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Threading.Timer _timer;
    private MirrorEngine? _engine;
    private volatile bool _restartNeeded;
    private volatile bool _disposed;
    private int _failures;

    public MirrorController(DeviceEnumerator devices)
    {
        _devices = devices;
        _timer = new System.Threading.Timer(_ => Reconcile());
    }

    /// <summary>Human-readable status lines such as "Stopped mirroring to X".</summary>
    public event EventHandler<string>? Message;

    public string? SourceName
    {
        get
        {
            lock (_gate)
            {
                return _engine?.SourceName;
            }
        }
    }

    public void Start()
    {
        _devices.DevicesChanged += OnDevicesChanged;
        Reconcile();
    }

    public void Select(string deviceId)
    {
        lock (_gate)
        {
            _selected.Add(deviceId);
            _suppressed.TryRemove(deviceId, out _);
        }

        Reconcile();
    }

    public void Deselect(string deviceId)
    {
        lock (_gate)
        {
            _selected.Remove(deviceId);
        }

        Reconcile();
    }

    public DeviceSettings GetSettings(string deviceId)
    {
        lock (_gate)
        {
            return GetOrCreate(deviceId);
        }
    }

    public void SetVolume(string deviceId, float volume) =>
        Update(deviceId, s => s.Volume = Math.Clamp(volume, 0f, 1f));

    public void SetMuted(string deviceId, bool muted) =>
        Update(deviceId, s => s.Muted = muted);

    public void SetBalance(string deviceId, float balance) =>
        Update(deviceId, s => s.Balance = Math.Clamp(balance, -1f, 1f));

    public void SetDelay(string deviceId, int milliseconds) =>
        Update(deviceId, s => s.DelayMilliseconds = Math.Clamp(milliseconds, 0, DelaySampleProvider.MaxDelayMilliseconds));

    public IReadOnlyList<MirrorTargetStatus> GetStatus()
    {
        lock (_gate)
        {
            var devices = _devices.GetRenderDevices();
            var running = (_engine?.Outputs.Select(o => o.DeviceId) ?? Enumerable.Empty<string>())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var result = new List<MirrorTargetStatus>();

            foreach (var id in _selected)
            {
                var device = devices.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));
                var name = device?.Name ?? NameOf(id);
                string state;

                if (running.Contains(id))
                {
                    state = "Mirroring";
                }
                else if (_suppressed.ContainsKey(id))
                {
                    state = "Stopped after an error";
                }
                else if (device is { IsDefault: true, Status: DeviceStatus.Active })
                {
                    state = "Plays natively (Windows default)";
                }
                else if (device is null || device.Status != DeviceStatus.Active)
                {
                    state = "Waiting for device";
                }
                else
                {
                    state = "Starting";
                }

                result.Add(new MirrorTargetStatus(id, name, state));
            }

            return result;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _devices.DevicesChanged -= OnDevicesChanged;
            _timer.Dispose();
            DisposeEngine();
        }
    }

    private void Update(string deviceId, Action<DeviceSettings> change)
    {
        lock (_gate)
        {
            var settings = GetOrCreate(deviceId);
            change(settings);

            var channel = _engine?.Outputs.FirstOrDefault(o =>
                string.Equals(o.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
            if (channel is not null)
            {
                Apply(channel, settings);
            }
        }
    }

    private DeviceSettings GetOrCreate(string deviceId)
    {
        if (!_settings.TryGetValue(deviceId, out var settings))
        {
            settings = new DeviceSettings();
            _settings[deviceId] = settings;
        }

        return settings;
    }

    private string NameOf(string deviceId) =>
        _names.TryGetValue(deviceId, out var name) ? name : deviceId;

    private static void Apply(OutputChannel channel, DeviceSettings settings)
    {
        channel.Volume = settings.Volume;
        channel.IsMuted = settings.Muted;
        channel.Balance = settings.Balance;
        channel.DelayMilliseconds = settings.DelayMilliseconds;
    }

    private void DisposeEngine()
    {
        _engine?.Dispose();
        _engine = null;
    }

    private void OnDevicesChanged(object? sender, EventArgs e)
    {
        _suppressed.Clear(); // a device event is the cue to give failed devices another chance
        Schedule(100);
    }

    private void OnCaptureStopped(object? sender, Exception? error)
    {
        _restartNeeded = true;
        Raise($"Capture stopped ({error?.Message ?? "device changed"}); restarting.");
        Schedule(500);
    }

    // Deliberately lock-free: this runs on the audio thread, which Dispose may be waiting on.
    private void OnChannelFaulted(object? sender, Exception error)
    {
        if (sender is OutputChannel channel)
        {
            _suppressed[channel.DeviceId] = 0;
            Raise($"{channel.Name} stopped: {error.Message}");
            Schedule(100);
        }
    }

    private void Schedule(int delayMilliseconds)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _timer.Change(delayMilliseconds, Timeout.Infinite);
        }
        catch (ObjectDisposedException)
        {
            // shutting down
        }
    }

    private void Raise(string text)
    {
        try
        {
            Message?.Invoke(this, text);
        }
        catch
        {
            // a misbehaving subscriber must never take down the audio pipeline
        }
    }

    private void Reconcile()
    {
        var messages = new List<string>();

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                ReconcileLocked(messages);
                _failures = 0;
            }
            catch (Exception ex)
            {
                _failures++;
                messages.Add($"Audio routing problem: {ex.Message}");

                if (_failures < MaxConsecutiveFailures)
                {
                    _restartNeeded = true;
                    Schedule(1500);
                }
                else
                {
                    messages.Add("Giving up after repeated failures. Change a device or re-select targets to retry.");
                }
            }
        }

        foreach (var text in messages)
        {
            Raise(text);
        }
    }

    private void ReconcileLocked(List<string> messages)
    {
        var devices = _devices.GetRenderDevices();
        foreach (var device in devices)
        {
            _names[device.Id] = device.Name;
        }

        var forceRestart = _restartNeeded || _engine is null;
        var wanted = _selected.Where(id => !_suppressed.ContainsKey(id)).ToList();
        var plan = MirrorPlanner.Plan(
            devices,
            wanted,
            forceRestart ? null : _engine!.SourceDeviceId,
            forceRestart ? Array.Empty<string>() : _engine!.Outputs.Select(o => o.DeviceId).ToList());

        if (plan.SourceId is null)
        {
            if (_engine is not null)
            {
                DisposeEngine();
                messages.Add("No active output device; mirroring paused.");
            }

            return;
        }

        MirrorEngine engine;
        if (plan.RestartCapture)
        {
            DisposeEngine();
            engine = new MirrorEngine();
            try
            {
                engine.CaptureStopped += OnCaptureStopped;
                engine.Start(plan.SourceId);
            }
            catch
            {
                engine.Dispose();
                throw;
            }

            _engine = engine;
            _restartNeeded = false;
            messages.Add($"Capturing audio from {engine.SourceName}.");
        }
        else
        {
            engine = _engine!;
            foreach (var id in plan.Remove)
            {
                engine.RemoveOutput(id);
                messages.Add($"Stopped mirroring to {NameOf(id)}.");
            }
        }

        foreach (var id in plan.Add)
        {
            try
            {
                var channel = engine.AddOutput(id, NameOf(id));
                Apply(channel, GetOrCreate(id));
                channel.Faulted += OnChannelFaulted;
                messages.Add($"Mirroring to {channel.Name}.");
            }
            catch (Exception ex)
            {
                _suppressed[id] = 0;
                messages.Add($"Could not start {NameOf(id)}: {ex.Message}");
            }
        }
    }
}
