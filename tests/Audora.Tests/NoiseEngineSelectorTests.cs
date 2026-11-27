using Audora.Core.Cleanup;

namespace Audora.Tests;

public class NoiseEngineSelectorTests
{
    private sealed class FakeDenoiser : IFrameDenoiser
    {
        public FakeDenoiser(string name) => Name = name;

        public string Name { get; }

        public int FrameSize => 480;

        public float Process(float[] input, float[] output)
        {
            Array.Copy(input, output, FrameSize);
            return 0f;
        }

        public void Dispose()
        {
        }
    }

    [Theory]
    [InlineData(null, "Studio")]
    [InlineData("", "Studio")]
    [InlineData("something else", "Studio")]
    [InlineData("Studio", "Studio")]
    [InlineData("studio", "Studio")]
    [InlineData("Standard", "Standard")]
    [InlineData("  STANDARD ", "Standard")]
    public void ASavedEngineNameIsReadSafely(string? saved, string expected)
    {
        Assert.Equal(expected, NoiseEngines.Normalize(saved));
    }

    [Fact]
    public void WhenStandardIsWantedStudioIsNeverTouched()
    {
        var studioCalls = 0;
        var selector = new NoiseEngineSelector(
            NoiseEngines.Standard,
            () =>
            {
                studioCalls++;
                return new FakeDenoiser("studio");
            },
            () => new FakeDenoiser("standard"));

        var denoiser = (FakeDenoiser)selector.Create();

        Assert.Equal("standard", denoiser.Name);
        Assert.Equal(0, studioCalls);
        Assert.Equal(NoiseEngines.Standard, selector.ActiveEngine);
        Assert.Null(selector.FallbackReason);
    }

    [Fact]
    public void StudioIsUsedWhenItWorks()
    {
        var selector = new NoiseEngineSelector(NoiseEngines.Studio, () => new FakeDenoiser("studio"), () => new FakeDenoiser("standard"));

        var denoiser = (FakeDenoiser)selector.Create();

        Assert.Equal("studio", denoiser.Name);
        Assert.Equal(NoiseEngines.Studio, selector.ActiveEngine);
        Assert.Null(selector.FallbackReason);
    }

    [Fact]
    public void WhenStudioFailsToStartStandardTakesOverAndStaysThere()
    {
        var studioCalls = 0;
        var selector = new NoiseEngineSelector(
            NoiseEngines.Studio,
            () =>
            {
                studioCalls++;
                throw new InvalidOperationException("The DeepFilterNet model could not be loaded.");
            },
            () => new FakeDenoiser("standard"));

        var first = (FakeDenoiser)selector.Create();
        var second = (FakeDenoiser)selector.Create();

        Assert.Equal("standard", first.Name);
        Assert.Equal("standard", second.Name);
        Assert.Equal(1, studioCalls);
        Assert.Equal(NoiseEngines.Standard, selector.ActiveEngine);
        Assert.Contains("model could not be loaded", selector.FallbackReason);
        Assert.Contains("Standard", selector.FallbackReason);
    }

    [Fact]
    public void MissingStudioFilesGiveTheReasonThatWasPassedIn()
    {
        var selector = new NoiseEngineSelector(NoiseEngines.Studio, null, () => new FakeDenoiser("standard"), "The files are not here.");

        var denoiser = (FakeDenoiser)selector.Create();

        Assert.Equal("standard", denoiser.Name);
        Assert.Equal(NoiseEngines.Standard, selector.ActiveEngine);
        Assert.Equal("The files are not here.", selector.FallbackReason);
    }

    [Fact]
    public void MissingStudioFilesWithoutAReasonStillExplainThemselves()
    {
        var selector = new NoiseEngineSelector(NoiseEngines.Studio, null, () => new FakeDenoiser("standard"));

        Assert.Contains("Studio files", selector.FallbackReason);
    }

    [Fact]
    public void AFailureAfterStudioHasStartedIsNotHiddenByAMixedPair()
    {
        var calls = 0;
        var selector = new NoiseEngineSelector(
            NoiseEngines.Studio,
            () =>
            {
                calls++;
                if (calls > 1)
                {
                    throw new InvalidOperationException("broken");
                }

                return new FakeDenoiser("studio");
            },
            () => new FakeDenoiser("standard"));

        selector.Create();

        Assert.Throws<InvalidOperationException>(() => selector.Create());
        Assert.Equal(NoiseEngines.Studio, selector.ActiveEngine);
        Assert.Null(selector.FallbackReason);
    }

    [Fact]
    public void StudioFilesAreFoundOnlyWhenBothAreThere()
    {
        var folder = Path.Combine(Path.GetTempPath(), "audora-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Null(StudioFiles.Locate(folder));
            Assert.Null(StudioFiles.Locate(null));

            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, StudioFiles.LibraryName), "x");
            Assert.Null(StudioFiles.Locate(folder));
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    [Fact]
    public void StudioFilesAreReturnedWhenBothAreThere()
    {
        var folder = Path.Combine(Path.GetTempPath(), "audora-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, StudioFiles.LibraryName), "x");
            File.WriteAllText(Path.Combine(folder, StudioFiles.ModelName), "x");

            var files = StudioFiles.Locate(folder);

            Assert.True(files.HasValue);
            Assert.Equal(Path.Combine(folder, "deepfilter.dll"), files.Value.Library);
            Assert.Equal(Path.Combine(folder, "DeepFilterNet3_onnx.tar.gz"), files.Value.Model);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    [Fact]
    public void TheStudioFolderSitsBesideTheApp()
    {
        Assert.Equal(Path.Combine("app", "studio"), StudioFiles.FolderIn("app"));
    }

    [Fact]
    public void WithoutStudioFilesAMicrophoneUsesRnNoiseAndSaysWhy()
    {
        var empty = Path.Combine(Path.GetTempPath(), "audora-test-" + Guid.NewGuid().ToString("N"));
        var selector = NoiseEngineSelector.ForMicrophone(NoiseEngines.Studio, empty);

        using var denoiser = selector.Create();

        Assert.IsType<RnNoiseDenoiser>(denoiser);
        Assert.Equal(NoiseEngines.Standard, selector.ActiveEngine);
        Assert.Contains("not installed", selector.FallbackReason);
    }

    [Fact]
    public void StudioIsDescribedWithItsCost()
    {
        var text = NoiseEngines.Describe("Studio", null);

        Assert.Contains("DeepFilterNet3", text);
        Assert.Contains("40 ms", text);
    }

    [Fact]
    public void StandardIsDescribedAsLight()
    {
        var text = NoiseEngines.Describe("Standard", null);

        Assert.Contains("RNNoise", text);
        Assert.Contains("20 ms", text);
    }

    [Fact]
    public void AFallbackReasonReplacesTheDescription()
    {
        Assert.Equal("The files are not here.", NoiseEngines.Describe("Studio", "The files are not here."));
    }
}
