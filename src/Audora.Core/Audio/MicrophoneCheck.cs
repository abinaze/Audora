namespace Audora.Core.Audio;

/// <summary>What a short listen to a microphone says about it.</summary>
public enum MicrophoneVerdict
{
    /// <summary>Only silence arrived.</summary>
    Silent,

    /// <summary>Some sound arrived, but very little.</summary>
    VeryQuiet,

    /// <summary>A normal level.</summary>
    Good,

    /// <summary>So loud that the sound is probably cut off.</summary>
    TooLoud,
}

/// <summary>
/// Judges the highest sample (0 to 1) heard from a microphone. All it can say is what arrived. A microphone that
/// sends only silence is often blocked by the Windows microphone privacy setting or muted, so the words never call
/// it broken.
/// </summary>
public static class MicrophoneCheck
{
    /// <summary>Below this the microphone counts as sending only silence.</summary>
    public const float SilentBelow = 0.0001f;

    /// <summary>Below this the sound is very quiet.</summary>
    public const float QuietBelow = 0.01f;

    /// <summary>From this level up the sound is probably cut off.</summary>
    public const float LoudFrom = 0.98f;

    public static MicrophoneVerdict Judge(float peak)
    {
        if (!float.IsFinite(peak) || peak < SilentBelow)
        {
            return MicrophoneVerdict.Silent;
        }

        if (peak < QuietBelow)
        {
            return MicrophoneVerdict.VeryQuiet;
        }

        return peak >= LoudFrom ? MicrophoneVerdict.TooLoud : MicrophoneVerdict.Good;
    }

    public static string Describe(MicrophoneVerdict verdict) => verdict switch
    {
        MicrophoneVerdict.Silent =>
            "Only silence arrived. The microphone may be muted, or blocked by the Windows microphone privacy setting.",
        MicrophoneVerdict.VeryQuiet =>
            "Very little sound arrived. Speak closer to the microphone or raise its level in Windows.",
        MicrophoneVerdict.TooLoud =>
            "The sound is very loud and may be cut off. Lower the microphone level in Windows.",
        _ => "The microphone level is fine.",
    };
}
