using Audora.App.Infrastructure;
using Audora.Core.Devices;
using Audora.Core.Settings;

namespace Audora.App.ViewModels;

public sealed partial class DeviceViewModel
{
    /// <summary>Multiplier from the group that is playing (its volume, or 0 when muted). 1 when there is none.</summary>
    public double GroupFactor { get; private set; } = 1.0;

    public bool IsBluetooth => _type == ConnectionType.Bluetooth;

    public void SetGroupFactor(double factor)
    {
        factor = Math.Clamp(factor, 0.0, 2.0);
        if (Math.Abs(factor - GroupFactor) < 0.0001)
        {
            return;
        }

        GroupFactor = factor;
        ApplyAudio();
    }

    /// <summary>Puts back what was saved on disk and sends it to the audio engine.</summary>
    public void RestoreFromSaved(SavedDevice saved)
    {
        if (_isDefault)
        {
            return;
        }

        Restore(new DeviceMemory(saved.Volume, saved.Muted, saved.Balance, saved.Delay));

        var id = Id;
        var muted = saved.Muted;
        var balance = (float)(saved.Balance / 100.0);
        var delay = (int)saved.Delay;

        ApplyAudio();
        SerialWorker.Run(() =>
        {
            _controller.SetMuted(id, muted);
            _controller.SetBalance(id, balance);
            _controller.SetDelay(id, delay);
        });
    }

    /// <summary>Copies the current settings into a saved record. The default device keeps its older saved values.</summary>
    public void FillSaved(SavedDevice target)
    {
        target.Name = _name;
        target.Type = _type.ToString();
        target.Selected = _isSelected && !_isDefault;

        if (_isDefault)
        {
            return;
        }

        target.Volume = _volume;
        target.Muted = _isMuted;
        target.Balance = _balance;
        target.Delay = _delay;
    }
}
