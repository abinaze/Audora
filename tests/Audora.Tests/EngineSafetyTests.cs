using Audora.Core.Devices;
using Audora.Core.Settings;

namespace Audora.Tests;

public class EngineSafetyTests
{
    [Theory]
    [InlineData(@"E:\github-projects\Audora\src\Audora.App\bin\Debug\net10.0-windows\Audora.exe", true)]
    [InlineData(@"E:\x\src\Audora.App\bin\Release\net10.0-windows\Audora.exe", true)]
    [InlineData(@"C:\Program Files\dotnet\dotnet.exe", true)]
    [InlineData("", true)]
    [InlineData(@"C:\Users\me\AppData\Local\Programs\Audora\Audora.exe", false)]
    [InlineData(@"D:\Tools\Audora-Portable\Audora\Audora.exe", false)]
    [InlineData(@"E:\github-projects\Audora\artifacts\publish\Audora.exe", false)]
    public void DevelopmentBuildsAreRecognised(string path, bool expected) =>
        Assert.Equal(expected, BuildLocation.IsDevelopmentBuild(path));

    private static AudioDeviceInfo Dev(string id, string name, bool isDefault = false) =>
        new(id, name, ConnectionType.Unknown, DeviceStatus.Active, isDefault);

    [Fact]
    public void RestorePoint_FindsTheDeviceById()
    {
        var point = new SavedRestorePoint { DefaultDeviceId = "a", DefaultDeviceName = "Speakers (Realtek(R) Audio)" };
        var found = AudioRestore.Resolve(point, new[] { Dev("b", "Headphones (MT)"), Dev("a", "Speakers (Realtek(R) Audio)") });
        Assert.Equal("a", found?.Id);
    }

    [Fact]
    public void RestorePoint_FallsBackToTheNameWhenTheIdChanged()
    {
        var point = new SavedRestorePoint { DefaultDeviceId = "old", DefaultDeviceName = "Speakers (AB13X USB Audio)" };
        var found = AudioRestore.Resolve(point, new[] { Dev("new", "Speakers (2- AB13X USB Audio)") });
        Assert.Equal("new", found?.Id);
    }

    [Fact]
    public void RestorePoint_IsNullWhenTheDeviceIsGone()
    {
        var point = new SavedRestorePoint { DefaultDeviceId = "a", DefaultDeviceName = "Speakers" };
        Assert.Null(AudioRestore.Resolve(point, new[] { Dev("b", "Headphones (MT)") }));
    }

    [Fact]
    public void EngineSettings_AreSavedAndLoadedBack()
    {
        var file = Path.Combine(Path.GetTempPath(), "audora-test-" + Guid.NewGuid().ToString("N"), "settings.json");
        try
        {
            var store = new SettingsStore(file);
            store.Save(store.Serialize(new AppSettings
            {
                StartEngineOn = true,
                RestorePoint = new SavedRestorePoint { DefaultDeviceId = "a", DefaultDeviceName = "Speakers", Volume = 40, Muted = true }
            }));

            var loaded = store.Load();

            Assert.True(loaded.StartEngineOn);
            Assert.Equal("Speakers", loaded.RestorePoint?.DefaultDeviceName);
            Assert.Equal(40, loaded.RestorePoint?.Volume);
            Assert.True(loaded.RestorePoint?.Muted);
        }
        finally
        {
            var dir = Path.GetDirectoryName(file)!;
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }

    [Fact]
    public void ByDefault_TheEngineDoesNotStartByItself() =>
        Assert.False(new AppSettings().StartEngineOn);
}
