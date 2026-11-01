namespace Audora.Core.Audio;

public sealed partial class OutputChannel
{
    /// <summary>True once Windows has stopped this output because of an error (for example, the device was reset).</summary>
    public bool IsFaulted { get; private set; }
}
