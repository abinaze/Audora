using Audora.Core;

namespace Audora.Tests;

public class AppInfoTests
{
    [Fact]
    public void Name_IsAudora() => Assert.Equal("Audora", AppInfo.Name);

    [Fact]
    public void Version_IsSemanticVersion() =>
        Assert.Matches(@"^\d+\.\d+\.\d+$", AppInfo.Version);
}
