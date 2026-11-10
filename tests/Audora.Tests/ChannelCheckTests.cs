using Audora.Core.Diagnostics;

namespace Audora.Tests;

public class ChannelCheckTests
{
    [Fact]
    public void NothingMeasuredIsNoSignal()
    {
        Assert.Equal(ChannelVerdict.NoSignal, ChannelCheck.Judge(ToneChannel.Left, 0f, 0f));
        Assert.Equal(ChannelVerdict.NoSignal, ChannelCheck.Judge(ToneChannel.Both, 0.001f, 0.001f));
    }

    [Fact]
    public void LeftToneOnTheLeftMatches()
    {
        Assert.Equal(ChannelVerdict.Matches, ChannelCheck.Judge(ToneChannel.Left, 0.09f, 0f));
    }

    [Fact]
    public void LeftToneAlsoOnTheRightIsALeakUnlessItIsTiny()
    {
        Assert.Equal(ChannelVerdict.LeaksToOtherSide, ChannelCheck.Judge(ToneChannel.Left, 0.09f, 0.05f));
        Assert.Equal(ChannelVerdict.Matches, ChannelCheck.Judge(ToneChannel.Left, 0.09f, 0.005f));
    }

    [Fact]
    public void RightToneIsJudgedTheSameWayFromTheOtherSide()
    {
        Assert.Equal(ChannelVerdict.Matches, ChannelCheck.Judge(ToneChannel.Right, 0f, 0.09f));
        Assert.Equal(ChannelVerdict.LeaksToOtherSide, ChannelCheck.Judge(ToneChannel.Right, 0.05f, 0.09f));
    }

    [Fact]
    public void SilenceOnTheExpectedSideIsReported()
    {
        Assert.Equal(ChannelVerdict.ExpectedSideSilent, ChannelCheck.Judge(ToneChannel.Left, 0f, 0.09f));
        Assert.Equal(ChannelVerdict.ExpectedSideSilent, ChannelCheck.Judge(ToneChannel.Right, 0.09f, 0f));
        Assert.Equal(ChannelVerdict.ExpectedSideSilent, ChannelCheck.Judge(ToneChannel.Both, 0.09f, 0f));
    }

    [Fact]
    public void BothSidesNeedSimilarLevels()
    {
        Assert.Equal(ChannelVerdict.Matches, ChannelCheck.Judge(ToneChannel.Both, 0.09f, 0.08f));
        Assert.Equal(ChannelVerdict.Unbalanced, ChannelCheck.Judge(ToneChannel.Both, 0.09f, 0.03f));
    }

    [Fact]
    public void EveryVerdictHasPlainWords()
    {
        foreach (var verdict in Enum.GetValues<ChannelVerdict>())
        {
            Assert.False(string.IsNullOrWhiteSpace(ChannelCheck.Describe(verdict)));
        }
    }
}
