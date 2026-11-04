using System.Collections.Concurrent;
using System.Diagnostics;
using Audora.Core.Devices;

namespace Audora.Core.Audio;

public sealed record MirrorTargetStatus(string DeviceId, string Name, string State, string? Detail = null);

/// <summary>
/// Keeps the user's selected devices playing: starts and stops outputs as devices come and go,
/// follows changes of the Windows default output, restarts capture if it dies, and retries
/// outputs that Windows stopped because of an error (with a limit, so a broken device cannot loop).
/// Per-device settings are remembered and re-applied whenever a device returns.
/// <see cref="Message"/> may be raised on any thread; UI code must marshal to its own thread.
/// </summary>
public sealed class MirrorController : IDisposable
{
    private const int MaxConsecutiveFailures = 5;
    private const int MaxRapidFailures = 3;
    private const int RetryDelayMilliseconds = 2000;
    private static readonly TimeSpan HealthyAfter = TimeSpan.FromSeconds(30);

    private readonly DeviceEnumerator _devices;
    private readonly object _gate = new();
    private readonly HashSet<string> _selected = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DeviceSettings> _settings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _names = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _blockedUntil = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _failureCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _addedAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _lastErrors = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<AudioDeviceInfo> _lastDevices = Array.Empty<AudioDeviceInfo>();
    private readonly ConcurrentDictionary<string, string> _faultReasons = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Threading.Timer _timer;
    private MirrorEngine? _engine;
    private volatile bool _restartNeeded;
    private volatile bool _disposed;
    private int _devicesChanged;
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
            _blockedUntil.Remove(deviceId);
            _failureCounts.Remove(deviceId);
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
            var devices = _lastDevices;
            var running = (_engine?.Outputs.Where(o => !o.IsFaulted).Select(o => o.DeviceId) ?? Enumerable.Empty<string>())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var now = DateTime.UtcNow;
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
                else if (device is { IsDefault: true, Status: DeviceStatus.Active })
                {
                    state = "Plays natively (Windows default)";
                }
                else if (_blockedUntil.TryGetValue(id, out var until))
                {
                    state = until == DateTime.MaxValue
                        ? "Paused after repeated errors"
                        : until > now ? "Recovering after an error" : "Starting";
                }
                else if (device is null || device.Status != DeviceStatus.Active)
                {
                    state = "Waiting for device";
                }
                else
                {
                    state = "Starting";
                }

                _lastErrors.TryGetValue(id, out var detail);
                result.Add(new MirrorTargetStatus(id, name, state, detail));
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

    // The three handlers below are deliberately lock-free: they can run on audio threads
    // that Dispose may be waiting on. They only record what happened and ask for a reconcile.
    private void OnDevicesChanged(object? sender, EventArgs e)
    {
        Interlocked.Exchange(ref _devicesChanged, 1);
        Schedule(100);
    }

    private void OnCaptureStopped(object? sender, Exception? error)
    {
        _restartNeeded = true;
        Raise($"Capture stopped ({error?.Message ?? "device changed"}); restarting.");
        Schedule(500);
    }

    private void OnChannelFaulted(object? sender, Exception error)
    {
        if (sender is OutputChannel channel)
        {
            _faultReasons[channel.DeviceId] = error.Message;
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

            var retryMilliseconds = 0;

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
                    retryMilliseconds = 1500;
                }
                else
                {
                    messages.Add("Giving up after repeated failures. Change a device or re-select targets to retry.");
                }
            }

            var blockRetry = NextBlockRetryDelay();
            if (blockRetry > 0 && (retryMilliseconds == 0 || blockRetry < retryMilliseconds))
            {
                retryMilliseconds = blockRetry;
            }

