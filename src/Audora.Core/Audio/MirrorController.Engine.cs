namespace Audora.Core.Audio;

public sealed partial class MirrorController
{
    private bool _running;

    /// <summary>True while the engine is listening to devices and playing on them.</summary>
    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _running;
            }
        }
    }

    /// <summary>Starts watching devices and playing on the devices that are switched on.</summary>
    public void StartEngine()
    {
        lock (_gate)
        {
            if (_disposed || _running)
            {
                return;
            }

            _running = true;
        }

        _devices.DevicesChanged += OnDevicesChanged;
        Reconcile();
    }

    /// <summary>
    /// Stops everything: capture, every output and every timer, and stops reacting to device events.
    /// The devices you switched on are remembered for next time. Windows audio is not touched.
    /// </summary>
    public void StopEngine()
    {
        lock (_gate)
        {
            if (!_running)
            {
                return;
            }

            _running = false;
            _devices.DevicesChanged -= OnDevicesChanged;
            _restartNeeded = false;
            _blockedUntil.Clear();
            _failureCounts.Clear();
            _addedAt.Clear();
            _lastErrors.Clear();
            _faultReasons.Clear();
            DisposeEngine();

            try
            {
                _timer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            catch (ObjectDisposedException)
            {
                // already shutting down
            }
        }
    }
}
