namespace Audora.Core.Diagnostics;

public enum GuardDecision
{
    Play,
    Muted,
    Loud,
    TooLoud
}

/// <summary>
/// Decides whether a test tone may play, from the Windows volume of the device. It never changes the volume.
/// Above 50% the tone is held back unless the person says to play anyway. Above 80% it never plays.
/// </summary>
public static class SoundCheckGuard
{
    public const float LoudVolume = 0.5f;
    public const float HardLimit = 0.8f;

    public static GuardDecision Decide(DeviceVolumeState? state, bool allowLoud)
    {
        if (state is null)
        {
            return GuardDecision.Play; // the volume could not be read; the tone itself is quiet and capped
        }

        if (state.Muted)
        {
            return GuardDecision.Muted;
        }

        if (state.Volume > HardLimit)
        {
            return GuardDecision.TooLoud;
        }

        if (state.Volume > LoudVolume && !allowLoud)
        {
            return GuardDecision.Loud;
        }

        return GuardDecision.Play;
    }
}
