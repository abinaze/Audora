using Audora.Core.Diagnostics;

namespace Audora.Tests;

public class LoopbackPeakTests
{
    private static byte[] Bytes(params float[] samples)
    {
        var bytes = new byte[samples.Length * sizeof(float)];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    [Fact]
    public void ThePeakIsTheLargestSampleWhateverItsSign()
    {
        var buffer = Bytes(0.1f, -0.4f, 0.3f);

        Assert.Equal(0.4, LoopbackPeakMeter.PeakOf(buffer, buffer.Length), 3);
    }

    [Fact]
    public void SilenceHasNoPeak()
    {
        var buffer = Bytes(0f, 0f, 0f, 0f);

        Assert.Equal(0.0, LoopbackPeakMeter.PeakOf(buffer, buffer.Length), 3);
    }

    [Fact]
    public void OnlyTheBytesThatWereRecordedCount()
    {
        var buffer = Bytes(0.2f, 0.9f, 0.9f);

        // two whole samples plus two stray bytes of a third one
        Assert.Equal(0.9, LoopbackPeakMeter.PeakOf(buffer, 10), 3);
        // only the first sample
        Assert.Equal(0.2, LoopbackPeakMeter.PeakOf(buffer, 4), 3);
        // nothing recorded
        Assert.Equal(0.0, LoopbackPeakMeter.PeakOf(buffer, 0), 3);
    }
}
