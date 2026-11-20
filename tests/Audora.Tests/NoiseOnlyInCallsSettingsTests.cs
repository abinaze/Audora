using Audora.Core.Settings;

namespace Audora.Tests;

public class NoiseOnlyInCallsSettingsTests
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
    public void ADeviceCleansOnlyDuringCallsUnlessToldOtherwise()
    {
        Assert.True(new SavedDevice().NoiseOnlyInCalls);
    }

    [Fact]
    public void AnOlderSettingsFileKeepsTheCallsOnlyDefault()
    {
        var file = TempFile();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "{ \"Devices\": [ { \"Id\": \"a\", \"Name\": \"Speaker\", \"NoiseRemoval\": true } ] }");

            var loaded = new SettingsStore(file).Load();

            Assert.True(loaded.Devices[0].NoiseRemoval);
            Assert.True(loaded.Devices[0].NoiseOnlyInCalls);
        }
        finally
        {
            Cleanup(file);
        }
    }

    [Fact]
    public void SwitchingCallsOnlyOffIsRemembered()
    {
        var file = TempFile();
        try
        {
            var store = new SettingsStore(file);
            var settings = new AppSettings();
            settings.Devices.Add(new SavedDevice { Id = "a", Name = "Speaker", NoiseRemoval = true, NoiseOnlyInCalls = false });

            store.Save(store.Serialize(settings));
            var loaded = store.Load();

            Assert.False(loaded.Devices[0].NoiseOnlyInCalls);
        }
        finally
        {
            Cleanup(file);
        }
    }
}
