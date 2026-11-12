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
            "Matches the test. If the sound still seems wrong, the cause lies after Windows: the driver, the Bluetooth link or the headphones.",
        ChannelVerdict.MonoMix =>
            "Both sides measured the same, so left and right are being mixed into mono. The Windows Mono audio setting, a driver or a sound enhancement can cause this. Check Settings, Accessibility, Audio, and the device's Audio enhancements. Audora does not change these.",
        ChannelVerdict.LeaksToOtherSide =>
            "Sound also appeared on the other side, so left and right are being mixed before the device: an app, a Windows setting, or Audora's sound profile. Other audio on this device, including copies made by Audora, also counts.",
        ChannelVerdict.ExpectedSideSilent =>
            "No sound was measured on the expected side.",
        ChannelVerdict.Unbalanced =>
            "The two sides measured clearly different levels.",
        _ =>
            "No signal was measured during the tone. The device may be muted, in use by another app, or not receiving sound."
    };
}
