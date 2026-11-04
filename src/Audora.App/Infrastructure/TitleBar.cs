using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Audora.App.Infrastructure;

/// <summary>Colours the Windows title bar (and its buttons) to match the app theme.</summary>
public static class TitleBar
{
    private const int UseImmersiveDarkModeOld = 19;
    private const int UseImmersiveDarkMode = 20;
    private const int BorderColor = 34;
    private const int CaptionColor = 35;
    private const int TextColor = 36;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void Apply(Window window, bool dark)
    {
        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            // Windows 10 and 11: dark or light bar with matching buttons.
            var immersive = dark ? 1 : 0;
            if (DwmSetWindowAttribute(handle, UseImmersiveDarkMode, ref immersive, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(handle, UseImmersiveDarkModeOld, ref immersive, sizeof(int));
            }

            // Windows 11: also use the exact colours of the app.
            var caption = ToColorRef("BgBrush");
            var text = ToColorRef("TextBrush");
            DwmSetWindowAttribute(handle, CaptionColor, ref caption, sizeof(int));
            DwmSetWindowAttribute(handle, BorderColor, ref caption, sizeof(int));
            DwmSetWindowAttribute(handle, TextColor, ref text, sizeof(int));
        }
        catch
        {
            // purely cosmetic: ignore on systems that do not support it
        }
    }

    private static int ToColorRef(string key)
    {
        if (Application.Current.Resources[key] is SolidColorBrush brush)
        {
            var c = brush.Color;
            return c.R | (c.G << 8) | (c.B << 16);
        }

        return 0;
    }
}
