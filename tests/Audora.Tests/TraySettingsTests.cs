using Audora.Core.Settings;

namespace Audora.Tests;

public class TraySettingsTests
{
    [Fact]
    public void ByDefault_ClosingKeepsAudoraRunningInTheTray()
    {
        var settings = new AppSettings();

        Assert.True(settings.CloseToTray);
        Assert.False(settings.MinimizeToTray);
        Assert.False(settings.TrayHintShown);
    }

    [Fact]
    public void TraySettings_AreSavedAndLoadedBack()
    {
        var file = Path.Combine(Path.GetTempPath(), "audora-test-" + Guid.NewGuid().ToString("N"), "settings.json");
        try
        {
            var store = new SettingsStore(file);
            store.Save(store.Serialize(new AppSettings { CloseToTray = false, MinimizeToTray = true, TrayHintShown = true }));

            var loaded = store.Load();

            Assert.False(loaded.CloseToTray);
            Assert.True(loaded.MinimizeToTray);
            Assert.True(loaded.TrayHintShown);
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
}
