using Microsoft.Win32;

namespace Audora.Core.Diagnostics;

/// <summary>Reads whether the Windows Mono audio setting is on. Read only: it changes nothing.</summary>
public static class MonoAudioProbe
{
    private const string KeyPath = @"Software\Microsoft\Multimedia\Audio";
    private const string ValueName = "AccessibilityMonoMixState";

    /// <summary>True when Mono audio is on. A missing value means off. Null when the registry cannot be read.</summary>
    public static bool? Read()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            var value = key?.GetValue(ValueName);
            return value is int number && number != 0;
        }
        catch
        {
            return null;
        }
    }
}
