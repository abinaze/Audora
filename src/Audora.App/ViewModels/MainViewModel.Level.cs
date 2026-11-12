using Audora.App.Infrastructure;
using Audora.Core.Audio;

namespace Audora.App.ViewModels;

public sealed partial class MainViewModel
{
    private static readonly TimeSpan LevelLogEvery = TimeSpan.FromSeconds(15);
    private System.Threading.Timer? _levelLog;
    private readonly LevelLogGate _levelGate = new();

    /// <summary>While Audora is on, writes one line every 15 seconds saying whether it heard sound.</summary>
    private void StartLevelLog()
    {
        StopLevelLog();
        _levelGate.Reset();
        _levelLog = new System.Threading.Timer(_ => LogLevel(), null, LevelLogEvery, LevelLogEvery);
    }

    private void StopLevelLog()
    {
        _levelLog?.Dispose();
        _levelLog = null;
    }

    private void LogLevel()
    {
        try
        {
            var level = _controller.TakeCaptureLevel();
            if (level is not null)
            {
                if (_levelGate.ShouldLog(level, DateTime.UtcNow))
                {
                    Log.Info(level.Describe());
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("Level check failed", ex);
        }
    }
}
