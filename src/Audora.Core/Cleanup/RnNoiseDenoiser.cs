using System.Runtime.InteropServices;

namespace Audora.Core.Cleanup;

/// <summary>
/// RNNoise, a small neural network that removes steady background noise (fans, hum, keyboards, traffic) from
/// speech. One instance cleans one channel of 48 kHz mono sound, 480 samples at a time. It is not thread-safe.
/// The native library (rnnoise.dll) comes with the YellowDogMan.RRNoise.NET package. Only that DLL is used,
/// through the few calls at the bottom of this file.
/// </summary>
public sealed class RnNoiseDenoiser : IFrameDenoiser
{
    /// <summary>Samples in one frame: 10 ms at 48 kHz.</summary>
    public const int Frame = 480;

    // RNNoise works on numbers in the 16-bit range, so the samples are scaled in and out.
    private const float Scale = 32767f;

    private readonly float[] _scaledIn = new float[Frame];
    private readonly float[] _scaledOut = new float[Frame];
    private IntPtr _state;

    public RnNoiseDenoiser()
    {
        _state = NativeMethods.rnnoise_create(IntPtr.Zero);
        if (_state == IntPtr.Zero)
        {
            throw new InvalidOperationException("The noise removal library could not be started.");
        }
    }

    ~RnNoiseDenoiser() => Release();

    public int FrameSize => Frame;

    /// <summary>True when the native library loads and starts on this computer.</summary>
    public static bool IsAvailable()
    {
        try
        {
            using var denoiser = new RnNoiseDenoiser();
            return true;
        }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException or InvalidOperationException)
        {
            return false;
        }
    }

    public float Process(float[] input, float[] output)
    {
        if (_state == IntPtr.Zero)
        {
            throw new ObjectDisposedException(nameof(RnNoiseDenoiser));
        }

        if (input.Length != Frame || output.Length != Frame)
        {
            throw new ArgumentException($"A frame holds exactly {Frame} samples.");
        }

        for (var i = 0; i < Frame; i++)
        {
            _scaledIn[i] = Math.Clamp(input[i], -1f, 1f) * Scale;
        }

        var speech = NativeMethods.rnnoise_process_frame(_state, _scaledOut, _scaledIn);

        for (var i = 0; i < Frame; i++)
        {
            var value = _scaledOut[i] / Scale;
            output[i] = float.IsFinite(value) ? Math.Clamp(value, -1f, 1f) : 0f;
        }

        return speech;
    }

    public void Dispose()
    {
        Release();
        GC.SuppressFinalize(this);
    }

    private void Release()
    {
        var state = _state;
        _state = IntPtr.Zero;
        if (state != IntPtr.Zero)
        {
            NativeMethods.rnnoise_destroy(state);
        }
    }

    private static class NativeMethods
    {
        [DllImport("rnnoise", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr rnnoise_create(IntPtr model);

        [DllImport("rnnoise", CallingConvention = CallingConvention.Cdecl)]
        public static extern void rnnoise_destroy(IntPtr state);

        [DllImport("rnnoise", CallingConvention = CallingConvention.Cdecl)]
        public static extern float rnnoise_process_frame(IntPtr state, float[] output, float[] input);
    }
}
