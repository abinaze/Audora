namespace Audora.Core.Diagnostics;

public enum ChannelVerdict
{
    Matches,
    MonoMix,
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

    /// <summary>A one-sided tone that measures this similar on both sides (within about 1 dB) is a mono mix.</summary>
    public const float MonoRatio = 0.9f;

    public static ChannelVerdict Judge(ToneChannel expected, float left, float right)
    {
        if (Math.Max(left, right) < Silent)
        {
            return ChannelVerdict.NoSignal;
        }

        switch (expected)
        {
            case ToneChannel.Left:
                return JudgeOneSide(left, right);

            case ToneChannel.Right:
                return JudgeOneSide(right, left);

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

    private static ChannelVerdict JudgeOneSide(float playing, float other)
    {
        if (playing < Silent)
        {
            return ChannelVerdict.ExpectedSideSilent;
        }

        if (other >= Silent && Math.Min(playing, other) / Math.Max(playing, other) >= MonoRatio)
        {
            return ChannelVerdict.MonoMix;
        }

        return other > playing * LeakRatio ? ChannelVerdict.LeaksToOtherSide : ChannelVerdict.Matches;
    }

    public static string Describe(ChannelVerdict verdict) => verdict switch
    {
        ChannelVerdict.Matches =>
            "That matches the test. If you still hear it wrongly, the cause is after this point: the driver, the Bluetooth link or the headphones.",
        ChannelVerdict.MonoMix =>
            "Both sides measured the same level, so something is mixing left and right into one (mono). The Windows Mono audio setting does this, and so can a driver or sound enhancement. Check Windows Settings, Accessibility, Audio, Mono audio, and the device's Audio enhancements. Audora does not change these.",
        ChannelVerdict.LeaksToOtherSide =>
            "Windows already showed sound on the other side too. Something before the device is mixing left and right: an app, a Windows setting, or Audora's own sound profile on this device. Other sound playing on the device, including copies made by Audora, also counts.",
        ChannelVerdict.ExpectedSideSilent =>
            "Windows showed no sound on the side that should play.",
        ChannelVerdict.Unbalanced =>
            "Windows showed clearly different levels on the two sides.",
        _ =>
            "Windows showed no signal on this device during the tone. The device may be muted, in use by another app, or not receiving sound."
    };
}
