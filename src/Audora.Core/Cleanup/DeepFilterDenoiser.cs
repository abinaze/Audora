using System.Runtime.InteropServices;

namespace Audora.Core.Cleanup;

/// <summary>
/// DeepFilterNet3 noise removal through its native library (deepfilter.dll) and model file. It works on 48 kHz sound in
/// frames of 480 samples (10 ms), like RNNoise. The library and the model are not part of Audora yet: the caller says
/// where they are. If either is missing or does not load, <see cref="Create"/> throws, and the cleaner above it then
/// leaves the sound untouched.
/// </summary>
public sealed class DeepFilterDenoiser : IFrameDenoiser
{
    public const int Frame = 480;

    /// <summary>
    /// Samples the library holds back on top of the one frame that the stream wrapper adds. The lab measured 40 ms in all
    /// (1920 samples), so this is 1920 minus the 480 of the wrapper. The cleaner delays the original sound by the same
    /// amount, which keeps the original and the cleaned copy in step when the strength is below 100%.
    /// </summary>
    public const int AlgorithmicDelay = 1440;

    private readonly IntPtr _library;
    private readonly Api _api;
    private readonly IntPtr _input;
    private readonly IntPtr _output;
    private readonly float[] _scratch = new float[Frame];
    private IntPtr _state;
    private bool _disposed;

    private DeepFilterDenoiser(IntPtr library, Api api, IntPtr state)
    {
        _library = library;
        _api = api;
        _state = state;
        _input = Marshal.AllocHGlobal(Frame * sizeof(float));
        _output = Marshal.AllocHGlobal(Frame * sizeof(float));
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr CreateFunction(IntPtr modelPath, float attenuationLimitDb, IntPtr logLevel);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nuint FrameLengthFunction(IntPtr state);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate float ProcessFunction(IntPtr state, IntPtr input, IntPtr output);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FreeFunction(IntPtr state);

    public int FrameSize => Frame;

    /// <summary>The library's own delay, see <see cref="AlgorithmicDelay"/>.</summary>
    public int AlgorithmicDelaySamples => AlgorithmicDelay;

    /// <summary>Loads the library and the model. Throws a clear error if anything is missing or does not fit.</summary>
    public static DeepFilterDenoiser Create(string libraryPath, string modelPath, float attenuationLimitDb = 100f)
    {
        if (!File.Exists(libraryPath))
        {
            throw new FileNotFoundException("The DeepFilterNet library was not found.", libraryPath);
        }

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException("The DeepFilterNet model was not found.", modelPath);
        }

        var library = NativeLibrary.Load(libraryPath);
        var path = IntPtr.Zero;
        var level = IntPtr.Zero;
        try
        {
            var api = Api.From(library);
            path = Marshal.StringToCoTaskMemUTF8(modelPath);
            level = Marshal.StringToCoTaskMemUTF8("error");
            var state = api.Create(path, attenuationLimitDb, level);
            if (state == IntPtr.Zero)
            {
                throw new InvalidOperationException("The DeepFilterNet model could not be loaded.");
            }

            var length = (long)api.FrameLength(state);
            if (length != Frame)
            {
                api.Free(state);
                throw new InvalidOperationException($"DeepFilterNet reported a frame of {length} samples, but {Frame} were expected.");
            }

            return new DeepFilterDenoiser(library, api, state);
        }
        catch
        {
            NativeLibrary.Free(library);
            throw;
        }
        finally
        {
            if (path != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(path);
            }

            if (level != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(level);
            }
        }
    }

    /// <summary>Cleans one frame. The speech probability that RNNoise gives is not available here, so it returns 0.</summary>
    public float Process(float[] input, float[] output)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (input.Length != Frame || output.Length != Frame)
        {
            throw new ArgumentException($"A frame holds exactly {Frame} samples.");
        }

        KeepInRange(input, _scratch);
        Marshal.Copy(_scratch, 0, _input, Frame);
        _api.Process(_state, _input, _output);
        Marshal.Copy(_output, output, 0, Frame);
        KeepInRange(output, output);
        return 0f;
    }

    /// <summary>Copies samples, turns anything that is not a number into silence and holds the rest between -1 and 1. It may copy a frame onto itself.</summary>
    public static void KeepInRange(float[] source, float[] result)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(result);

        var count = Math.Min(source.Length, result.Length);
        for (var i = 0; i < count; i++)
        {
            var value = source[i];
            result[i] = float.IsFinite(value) ? Math.Clamp(value, -1f, 1f) : 0f;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_state != IntPtr.Zero)
        {
            _api.Free(_state);
            _state = IntPtr.Zero;
        }

        Marshal.FreeHGlobal(_input);
        Marshal.FreeHGlobal(_output);
        NativeLibrary.Free(_library);
    }

    private sealed class Api
    {
        private Api(CreateFunction create, FrameLengthFunction frameLength, ProcessFunction process, FreeFunction free)
        {
            Create = create;
            FrameLength = frameLength;
            Process = process;
            Free = free;
        }

        public CreateFunction Create { get; }

        public FrameLengthFunction FrameLength { get; }

        public ProcessFunction Process { get; }

        public FreeFunction Free { get; }

        public static Api From(IntPtr library) => new(
            Marshal.GetDelegateForFunctionPointer<CreateFunction>(NativeLibrary.GetExport(library, "df_create")),
            Marshal.GetDelegateForFunctionPointer<FrameLengthFunction>(NativeLibrary.GetExport(library, "df_get_frame_length")),
            Marshal.GetDelegateForFunctionPointer<ProcessFunction>(NativeLibrary.GetExport(library, "df_process_frame")),
            Marshal.GetDelegateForFunctionPointer<FreeFunction>(NativeLibrary.GetExport(library, "df_free")));
    }
}
