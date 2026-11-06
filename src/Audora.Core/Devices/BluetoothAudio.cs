using System.Runtime.InteropServices;

namespace Audora.Core.Devices;

public sealed record BluetoothAudioDevice(ulong Address, string Name, bool IsConnected);

public enum BluetoothResult
{
    Done,
    NotFound,
    Failed
}

/// <summary>
/// Lists paired Bluetooth speakers and headphones and connects or disconnects them with the official
/// Windows function BluetoothSetServiceState, which switches a device's audio service on or off.
/// New devices still have to be paired once in Windows.
/// </summary>
public static class BluetoothAudio
{
    private static readonly Guid AudioSink = new("0000110B-0000-1000-8000-00805F9B34FB");   // A2DP: music
    private static readonly Guid Handsfree = new("0000111E-0000-1000-8000-00805F9B34FB");   // calls

    private const uint Disable = 0;
    private const uint Enable = 1;
    private const uint AlreadyInThatState = 87;
    private const int AudioVideoMajorClass = 0x04;

    /// <summary>True for headphones, speakers, headsets and other audio gear (Bluetooth device class).</summary>
    public static bool IsAudioClass(uint classOfDevice) => ((classOfDevice >> 8) & 0x1F) == AudioVideoMajorClass;

    public static IReadOnlyList<BluetoothAudioDevice> ListPaired()
    {
        var found = new List<BluetoothAudioDevice>();
        try
        {
            ForEachDevice((_, info) =>
            {
                if (IsAudioClass(info.ulClassofDevice))
                {
                    found.Add(new BluetoothAudioDevice(info.Address, info.szName ?? string.Empty, info.fConnected != 0));
                }

                return false; // keep looking
            });
        }
        catch
        {
            // no Bluetooth adapter, or Bluetooth is not available
        }

        return found
            .GroupBy(d => d.Address)
            .Select(g => g.First())
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static BluetoothResult Connect(ulong address) => Change(address, connect: true);

    public static BluetoothResult Disconnect(ulong address) => Change(address, connect: false);

    private static BluetoothResult Change(ulong address, bool connect)
    {
        var found = false;
        var ok = false;

        try
        {
            ForEachDevice((radio, info) =>
            {
                if (info.Address != address)
                {
                    return false;
                }

                found = true;
                var copy = info;

                if (connect)
                {
                    var sink = AudioSink;
                    var code = BluetoothSetServiceState(radio, ref copy, ref sink, Enable);
                    if (code == AlreadyInThatState)
                    {
                        // Windows thinks the service is already on: switch it off and on again to reconnect.
                        BluetoothSetServiceState(radio, ref copy, ref sink, Disable);
                        Thread.Sleep(600);
                        code = BluetoothSetServiceState(radio, ref copy, ref sink, Enable);
                    }

                    ok = code == 0;
                }
                else
                {
                    foreach (var service in new[] { AudioSink, Handsfree })
                    {
                        var guid = service;
                        if (BluetoothSetServiceState(radio, ref copy, ref guid, Disable) == 0)
                        {
                            ok = true;
                        }
                    }
                }

                return true; // done
            });
        }
        catch
        {
            return BluetoothResult.Failed;
        }

        return !found ? BluetoothResult.NotFound : ok ? BluetoothResult.Done : BluetoothResult.Failed;
    }

    // Walks through every Bluetooth adapter and every paired or connected device, until visit returns true.
    private static void ForEachDevice(Func<IntPtr, DeviceInfo, bool> visit)
    {
        var radioParameters = new FindRadioParams { Size = (uint)Marshal.SizeOf<FindRadioParams>() };
        var findRadio = BluetoothFindFirstRadio(ref radioParameters, out var radio);
        if (findRadio == IntPtr.Zero)
        {
            return; // no Bluetooth adapter
        }

        try
        {
            do
            {
                try
                {
                    var search = new SearchParams
                    {
                        Size = (uint)Marshal.SizeOf<SearchParams>(),
                        ReturnAuthenticated = 1,
                        ReturnRemembered = 1,
                        ReturnUnknown = 0,
                        ReturnConnected = 1,
                        IssueInquiry = 0,
                        TimeoutMultiplier = 0,
                        Radio = radio,
                    };
                    var info = new DeviceInfo { Size = (uint)Marshal.SizeOf<DeviceInfo>() };

                    var findDevice = BluetoothFindFirstDevice(ref search, ref info);
                    if (findDevice != IntPtr.Zero)
                    {
                        try
                        {
                            do
                            {
                                if (visit(radio, info))
                                {
                                    return;
                                }
                            }
                            while (BluetoothFindNextDevice(findDevice, ref info));
                        }
                        finally
                        {
                            BluetoothFindDeviceClose(findDevice);
                        }
                    }
                }
                finally
                {
                    CloseHandle(radio);
                }
            }
            while (BluetoothFindNextRadio(findRadio, out radio));
        }
        finally
        {
            BluetoothFindRadioClose(findRadio);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FindRadioParams
    {
        public uint Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SearchParams
    {
        public uint Size;
        public int ReturnAuthenticated;
        public int ReturnRemembered;
        public int ReturnUnknown;
        public int ReturnConnected;
        public int IssueInquiry;
        public byte TimeoutMultiplier;
        public IntPtr Radio;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemTime
    {
        public ushort Year, Month, DayOfWeek, Day, Hour, Minute, Second, Milliseconds;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DeviceInfo
    {
        public uint Size;
        public ulong Address;
        public uint ulClassofDevice;
        public int fConnected;
        public int fRemembered;
        public int fAuthenticated;
        public SystemTime LastSeen;
        public SystemTime LastUsed;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)]
        public string szName;
    }

    [DllImport("bthprops.cpl", SetLastError = true)]
    private static extern IntPtr BluetoothFindFirstRadio(ref FindRadioParams parameters, out IntPtr radio);

    [DllImport("bthprops.cpl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothFindNextRadio(IntPtr find, out IntPtr radio);

    [DllImport("bthprops.cpl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothFindRadioClose(IntPtr find);

    [DllImport("bthprops.cpl", SetLastError = true)]
    private static extern IntPtr BluetoothFindFirstDevice(ref SearchParams parameters, ref DeviceInfo info);

    [DllImport("bthprops.cpl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothFindNextDevice(IntPtr find, ref DeviceInfo info);

    [DllImport("bthprops.cpl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothFindDeviceClose(IntPtr find);

    [DllImport("bthprops.cpl")]
    private static extern uint BluetoothSetServiceState(IntPtr radio, ref DeviceInfo info, ref Guid service, uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
