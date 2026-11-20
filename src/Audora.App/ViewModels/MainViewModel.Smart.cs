using System.Windows.Threading;
using Audora.App.Infrastructure;
using Audora.Core.Audio;

namespace Audora.App.ViewModels;

public sealed partial class MainViewModel
{
    private DispatcherTimer? _smartTimer;
    private string? _smartPending;
    private readonly CallGate _callGate = new();
    private bool _callActive;

    private void StartSmartWatcher()
    {
        _smartTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _smartTimer.Tick += (_, _) => SmartTick();
        _smartTimer.Start();
    }

    // Looks at the app in front only when Smart mode or call-only noise removal needs it. Smart mode switches only when
    // two checks in a row agree, so the sound never flickers between profiles. The call check works the same way.
    private void SmartTick()
    {
        var needCalls = !_disposed && _engineOn && Devices.Any(d => d.NeedsCallDetection);
        var smart = !_disposed && _engineOn && Sound.IsSmartOn;
        if (!needCalls && !smart)
        {
            _smartPending = null;
            _callGate.Reset();
            SetCallActive(false);
            return;
        }

        var info = ForegroundWatcher.Read();
        if (needCalls)
        {
            SetCallActive(_callGate.Observe(info is null ? null : CallGate.IsCall(info.Process, info.Title)));
        }
        else
        {
            _callGate.Reset();
            SetCallActive(false);
        }

        if (!smart)
        {
            _smartPending = null;
            return;
        }

        if (info is null)
        {
            return;
        }

        var choice = SmartRules.Choose(info.Process, info.Title, info.FullscreenGame);
        if (choice == _smartPending)
        {
            Sound.ApplySmart(choice);
        }

        _smartPending = choice;
    }

    private void SetCallActive(bool active)
    {
        if (_callActive == active)
        {
            return;
        }

        _callActive = active;
        Log.Info(active
            ? "A call app is in front: noise removal can clean the sound."
            : "No call app in front: noise removal waits.");
        foreach (var device in Devices)
        {
            device.SetCallActive(active);
        }
    }
}
