namespace Audora.Core.Audio;

public sealed partial class MirrorController
{
    /// <summary>
    /// What the engine has heard since the last call. Read only. Returns null while the engine is off.
    /// </summary>
    public CaptureLevel? TakeCaptureLevel()
    {
        lock (_gate)
        {
            var engine = _engine;
            if (!_running || engine is null)
            {
                return null;
            }

            return new CaptureLevel(
                engine.SourceName,
                engine.TakePeakLevel(),
                engine.Outputs.Count(o => !o.IsFaulted));
        }
    }
}
