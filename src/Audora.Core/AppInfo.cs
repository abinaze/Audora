namespace Audora.Core;

public static class AppInfo
{
    public const string Name = "Audora";
    public const string Tagline = "Universal audio control center for Windows";

    public static string Version =>
        typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}
