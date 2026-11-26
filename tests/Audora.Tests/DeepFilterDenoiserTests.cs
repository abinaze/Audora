using Audora.Core.Cleanup;

namespace Audora.Tests;

public class DeepFilterDenoiserTests
{
    [Fact]
    public void AMissingLibraryIsReportedClearly()
    {
        var missing = Path.Combine(Path.GetTempPath(), "audora-no-such-" + Guid.NewGuid().ToString("N") + ".dll");

        var error = Assert.Throws<FileNotFoundException>(() => DeepFilterDenoiser.Create(missing, missing));

        Assert.Contains("library", error.Message);
    }

    [Fact]
    public void AMissingModelIsReportedBeforeAnythingIsLoaded()
    {
        var library = Path.GetTempFileName();
        try
        {
            var missingModel = Path.Combine(Path.GetTempPath(), "audora-no-such-" + Guid.NewGuid().ToString("N") + ".tar.gz");

            var error = Assert.Throws<FileNotFoundException>(() => DeepFilterDenoiser.Create(library, missingModel));

            Assert.Contains("model", error.Message);
        }
        finally
        {
            File.Delete(library);
        }
    }

    [Fact]
    public void DeepFilterNetSaysHowMuchSoundItHoldsBack()
    {
        var total = DeepFilterDenoiser.Frame + DeepFilterDenoiser.AlgorithmicDelay;

        Assert.Equal(1440, DeepFilterDenoiser.AlgorithmicDelay);
        Assert.Equal(1920, total);
        Assert.Equal(40.0, total / 48.0, 6);
    }

    [Fact]
    public void KeepInRangeFixesBrokenSamplesAndLeavesGoodOnesAlone()
    {
        var samples = new[] { 0.25f, -0.5f, float.NaN, float.PositiveInfinity, float.NegativeInfinity, 1.5f, -2f };

        DeepFilterDenoiser.KeepInRange(samples, samples);

        Assert.Equal(new[] { 0.25f, -0.5f, 0f, 0f, 0f, 1f, -1f }, samples);
    }
}
