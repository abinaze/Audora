using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace Audora.App.Infrastructure;

/// <summary>
/// Adds or removes Audora from the Windows startup list of the current user only.
/// It is one entry (a registry value under HKEY_CURRENT_USER); no administrator rights are needed.
/// </summary>
public static class StartupRegistration
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Audora";

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
                return key?.GetValue(ValueName) is string value && value.Length > 0;
            }
            catch
            {
                return false;
            }
        }
    }

    public static void Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
            if (enabled)
            {
                var path = ExePath();
                if (!string.IsNullOrEmpty(path))
                {
                    key.SetValue(ValueName, $"\"{path}\" --minimized");
                }
            }
            else
            {
                key.DeleteValue(ValueName, false);
            }
        }
        catch (Exception ex)
        {
            Log.Error("Could not change the start-with-Windows setting", ex);
        }
    }

    // When started with "dotnet run" the process is dotnet.exe; use Audora's own program file instead.
    private static string? ExePath()
    {
        var process = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(process)
            && !Path.GetFileNameWithoutExtension(process).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return process;
        }

        var dll = Assembly.GetEntryAssembly()?.Location;
        if (!string.IsNullOrEmpty(dll))
        {
            var exe = Path.ChangeExtension(dll, ".exe");
            if (File.Exists(exe))
            {
                return exe;
            }
        }

        return process;
    }
}
