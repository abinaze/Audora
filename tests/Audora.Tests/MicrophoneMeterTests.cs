using Audora.Core.Audio;

namespace Audora.Tests;

public class MicrophoneMeterTests
{
    [Fact]
    public void TheBarIsEmptyAtTheFloorAndFullAtZeroDecibels()
    {
        Assert.Equal(0.0, MicrophoneMeter.Bar(0f), 5);
        Assert.Equal(0.0, MicrophoneMeter.Bar(0.001f), 3);
        Assert.Equal(1.0, MicrophoneMeter.Bar(1f), 5);
        Assert.Equal(1.0, MicrophoneMeter.Bar(2f), 5);
    }

    [Fact]
    public void MinusThirtyDecibelsIsHalfWay()
    {
        Assert.Equal(0.5, MicrophoneMeter.Bar(0.03162278f), 3);
    }

    [Fact]
    public void BadNumbersGiveAnEmptyBar()
    {
        Assert.Equal(0.0, MicrophoneMeter.Bar(float.NaN), 5);
        Assert.Equal(0.0, MicrophoneMeter.Bar(-1f), 5);
    }

    [Fact]
    public void SilenceReadsMinusOneHundredTwentyDecibels()
    {
        Assert.Equal(-120.0, MicrophoneMeter.Db(0f), 3);
        Assert.Equal(-120.0, MicrophoneMeter.Db(float.NaN), 3);
    }

    [Fact]
    public void ItSaysHowMuchQuieterTheCleanedSoundIs()
    {
        Assert.Equal("Quieter by 20.0 dB.", MicrophoneMeter.ChangeText(0.1f, 0.01f));
    }

    [Fact]
    public void ItSaysWhenNothingWasRemoved()
    {
        Assert.Equal("Nothing was removed.", MicrophoneMeter.ChangeText(0.1f, 0.1f));
        Assert.Equal("Nothing was removed.", MicrophoneMeter.ChangeText(0.1f, 0.2f));
    }

    [Fact]
    public void ItSaysWhenTheRoomIsTooQuietToCompare()
    {
        Assert.Equal("Too quiet to compare.", MicrophoneMeter.ChangeText(0.0001f, 0.00001f));
    }
}
