using Audora.Core.Diagnostics;

namespace Audora.Tests;

public class SoundCheckTestsTests
{
    [Fact]
    public void EveryTestHasAUniqueKeyAndAShortLength()
    {
        var keys = SoundCheckTests.All.Select(t => t.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.All(SoundCheckTests.All, t => Assert.InRange(t.Request.Seconds, 1.0, 10.0));
    }

    [Fact]
    public void FindIgnoresCaseAndReturnsNothingForUnknownKeys()
    {
        Assert.Equal(ToneChannel.Left, SoundCheckTests.Find("LEFT")!.Request.Channel);
        Assert.Null(SoundCheckTests.Find("nonsense"));
    }

    [Fact]
    public void SideTestsUseOneSideAndTheSweepUsesBoth()
    {
        Assert.Equal(ToneChannel.Left, SoundCheckTests.Find("left")!.Request.Channel);
        Assert.Equal(ToneChannel.Right, SoundCheckTests.Find("right")!.Request.Channel);
        Assert.Equal(ToneShape.Sweep, SoundCheckTests.Find("sweep")!.Request.Shape);
        Assert.Equal(ToneChannel.Both, SoundCheckTests.Find("sweep")!.Request.Channel);
    }
}
