using System.Runtime.InteropServices;

namespace Audora.Core.Audio;

public sealed partial class MirrorEngine
{
    private long _capturedBytes;
    private volatile float _peak;

    /// <summary>Total bytes of audio captured since Start().</summary>
    public long CapturedBytes => Interlocked.Read(ref _capturedBytes);

    /// <summary>Highest sample magnitude (0..1) seen since the last call. Resets after reading.</summary>
    public float TakePeakLevel()
    {
        var value = _peak;
        _peak = 0f;
        return value;
    }

    private void RecordStats(byte[] buffer, int bytes)
    {
        Interlocked.Add(ref _capturedBytes, bytes);

        var samples = MemoryMarshal.Cast<byte, float>(buffer.AsSpan(0, bytes - bytes % 4));
        var peak = 0f;
        foreach (var sample in samples)
        {
            var magnitude = Math.Abs(sample);
            if (magnitude > peak)
            {
                peak = magnitude;
            }
        }

        if (peak > _peak)
        {
            _peak = peak;
        }
    }
}
