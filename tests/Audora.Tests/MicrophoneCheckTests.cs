using Audora.Core.Audio;

namespace Audora.Tests;

public class MicrophoneCheckTests
{
    [Fact]
    public void ZeroMeansOnlySilenceAndPointsAtPrivacy()
    {
        var verdict = MicrophoneCheck.Judge(0f);

        Assert.Equal(MicrophoneVerdict.Silent, verdict);
        Assert.Contains("privacy", MicrophoneCheck.Describe(verdict));
    }

    [Fact]
    public void ANumberThatIsNotValidCountsAsSilence()
    {
        Assert.Equal(MicrophoneVerdict.Silent, MicrophoneCheck.Judge(float.NaN));
        Assert.Equal(MicrophoneVerdict.Silent, MicrophoneCheck.Judge(float.PositiveInfinity));
    }

    [Fact]
    public void VeryLittleSoundIsVeryQuiet()
    {
        Assert.Equal(MicrophoneVerdict.VeryQuiet, MicrophoneCheck.Judge(0.005f));
    }

    [Fact]
    public void ANormalLevelIsGood()
    {
        Assert.Equal(MicrophoneVerdict.Good, MicrophoneCheck.Judge(0.2f));
    }

    [Fact]
    public void ANearFullLevelIsTooLoud()
    {
        Assert.Equal(MicrophoneVerdict.TooLoud, MicrophoneCheck.Judge(0.99f));
        Assert.Equal(MicrophoneVerdict.TooLoud, MicrophoneCheck.Judge(1f));
    }

    [Fact]
    public void TheEdgesBelongToTheHigherGroup()
    {
        Assert.Equal(MicrophoneVerdict.VeryQuiet, MicrophoneCheck.Judge(MicrophoneCheck.SilentBelow));
        Assert.Equal(MicrophoneVerdict.Good, MicrophoneCheck.Judge(MicrophoneCheck.QuietBelow));
        Assert.Equal(MicrophoneVerdict.TooLoud, MicrophoneCheck.Judge(MicrophoneCheck.LoudFrom));
    }

    [Fact]
    public void NoVerdictCallsTheMicrophoneBroken()
    {
        foreach (var verdict in Enum.GetValues<MicrophoneVerdict>())
        {
            Assert.DoesNotContain("broken", MicrophoneCheck.Describe(verdict));
        }
    }
}
