using System.Windows.Threading;
using Audora.App.Infrastructure;
using Audora.Core.Audio;

namespace Audora.App.ViewModels;

public sealed partial class MainViewModel
{
    private DispatcherTimer? _smartTimer;
    private string? _smartPending;

    private void StartSmartWatcher()
    {
        _smartTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _smartTimer.Tick += (_, _) => SmartTick();
        _smartTimer.Start();
    }

    // Smart mode: switch only when two checks in a row agree, so the sound never flickers between profiles.
    private void SmartTick()
    {
        if (_disposed || !Sound.IsSmartOn)
        {
            _smartPending = null;
            return;
        }

        var info = ForegroundWatcher.Read();
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
}
