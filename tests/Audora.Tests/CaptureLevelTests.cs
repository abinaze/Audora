using Audora.Core.Audio;

namespace Audora.Tests;

public class CaptureLevelTests
{
    [Fact]
    public void VeryQuietCaptureCountsAsSilence()
    {
        Assert.True(new CaptureLevel("X", 0f, 1).IsSilent);
        Assert.True(new CaptureLevel("X", 0.0005f, 1).IsSilent);
        Assert.False(new CaptureLevel("X", 0.01f, 1).IsSilent);
    }

    [Fact]
    public void SilenceIsDescribedPlainly()
    {
        var text = new CaptureLevel("Headphones (MT)", 0f, 1).Describe();

        Assert.Contains("only silence", text);
        Assert.Contains("Headphones (MT)", text);
    }

    [Fact]
    public void SoundShowsThePeakAndTheOutputCount()
    {
        var text = new CaptureLevel("Headphones (MT)", 0.314f, 2).Describe();

        Assert.Contains("heard sound", text);
        Assert.Contains("peak 0.31", text);
        Assert.Contains("2 outputs", text);
    }
}
