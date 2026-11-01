using System.Runtime.InteropServices;

namespace Audora.Core.Devices;

/// <summary>
/// Changes the Windows default output device. Windows has no public API for this,
/// so it uses the long-standing (undocumented) IPolicyConfig interface.
/// Sets the Console and Multimedia roles; the Communications device is left alone.
/// </summary>
public static class DefaultDeviceSwitcher
{
    private const int RoleConsole = 0;
    private const int RoleMultimedia = 1;

    public static void SetDefaultOutput(string deviceId)
    {
        var config = (IPolicyConfig)new PolicyConfigClient();
        try
        {
            Marshal.ThrowExceptionForHR(config.SetDefaultEndpoint(deviceId, RoleConsole));
            Marshal.ThrowExceptionForHR(config.SetDefaultEndpoint(deviceId, RoleMultimedia));
        }
        finally
        {
            Marshal.ReleaseComObject(config);
        }
    }

    [ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
    private class PolicyConfigClient
    {
    }

    // Method order must match the native vtable exactly; unused methods are placeholders.
    [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr format);
        [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string name, int useDefault, IntPtr format);
        [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string name);
        [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr endpointFormat, IntPtr mixFormat);
        [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string name, int useDefault, IntPtr defaultPeriod, IntPtr minimumPeriod);
        [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr period);
        [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr mode);
        [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr mode);
        [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string name, int fx, IntPtr key, IntPtr value);
        [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string name, int fx, IntPtr key, IntPtr value);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string name, int role);
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string name, int visible);
    }
}
