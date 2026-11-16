using Audora.Core.Settings;

namespace Audora.Tests;

public class NoiseSettingsTests
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

    [Fact]
    public void ASavedDeviceStartsWithNoiseRemovalOffAtFullStrength()
    {
        var device = new SavedDevice();

        Assert.False(device.NoiseRemoval);
        Assert.Equal(100, device.NoiseStrength);
    }

    [Fact]
    public void NoiseRemovalSurvivesSavingAndLoading()
    {
        var file = TempFile();
        try
        {
            var store = new SettingsStore(file);
            var settings = new AppSettings();
            settings.Devices.Add(new SavedDevice { Id = "a", Name = "Headphones", NoiseRemoval = true, NoiseStrength = 65 });

            store.Save(store.Serialize(settings));
            var loaded = store.Load();

            Assert.True(loaded.Devices[0].NoiseRemoval);
            Assert.Equal(65, loaded.Devices[0].NoiseStrength);
        }
        finally
        {
            Cleanup(file);
        }
    }

    [Fact]
    public void AnOlderSettingsFileWithoutNoiseRemovalStillLoads()
    {
        var file = TempFile();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "{ \"Devices\": [ { \"Id\": \"a\", \"Name\": \"Speaker\", \"Volume\": 120 } ] }");

            var loaded = new SettingsStore(file).Load();

            Assert.Single(loaded.Devices);
            Assert.Equal(120, loaded.Devices[0].Volume);
            Assert.False(loaded.Devices[0].NoiseRemoval);
            Assert.Equal(100, loaded.Devices[0].NoiseStrength);
        }
        finally
        {
            Cleanup(file);
        }
    }
}
