namespace Audora.Core.Diagnostics;

public enum ChannelVerdict
{
    Matches,
    LeaksToOtherSide,
    ExpectedSideSilent,
    Unbalanced,
    NoSignal
}

/// <summary>
/// Judges the left and right peak levels Windows measured at a device while a test tone played.
/// This is the digital signal at the device. It cannot see the driver, the Bluetooth link or the speaker after it.
/// </summary>
public static class ChannelCheck
{
    /// <summary>Below this a side counts as silent (about -54 dB).</summary>
    public const float Silent = 0.002f;

    /// <summary>The other side may be this fraction of the playing side before it counts as a leak (about -20 dB).</summary>
    public const float LeakRatio = 0.1f;

    /// <summary>Two sides playing the same tone may differ by this much before it counts as unbalanced (about -6 dB).</summary>
    public const float BalanceRatio = 0.5f;

    public static ChannelVerdict Judge(ToneChannel expected, float left, float right)
    {
        if (Math.Max(left, right) < Silent)
        {
            return ChannelVerdict.NoSignal;
        }

        switch (expected)
        {
            case ToneChannel.Left:
                if (left < Silent)
                {
                    return ChannelVerdict.ExpectedSideSilent;
                }

                return right > left * LeakRatio ? ChannelVerdict.LeaksToOtherSide : ChannelVerdict.Matches;

            case ToneChannel.Right:
                if (right < Silent)
                {
                    return ChannelVerdict.ExpectedSideSilent;
                }

                return left > right * LeakRatio ? ChannelVerdict.LeaksToOtherSide : ChannelVerdict.Matches;

            default:
                if (left < Silent || right < Silent)
                {
                    return ChannelVerdict.ExpectedSideSilent;
                }

                return Math.Min(left, right) < Math.Max(left, right) * BalanceRatio
                    ? ChannelVerdict.Unbalanced
                    : ChannelVerdict.Matches;
        }
    }

    public static string Describe(ChannelVerdict verdict) => verdict switch
    {
        ChannelVerdict.Matches =>
            "That matches the test. If you still hear it wrongly, the cause is after this point: the driver, the Bluetooth link or the headphones.",
        ChannelVerdict.LeaksToOtherSide =>
            "Windows already showed sound on the other side too. Something before the device is mixing left and right: an app, a Windows setting such as Mono audio, or Audora's own sound profile on this device. Other sound playing on the device, including copies made by Audora, also counts.",
        ChannelVerdict.ExpectedSideSilent =>
            "Windows showed no sound on the side that should play.",
        ChannelVerdict.Unbalanced =>
            "Windows showed clearly different levels on the two sides.",
        _ =>
            "Windows showed no signal on this device during the tone. The device may be muted, in use by another app, or not receiving sound."
    };
}
