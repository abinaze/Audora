namespace Audora.Core.Audio;

public sealed partial class OutputChannel
{
    /// <summary>Extra delay for this device in milliseconds (0..2000), used to line it up with slower devices.</summary>
    public int DelayMilliseconds
    {
        get => _delay.DelayMilliseconds;
        set => _delay.DelayMilliseconds = value;
    }
}
