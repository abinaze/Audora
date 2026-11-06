using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Audora.App.Infrastructure;

public sealed record ForegroundInfo(string Process, string Title, bool FullscreenGame);

/// <summary>
/// Looks at which app is in front, for Smart mode. It reads the app's name and window title and whether a
/// fullscreen game is running. Nothing is stored or sent anywhere.
/// </summary>
public static class ForegroundWatcher
{
    private const int RunningD3DFullScreen = 3;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int count);

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);

    /// <summary>Null when there is nothing useful to read, or when Audora itself is in front.</summary>
    public static ForegroundInfo? Read()
    {
        try
        {
            var window = GetForegroundWindow();
            if (window == IntPtr.Zero)
            {
                return null;
            }

            GetWindowThreadProcessId(window, out var processId);

            string name;
            using (var process = Process.GetProcessById((int)processId))
            {
                name = process.ProcessName;
            }

            if (name.StartsWith("Audora", StringComparison.OrdinalIgnoreCase)
                || name.Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var title = new StringBuilder(256);
            GetWindowText(window, title, title.Capacity);

            var fullscreen = SHQueryUserNotificationState(out var state) == 0 && state == RunningD3DFullScreen;
            return new ForegroundInfo(name, title.ToString(), fullscreen);
        }
        catch
        {
            return null;
        }
    }
}
