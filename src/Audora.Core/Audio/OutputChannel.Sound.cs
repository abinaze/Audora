namespace Audora.Core.Audio;

public sealed partial class OutputChannel
{
    /// <summary>Applies the equalizer and tone settings to this device.</summary>
    public void ApplySound(EqSettings settings) => _eq.Apply(settings);
}
