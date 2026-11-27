using Audora.Core.Settings;

namespace Audora.Tests;

public class MicrophoneSettingsTests
{
    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), "audora-test-" + Guid.NewGuid().ToString("N"), "settings.json");

    private static void Cleanup(string file)
    {
        var dir = Path.GetDirectoryName(file)!;
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, true);
        }
    }

    private static AppSettings LoadJson(string json)
    {
        var file = TempFile();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, json);
            return new SettingsStore(file).Load();
        }
        finally
        {
            Cleanup(file);
        }
    }

    [Fact]
    public void AMicrophoneStartsWithNoiseRemovalOffAtFullStrength()
    {
        var microphone = new SavedMicrophone();

        Assert.False(microphone.NoiseRemoval);
        Assert.Equal(100, microphone.NoiseStrength);
    }

    [Fact]
    public void MicrophoneSettingsSurviveSavingAndLoading()
    {
        var file = TempFile();
        try
        {
            var store = new SettingsStore(file);
            var settings = new AppSettings();
            settings.Microphones.Add(new SavedMicrophone { Id = "m1", Name = "USB microphone", NoiseRemoval = true, NoiseStrength = 40 });

            store.Save(store.Serialize(settings));
            var loaded = store.Load();

            Assert.Single(loaded.Microphones);
            Assert.Equal("m1", loaded.Microphones[0].Id);
            Assert.True(loaded.Microphones[0].NoiseRemoval);
            Assert.Equal(40, loaded.Microphones[0].NoiseStrength);
        }
        finally
        {
            Cleanup(file);
        }
    }

    [Fact]
    public void AnOlderSettingsFileWithoutMicrophonesStillLoads()
    {
        var loaded = LoadJson("{ \"Devices\": [ { \"Id\": \"a\", \"Name\": \"Speaker\" } ] }");

        Assert.Single(loaded.Devices);
        Assert.NotNull(loaded.Microphones);
        Assert.Empty(loaded.Microphones);
    }

    [Fact]
    public void ANullMicrophoneListIsRepaired()
    {
        var loaded = LoadJson("{ \"Microphones\": null }");

        Assert.NotNull(loaded.Microphones);
        Assert.Empty(loaded.Microphones);
    }

    [Fact]
    public void AStrengthOutsideZeroToOneHundredIsPulledBack()
    {
        var loaded = LoadJson("{ \"Microphones\": [ { \"Id\": \"a\", \"NoiseStrength\": 250 }, { \"Id\": \"b\", \"NoiseStrength\": -5 } ] }");

        Assert.Equal(100, loaded.Microphones[0].NoiseStrength);
        Assert.Equal(0, loaded.Microphones[1].NoiseStrength);
    }

    [Fact]
    public void AMicrophoneStartsWithTheStudioEngine()
    {
        Assert.Equal("Studio", new SavedMicrophone().NoiseEngine);
    }

    [Fact]
    public void TheEngineChoiceSurvivesSavingAndLoading()
    {
        var file = TempFile();
        try
        {
            var store = new SettingsStore(file);
            var settings = new AppSettings();
            settings.Microphones.Add(new SavedMicrophone { Id = "m1", NoiseEngine = "Standard" });

            store.Save(store.Serialize(settings));
            var loaded = store.Load();

            Assert.Equal("Standard", loaded.Microphones[0].NoiseEngine);
        }
        finally
        {
            Cleanup(file);
        }
    }

    [Fact]
    public void AnOlderMicrophoneRecordWithoutAnEngineLoadsAsStudio()
    {
        var loaded = LoadJson("{ \"Microphones\": [ { \"Id\": \"a\", \"NoiseRemoval\": true, \"NoiseStrength\": 70 } ] }");

        Assert.Equal("Studio", loaded.Microphones[0].NoiseEngine);
        Assert.True(loaded.Microphones[0].NoiseRemoval);
        Assert.Equal(70, loaded.Microphones[0].NoiseStrength);
    }

    [Fact]
    public void ADamagedEngineNameIsRepairedAndAStandardOneIsKept()
    {
        var loaded = LoadJson("{ \"Microphones\": [ { \"Id\": \"a\", \"NoiseEngine\": \"banana\" }, { \"Id\": \"b\", \"NoiseEngine\": null }, { \"Id\": \"c\", \"NoiseEngine\": \"standard\" } ] }");

        Assert.Equal("Studio", loaded.Microphones[0].NoiseEngine);
        Assert.Equal("Studio", loaded.Microphones[1].NoiseEngine);
        Assert.Equal("Standard", loaded.Microphones[2].NoiseEngine);
    }
}
