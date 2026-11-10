using Audora.Core.Diagnostics;

namespace Audora.Tests;

public class SoundCheckGuardTests
{
    private static DeviceVolumeState Volume(float level, bool muted = false) => new(level, muted);

    [Fact]
    public void UnreadableVolumeStillPlaysBecauseTheToneIsQuiet()
    {
        Assert.Equal(GuardDecision.Play, SoundCheckGuard.Decide(null, false));
    }

    [Fact]
    public void MutedDeviceNeverPlays()
    {
        Assert.Equal(GuardDecision.Muted, SoundCheckGuard.Decide(Volume(0.3f, muted: true), true));
    }

    [Fact]
    public void LowVolumePlays()
    {
        Assert.Equal(GuardDecision.Play, SoundCheckGuard.Decide(Volume(0.4f), false));
        Assert.Equal(GuardDecision.Play, SoundCheckGuard.Decide(Volume(0.5f), false));
    }

    [Fact]
    public void ModeratelyHighVolumeIsHeldBackUntilThePersonSaysSo()
    {
        Assert.Equal(GuardDecision.Loud, SoundCheckGuard.Decide(Volume(0.56f), false));
        Assert.Equal(GuardDecision.Play, SoundCheckGuard.Decide(Volume(0.56f), true));
    }

    [Fact]
    public void VeryHighVolumeNeverPlaysEvenWhenAllowed()
    {
        Assert.Equal(GuardDecision.TooLoud, SoundCheckGuard.Decide(Volume(0.9f), true));
        Assert.Equal(GuardDecision.TooLoud, SoundCheckGuard.Decide(Volume(1.0f), false));
    }

    [Fact]
    public void ExactlyAtTheHardLimitIsStillAllowedWhenAsked()
    {
        Assert.Equal(GuardDecision.Play, SoundCheckGuard.Decide(Volume(0.8f), true));
    }
}
