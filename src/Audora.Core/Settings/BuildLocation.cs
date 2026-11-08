namespace Audora.Core.Settings;

public static class BuildLocation
{
    /// <summary>
    /// True for a program that runs from a source folder (Debug or Release build output) or through dotnet.
    /// Such a build must never be added to the Windows startup list: it would launch by itself after every
    /// sign-in, long after the developer forgot about it.
    /// </summary>
    public static bool IsDevelopmentBuild(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        var normal = path.Replace('/', '\\');
        if (Path.GetFileNameWithoutExtension(normal).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return normal.Contains(@"\bin\Debug\", StringComparison.OrdinalIgnoreCase)
            || normal.Contains(@"\bin\Release\", StringComparison.OrdinalIgnoreCase)
            || normal.Contains(@"\obj\", StringComparison.OrdinalIgnoreCase);
    }
}
