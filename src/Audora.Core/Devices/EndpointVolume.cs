using NAudio.CoreAudioApi;

namespace Audora.Core.Devices;

/// <summary>Reads and changes the Windows volume and mute of one output device.</summary>
public static class EndpointVolume
{
    public static float? GetVolume(string deviceId) => With(deviceId, v => v.MasterVolumeLevelScalar);

    public static bool? GetMuted(string deviceId) => With(deviceId, v => v.Mute);

    public static void SetVolume(string deviceId, float volume) =>
        With(deviceId, v => { v.MasterVolumeLevelScalar = Math.Clamp(volume, 0f, 1f); return 0; });

    public static void SetMuted(string deviceId, bool muted) =>
        With(deviceId, v => { v.Mute = muted; return 0; });

    private static T? With<T>(string deviceId, Func<AudioEndpointVolume, T> action) where T : struct
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDevice(deviceId);
            return action(device.AudioEndpointVolume);
        }
        catch
        {
            return null; // device gone or not controllable
        }
    }
}
