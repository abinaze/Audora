namespace Audora.Core.Audio;

public sealed partial class MirrorController
{
    private EqSettings _sound = EqSettings.Original;

    /// <summary>Sets the equalizer and tone for every device Audora plays on, now and when devices return.</summary>
    public void SetSound(EqSettings settings)
    {
        lock (_gate)
        {
            _sound = settings;

            if (_engine is null)
            {
                return;
            }

            foreach (var output in _engine.Outputs)
            {
                output.ApplySound(settings);
            }
        }
    }
}
