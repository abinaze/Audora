using Audora.Core.Settings;

namespace Audora.Tests;

public class SettingsTests
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
    public void MissingFile_GivesDefaults()
    {
        var file = TempFile();
        var settings = new SettingsStore(file).Load();

        Assert.True(settings.DarkTheme);
        Assert.Empty(settings.Groups);
        Assert.Equal(10, settings.SharedSound.Bands.Length);
    }

    [Fact]
    public void SavedSettings_AreLoadedBackUnchanged()
    {
        var file = TempFile();
        try
        {
            var store = new SettingsStore(file);
            var settings = new AppSettings { DarkTheme = false, ActiveGroupId = "g1" };
            settings.Devices.Add(new SavedDevice { Id = "a", Name = "Headphones (MT)", Type = "Bluetooth", Volume = 140, Selected = true });
            settings.Groups.Add(new SavedGroup
            {
                Id = "g1",
                Name = "Living room",
                Volume = 80,
                Members = { new SavedMember { DeviceId = "a", Name = "Headphones (MT)" } }
            });

            store.Save(store.Serialize(settings));
            var loaded = store.Load();

            Assert.False(loaded.DarkTheme);
            Assert.Equal("g1", loaded.ActiveGroupId);
            Assert.Equal(140, loaded.Devices[0].Volume);
            Assert.True(loaded.Devices[0].Selected);
            Assert.Equal("Living room", loaded.Groups[0].Name);
            Assert.Single(loaded.Groups[0].Members);
        }
        finally
        {
            Cleanup(file);
        }
    }

    [Fact]
    public void DamagedFile_IsSetAsideAndDefaultsAreUsed()
    {
        var file = TempFile();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "{ this is not valid json");

            var settings = new SettingsStore(file).Load();

            Assert.Empty(settings.Devices);
            Assert.True(File.Exists(file + ".bad"));
        }
        finally
        {
            Cleanup(file);
        }
    }

    [Fact]
    public void WrongNumberOfBands_IsRepaired()
    {
        var file = TempFile();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "{\"SharedSound\":{\"Preset\":\"Custom\",\"Bands\":[1,2,3]}}");

            var settings = new SettingsStore(file).Load();

            Assert.Equal(10, settings.SharedSound.Bands.Length);
            Assert.Equal(3, settings.SharedSound.Bands[2]);
            Assert.Equal(0, settings.SharedSound.Bands[9]);
        }
        finally
        {
            Cleanup(file);
        }
    }

    [Fact]
    public void UsbPortNumber_DoesNotChangeTheDevice()
    {
        Assert.Equal(
            DeviceMatcher.Normalize("Speakers (AB13X USB Audio)"),
            DeviceMatcher.Normalize("Speakers (2- AB13X USB Audio)"));
    }

    [Fact]
    public void DifferentDevices_StayDifferent()
    {
        Assert.NotEqual(
            DeviceMatcher.Normalize("Headphones (MT)"),
            DeviceMatcher.Normalize("Headphones (Chu2 DSP)"));
    }
}
