using Audora.Core.Cleanup;

namespace Audora.Tests;

public class StudioPinTests
{
    private static string? FindPins()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "third-party", "studio.sha256");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }

    [Fact]
    public void TheStudioFilesAreAllPinnedWithAValidChecksum()
    {
        var path = FindPins();
        Assert.False(string.IsNullOrEmpty(path), "third-party/studio.sha256 was not found above the test folder.");

        var pins = File.ReadAllLines(path!)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToList();

        Assert.All(pins, pin =>
        {
            Assert.Equal(2, pin.Length);
            Assert.Matches("^[0-9a-f]{64}$", pin[0]);
        });

        var names = pins.Select(pin => pin[1].TrimStart('*')).ToList();
        Assert.Contains(StudioFiles.LibraryName, names);
        Assert.Contains(StudioFiles.ModelName, names);
        Assert.Contains(names, name => name.EndsWith(".whl", StringComparison.Ordinal));
    }
}
