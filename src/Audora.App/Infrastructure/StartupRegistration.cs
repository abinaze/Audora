using System.IO;
using System.Reflection;
using Audora.Core.Settings;
using Microsoft.Win32;

namespace Audora.App.Infrastructure;

/// <summary>
/// Adds or removes Audora from the Windows startup list of the current user only.
/// It is one entry (a registry value under HKEY_CURRENT_USER); no administrator rights are needed.
/// A build that runs from a source folder is never added, because it would keep starting by itself.
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

    /// <summary>Returns false when it refused (a development build) or could not change the list.</summary>
    public static bool Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
            if (!enabled)
            {
                key.DeleteValue(ValueName, false);
                return true;
            }

            var path = ExePath();
            if (string.IsNullOrEmpty(path) || BuildLocation.IsDevelopmentBuild(path))
            {
                Log.Info("Start with Windows was refused: this copy runs from a source folder.");
                return false;
            }

            key.SetValue(ValueName, $"\"{path}\" --minimized");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Could not change the start-with-Windows setting", ex);
            return false;
        }
    }

    /// <summary>Removes a startup entry that points at a development build, left over from earlier testing.</summary>
    public static void RemoveStaleDevelopmentEntry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
            if (key?.GetValue(ValueName) is not string value)
            {
                return;
            }

            var path = ExtractPath(value);
            if (!string.IsNullOrWhiteSpace(path) && BuildLocation.IsDevelopmentBuild(path))
            {
                key.DeleteValue(ValueName, false);
                Log.Info("Removed a start-with-Windows entry that pointed at a development build.");
            }
        }
        catch (Exception ex)
        {
            Log.Error("Could not check the startup entry", ex);
        }
    }

    private static string ExtractPath(string value)
    {
        value = value.Trim();
        if (value.StartsWith('"'))
        {
            var end = value.IndexOf('"', 1);
            return end > 1 ? value[1..end] : value.Trim('"');
        }

        var space = value.IndexOf(" --", StringComparison.Ordinal);
        return space > 0 ? value[..space] : value;
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
