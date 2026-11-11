using Audora.Core.Diagnostics;

namespace Audora.Tests;

public class ChannelCheckMonoTests
{
    [Fact]
    public void OneSidedToneThatMeasuresTheSameOnBothSidesIsAMonoMix()
    {
        Assert.Equal(ChannelVerdict.MonoMix, ChannelCheck.Judge(ToneChannel.Left, 0.062f, 0.062f));
        Assert.Equal(ChannelVerdict.MonoMix, ChannelCheck.Judge(ToneChannel.Right, 0.062f, 0.062f));
        Assert.Equal(ChannelVerdict.MonoMix, ChannelCheck.Judge(ToneChannel.Left, 0.09f, 0.085f));
    }

    [Fact]
    public void ALeakAFairlyCleanSideAndABothSidesToneAreNotMixedUp()
    {
        Assert.Equal(ChannelVerdict.LeaksToOtherSide, ChannelCheck.Judge(ToneChannel.Left, 0.09f, 0.05f));
        Assert.Equal(ChannelVerdict.Matches, ChannelCheck.Judge(ToneChannel.Left, 0.09f, 0f));
        Assert.Equal(ChannelVerdict.Matches, ChannelCheck.Judge(ToneChannel.Both, 0.124f, 0.124f));
    }
}
