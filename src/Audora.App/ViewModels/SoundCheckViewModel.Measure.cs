using Audora.App.Infrastructure;
using Audora.Core.Diagnostics;

namespace Audora.App.ViewModels;

public sealed partial class SoundCheckViewModel
{
    private readonly object _measureGate = new();
    private float _peakLeft;
    private float _peakRight;
    private bool _measured;
    private ToneChannel _expected;
    private string _measuredShort = string.Empty;
    private float _playedVolumePercent;

    /// <summary>While a tone plays, reads the left and right peak at the device every 40 ms and keeps the highest.</summary>
    private void StartMeasuring(ToneChannel expected)
    {
        lock (_measureGate)
        {
            _peakLeft = 0;
            _peakRight = 0;
            _measured = false;
            _expected = expected;
        }

        _ = Task.Run(async () =>
        {
            while (Volatile.Read(ref _playing) && !Volatile.Read(ref _disposed))
            {
                var peaks = _player.ReadChannelPeaks();
                if (peaks is { Length: >= 2 })
                {
                    lock (_measureGate)
                    {
                        _peakLeft = Math.Max(_peakLeft, peaks[0]);
                        _peakRight = Math.Max(_peakRight, peaks[1]);
                        _measured = true;
                    }
                }

                await Task.Delay(40);
            }
        });
    }

    /// <summary>Judges what was measured. Returns text to add to the message, or an empty string.</summary>
    private string FinishMeasuring()
    {
        float left;
        float right;
        bool measured;
        ToneChannel expected;
        lock (_measureGate)
        {
            left = _peakLeft;
            right = _peakRight;
            measured = _measured;
            expected = _expected;
        }

        _measuredShort = string.Empty;
        if (!measured)
        {
            Log.Info("Sound Check: the Windows level meter could not be read.");
            return string.Empty;
        }

        var verdict = ChannelCheck.Judge(expected, left, right);
        _measuredShort = $" Windows measured left {left:0.00}, right {right:0.00} at a Windows volume of {_playedVolumePercent:0}%.";
        Log.Info($"Sound Check measured on {_playedOn}: left {left:0.000}, right {right:0.000} ({verdict}) at a Windows volume of {_playedVolumePercent:0}%.");
        return $"{_measuredShort} {ChannelCheck.Describe(verdict)}";
    }
}