            if (retryMilliseconds > 0)
            {
                Schedule(retryMilliseconds);
            }
        }

        foreach (var text in messages)
        {
            Raise(text);
        }
    }

    private int NextBlockRetryDelay()
    {
        var next = _blockedUntil.Values
            .Where(v => v != DateTime.MaxValue)
            .DefaultIfEmpty(DateTime.MaxValue)
            .Min();

        if (next == DateTime.MaxValue)
        {
            return 0;
        }

        return Math.Max(100, (int)(next - DateTime.UtcNow).TotalMilliseconds + 50);
    }

    private void RecordFailure(string deviceId, string name, DateTime now, List<string> messages, string? reason)
    {
        var healthy = _addedAt.TryGetValue(deviceId, out var added) && now - added > HealthyAfter;
        _failureCounts.TryGetValue(deviceId, out var previous);
        var count = (healthy ? 0 : previous) + 1;

        _failureCounts[deviceId] = count;
        _lastErrors[deviceId] = string.IsNullOrWhiteSpace(reason) ? "Windows stopped the output" : reason;
        _addedAt.Remove(deviceId);

        var why = string.IsNullOrWhiteSpace(reason) ? "" : $" ({reason})";
        if (count <= MaxRapidFailures)
        {
            _blockedUntil[deviceId] = now.AddMilliseconds(RetryDelayMilliseconds);
            messages.Add($"{name} stopped{why}. Retrying shortly.");
        }
        else
        {
            _blockedUntil[deviceId] = DateTime.MaxValue;
            messages.Add($"{name} keeps failing{why}. Paused until devices change or you re-select it.");
        }
    }

    private void ReconcileLocked(List<string> messages)
    {
        var now = DateTime.UtcNow;

        if (Interlocked.Exchange(ref _devicesChanged, 0) == 1)
        {
            // A device event is the cue to give paused devices one more chance.
            foreach (var id in _blockedUntil.Where(p => p.Value == DateTime.MaxValue).Select(p => p.Key).ToList())
            {
                _blockedUntil.Remove(id);
                _failureCounts[id] = MaxRapidFailures - 1;
            }
        }

        var scanTimer = Stopwatch.StartNew();
        var devices = _devices.GetActiveRenderDevices();
        _lastDevices = devices;
        if (scanTimer.ElapsedMilliseconds > 750)
        {
            messages.Add($"Device scan took {scanTimer.ElapsedMilliseconds} ms.");
        }
        foreach (var device in devices)
        {
            _names[device.Id] = device.Name;
        }

        if (_engine is not null)
        {
            foreach (var dead in _engine.Outputs.Where(o => o.IsFaulted).ToList())
            {
                try
                {
                    _engine.RemoveOutput(dead.DeviceId);
                }
                catch
                {
                    // the device is already gone; nothing more to release
                }

                _faultReasons.TryRemove(dead.DeviceId, out var reason);
                RecordFailure(dead.DeviceId, dead.Name, now, messages, reason);
            }
        }

        foreach (var id in _blockedUntil.Where(p => p.Value <= now).Select(p => p.Key).ToList())
        {
            _blockedUntil.Remove(id);
        }

        var forceRestart = _restartNeeded || _engine is null;
        var wanted = _selected.Where(id => !_blockedUntil.ContainsKey(id)).ToList();
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
            _addedAt.Clear();
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
                _addedAt.Remove(id);
                messages.Add($"Stopped mirroring to {NameOf(id)}.");
            }
        }

        foreach (var id in plan.Add)
        {
            var timer = Stopwatch.StartNew();
            try
            {
                var channel = engine.AddOutput(id, NameOf(id));
                Apply(channel, GetOrCreate(id));
                channel.Faulted += OnChannelFaulted;
                _addedAt[id] = DateTime.UtcNow;
                _lastErrors.Remove(id);
                messages.Add($"Mirroring to {channel.Name} (opened in {timer.ElapsedMilliseconds} ms).");
            }
            catch (Exception ex)
            {
                RecordFailure(id, NameOf(id), DateTime.UtcNow, messages, ex.Message);
            }
        }
    }
}
