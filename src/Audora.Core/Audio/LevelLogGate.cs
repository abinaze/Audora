namespace Audora.Core.Audio;

/// <summary>
/// Keeps the level-check log short: a line is written when the state changes between silence and sound,
/// and otherwise only once in a while as a heartbeat.
/// </summary>
public sealed class LevelLogGate
{
    public static readonly TimeSpan Heartbeat = TimeSpan.FromMinutes(5);

    private bool? _lastSilent;
    private DateTime _lastLoggedUtc = DateTime.MinValue;

    public void Reset()
    {
        _lastSilent = null;
        _lastLoggedUtc = DateTime.MinValue;
    }

    public bool ShouldLog(CaptureLevel level, DateTime nowUtc)
    {
        var changed = _lastSilent != level.IsSilent;
        var due = nowUtc - _lastLoggedUtc >= Heartbeat;
        if (!changed && !due)
        {
            return false;
        }

        _lastSilent = level.IsSilent;
        _lastLoggedUtc = nowUtc;
        return true;
    }
}
